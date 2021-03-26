Option Strict On

' This is the base class for the DTA and MGF file readers
'
' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2006, Battelle Memorial Institute.  All Rights Reserved.
' Started March 26, 2006
'
' E-mail: matthew.monroe@pnl.gov or proteomics@pnnl.gov
' Website: http://omics.pnl.gov/ or http://panomics.pnnl.gov/
' -------------------------------------------------------------------------------
'

Imports System.Collections.Generic
Imports System.Runtime.InteropServices
Imports System.Text.RegularExpressions

Public MustInherit Class clsMSTextFileReaderBaseClass
    Inherits clsMSDataFileReaderBaseClass

    Public Sub New()
        InitializeLocalVariables()
    End Sub

    Protected Overrides Sub Finalize()
        MyBase.Finalize()

        Try
            If mFileReader IsNot Nothing Then
                mFileReader.Close()
            End If
        Catch ex As Exception
        End Try
    End Sub

#Region "Constants and Enums"

#End Region

#Region "Structures"

#End Region

#Region "Classwide Variables"

    ' Number between 0 and 100; if the percentage of ions greater than the parent ion m/z is less than this number, then the charge is definitely 1+
    Protected mThresholdIonPctForSingleCharge As Single

    ' Number between 0 and 100; if the percentage of ions greater than the parent ion m/z is greater than this number, then the charge is definitely 2+ or higher
    Protected mThresholdIonPctForDoubleCharge As Single

    Protected mFileReader As IO.TextReader
    Protected mCommentLineStartChar As Char = "="c

    Private mSecondMostRecentSpectrumFileText As String
    Private mMostRecentSpectrumFileText As Text.StringBuilder

    Protected mInFileLineNumber As Integer

    Protected mCurrentSpectrum As clsSpectrumInfoMsMsText

    Protected mCurrentMsMsDataList As List(Of String)

    ' When true, read the data and populate mCurrentMsMsDataList but do not populate mCurrentSpectrum.MZList() or mCurrentSpectrum.IntensityList()
    Protected mReadTextDataOnly As Boolean

    Protected mTotalBytesRead As Long
    Protected mInFileStreamLength As Long

#End Region

#Region "Processing Options and Interface Functions"

    Public Property CommentLineStartChar() As Char
        Get
            Return mCommentLineStartChar
        End Get
        Set(Value As Char)
            mCommentLineStartChar = Value
        End Set
    End Property

    Public ReadOnly Property CurrentSpectrum As clsSpectrumInfoMsMsText
        Get
            Return mCurrentSpectrum
        End Get
    End Property

    Public Property ReadTextDataOnly() As Boolean
        Get
            Return mReadTextDataOnly
        End Get
        Set(Value As Boolean)
            mReadTextDataOnly = Value
        End Set
    End Property

    Public Property ThresholdIonPctForSingleCharge() As Single
        Get
            Return mThresholdIonPctForSingleCharge
        End Get
        Set(Value As Single)
            If Value < 0 Or Value > 100 Then Value = 10
            mThresholdIonPctForSingleCharge = Value
        End Set
    End Property

    Public Property ThresholdIonPctForDoubleCharge() As Single
        Get
            Return mThresholdIonPctForDoubleCharge
        End Get
        Set(Value As Single)
            If Value < 0 Or Value > 100 Then Value = 25
            mThresholdIonPctForDoubleCharge = Value
        End Set
    End Property

