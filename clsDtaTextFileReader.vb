Option Strict On

' This class can be used to open a _Dta.txt file and return each spectrum present
'
' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2005, Battelle Memorial Institute.  All Rights Reserved.
' Started November 14, 2003
'
' E-mail: matthew.monroe@pnl.gov or matt@alchemistmatt.com
' Website: http://omics.pnl.gov/ or http://www.sysbio.org/resources/staff/
' -------------------------------------------------------------------------------

Imports System.Collections.Generic
Imports System.IO
Imports System.Runtime.InteropServices

Public Class clsDtaTextFileReader
    Inherits clsMSTextFileReaderBaseClass

    Public Sub New()
        Me.New(True)
    End Sub

    Public Sub New(blnCombineIdenticalSpectra As Boolean)
        mCombineIdenticalSpectra = blnCombineIdenticalSpectra
        InitializeLocalVariables()
    End Sub

#Region "Constants and Enums"
    ' Note: The extension must be in all caps
    Public Const DTA_TEXT_FILE_EXTENSION As String = "_DTA.TXT"

    Protected Const COMMENT_LINE_START_CHAR As Char = "="c        ' The comment character is an Equals sign

#End Region

#Region "Classwide Variables"

    Protected mCombineIdenticalSpectra As Boolean

    ' mHeaderSaved is used to store the previous header title; it is needed when the next
    '  header was read for comparison with the current scan, but it didn't match, and thus
    '  wasn't used for grouping
    Protected mHeaderSaved As String

#End Region

#Region "Processing Options and Interface Functions"

    Public Property CombineIdenticalSpectra() As Boolean
        Get
            Return mCombineIdenticalSpectra
        End Get
        Set(Value As Boolean)
            mCombineIdenticalSpectra = Value
        End Set
    End Property

