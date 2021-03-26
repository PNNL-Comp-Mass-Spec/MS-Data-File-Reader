Option Strict On

' This class can be used to open a Mascot Generic File (.MGF) and return each spectrum present
'
' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2005, Battelle Memorial Institute.  All Rights Reserved.
' Started November 15, 2003
'
' E-mail: matthew.monroe@pnl.gov or proteomics@pnnl.gov
' Website: http://omics.pnl.gov/ or http://panomics.pnnl.gov/
' -------------------------------------------------------------------------------

Imports System.Collections.Generic
Imports System.IO
Imports System.Runtime.InteropServices

Public Class clsMGFFileReader
    Inherits clsMSTextFileReaderBaseClass

    Public Sub New()
        InitializeLocalVariables()
    End Sub

#Region "Constants and Enums"
    ' Note: The extension must be in all caps
    Public Const MGF_FILE_EXTENSION As String = ".MGF"

    Private Const COMMENT_LINE_START_CHAR As Char = "#"c        ' The comment character is an Equals sign
    Private Const LINE_START_BEGIN_IONS As String = "BEGIN IONS"
    Private Const LINE_START_END_IONS As String = "END IONS"

    Private Const LINE_START_MSMS As String = "MSMS:"
    Private Const LINE_START_PEPMASS As String = "PEPMASS="
    Private Const LINE_START_CHARGE As String = "CHARGE="
    Private Const LINE_START_TITLE As String = "TITLE="

    Private Const LINE_START_RT As String = "RTINSECONDS="
    Private Const LINE_START_SCANS As String = "SCANS="

#End Region

#Region "Classwide Variables"
    ' mScanNumberStartSaved is used to create fake scan numbers when reading .MGF files that do not have
    '  scan numbers defined using   ###MSMS: #1234   or   TITLE=Filename.1234.1234.2.dta  or  TITLE=Filename.1234.1234.2
    Private mScanNumberStartSaved As Integer