#End Region

    ''' <summary>
    ''' Remove any instance of strCommentChar from the beginning and end of strCommentIn
    ''' </summary>
    ''' <param name="strCommentIn"></param>
    ''' <param name="strCommentChar"></param>
    ''' <param name="blnRemoveQuoteMarks">When True, also look for double quotation marks at the beginning and end</param>
    ''' <returns></returns>
    Protected Function CleanupComment(
      strCommentIn As String,
      strCommentChar As Char,
      blnRemoveQuoteMarks As Boolean) As String

        ' Extract out the comment
        If strCommentIn Is Nothing Then
            strCommentIn = String.Empty
        Else
            strCommentIn = strCommentIn.TrimStart(strCommentChar).Trim()
            strCommentIn = strCommentIn.TrimEnd(strCommentChar).Trim()

            If blnRemoveQuoteMarks Then
                strCommentIn = strCommentIn.TrimStart(ControlChars.Quote)
                strCommentIn = strCommentIn.TrimEnd(ControlChars.Quote)
            End If

            strCommentIn = strCommentIn.Trim()
        End If

        Return strCommentIn
    End Function

    Protected Sub AddNewRecentFileText(strNewText As String, Optional blnNewSpectrum As Boolean = False,
                                       Optional blnAddCrLfIfNeeded As Boolean = True)

        If blnNewSpectrum Then
            mSecondMostRecentSpectrumFileText = mMostRecentSpectrumFileText.ToString
            mMostRecentSpectrumFileText.Length = 0
        End If

        If blnAddCrLfIfNeeded Then
            If Not (strNewText.EndsWith(ControlChars.Cr) Or strNewText.EndsWith(ControlChars.Lf)) Then
                strNewText &= ControlChars.NewLine
            End If
        End If

        mMostRecentSpectrumFileText.Append(strNewText)
    End Sub

    Public Overrides Sub CloseFile()
        Try
            If mFileReader IsNot Nothing Then
                mFileReader.Close()
            End If

            mInFileLineNumber = 0
            mInputFilePath = String.Empty
        Catch ex As Exception
        End Try
    End Sub

    Private Sub ComputePercentageDataAboveThreshold(
                                                      objSpectrumInfo As clsSpectrumInfoMsMsText,
                                                      <Out()> ByRef sngPctByCount As Single,
                                                      <Out()> ByRef sngPctByIntensity As Single)

        With objSpectrumInfo
            ComputePercentageDataAboveThreshold(.DataCount, .MZList, .IntensityList, .ParentIonMZ,
                                                sngPctByCount, sngPctByIntensity)
        End With
    End Sub

    Protected Sub ComputePercentageDataAboveThreshold(intDataCount As Integer,
                                                      dblMZList() As Double,
                                                      sngIntensityList() As Single,
                                                      dblThresholdMZ As Double,
                                                      <Out()> ByRef sngPctByCount As Single,
                                                      <Out()> ByRef sngPctByIntensity As Single)

        Dim intIndex As Integer

        Dim intCountAboveThreshold = 0
        Dim dblIntensitySumAboveThreshold As Double = 0
        Dim dblTotalIntensitySum As Double = 0

        For intIndex = 0 To intDataCount - 1
            dblTotalIntensitySum += sngIntensityList(intIndex)
            If dblMZList(intIndex) > dblThresholdMZ Then
                intCountAboveThreshold += 1
                dblIntensitySumAboveThreshold += sngIntensityList(intIndex)
            End If
        Next intIndex

        If intDataCount = 0 Then
            sngPctByCount = 0
            sngPctByIntensity = 0
        Else
            sngPctByCount = intCountAboveThreshold / CSng(intDataCount) * 100.0!
            sngPctByIntensity = CSng(dblIntensitySumAboveThreshold / dblTotalIntensitySum * 100.0)
        End If
    End Sub

    Public Function ExtractScanInfoFromDtaHeader(
      strSpectrumHeader As String,
      <Out()> ByRef intScanNumberStart As Integer,
      <Out()> ByRef intScanNumberEnd As Integer,
      <Out()> ByRef intScanCount As Integer) As Boolean
        Return ExtractScanInfoFromDtaHeader(strSpectrumHeader, intScanNumberStart, intScanNumberEnd, intScanCount, 0)
    End Function

    Public Function ExtractScanInfoFromDtaHeader(
      strSpectrumHeader As String,
      <Out()> ByRef intScanNumberStart As Integer,
      <Out()> ByRef intScanNumberEnd As Integer,
      <Out()> ByRef intScanCount As Integer,
      <Out()> ByRef intCharge As Integer) As Boolean

        ' The header should be similar to one of the following
        '   FileName.1234.1234.2.dta
        '   FileName.1234.1234.2      (StartScan.EndScan.Charge)
        '   FileName.1234.1234.       (Proteowizard uses this format to indicate unknown charge)
        ' Returns True if the scan numbers are found in the header

        ' ReSharper disable once UseImplicitlyTypedVariableEvident
        Static reDtaHeaderScanAndCharge As Regex = New Regex(".+\.(\d+)\.(\d+)\.(\d*)$", RegexOptions.Compiled)

        Dim blnScanNumberFound As Boolean
        Dim reMatch As Text.RegularExpressions.Match

        intScanNumberStart = 0
        intScanNumberEnd = 0
        intScanCount = 0
        intCharge = 0

        Try
            blnScanNumberFound = False
            If strSpectrumHeader IsNot Nothing Then
                strSpectrumHeader = strSpectrumHeader.Trim()
                If strSpectrumHeader.ToLower.EndsWith(".dta") Then
                    ' Remove the trailing .dta
                    strSpectrumHeader = strSpectrumHeader.Substring(0, strSpectrumHeader.Length - 4)
                End If

                ' Extract the scans and charge using a RegEx
                reMatch = reDtaHeaderScanAndCharge.Match(strSpectrumHeader)

                If reMatch.Success Then

                    If Int32.TryParse(reMatch.Groups(1).Value, intScanNumberStart) Then
                        If Int32.TryParse(reMatch.Groups(2).Value, intScanNumberEnd) Then

                            If intScanNumberEnd > intScanNumberStart Then
                                intScanCount = intScanNumberEnd - intScanNumberStart + 1
                            Else
                                intScanCount = 1
                            End If

                            blnScanNumberFound = True

                            ' Also try to parse out the charge
                            Int32.TryParse(reMatch.Groups(3).Value, intCharge)

                        End If
                    End If
                End If

            End If
        Catch ex As Exception
            LogErrors("ExtractScanInfoFromDtaHeader", ex.Message)
        End Try

        Return blnScanNumberFound
    End Function

    Protected Overrides Function GetInputFileLocation() As String
        Return "Line " & mInFileLineNumber.ToString
    End Function

    Public Function GetMSMSDataAsText() As List(Of String)
        If mCurrentMsMsDataList Is Nothing Then
            Return New List(Of String)
        Else
            Return mCurrentMsMsDataList
        End If
    End Function

    Public Function GetMostRecentSpectrumFileText() As String
        If mMostRecentSpectrumFileText Is Nothing Then
            Return String.Empty
        Else
            Return mMostRecentSpectrumFileText.ToString
        End If
    End Function

    Public Function GetSecondMostRecentSpectrumFileText() As String
        Return mSecondMostRecentSpectrumFileText
    End Function

    Public Function GetSpectrumTitle() As String
        Return mCurrentSpectrum.SpectrumTitle
    End Function

    Public Function GetSpectrumTitleWithCommentChars() As String
        Return mCurrentSpectrum.SpectrumTitleWithCommentChars
    End Function

    Public Sub GuesstimateCharge(objSpectrumInfo As clsSpectrumInfoMsMsText,
                                 Optional blnAddToExistingChargeList As Boolean = False,
                                 Optional blnForceChargeAddnFor2and3Plus As Boolean = False)

        ' Guesstimate the parent ion charge based on its m/z and the ions in the fragmentation spectrum
        '
        ' Strategy:
        '  1) If all frag peaks have m/z values less than the parent ion m/z, then definitely assume a
        '       1+ parent ion
        '
        '  2) If less than mThresholdIonPctForSingleCharge percent of the data's m/z values are greater
        '       than the parent ion, then definitely assume 1+ parent ion
        '     When determining percentage, use both # of data points and the sum of the ion intensities.
        '     Both values must be less than mThresholdIonPctForSingleCharge percent to declare 1+
        '
        '  3) If mThresholdIonPctForSingleCharge percent to mThresholdIonPctForDoubleCharge percent
        '       of the data's m/z values are greater than the parent ion, then declare 1+, 2+, 3+ ...
        '       up to the charge that gives a deconvoluted parent ion that matches the above test (#2)
        '     At a minimum, include 2+ and 3+
        '     Allow up to 5+
        '     Allow a 3 Da mass tolerance when comparing deconvoluted mass to maximum ion mass
        '     E.g. if parent ion m/z is 476, but frag data ranges from 624 to 1922, then guess 2+ to 5+
        '       Math involved: 476*2-1 = 951:  this is less than 1922
        '                      476*3-2 = 1426: this is less than 1922
        '                      476*4-3 = 1902: this is less than 1922
        '                      476*5-4 = 2376: this is greater than 1922
        '       Thus, assign charges 2+ to 5+
        '
        '  4) Otherwise, if both test 2 and test 3 fail, then assume 2+, 3+, ... up to the charge that
        '       gives a deconvoluted parent ion that matches the above test (#2)
        '     The same tests as outlined in step 3 will be performed to determine the maximum charge
        '       to assign

        ' Example, for parent ion at 700 m/z and following data, decide 1+, 2+, 3+ since percent above 700 m/z is 21%
        ' m/z		Intensity
        ' 300		10
        ' 325		15
        ' 400		40
        ' 450		20
        ' 470		30
        ' 520		15
        ' 580		50
        ' 650		40
        ' 720		10
        ' 760		30
        ' 820		5
        ' 830		15
        ' Sum all:	280
        ' Sum below 700:	220
        ' Sum above 700:	60
        ' % above 700 by intensity sum:	21%
        ' % above 700 by data point count:	33%

        Dim sngPctByCount As Single, sngPctByIntensity As Single
        Dim intChargeStart As Integer, intChargeEnd As Integer
        Dim intChargeIndex As Integer

        Dim dblParentIonMH As Double

        If objSpectrumInfo.DataCount <= 0 OrElse objSpectrumInfo.MZList Is Nothing Then
            ' This shouldn't happen, but we'll handle it anyway
            objSpectrumInfo.AddOrUpdateChargeList(1, False)
        Else
            ' Test 1: See if all m/z values are less than the parent ion m/z
            ' Assume the data in .IonList() is sorted by ascending m/z

            If objSpectrumInfo.MZList(objSpectrumInfo.DataCount - 1) <= objSpectrumInfo.ParentIonMZ Then
                ' Yes, all data is less than the parent ion m/z
                objSpectrumInfo.AddOrUpdateChargeList(1, blnAddToExistingChargeList)
            Else
                ' Find percentage of data with m/z values greater than the Parent Ion m/z
                ' Compute this number using both raw data point counts and sum of intensity values
                ComputePercentageDataAboveThreshold(objSpectrumInfo, sngPctByCount, sngPctByIntensity)
                If sngPctByCount < mThresholdIonPctForSingleCharge And
                   sngPctByIntensity < mThresholdIonPctForSingleCharge Then
                    ' Both percentages are less than the threshold for definitively single charge
                    objSpectrumInfo.AddOrUpdateChargeList(1, blnAddToExistingChargeList)
                Else
                    If sngPctByCount >= mThresholdIonPctForDoubleCharge And
                       sngPctByIntensity >= mThresholdIonPctForDoubleCharge Then
                        ' Both percentages are above the threshold for definitively double charge (or higher)
                        intChargeStart = 2
                    Else
                        intChargeStart = 1
                    End If
                    intChargeEnd = 3

                    ' Determine whether intChargeEnd should be higher than 3+
                    Do
                        dblParentIonMH = MyBase.ConvoluteMass(objSpectrumInfo.ParentIonMZ, intChargeEnd, 1)
                        If dblParentIonMH < objSpectrumInfo.MZList(objSpectrumInfo.DataCount - 1) + 3 Then
                            intChargeEnd += 1
                        Else
                            Exit Do
                        End If
                    Loop While intChargeEnd < clsSpectrumInfoMsMsText.MAX_CHARGE_COUNT

                    If blnAddToExistingChargeList Then
                        If Not blnForceChargeAddnFor2and3Plus And intChargeStart = 2 And intChargeEnd = 3 Then
                            ' See if objSpectrumInfo already contains a single entry and it is 2+ or 3+
                            ' If so, do not alter the charge list

                            If objSpectrumInfo.ParentIonChargeCount = 1 Then
                                If objSpectrumInfo.ParentIonCharges(0) = 2 OrElse
                                   objSpectrumInfo.ParentIonCharges(0) = 3 Then
                                    ' The following will guarantee that the For intChargeIndex loop doesn't run
                                    intChargeStart = 0
                                    intChargeEnd = -1
                                End If
                            End If

                        End If
                    Else
                        objSpectrumInfo.ParentIonChargeCount = 0
                    End If

                    For intChargeIndex = 0 To intChargeEnd - intChargeStart
                        objSpectrumInfo.AddOrUpdateChargeList(intChargeStart + intChargeIndex, True)
                    Next intChargeIndex

                End If
            End If
        End If
    End Sub

    Protected Overrides Sub InitializeLocalVariables()
        MyBase.InitializeLocalVariables()

        mTotalBytesRead = 0

        mThresholdIonPctForSingleCharge = 10    ' Percentage
        mThresholdIonPctForDoubleCharge = 25    ' Percentage

        mMostRecentSpectrumFileText = New Text.StringBuilder With {
            .Length = 0
        }

        mSecondMostRecentSpectrumFileText = String.Empty

        mInFileLineNumber = 0

        mCurrentMsMsDataList = New List(Of String)
    End Sub

    Public Overrides Function OpenFile(strInputFilePath As String) As Boolean
        ' Returns true if the file is successfully opened

        Dim blnSuccess As Boolean
        Dim objStreamReader As IO.StreamReader

        Try
            blnSuccess = OpenFileInit(strInputFilePath)
            If Not blnSuccess Then Return False

            objStreamReader = New IO.StreamReader(New IO.FileStream(strInputFilePath, IO.FileMode.Open,
                                                                    IO.FileAccess.Read, IO.FileShare.ReadWrite))
            mInFileStreamLength = objStreamReader.BaseStream.Length
            mFileReader = objStreamReader

            InitializeLocalVariables()

            MyBase.ResetProgress("Parsing " & IO.Path.GetFileName(strInputFilePath))

            blnSuccess = True

        Catch ex As Exception
            mErrorMessage = "Error opening file: " & strInputFilePath & "; " & ex.Message
            blnSuccess = False
        End Try

        Return blnSuccess
    End Function

    Public Overrides Function OpenTextStream(strTextStream As String) As Boolean
        ' Returns true if the text stream is successfully opened

        Dim blnSuccess As Boolean

        ' Make sure any open file or text stream is closed
        CloseFile()

        Try
            mInputFilePath = "TextStream"

            mFileReader = New IO.StringReader(strTextStream)
            mInFileStreamLength = strTextStream.Length

            InitializeLocalVariables()

            MyBase.ResetProgress("Parsing text stream")

            blnSuccess = True
        Catch ex As Exception
            mErrorMessage = "Error opening text stream"
            blnSuccess = False
        End Try

        Return blnSuccess
    End Function

    Public Function ParseMsMsDataList(
      strMSMSData() As String, intMsMsDataCount As Integer,
      <Out()> ByRef dblMasses() As Double,
      <Out()> ByRef sngIntensities() As Single,
      blnShrinkDataArrays As Boolean) As Integer

        Dim lstMSMSData = New List(Of String)

        For intIndex = 0 To intMsMsDataCount - 1
            lstMSMSData.Add(strMSMSData(intIndex))
        Next

        Return ParseMsMsDataList(lstMSMSData, dblMasses, sngIntensities, blnShrinkDataArrays)
    End Function

    Public Function ParseMsMsDataList(
      lstMSMSData As List(Of String),
      <Out()> ByRef dblMasses() As Double,
      <Out()> ByRef sngIntensities() As Single,
      blnShrinkDataArrays As Boolean) As Integer

        ' Returns the number of data points in dblMasses() and sngIntensities()
        ' If blnShrinkDataArrays = False, then will not shrink dblMasses or sngIntensities

        Dim strSplitLine() As String

        Dim intDataCount As Integer

        Dim strSepChars = New Char() {" "c, ControlChars.Tab}

        If lstMSMSData IsNot Nothing AndAlso lstMSMSData.Count > 0 Then

            ReDim dblMasses(lstMSMSData.Count - 1)
            ReDim sngIntensities(lstMSMSData.Count - 1)

            intDataCount = 0
            For Each strItem As String In lstMSMSData

                ' Each line in strMSMSData should contain a mass and intensity pair, separated by a space or Tab
                ' MGF files sometimes contain a third number, the charge of the ion
                ' Use the .Split function to parse the numbers in the line to extract the mass and intensity, and ignore the charge (if present)
                strSplitLine = strItem.Split(strSepChars)
                If strSplitLine.Length >= 2 Then
                    If IsNumber(strSplitLine(0)) And IsNumber(strSplitLine(1)) Then
                        dblMasses(intDataCount) = CDbl(strSplitLine(0))
                        sngIntensities(intDataCount) = CSng(strSplitLine(1))
                        intDataCount += 1
                    End If
                End If
            Next

            If intDataCount <= 0 Then
                ReDim dblMasses(0)
                ReDim sngIntensities(0)
            Else
                If intDataCount <> lstMSMSData.Count And blnShrinkDataArrays Then
                    ReDim Preserve dblMasses(intDataCount - 1)
                    ReDim Preserve sngIntensities(intDataCount - 1)
                End If
            End If

        Else
            intDataCount = 0
            ReDim dblMasses(0)
            ReDim sngIntensities(0)
        End If

        Return intDataCount
    End Function

    Protected Sub UpdateStreamReaderProgress()
        Dim objStreamReader = TryCast(mFileReader, IO.StreamReader)

        If objStreamReader IsNot Nothing Then
            MyBase.UpdateProgress((objStreamReader.BaseStream.Position / objStreamReader.BaseStream.Length * 100.0))
        ElseIf mInFileStreamLength > 0 Then
            MyBase.UpdateProgress(mTotalBytesRead / mInFileStreamLength * 100.0)
        End If
    End Sub
End Class