#End Region

    Protected Overrides Sub InitializeLocalVariables()
        MyBase.InitializeLocalVariables()

        mCommentLineStartChar = COMMENT_LINE_START_CHAR
        mHeaderSaved = String.Empty
    End Sub

    Protected Overrides Sub LogErrors(strCallingFunction As String, strErrorDescription As String)
        MyBase.LogErrors("clsDtaTextFileReader." & strCallingFunction, strErrorDescription)
    End Sub

    Public Overrides Function ReadNextSpectrum(<Out()> ByRef objSpectrumInfo As clsSpectrumInfo) As Boolean
        ' Reads the next spectrum from a _Dta.txt file
        ' Returns True if a spectrum is found, otherwise, returns False
        ' If blnCombineIdenticalSpectra = True, then combines spectra that only differ by their charge state

        Dim strLineIn As String
        Dim strMostRecentLineIn As String = String.Empty
        Dim intLastProgressUpdateLine As Integer
        Dim strCompareTitle As String

        Dim blnSpectrumFound As Boolean

        Try
            If MyBase.ReadingAndStoringSpectra OrElse mCurrentSpectrum Is Nothing Then
                mCurrentSpectrum = New clsSpectrumInfoMsMsText
            Else
                mCurrentSpectrum.Clear()
            End If

            mCurrentSpectrum.AutoShrinkDataLists = MyBase.AutoShrinkDataLists

            blnSpectrumFound = False
            If mFileReader Is Nothing Then
                objSpectrumInfo = New clsSpectrumInfo()
                mErrorMessage = "Data file not currently open"
            Else
                MyBase.AddNewRecentFileText(String.Empty, True, False)

                intLastProgressUpdateLine = mInFileLineNumber
                Do While Not blnSpectrumFound And mFileReader.Peek() > -1 And Not mAbortProcessing

                    If mHeaderSaved.Length > 0 Then
                        strLineIn = String.Copy(mHeaderSaved)
                        mHeaderSaved = String.Empty
                    Else
                        strLineIn = mFileReader.ReadLine()
                        If Not strLineIn Is Nothing Then mTotalBytesRead += strLineIn.Length + 2
                        mInFileLineNumber += 1
                    End If

                    ' See if strLineIn is nothing or starts with the comment line character (equals sign)
                    If Not strLineIn Is Nothing AndAlso strLineIn.Trim.StartsWith(mCommentLineStartChar) Then
                        MyBase.AddNewRecentFileText(strLineIn)

                        With mCurrentSpectrum
                            .SpectrumTitleWithCommentChars = strLineIn
                            .SpectrumTitle = MyBase.CleanupComment(strLineIn, mCommentLineStartChar, True)
                            MyBase.ExtractScanInfoFromDtaHeader(.SpectrumTitle, .ScanNumber, .ScanNumberEnd, .ScanCount)

                            .MSLevel = 2
                            .SpectrumID = .ScanNumber
                        End With

                        ' Read the next line, which should have the parent ion MH value and charge
                        If mFileReader.Peek() > -1 Then
                            strLineIn = mFileReader.ReadLine()
                        Else
                            strLineIn = String.Empty
                        End If

                        If Not strLineIn Is Nothing Then mTotalBytesRead += strLineIn.Length + 2
                        mInFileLineNumber += 1

                        If String.IsNullOrWhiteSpace(strLineIn) Then
                            ' Spectrum header is not followed by a parent ion value and charge; ignore the line
                        Else
                            MyBase.AddNewRecentFileText(strLineIn)

                            ' Parse the parent ion info and read the MsMs Data
                            blnSpectrumFound = ReadSingleSpectrum(mFileReader, strLineIn, mCurrentMsMsDataList,
                                                                  mCurrentSpectrum, mInFileLineNumber,
                                                                  intLastProgressUpdateLine, strMostRecentLineIn)

                            If blnSpectrumFound Then
                                If MyBase.mReadTextDataOnly Then
                                    ' Do not parse the text data to populate .MZList and .IntensityList
                                    mCurrentSpectrum.DataCount = 0
                                Else
                                    With mCurrentSpectrum
                                        Try
                                            .DataCount = MyBase.ParseMsMsDataList(mCurrentMsMsDataList, .MZList,
                                                                                  .IntensityList, .AutoShrinkDataLists)

                                            .Validate(blnComputeBasePeakAndTIC:=True, blnUpdateMZRange:=True)

                                        Catch ex As Exception
                                            .DataCount = 0
                                            blnSpectrumFound = False
                                        End Try
                                    End With
                                End If
                            End If

                            If blnSpectrumFound AndAlso
                               mCombineIdenticalSpectra AndAlso
                               mCurrentSpectrum.ParentIonCharges(0) = 2 Then
                                ' See if the next spectrum is the identical data, but the charge is 3 (this is a common situation with .dta files prepared by Lcq_Dta)

                                strLineIn = String.Copy(strMostRecentLineIn)
                                If String.IsNullOrWhiteSpace(strLineIn) AndAlso mFileReader.Peek() > -1 Then
                                    ' Read the next line
                                    strLineIn = mFileReader.ReadLine()
                                    If Not strLineIn Is Nothing Then mTotalBytesRead += strLineIn.Length + 2
                                    mInFileLineNumber += 1
                                End If

                                If Not strLineIn Is Nothing AndAlso strLineIn.StartsWith(mCommentLineStartChar) Then
                                    mHeaderSaved = String.Copy(strLineIn)
                                    strCompareTitle = MyBase.CleanupComment(mHeaderSaved, mCommentLineStartChar, True)

                                    If strCompareTitle.ToLower.EndsWith("3.dta") Then
                                        If String.Equals(mCurrentSpectrum.SpectrumTitle.Substring(0, mCurrentSpectrum.SpectrumTitle.Length - 5),
                                                         strCompareTitle.Substring(0, strCompareTitle.Length - 5),
                                                         StringComparison.InvariantCultureIgnoreCase) Then

                                            ' Yes, the spectra match

                                            With mCurrentSpectrum
                                                .ParentIonChargeCount = 2
                                                .ParentIonCharges(1) = 3
                                                .ChargeIs2And3Plus = True
                                            End With

                                            mHeaderSaved = String.Empty

                                            ' Read the next set of lines until the next blank line or comment line is found
                                            Do While mFileReader.Peek() > -1
                                                strLineIn = mFileReader.ReadLine()
                                                mInFileLineNumber += 1

                                                ' See if strLineIn is blank or starts with an equals sign
                                                If Not strLineIn Is Nothing Then
                                                    mTotalBytesRead += strLineIn.Length + 2
                                                    If strLineIn.Trim().Length = 0 Then
                                                        Exit Do
                                                    ElseIf strLineIn.Trim.StartsWith(mCommentLineStartChar) Then
                                                        mHeaderSaved = String.Copy(strLineIn)
                                                        Exit Do
                                                    End If
                                                End If
                                            Loop
                                        End If
                                    End If
                                End If
                            Else
                                If strMostRecentLineIn.StartsWith(mCommentLineStartChar) Then
                                    mHeaderSaved = String.Copy(strMostRecentLineIn)
                                End If
                            End If  ' EndIf for blnSpectrumFound = True
                        End If
                    End If  ' EndIf for strLineIn.Trim.StartsWith(mCommentLineStartChar)

                    If mInFileLineNumber - intLastProgressUpdateLine >= 250 Or blnSpectrumFound Then
                        intLastProgressUpdateLine = mInFileLineNumber
                        UpdateStreamReaderProgress()
                    End If
                Loop

                objSpectrumInfo = mCurrentSpectrum

                If blnSpectrumFound AndAlso Not MyBase.ReadingAndStoringSpectra Then
                    MyBase.UpdateFileStats(objSpectrumInfo.ScanNumber)
                End If
            End If

        Catch ex As Exception
            LogErrors("ReadNextSpectrum", ex.Message)
            objSpectrumInfo = New clsSpectrumInfo()
        End Try

        Return blnSpectrumFound
    End Function

    Public Function ReadSingleDtaFile(
      strInputFilePath As String,
      <Out()> ByRef strMsMsDataList() As String,
      <Out()> ByRef intMsMsDataCount As Integer,
      <Out()> ByRef objSpectrumInfoMsMsText As clsSpectrumInfoMsMsText) As Boolean

        ' Open the .Dta file and read the spectrum
        Dim intLastProgressUpdateLine As Integer

        Dim strLineIn As String
        Dim blnSpectrumFound As Boolean

        Dim lstMsMsDataList = New List(Of String)

        intMsMsDataCount = 0
        objSpectrumInfoMsMsText = New clsSpectrumInfoMsMsText()

        Try

            Using fileReader = New StreamReader(strInputFilePath)

                mTotalBytesRead = 0
                MyBase.ResetProgress("Parsing " & Path.GetFileName(strInputFilePath))

                mInFileLineNumber = 0
                intLastProgressUpdateLine = mInFileLineNumber
                Do While Not fileReader.EndOfStream And Not mAbortProcessing
                    strLineIn = fileReader.ReadLine
                    mInFileLineNumber += 1

                    If Not strLineIn Is Nothing Then mTotalBytesRead += strLineIn.Length + 2
                    If Not String.IsNullOrWhiteSpace(strLineIn) Then
                        If Char.IsDigit(strLineIn.Trim(), 0) Then
                            blnSpectrumFound = ReadSingleSpectrum(fileReader, strLineIn, lstMsMsDataList,
                                                                  objSpectrumInfoMsMsText, mInFileLineNumber,
                                                                  intLastProgressUpdateLine)
                            Exit Do
                        End If
                    End If

                    If mInFileLineNumber - intLastProgressUpdateLine >= 100 Then
                        intLastProgressUpdateLine = mInFileLineNumber
                        'MyBase.UpdateProgress(srInFile.BaseStream.Position / srInFile.BaseStream.Length * 100.0)
                        MyBase.UpdateProgress(mTotalBytesRead / fileReader.BaseStream.Length * 100.0)
                    End If
                Loop

                If blnSpectrumFound Then
                    ' Try to determine the scan numbers by parsing strInputFilePath
                    With objSpectrumInfoMsMsText
                        MyBase.ExtractScanInfoFromDtaHeader(Path.GetFileName(strInputFilePath), .ScanNumber,
                                                            .ScanNumberEnd, .ScanCount)
                    End With

                    ReDim strMsMsDataList(lstMsMsDataList.Count - 1)
                    lstMsMsDataList.CopyTo(strMsMsDataList)

                Else
                    ReDim strMsMsDataList(0)
                End If

                If Not mAbortProcessing Then
                    MyBase.OperationComplete()
                End If

            End Using

        Catch ex As Exception
            LogErrors("ReadSingleDtaFile", ex.Message)
            objSpectrumInfoMsMsText = New clsSpectrumInfoMsMsText()
            ReDim strMsMsDataList(0)
        End Try

        Return blnSpectrumFound
    End Function

    Private Function ReadSingleSpectrum(
      srReader As TextReader,
      strParentIonLineText As String,
      <Out()> ByRef lstMsMsDataList As List(Of String),
      objSpectrumInfoMsMsText As clsSpectrumInfoMsMsText,
      ByRef intLinesRead As Integer,
      ByRef intLastProgressUpdateLine As Integer,
      Optional ByRef strMostRecentLineIn As String = "") As Boolean

        ' Returns True if a valid spectrum is found, otherwise, returns False

        Dim intCharIndex As Integer
        Dim strLineIn As String
        Dim strValue As String
        Dim dblValue As Double

        Dim blnSpectrumFound As Boolean
        Dim intCharge As Integer

        objSpectrumInfoMsMsText.ParentIonLineText = String.Copy(strParentIonLineText)
        strParentIonLineText = strParentIonLineText.Trim

        ' Look for the first space
        intCharIndex = strParentIonLineText.IndexOf(" "c)
        If intCharIndex >= 1 Then
            strValue = strParentIonLineText.Substring(0, intCharIndex)
            If Double.TryParse(strValue, dblValue) Then
                With objSpectrumInfoMsMsText
                    .ParentIonMH = dblValue

                    strValue = strParentIonLineText.Substring(intCharIndex + 1)

                    ' See if strValue contains another space
                    intCharIndex = strValue.IndexOf(" "c)
                    If intCharIndex > 0 Then
                        strValue = strValue.Substring(0, intCharIndex)
                    End If

                    If Integer.TryParse(strValue, intCharge) Then
                        .ParentIonChargeCount = 1
                        .ParentIonCharges(0) = intCharge

                        ' Note: Dta files have Parent Ion MH defined by not Parent Ion m/z
                        ' Thus, compute .ParentIonMZ using .ParentIonMH
                        If .ParentIonCharges(0) <= 1 Then
                            .ParentIonMZ = .ParentIonMH
                            .ParentIonCharges(0) = 1
                        Else
                            .ParentIonMZ = MyBase.ConvoluteMass(.ParentIonMH, 1, .ParentIonCharges(0))
                        End If

                        blnSpectrumFound = True
                    End If
                End With
            End If
        End If

        strMostRecentLineIn = String.Empty
        lstMsMsDataList = New List(Of String)

        If blnSpectrumFound Then
            ' Read all of the MS/MS spectrum ions up to the next blank line or up to the next line starting with COMMENT_LINE_START_CHAR

            Do While srReader.Peek() > -1
                strLineIn = srReader.ReadLine
                intLinesRead += 1

                ' See if strLineIn is blank
                If Not strLineIn Is Nothing Then
                    mTotalBytesRead += strLineIn.Length + 2
                    strMostRecentLineIn = String.Copy(strLineIn)

                    If strLineIn.Trim().Length = 0 OrElse strLineIn.StartsWith(COMMENT_LINE_START_CHAR) Then
                        Exit Do
                    Else

                        ' Add to MS/MS data string list
                        lstMsMsDataList.Add(strLineIn.Trim)

                        MyBase.AddNewRecentFileText(strLineIn)
                    End If
                End If

                If intLinesRead - intLastProgressUpdateLine >= 250 Then
                    intLastProgressUpdateLine = intLinesRead
                    UpdateStreamReaderProgress()
                End If

            Loop

        End If

        Return blnSpectrumFound
    End Function
End Class
