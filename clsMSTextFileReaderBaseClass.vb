Option Strict On

' This is the base class for the DTA and MGF file readers
'
' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2006, Battelle Memorial Institute.  All Rights Reserved.
' Started March 26, 2006
'
' E-mail: matthew.monroe@pnl.gov or matt@alchemistmatt.com
' Website: http://ncrr.pnl.gov/ or http://www.sysbio.org/resources/staff/
' -------------------------------------------------------------------------------
'
' Last modified September 24, 2009

Public MustInherit Class clsMSTextFileReaderBaseClass
    Inherits clsMsDataFileReaderBaseClass

    Public Sub New()
        InitializeLocalVariables()
    End Sub

    Protected Overrides Sub Finalize()
        MyBase.Finalize()

        Try
            If Not srInFile Is Nothing Then
                srInFile.Close()
            End If
        Catch ex As Exception
        End Try

    End Sub

#Region "Constants and Enums"
#End Region

#Region "Structures"
#End Region

#Region "Classwide Variables"
    Protected mThresholdIonPctForSingleCharge As Single       ' Number between 0 and 100; if the percentage of ions greater than the parent ion m/z is less than this number, then the charge is definitely 1+
    Protected mThresholdIonPctForDoubleCharge As Single       ' Number between 0 and 100; if the percentage of ions greater than the parent ion m/z is greater than this number, then the charge is definitely 2+ or higher

    Protected srInFile As System.IO.TextReader
    Protected mCommentLineStartChar As Char = "="c

    Private mSecondMostRecentSpectrumFileText As String
    Private mMostRecentSpectrumFileText As System.Text.StringBuilder

    Protected mInFileLineNumber As Integer

    Protected mCurrentSpectrum As clsSpectrumInfoMsMsText
    Protected mCurrentMsMsDataCount As Integer
    Protected mCurrentMsMsDataList() As String

    Protected mReadTextDataOnly As Boolean  ' When true, then reads the data and populates mCurrentMsMsDataList() but does not populate mCurrentSpectrum.MZList() or mCurrentSpectrum.IntensityList()

    Protected mTotalBytesRead As Long
    Protected mInFileStreamLength As Long
#End Region

#Region "Processing Options and Interface Functions"
    Public Property CommentLineStartChar() As Char
        Get
            Return mCommentLineStartChar
        End Get
        Set(ByVal Value As Char)
            mCommentLineStartChar = Value
        End Set
    End Property

    Public Property ReadTextDataOnly() As Boolean
        Get
            Return mReadTextDataOnly
        End Get
        Set(ByVal Value As Boolean)
            mReadTextDataOnly = Value
        End Set
    End Property

    Public Property ThresholdIonPctForSingleCharge() As Single
        Get
            Return mThresholdIonPctForSingleCharge
        End Get
        Set(ByVal Value As Single)
            If Value < 0 Or Value > 100 Then Value = 10
            mThresholdIonPctForSingleCharge = Value
        End Set
    End Property

    Public Property ThresholdIonPctForDoubleCharge() As Single
        Get
            Return mThresholdIonPctForDoubleCharge
        End Get
        Set(ByVal Value As Single)
            If Value < 0 Or Value > 100 Then Value = 25
            mThresholdIonPctForDoubleCharge = Value
        End Set
    End Property