#End Region

    Protected Overrides Sub InitializeLocalVariables()
        MyBase.InitializeLocalVariables()

        mCommentLineStartChar = COMMENT_LINE_START_CHAR
        mScanNumberStartSaved = 0
    End Sub

    Protected Overrides Sub LogErrors(strCallingFunction As String, strErrorDescription As String)
        MyBase.LogErrors("clsMGFFileReader." & strCallingFunction, strErrorDescription)
    End Sub

    ''' <summary>
    ''' Parse out a scan number or scan number range from strData
    ''' </summary>
    ''' <param name="strData">Single integer or two integers separated by a dash</param>
    ''' <param name="spectrumInfo"></param>
    ''' <returns></returns>
    Private Function ExtractScanRange(strData As String, spectrumInfo As clsSpectrumInfo) As Boolean

        Dim scanNumberFound = False

        Dim charIndex = strData.IndexOf("-"c)
        If charIndex > 0 Then
            ' strData contains a dash, and thus a range of scans
            Dim strRemaining = strData.Substring(charIndex + 1).Trim()
            strData = strData.Substring(0, charIndex).Trim()

            If IsNumber(strData) Then
                spectrumInfo.ScanNumber = CInt(strData)
                If IsNumber(strRemaining) Then
                    If spectrumInfo.ScanNumberEnd = 0 Then
                        spectrumInfo.ScanNumberEnd = CInt(strRemaining)
                    End If
                Else
                    spectrumInfo.ScanNumberEnd = spectrumInfo.ScanNumber
                End If
                scanNumberFound = True
            End If
        Else
            If IsNumber(strData) Then
                spectrumInfo.ScanNumber = CInt(strData)
                If spectrumInfo.ScanNumberEnd = 0 Then
                    spectrumInfo.ScanNumberEnd = spectrumInfo.ScanNumber
                End If
                scanNumberFound = True
            End If
        End If


        If scanNumberFound Then
            mCurrentSpectrum.SpectrumID = mCurrentSpectrum.ScanNumber
            If spectrumInfo.ScanNumber = spectrumInfo.ScanNumberEnd OrElse spectrumInfo.ScanNumber > spectrumInfo.ScanNumberEnd Then
                mCurrentSpectrum.ScanCount = 1
            Else
                mCurrentSpectrum.ScanCount = spectrumInfo.ScanNumberEnd - spectrumInfo.ScanNumber + 1
            End If
        End If

        Return False
    End Function

    Public Overrides Function ReadNextSpectrum(<Out()> ByRef objSpectrumInfo As clsSpectrumInfo) As Boolean
        ' Reads the next spectrum from a .MGF file
        ' Returns True if a spectrum is found, otherwise, returns False

        Dim strLineIn As String, strTemp As String
        Dim strSplitLine() As String
        Dim strSepChars = New Char() {" "c, ControlChars.Tab}

        Dim intIndex As Integer
        Dim charIndex As Integer
        Dim intLastProgressUpdateLine As Integer

        Dim blnScanNumberFound As Boolean
        Dim blnParentIonFound As Boolean

        Dim blnSpectrumFound As Boolean

        Try
            If MyBase.ReadingAndStoringSpectra OrElse mCurrentSpectrum Is Nothing Then
                mCurrentSpectrum = New clsSpectrumInfoMsMsText
            Else
                mCurrentSpectrum.Clear()
            End If

            mCurrentSpectrum.AutoShrinkDataLists = MyBase.AutoShrinkDataLists

            blnSpectrumFound = False
            blnScanNumberFound = False

            ' Initialize mCurrentMsMsDataList
            If mCurrentMsMsDataList Is Nothing Then
                mCurrentMsMsDataList = New List(Of String)
            Else
                mCurrentMsMsDataList.Clear()
            End If

            If mFileReader Is Nothing Then
                objSpectrumInfo = New clsSpectrumInfoMsMsText()
                mErrorMessage = "Data file not currently open"
            Else
                MyBase.AddNewRecentFileText(String.Empty, True, False)

                With mCurrentSpectrum
                    .SpectrumTitleWithCommentChars = String.Empty
                    .SpectrumTitle = String.Empty
                    .MSLevel = 2
                End With

                intLastProgressUpdateLine = mInFileLineNumber
                Do While Not blnSpectrumFound AndAlso mFileReader.Peek() > -1 AndAlso Not mAbortProcessing

                    strLineIn = mFileReader.ReadLine
                    If strLineIn IsNot Nothing Then mTotalBytesRead += strLineIn.Length + 2
                    mInFileLineNumber += 1

                    If strLineIn IsNot Nothing AndAlso strLineIn.Trim().Length > 0 Then
                        MyBase.AddNewRecentFileText(strLineIn)
                        strLineIn = strLineIn.Trim()

                        ' See if strLineIn starts with the comment line start character (a pound sign, #)
                        If strLineIn.StartsWith(mCommentLineStartChar) Then
                            ' Remove any comment characters at the start of strLineIn
                            strLineIn = strLineIn.TrimStart(mCommentLineStartChar).Trim()

                            ' Look for LINE_START_MSMS in strLineIn
                            ' This will be present in MGF files created using Agilent's DataAnalysis software
                            If strLineIn.ToUpper.StartsWith(LINE_START_MSMS) Then
                                strLineIn = strLineIn.Substring(LINE_START_MSMS.Length).Trim()

                                ' Initialize these values
                                mCurrentSpectrum.ScanNumberEnd = 0
                                mCurrentSpectrum.ScanCount = 1

                                ' Remove the # sign in front of the scan number
                                strLineIn = strLineIn.TrimStart("#"c).Trim()

                                ' Look for the / sign and remove any text following it
                                ' For example,
                                '   ###MS: 4458/4486/
                                '   ###MSMS: 4459/4488/
                                ' The / sign is used to indicate that several MS/MS scans were combined to make the given spectrum; we'll just keep the first one
                                charIndex = strLineIn.IndexOf("/"c)
                                If charIndex > 0 Then
                                    If charIndex < strLineIn.Length - 1 Then
                                        strTemp = strLineIn.Substring(charIndex + 1).Trim()
                                    Else
                                        strTemp = String.Empty
                                    End If

                                    strLineIn = strLineIn.Substring(0, charIndex).Trim()
                                    mCurrentSpectrum.ScanCount = 1

                                    If strTemp.Length > 0 Then
                                        Do
                                            charIndex = strTemp.IndexOf("/"c)
                                            If charIndex > 0 Then
                                                mCurrentSpectrum.ScanCount += 1
                                                If charIndex < strTemp.Length - 1 Then
                                                    strTemp = strTemp.Substring(charIndex + 1).Trim()
                                                Else
                                                    strTemp = strTemp.Substring(0, charIndex).Trim()
                                                    Exit Do
                                                End If
                                            Else
                                                Exit Do
                                            End If
                                        Loop

                                        If IsNumber(strTemp) Then
                                            mCurrentSpectrum.ScanNumberEnd = CInt(strTemp)
                                        End If
                                    End If
                                End If

                                blnScanNumberFound = ExtractScanRange(strLineIn, mCurrentSpectrum)

                            End If
                        Else
                            ' Line does not start with a comment character
                            ' Look for LINE_START_BEGIN_IONS in strLineIn
                            If strLineIn.ToUpper.StartsWith(LINE_START_BEGIN_IONS) Then
                                If Not blnScanNumberFound Then
                                    ' Need to update intScanNumberStart
                                    ' Set it to one more than mScanNumberStartSaved
                                    mCurrentSpectrum.ScanNumber = mScanNumberStartSaved + 1
                                    mCurrentSpectrum.ScanNumberEnd = mCurrentSpectrum.ScanNumber
                                    mCurrentSpectrum.SpectrumID = mCurrentSpectrum.ScanNumber
                                    mCurrentSpectrum.ScanCount = 1
                                End If

                                blnParentIonFound = False

                                ' We have found an MS/MS scan
                                ' Look for LINE_START_PEPMASS and LINE_START_CHARGE to determine the parent ion m/z and charge
                                Do While mFileReader.Peek() > -1
                                    strLineIn = mFileReader.ReadLine()
                                    mInFileLineNumber += 1

                                    If strLineIn IsNot Nothing Then
                                        mTotalBytesRead += strLineIn.Length + 2
                                        MyBase.AddNewRecentFileText(strLineIn)

                                        If strLineIn.Trim().Length > 0 Then
                                            strLineIn = strLineIn.Trim()
                                            If strLineIn.ToUpper.StartsWith(LINE_START_PEPMASS) Then
                                                ' This line defines the peptide mass as an m/z value
                                                ' It may simply contain the m/z value, or it may also contain an intensity value
                                                ' The two values will be separated by a space or a tab
                                                ' We do not save the intensity value since it cannot be included in a .Dta file
                                                strLineIn = strLineIn.Substring(LINE_START_PEPMASS.Length).Trim()
                                                strSplitLine = strLineIn.Split(strSepChars)
                                                If strSplitLine.Length > 0 AndAlso IsNumber(strSplitLine(0)) Then
                                                    mCurrentSpectrum.ParentIonMZ = CDbl(strSplitLine(0))
                                                    blnParentIonFound = True
                                                Else
                                                    ' Invalid LINE_START_PEPMASS Line
                                                    ' Ignore this entire scan
                                                    Exit Do
                                                End If
                                            ElseIf strLineIn.ToUpper.StartsWith(LINE_START_CHARGE) Then
                                                ' This line defines the peptide charge
                                                ' It may simply contain a single charge, like 1+ or 2+
                                                ' It may also contain two charges, as in 2+ and 3+
                                                ' Not all spectra in the MGF file will have a CHARGE= entry
                                                strLineIn = strLineIn.Substring(LINE_START_CHARGE.Length).Trim()

                                                ' Remove any + signs in the line
                                                strLineIn = strLineIn.Replace("+", String.Empty)
                                                If strLineIn.IndexOf(" "c) > 0 Then
                                                    ' Multiple charges may be present
                                                    strSplitLine = strLineIn.Split(strSepChars)
                                                    For intIndex = 0 To strSplitLine.Length - 1
                                                        ' Step through the split line and add any numbers to the charge list
                                                        ' Typically, strSplitLine(1) will contain "and"
                                                        If IsNumber(strSplitLine(intIndex).Trim()) Then
                                                            With mCurrentSpectrum
                                                                If .ParentIonChargeCount < clsSpectrumInfoMsMsText.MAX_CHARGE_COUNT Then
                                                                    .ParentIonCharges(.ParentIonChargeCount) = CInt(strSplitLine(intIndex).Trim())
                                                                    .ParentIonChargeCount += 1
                                                                End If
                                                            End With
                                                        End If
                                                    Next intIndex
                                                Else
                                                    If IsNumber(strLineIn) Then
                                                        With mCurrentSpectrum
                                                            .ParentIonChargeCount = 1
                                                            .ParentIonCharges(0) = CInt(strLineIn)
                                                        End With
                                                    End If
                                                End If

                                            ElseIf strLineIn.ToUpper.StartsWith(LINE_START_TITLE) Then
                                                mCurrentSpectrum.SpectrumTitle = String.Copy(strLineIn)

                                                strLineIn = strLineIn.Substring(LINE_START_TITLE.Length).Trim()
                                                mCurrentSpectrum.SpectrumTitleWithCommentChars = String.Copy(strLineIn)

                                                If Not blnScanNumberFound Then
                                                    ' We didn't find a scan number in a ### MSMS: comment line
                                                    ' Attempt to extract out the scan numbers from the Title
                                                    With mCurrentSpectrum
                                                        MyBase.ExtractScanInfoFromDtaHeader(strLineIn, .ScanNumber,
                                                                                            .ScanNumberEnd, .ScanCount)
                                                    End With
                                                End If
                                            ElseIf strLineIn.ToUpper.StartsWith(LINE_START_END_IONS) Then
                                                ' Empty ion list
                                                Exit Do
                                            ElseIf strLineIn.ToUpper.StartsWith(LINE_START_RT) Then

                                                strLineIn = strLineIn.Substring(LINE_START_RT.Length).Trim()

                                                Dim rtSeconds As Double
                                                If Double.TryParse(strLineIn, rtSeconds) Then
                                                    mCurrentSpectrum.RetentionTimeMin = CSng(rtSeconds / 60.0)
                                                End If

                                            ElseIf strLineIn.ToUpper.StartsWith(LINE_START_SCANS) Then

                                                strLineIn = strLineIn.Substring(LINE_START_SCANS.Length).Trim()
                                                blnScanNumberFound = ExtractScanRange(strLineIn, mCurrentSpectrum)

                                            ElseIf Char.IsNumber(strLineIn, 0) Then
                                                ' Found the start of the ion list
                                                ' Add to the MsMs data list
                                                If blnParentIonFound Then
                                                    mCurrentMsMsDataList.Add(strLineIn)
                                                End If

                                                Exit Do
                                            End If

                                        End If
                                    End If
                                Loop

                                If blnParentIonFound AndAlso mCurrentMsMsDataList.Count > 0 Then
                                    ' We have determined the parent ion

                                    ' Note: MGF files have Parent Ion MZ defined but not Parent Ion MH
                                    ' Thus, compute .ParentIonMH using .ParentIonMZ
                                    With mCurrentSpectrum
                                        If .ParentIonChargeCount >= 1 Then
                                            .ParentIonMH = MyBase.ConvoluteMass(.ParentIonMZ, .ParentIonCharges(0), 1)
                                        Else
                                            .ParentIonMH = .ParentIonMZ
                                        End If
                                    End With

                                    ' Read in the ions and populate mCurrentMsMsDataList
                                    ' Read all of the MS/MS spectrum ions up to the next blank line or up to LINE_START_END_IONS
                                    Do While mFileReader.Peek() > -1
                                        strLineIn = mFileReader.ReadLine()
                                        mInFileLineNumber += 1

                                        ' See if strLineIn is blank
                                        If strLineIn IsNot Nothing Then
                                            mTotalBytesRead += strLineIn.Length + 2
                                            MyBase.AddNewRecentFileText(strLineIn)

                                            If strLineIn.Trim().Length > 0 Then
                                                If strLineIn.Trim().ToUpper.StartsWith(LINE_START_END_IONS) Then
                                                    Exit Do
                                                Else
                                                    ' Add to MS/MS data sting list
                                                    mCurrentMsMsDataList.Add(strLineIn.Trim())
                                                End If
                                            End If
                                        End If

                                        If mInFileLineNumber - intLastProgressUpdateLine >= 250 Then
                                            intLastProgressUpdateLine = mInFileLineNumber
                                            UpdateStreamReaderProgress()
                                        End If
                                    Loop
                                    blnSpectrumFound = True

                                    If MyBase.mReadTextDataOnly Then
                                        ' Do not parse the text data to populate .MZList and .IntensityList
                                        mCurrentSpectrum.DataCount = 0
                                    Else
                                        With mCurrentSpectrum
                                            Try
                                                .DataCount = MyBase.ParseMsMsDataList(mCurrentMsMsDataList, .MZList,
                                                                                      .IntensityList,
                                                                                      .AutoShrinkDataLists)

                                                .Validate(blnComputeBasePeakAndTIC:=True, blnUpdateMZRange:=True)

                                            Catch ex As Exception
                                                .DataCount = 0
                                                blnSpectrumFound = False
                                            End Try
                                        End With
                                    End If
                                End If

                                ' Copy the scan number to mScanNumberStartSaved
                                If mCurrentSpectrum.ScanNumber > 0 Then
                                    mScanNumberStartSaved = mCurrentSpectrum.ScanNumber
                                End If
                            End If
                        End If
                    End If

                    If mInFileLineNumber - intLastProgressUpdateLine >= 250 Or blnSpectrumFound Then
                        intLastProgressUpdateLine = mInFileLineNumber

                        Dim objStreamReader = TryCast(mFileReader, StreamReader)

                        If objStreamReader IsNot Nothing Then
                            MyBase.UpdateProgress(
                                (objStreamReader.BaseStream.Position / objStreamReader.BaseStream.Length * 100.0))
                        ElseIf mInFileStreamLength > 0 Then
                            MyBase.UpdateProgress(mTotalBytesRead / mInFileStreamLength * 100.0)
                        End If
                    End If
                Loop

                objSpectrumInfo = mCurrentSpectrum

                If blnSpectrumFound AndAlso Not MyBase.ReadingAndStoringSpectra Then
                    MyBase.UpdateFileStats(objSpectrumInfo.ScanNumber)
                End If
            End If

        Catch ex As Exception
            LogErrors("ReadNextSpectrum", ex.Message)
            objSpectrumInfo = New clsSpectrumInfoMsMsText()
        End Try

        Return blnSpectrumFound
    End Function

End Class
