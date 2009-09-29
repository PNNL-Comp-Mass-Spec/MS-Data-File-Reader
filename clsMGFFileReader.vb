Option Strict On

' This class can be used to open a Mascot Generic File (.MGF) and return each spectrum present
'
' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2005, Battelle Memorial Institute.  All Rights Reserved.
' Started November 15, 2003
'
' E-mail: matthew.monroe@pnl.gov or matt@alchemistmatt.com
' Website: http://ncrr.pnl.gov/ or http://www.sysbio.org/resources/staff/
' -------------------------------------------------------------------------------
'
' Last modified December 14, 2006

Public Class clsMGFFileReader
    Inherits clsMSTextFileReaderBaseClass

    Public Sub New()
        Me.New(True)
    End Sub

    Public Sub New(ByVal blnCombineIdenticalSpectra As Boolean)
        InitializeLocalVariables()
    End Sub

#Region "Constants and Enums"
    ' Note: The extension must be in all caps
    Public Const MGF_FILE_EXTENSION As String = ".MGF"

    Protected Const COMMENT_LINE_START_CHAR As Char = "#"c        ' The comment character is an Equals sign
    Protected Const LINE_START_BEGIN_IONS As String = "BEGIN IONS"
    Protected Const LINE_START_END_IONS As String = "END IONS"

    Protected Const LINE_START_MSMS As String = "MSMS:"
    Protected Const LINE_START_PEPMASS As String = "PEPMASS="
    Protected Const LINE_START_CHARGE As String = "CHARGE="
    Protected Const LINE_START_TITLE As String = "TITLE="
#End Region

#Region "Classwide Variables"
    ' mScanNumberStartSaved is used to create fake scan numbers when reading .MGF files that do not have
    '  scan numbers defined using   ###MSMS: #1234   or   TITLE=Filename.1234.1234.2.dta
    Protected mScanNumberStartSaved As Integer