#End Region

    Protected Function CleanupComment(ByVal strCommentIn As String, ByVal strCommentChar As Char, ByVal blnRemoveQuoteMarks As Boolean) As String
        ' This function will remove any instance of strCommentChar from the beginning and end of strCommentIn
        ' If blnRemoveQuoteMarks is True, then also looks for double quotation marks at the beginning and end

        ' Extract out the comment
        If strCommentIn Is Nothing Then
            strCommentIn = String.Empty
        Else
            strCommentIn = strCommentIn.TrimStart(strCommentChar).Trim
            strCommentIn = strCommentIn.TrimEnd(strCommentChar).Trim

            If blnRemoveQuoteMarks Then
                strCommentIn = strCommentIn.TrimStart(ControlChars.Quote)
                strCommentIn = strCommentIn.TrimEnd(ControlChars.Quote)
            End If

            strCommentIn = strCommentIn.Trim
        End If

        Return strCommentIn

    End Function

    Protected Sub AddNewRecentFileText(ByVal strNewText As String, Optional ByVal blnNewSpectrum As Boolean = False, Optional ByVal blnAddCrLfIfNeeded As Boolean = True)

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
            If Not srInFile Is Nothing Then
                srInFile.Close()
            End If

            mInFileLineNumber = 0
        Catch ex As Exception
        End Try

    End Sub

    Protected Sub ComputePercentageDataAboveThreshold(ByRef objSpectrumInfo As clsSpectrumInfoMsMsText, _
                                                      ByRef sngPctByCount As Single, _
                                                      ByRef sngPctByIntensity As Single)
        With objSpectrumInfo
            ComputePercentageDataAboveThreshold(.DataCount, .MZList, .IntensityList, .ParentIonMZ, _
                                                sngPctByCount, sngPctByIntensity)
        End With
    End Sub

    Protected Sub ComputePercentageDataAboveThreshold(ByVal intDataCount As Integer, _
                                                      ByRef dblMZList() As Double, _
                                                      ByRef sngIntensityList() As Single, _
                                                      ByVal dblThresholdMZ As Double, _
                                                      ByRef sngPctByCount As Single, _
                                                      ByRef sngPctByIntensity As Single)

        Dim intIndex As Integer

        Dim intCountAboveThreshold As Integer = 0
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

    Public Function ExtractScanInfoFromDtaHeader(ByVal strSpectrumHeader As String, ByRef intScanNumberStart As Integer, ByRef intScanNumberEnd As Integer, ByRef intScanCount As Integer) As Boolean
        ' The header should be similar to: FileName.1234.1234.2.dta
        ' Returns True if the scan numbers are found in the header

        Dim strSplitLine() As String
        Dim blnScanNumberFound As Boolean

        Try
            blnScanNumberFound = False
            If Not strSpectrumHeader Is Nothing AndAlso strSpectrumHeader.ToLower.Trim.EndsWith(".dta") Then
                ' Remove the trailing charge and .dta
                strSpectrumHeader = strSpectrumHeader.Trim
                strSpectrumHeader = strSpectrumHeader.Substring(0, strSpectrumHeader.Length - 6)

                If Char.IsNumber(strSpectrumHeader.Chars(strSpectrumHeader.Length - 1)) Then
                    ' Split on the periods
                    strSplitLine = strSpectrumHeader.Split("."c)

                    If strSplitLine.Length >= 3 Then
                        ' Reverse strSplitLine
                        Array.Reverse(strSplitLine)

                        If MyBase.IsNumber(strSplitLine(0)) And MyBase.IsNumber(strSplitLine(1)) Then
                            ' Note: because we reversed strSplitLine, the start scan is at index 1, the end scan is at index 0
                            intScanNumberStart = CInt(strSplitLine(1))
                            intScanNumberEnd = CInt(strSplitLine(0))

                            If intScanNumberEnd > intScanNumberStart Then
                                intScanCount = intScanNumberEnd - intScanNumberStart + 1
                            Else
                                intScanCount = 1
                            End If

                            blnScanNumberFound = True
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

    Public Sub GuesstimateCharge(ByRef objSpectrumInfo As clsSpectrumInfoMsMsText, _
                         Optional ByVal blnAddToExistingChargeList As Boolean = False, _
                         Optional ByVal blnForceChargeAddnFor2and3Plus As Boolean = False)

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
                If sngPctByCount < mThresholdIonPctForSingleCharge And _
                   sngPctByIntensity < mThresholdIonPctForSingleCharge Then
                    ' Both percentages are less than the threshold for definitively single charge
                    objSpectrumInfo.AddOrUpdateChargeList(1, blnAddToExistingChargeList)
                Else
                    If sngPctByCount >= mThresholdIonPctForDoubleCharge And _
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
                    Loop While intChargeEnd < objSpectrumInfo.MAX_CHARGE_COUNT

                    If blnAddToExistingChargeList Then
                        If Not blnForceChargeAddnFor2and3Plus And intChargeStart = 2 And intChargeEnd = 3 Then
                            ' See if objSpectrumInfo already contains a single entry and it is 2+ or 3+
                            ' If so, do not alter the charge list

                            If objSpectrumInfo.ParentIonChargeCount = 1 Then
                                If objSpectrumInfo.ParentIonCharges(0) = 2 OrElse _
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

        mMostRecentSpectrumFileText = New System.text.StringBuilder
        mMostRecentSpectrumFileText.Length = 0

        mSecondMostRecentSpectrumFileText = String.Empty

        mInFileLineNumber = 0
    End Sub

    Public Overrides Function OpenFile(ByVal strInputFilePath As String) As Boolean
        ' Returns true if the file is successfully opened

        Dim blnSuccess As Boolean
        Dim objStreamReader As System.IO.StreamReader

        ' Make sure any open file or text stream is closed
        CloseFile()

        Try
            If strInputFilePath Is Nothing Then
                strInputFilePath = String.Empty
            End If

            objStreamReader = New System.IO.StreamReader(strInputFilePath)
            mInFileStreamLength = objStreamReader.BaseStream.Length
            srInFile = objStreamReader

            InitializeLocalVariables()

            MyBase.ResetProgress("Parsing " & System.IO.Path.GetFileName(strInputFilePath))

            blnSuccess = True

        Catch ex As Exception
            mErrorMessage = "Error opening file: " & strInputFilePath & "; " & ex.Message
            blnSuccess = False
        End Try

        Return blnSuccess

    End Function

    Public Overrides Function OpenTextStream(ByRef strTextStream As String) As Boolean
        ' Returns true if the text stream is successfully opened

        Dim blnSuccess As Boolean

        ' Make sure any open file or text stream is closed
        CloseFile()

        Try
            If strTextStream Is Nothing Then
                strTextStream = String.Empty
            End If

            srInFile = New System.IO.StringReader(strTextStream)
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

    Public Function ParseMsMsDataList(ByVal strMSMSData() As String, ByVal intMsMsDataCount As Integer, ByRef dblMasses() As Double, ByRef sngIntensities() As Single, ByVal blnShrinkDataArrays As Boolean) As Integer
        ' Returns the number of data points in dblMasses() and sngIntensities()
        ' If blnShrinkDataArrays = False, then will not shrink dblMasses or sngIntensities

        Dim strSplitLine() As String

        Dim intDataCount As Integer

        Dim strSepChars As Char() = New Char() {" "c, ControlChars.Tab}

        If intMsMsDataCount > 0 Then
            If blnShrinkDataArrays OrElse dblMasses Is Nothing OrElse sngIntensities Is Nothing OrElse _
               dblMasses.Length < intMsMsDataCount OrElse sngIntensities.Length < intMsMsDataCount Then
                ReDim dblMasses(intMsMsDataCount - 1)
                ReDim sngIntensities(intMsMsDataCount - 1)
            End If

            intDataCount = 0
            For i As Integer = 0 To intMsMsDataCount - 1
                ' Each line in strMSMSData should contain a mass and intensity pair, separated by a space or Tab
                ' MGF files sometimes contain a third number, the charge of the ion
                ' Use the .Split function to parse the numbers in the line to extract the mass and intensity, and ignore the charge (if present)
                strSplitLine = strMSMSData(i).Split(strSepChars)
                If strSplitLine.Length >= 2 Then
                    If MyBase.IsNumber(strSplitLine(0)) And MyBase.IsNumber(strSplitLine(1)) Then
                        dblMasses(intDataCount) = CDbl(strSplitLine(0))
                        sngIntensities(intDataCount) = CSng(strSplitLine(1))
                        intDataCount += 1
                    End If
                End If
            Next

        Else
            intDataCount = 0
        End If

        If intDataCount <= 0 Then
            ReDim dblMasses(0)
            ReDim sngIntensities(0)
        Else
            If intDataCount <> intMsMsDataCount And blnShrinkDataArrays Then
                ReDim Preserve dblMasses(intDataCount - 1)
                ReDim Preserve sngIntensities(intDataCount - 1)
            End If
        End If

        Return intDataCount

    End Function

    Protected Sub UpdateStreamReaderProgress()
        If TypeOf srInFile Is System.IO.StreamReader Then
            With CType(srInFile, System.IO.StreamReader)
                MyBase.UpdateProgress((.BaseStream.Position / .BaseStream.Length * 100.0))
            End With
        ElseIf mInFileStreamLength > 0 Then
            MyBase.UpdateProgress(mTotalBytesRead / mInFileStreamLength * 100.0)
        End If
    End Sub

End Class