#End Region

    Protected Overrides Sub InitializeLocalVariables()
        MyBase.InitializeLocalVariables()

        mCommentLineStartChar = COMMENT_LINE_START_CHAR
        mScanNumberStartSaved = 0
    End Sub

    Protected Overrides Sub LogErrors(ByVal strCallingFunction As String, ByVal strErrorDescription As String)
        MyBase.LogErrors("clsMGFFileReader." & strCallingFunction, strErrorDescription)
    End Sub

    Public Overrides Function ReadNextSpectrum(ByRef objSpectrumInfo As clsSpectrumInfo) As Boolean
        ' Reads the next spectrum from a .MGF file
        ' Returns True if a spectrum is found, otherwise, returns False

        Dim strLineIn As String, strRemaining As String, strTemp As String
        Dim strSplitLine() As String
        Dim strSepChars As Char() = New Char() {" "c, ControlChars.Tab}

        Dim intIndex As Integer
        Dim intCharIndex As Integer
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
            If Not objSpectrumInfo Is Nothing Then
                mCurrentSpectrum.AutoShrinkDataLists = objSpectrumInfo.AutoShrinkDataLists
            End If

            blnSpectrumFound = False
            blnScanNumberFound = False
            If srInFile Is Nothing Then
                If objSpectrumInfo Is Nothing Then
                    objSpectrumInfo = New clsSpectrumInfoMsMsText
                Else
                    objSpectrumInfo.Clear()
                End If
                mErrorMessage = "Data file not currently open"
            Else
                MyBase.AddNewRecentFileText(String.Empty, True, False)

                With mCurrentSpectrum
                    .SpectrumTitleWithCommentChars = String.Empty
                    .SpectrumTitle = String.Empty
                    .MSLevel = 2
                End With

                intLastProgressUpdateLine = mInFileLineNumber
                Do While Not blnSpectrumFound And srInFile.Peek() >= 0 And Not mAbortProcessing

                    strLineIn = srInFile.ReadLine
                    If Not strLineIn Is Nothing Then mTotalBytesRead += strLineIn.Length + 2
                    mInFileLineNumber += 1

                    If Not strLineIn Is Nothing AndAlso strLineIn.Trim.Length > 0 Then
                        MyBase.AddNewRecentFileText(strLineIn)
                        strLineIn = strLineIn.Trim

                        ' See if strLineIn starts with the comment line start character (a pound sign, #)
                        If strLineIn.StartsWith(mCommentLineStartChar) Then
                            ' Remove any comment characters at the start of strLineIn
                            strLineIn = strLineIn.TrimStart(mCommentLineStartChar).Trim

                            ' Look for LINE_START_MSMS in strLineIn
                            ' This will be present in MGF files created using Agilent's DataAnalysis software
                            If strLineIn.ToUpper.StartsWith(LINE_START_MSMS) Then
                                strLineIn = strLineIn.Substring(5).Trim

                                ' Initialize these values
                                mCurrentSpectrum.ScanNumberEnd = 0
                                mCurrentSpectrum.ScanCount = 1

                                ' Remove the # sign in front of the scan number
                                strLineIn = strLineIn.TrimStart("#"c).Trim

                                ' Look for the / sign and remove any text following it
                                ' For example, 
                                '   ###MS: 4458/4486/
                                '   ###MSMS: 4459/4488/
                                ' The / sign is used to indicate that several MS/MS scans were combined to make the given spectrum; we'll just keep the first one
                                intCharIndex = strLineIn.IndexOf("/"c)
                                If intCharIndex > 0 Then
                                    If intCharIndex < strLineIn.Length - 1 Then
                                        strTemp = strLineIn.Substring(intCharIndex + 1).Trim
                                    Else
                                        strTemp = String.Empty
                                    End If

                                    strLineIn = strLineIn.Substring(0, intCharIndex).Trim
                                    mCurrentSpectrum.ScanCount = 1

                                    If strTemp.Length > 0 Then
                                        Do
                                            intCharIndex = strTemp.IndexOf("/"c)
                                            If intCharIndex > 0 Then
                                                mCurrentSpectrum.ScanCount += 1
                                                If intCharIndex < strTemp.Length - 1 Then
                                                    strTemp = strTemp.Substring(intCharIndex + 1).Trim
                                                Else
                                                    strTemp = strTemp.Substring(0, intCharIndex).Trim
                                                    Exit Do
                                                End If
                                            Else
                                                Exit Do
                                            End If
                                        Loop

                                        If MyBase.IsNumber(strTemp) Then
                                            mCurrentSpectrum.ScanNumberEnd = CInt(strTemp)
                                        End If
                                    End If
                                End If

                                intCharIndex = strLineIn.IndexOf("-"c)
                                If intCharIndex > 0 Then
                                    ' strLineIn contains a dash, and thus a range of scans
                                    strRemaining = strLineIn.Substring(intCharIndex + 1).Trim
                                    strLineIn = strLineIn.Substring(0, intCharIndex).Trim

                                    If MyBase.IsNumber(strLineIn) Then
                                        mCurrentSpectrum.ScanNumber = CInt(strLineIn)
                                        If MyBase.IsNumber(strRemaining) Then
                                            If mCurrentSpectrum.ScanNumberEnd = 0 Then
                                                mCurrentSpectrum.ScanNumberEnd = CInt(strRemaining)
                                            End If
                                        Else
                                            mCurrentSpectrum.ScanNumberEnd = mCurrentSpectrum.ScanNumber
                                        End If
                                        blnScanNumberFound = True
                                    End If
                                Else
                                    If MyBase.IsNumber(strLineIn) Then
                                        mCurrentSpectrum.ScanNumber = CInt(strLineIn)
                                        If mCurrentSpectrum.ScanNumberEnd = 0 Then
                                            mCurrentSpectrum.ScanNumberEnd = mCurrentSpectrum.ScanNumber
                                        End If
                                        blnScanNumberFound = True
                                    End If
                                End If

                                If blnScanNumberFound Then
                                    mCurrentSpectrum.SpectrumID = mCurrentSpectrum.ScanNumber
                                End If
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
                                    mcurrentspectrum.ScanCount = 1
                                End If

                                ' Initialize mCurrentMsMsDataList
                                mCurrentMsMsDataCount = 0
                                If mCurrentMsMsDataList Is Nothing OrElse mCurrentMsMsDataList.Length < 100 Then
                                    ReDim mCurrentMsMsDataList(99)
                                End If

                                blnParentIonFound = False

                                ' We have found an MS/MS scan
                                ' Look for LINE_START_PEPMASS and LINE_START_CHARGE to determine the parent ion m/z and charge
                                Do While srInFile.Peek() >= 0
                                    strLineIn = srInFile.ReadLine
                                    mInFileLineNumber += 1

                                    If Not strLineIn Is Nothing Then
                                        mTotalBytesRead += strLineIn.Length + 2
                                        MyBase.AddNewRecentFileText(strLineIn)

                                        If strLineIn.Trim.Length > 0 Then
                                            strLineIn = strLineIn.Trim
                                            If strLineIn.ToUpper.StartsWith(LINE_START_PEPMASS) Then
                                                ' This line defines the peptide mass as an m/z value
                                                ' It may simply contain the m/z value, or it may also contain an intensity value
                                                ' The two values will be separated by a space or a tab
                                                ' We do not save the intensity value since it cannot be included in a .Dta file
                                                strLineIn = strLineIn.Substring(8).Trim
                                                strSplitLine = strLineIn.Split(strSepChars)
                                                If strSplitLine.Length > 0 AndAlso MyBase.IsNumber(strSplitLine(0)) Then
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
                                                strLineIn = strLineIn.Substring(7).Trim

                                                ' Remove any + signs in the line
                                                strLineIn = strLineIn.Replace("+", "")
                                                If strLineIn.IndexOf(" ") > 0 Then
                                                    ' Multiple charges may be present
                                                    strSplitLine = strLineIn.Split(strSepChars)
                                                    For intIndex = 0 To strSplitLine.Length - 1
                                                        ' Step through the split line and add any numbers to the charge list
                                                        ' Typically, strSplitLine(1) will contain "and"
                                                        If MyBase.IsNumber(strSplitLine(intIndex).Trim) Then
                                                            With mCurrentSpectrum
                                                                If .ParentIonChargeCount < mCurrentSpectrum.MAX_CHARGE_COUNT Then
                                                                    .ParentIonCharges(.ParentIonChargeCount) = CInt(strSplitLine(intIndex).Trim)
                                                                    .ParentIonChargeCount += 1
                                                                End If
                                                            End With
                                                        End If
                                                    Next intIndex
                                                Else
                                                    If MyBase.IsNumber(strLineIn) Then
                                                        With mCurrentSpectrum
                                                            .ParentIonChargeCount = 1
                                                            .ParentIonCharges(0) = CInt(strLineIn)
                                                        End With
                                                    End If
                                                End If

                                            ElseIf strLineIn.ToUpper.StartsWith(LINE_START_TITLE) Then
                                                mCurrentSpectrum.SpectrumTitle = strLineIn
                                                mCurrentSpectrum.SpectrumTitleWithCommentChars = strLineIn

                                                If Not blnScanNumberFound Then
                                                    ' We didn't find a scan number in a ### MSMS: comment line
                                                    ' See if the Title ends in .dta
                                                    ' If it does, extract out the scan numbers from the title
                                                    strLineIn = strLineIn.Substring(6).Trim
                                                    With mCurrentSpectrum
                                                        MyBase.ExtractScanInfoFromDtaHeader(strLineIn, .ScanNumber, .ScanNumberEnd, .ScanCount)
                                                    End With
                                                End If
                                            ElseIf strLineIn.ToUpper.StartsWith(LINE_START_END_IONS) Then
                                                ' Empty ion list
                                                Exit Do
                                            ElseIf Char.IsNumber(strLineIn, 0) Then
                                                ' Found the start of the ion list
                                                ' Add to the MsMs data list
                                                If blnParentIonFound Then
                                                    If mCurrentMsMsDataCount >= mCurrentMsMsDataList.Length Then
                                                        ReDim Preserve mCurrentMsMsDataList(mCurrentMsMsDataCount + 100 - 1)
                                                    End If
                                                    mCurrentMsMsDataList(mCurrentMsMsDataCount) = strLineIn
                                                    mCurrentMsMsDataCount += 1
                                                End If

                                                Exit Do
                                            End If

                                        End If
                                    End If
                                Loop

                                If blnParentIonFound Then
                                    ' We have determined the parent ion

                                    ' Note: MGF files have Parent Ion MZ defined by not Parent Ion MH
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
                                    Do While srInFile.Peek >= 0
                                        strLineIn = srInFile.ReadLine
                                        mInFileLineNumber += 1

                                        ' See if strLineIn is blank
                                        If Not strLineIn Is Nothing Then
                                            mTotalBytesRead += strLineIn.Length + 2
                                            MyBase.AddNewRecentFileText(strLineIn)

                                            If strLineIn.Trim.Length > 0 Then
                                                If strLineIn.Trim.ToUpper.StartsWith(LINE_START_END_IONS) Then
                                                    Exit Do
                                                Else
                                                    If mCurrentMsMsDataCount >= mCurrentMsMsDataList.Length Then
                                                        ReDim Preserve mCurrentMsMsDataList(mCurrentMsMsDataCount + 100 - 1)
                                                    End If
                                                    ' Add to MS/MS data sting list
                                                    mCurrentMsMsDataList(mCurrentMsMsDataCount) = strLineIn.Trim
                                                    mCurrentMsMsDataCount += 1
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
                                                .DataCount = MyBase.ParseMsMsDataList(mCurrentMsMsDataList, mCurrentMsMsDataCount, .MZList, .IntensityList, .AutoShrinkDataLists)

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

                        If TypeOf srInFile Is System.IO.StreamReader Then
                            With CType(srInFile, System.IO.StreamReader)
                                MyBase.UpdateProgress((.BaseStream.Position / .BaseStream.Length * 100.0))
                            End With
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
        End Try

        Return blnSpectrumFound

    End Function

End Class
