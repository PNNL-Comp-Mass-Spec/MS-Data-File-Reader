Option Strict On

' This module can be used to test the data file readers
'
' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2006, Battelle Memorial Institute.  All Rights Reserved.
' Program started March 24, 2006
'
' E-mail: matthew.monroe@pnl.gov or matt@alchemistmatt.com
' Website: http://ncrr.pnl.gov/ or http://www.sysbio.org/resources/staff/
' -------------------------------------------------------------------------------
'
' Last modified May 23, 2006

Module modMain

    Private WithEvents mMSFileReader As MSDataFileReader.clsMSDataFileReaderBaseClass
    Private mProgressForm As ProgressFormNET.frmProgress

    Public Sub Main()
        Try
            mProgressForm = New ProgressFormNET.frmProgress

            'TestDTATextReader(MSDataFileReader.clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Cached)
            'TestDTATextReader(MSDataFileReader.clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Sequential)

            'TestMGFReader(MSDataFileReader.clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Cached)
            'TestMGFReader(MSDataFileReader.clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Sequential)

            TestMZXmlReader(MSDataFileReader.clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Cached)
            TestMZXmlReader(MSDataFileReader.clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Sequential)
            TestMZXmlReader(MSDataFileReader.clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Indexed)

            'TestMZDataReader(MSDataFileReader.clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Cached)
            'TestMZDataReader(MSDataFileReader.clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Sequential)
            'TestMZDataReader(MSDataFileReader.clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Indexed)

            'TestBinaryTextReader("SampleData_QC_Standards_Excerpt.mzXML")
            'TestBinaryTextReader("Unicode_SampleData_myo_excerpt_1.05cv.mzdata")
            'TestBinaryTextReader("Combined_BigEndian.mzXml")

        Catch ex As Exception
            Console.WriteLine("Error: " & ex.Message)
        Finally
            mProgressForm.Close()
        End Try

    End Sub

    ''Private Sub TestRegEx()
    ''    Dim text As String = "One car red car blue car"
    ''    Dim pat As String = "(\w+)\s+(car)"

    ''    ' Compile the regular expression.
    ''    Dim r As System.Text.RegularExpressions.Regex = New System.Text.RegularExpressions.Regex(pat, System.Text.RegularExpressions.RegexOptions.IgnoreCase)
    ''    ' Match the regular expression pattern against a text string.
    ''    Dim m As System.Text.RegularExpressions.Match = r.Match(text)

    ''    Dim matchcount As Integer = 0
    ''    While (m.Success)
    ''        matchcount += 1
    ''        Console.WriteLine("Match" & (matchcount))
    ''        Dim i As Integer
    ''        For i = 1 To 2
    ''            Dim g As System.Text.RegularExpressions.Group = m.Groups(i)
    ''            Console.WriteLine("Group" & i & "='" & g.ToString() & "'")

    ''            Dim cc As System.Text.RegularExpressions.CaptureCollection = g.Captures
    ''            Dim j As Integer
    ''            For j = 0 To cc.Count - 1

    ''                Dim c As System.Text.RegularExpressions.Capture = cc(j)
    ''                Console.WriteLine("Capture" & j & "='" & c.ToString() _
    ''                   & "', Position=" & c.Index)
    ''            Next j
    ''        Next i
    ''        m = m.NextMatch()
    ''    End While

    ''End Sub

    Private Sub LogFileReadEvent(ByVal strInputFilePath As String, ByVal strTask As String, ByVal tsElapsedTime As TimeSpan, ByVal strAdditionalInfo As String)
        Dim strLogFilePath As String

        Dim objFileInfo As System.IO.FileInfo
        Dim dblFileSizeMB As Double

        Dim swOutFile As System.IO.StreamWriter

        Try
            objFileInfo = New System.IO.FileInfo(strInputFilePath)
            dblFileSizeMB = objFileInfo.Length / 1024.0 / 1024
        Catch ex As Exception
            dblFileSizeMB = 0
        End Try

        Try
            strLogFilePath = System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location) & "_EventLog.txt"

            swOutFile = New System.IO.StreamWriter(strLogFilePath, True)
            swOutFile.WriteLine(System.DateTime.Now().ToString & ControlChars.Tab & _
                                System.IO.Path.GetFileName(strInputFilePath) & ControlChars.Tab & _
                                Math.Round(dblFileSizeMB, 2) & ControlChars.Tab & _
                                strTask & ControlChars.Tab & _
                                tsElapsedTime.TotalSeconds & ControlChars.Tab & _
                                strAdditionalInfo)
            swOutFile.Close()

        Catch ex As Exception

        End Try

    End Sub

    ''Private Sub TestBinaryTextReader(ByVal strInputFilePath As String)
    ''    Const BYTE_OFFSET_JUMP_COUNT As Integer = 10

    ''    Dim intIndex As Integer
    ''    Dim eDirection As MSDataFileReader.clsBinaryTextReader.ReadDirectionConstants
    ''    Dim strMessage As String
    ''    Dim blnAtEnd As Boolean

    ''    Dim objBinaryReader As MSDataFileReader.clsBinaryTextReader

    ''    objBinaryReader = New MSDataFileReader.clsBinaryTextReader

    ''    If objBinaryReader.OpenFile(strInputFilePath) Then

    ''        objBinaryReader.MoveToByteOffset(18307)
    ''        eDirection = MSDataFileReader.clsBinaryTextReader.ReadDirectionConstants.Reverse
    ''        Do
    ''            If objBinaryReader.ReadLine(eDirection) Then
    ''                strMessage = "Bytes " & objBinaryReader.CurrentLineByteOffsetStart & " to " & objBinaryReader.CurrentLineByteOffsetEnd & "; " & objBinaryReader.CurrentLine
    ''                Console.WriteLine(strMessage)
    ''            Else
    ''                Exit Do
    ''            End If
    ''        Loop

    ''        eDirection = MSDataFileReader.clsBinaryTextReader.ReadDirectionConstants.Forward
    ''        Do
    ''            For intIndex = 0 To 1
    ''                If objBinaryReader.ReadLine(eDirection) Then
    ''                    strMessage = "Bytes " & objBinaryReader.CurrentLineByteOffsetStart & " to " & objBinaryReader.CurrentLineByteOffsetEnd & "; " & objBinaryReader.CurrentLine
    ''                    Console.WriteLine(strMessage)
    ''                End If
    ''            Next intIndex

    ''            If eDirection = MSDataFileReader.clsBinaryTextReader.ReadDirectionConstants.Forward Then
    ''                eDirection = MSDataFileReader.clsBinaryTextReader.ReadDirectionConstants.Reverse
    ''            ElseIf Not blnAtEnd Then
    ''                objBinaryReader.MoveToEnd()
    ''                blnAtEnd = True
    ''            Else
    ''                eDirection = MSDataFileReader.clsBinaryTextReader.ReadDirectionConstants.Reverse
    ''                For intIndex = BYTE_OFFSET_JUMP_COUNT To 0 Step -1
    ''                    objBinaryReader.MoveToByteOffset(CInt(intIndex * objBinaryReader.FileLengthBytes / BYTE_OFFSET_JUMP_COUNT))

    ''                    objBinaryReader.ReadLine(eDirection)
    ''                    strMessage = "Bytes " & objBinaryReader.CurrentLineByteOffsetStart & " to " & objBinaryReader.CurrentLineByteOffsetEnd & "; " & objBinaryReader.CurrentLine
    ''                    Console.WriteLine(strMessage)

    ''                    objBinaryReader.ReadLine(eDirection)
    ''                    strMessage = "Bytes " & objBinaryReader.CurrentLineByteOffsetStart & " to " & objBinaryReader.CurrentLineByteOffsetEnd & "; " & objBinaryReader.CurrentLine
    ''                    Console.WriteLine(strMessage)

    ''                Next intIndex

    ''                Exit Do
    ''            End If
    ''        Loop
    ''    End If

    ''End Sub

    Private Sub TestDTATextReader(ByVal eDataReaderMode As MSDataFileReader.clsMSDataFileReaderBaseClass.drmDataReaderModeConstants)
        TestDTATextReader("Shew220a_16May03_pegasus_0306-01_4-20_dta.txt", eDataReaderMode)
    End Sub

    Private Sub TestDTATextReader(ByVal strInputFilePath As String, ByVal eDataReaderMode As MSDataFileReader.clsMSDataFileReaderBaseClass.drmDataReaderModeConstants)
        Dim objSpectrumInfo As MSDataFileReader.clsSpectrumInfo

        mMSFileReader = New MSDataFileReader.clsDtaTextFileReader

        objSpectrumInfo = New MSDataFileReader.clsSpectrumInfoMsMsText
        objSpectrumInfo.AutoShrinkDataLists = False

        TestReader(strInputFilePath, mMSFileReader, objSpectrumInfo, eDataReaderMode)

    End Sub
    Private Sub TestMGFReader(ByVal eDataReaderMode As MSDataFileReader.clsMSDataFileReaderBaseClass.drmDataReaderModeConstants)
        TestMGFReader("Kolker10percentlessTFA3.mgf", eDataReaderMode)
    End Sub

    Private Sub TestMGFReader(ByVal strInputFilePath As String, ByVal eDataReaderMode As MSDataFileReader.clsMSDataFileReaderBaseClass.drmDataReaderModeConstants)
        Dim objSpectrumInfo As MSDataFileReader.clsSpectrumInfo

        mMSFileReader = New MSDataFileReader.clsMGFFileReader

        objSpectrumInfo = New MSDataFileReader.clsSpectrumInfoMsMsText
        objSpectrumInfo.AutoShrinkDataLists = False

        TestReader(strInputFilePath, mMSFileReader, objSpectrumInfo, eDataReaderMode)
    End Sub

    Private Sub TestMZXmlReader(ByVal eDataReaderMode As MSDataFileReader.clsMSDataFileReaderBaseClass.drmDataReaderModeConstants)
        TestMZXmlReader("SRM_HeavyPeptide_5nM_buffer2_mazama.mzXML", eDataReaderMode)

        TestMZXmlReader("SampleData_QC_Standards_Excerpt.mzXML", eDataReaderMode)
        'TestMZXmlReader("Mini_proteome_CytochromeC02-LCQ-1_Profile.mzXML", eDataReaderMode)
        'TestMZXmlReader("MSFMS_018_Agilent_Fusion_031305.mzXML", eDataReaderMode)

        'TestMZXmlReader("Gsulf326_LTQFT_run2_23Aug05_Andro_0705-06.mzXML", eDataReaderMode)
        'TestMZXmlReader("Unicode_Gsulf326_LTQFT.mzxml", eDataReaderMode)
        'TestMZXmlReader("Ding-UG-G-IMAC-Label-60.mzXML", eDataReaderMode)
    End Sub

    Private Sub TestMZXmlReader(ByVal strInputFilePath As String, ByVal eDataReaderMode As MSDataFileReader.clsMSDataFileReaderBaseClass.drmDataReaderModeConstants)
        Dim objSpectrumInfo As MSDataFileReader.clsSpectrumInfo

        If eDataReaderMode = MSDataFileReader.clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Indexed Then
            mMSFileReader = New MSDataFileReader.clsMzXMLFileAccessor
        Else
            mMSFileReader = New MSDataFileReader.clsMzXMLFileReader
        End If

        objSpectrumInfo = New MSDataFileReader.clsSpectrumInfoMzXML
        objSpectrumInfo.AutoShrinkDataLists = False

        If TypeOf mMSFileReader Is MSDataFileReader.clsMzXMLFileAccessor Then
            CType(mMSFileReader, MSDataFileReader.clsMzXMLFileAccessor).IgnoreEmbeddedIndex = False
        End If

        TestReader(strInputFilePath, mMSFileReader, objSpectrumInfo, eDataReaderMode)
    End Sub

    Private Sub TestMZDataReader(ByVal eDataReaderMode As MSDataFileReader.clsMSDataFileReaderBaseClass.drmDataReaderModeConstants)
        TestMZDataReader("SampleData_myo_excerpt_1.05cv.mzdata", eDataReaderMode)
        'TestMZDataReader("SampleData_myo_excerpt_1.05cv_OddFormatting.mzdata", eDataReaderMode)
        'TestMZDataReader("Unicode_SampleData_myo_excerpt_1.05cv.mzdata", eDataReaderMode)
    End Sub

    Private Sub TestMZDataReader(ByVal strInputFilePath As String, ByVal eDataReaderMode As MSDataFileReader.clsMSDataFileReaderBaseClass.drmDataReaderModeConstants)
        Dim objSpectrumInfo As MSDataFileReader.clsSpectrumInfo

        If eDataReaderMode = MSDataFileReader.clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Indexed Then
            mMSFileReader = New MSDataFileReader.clsMzDataFileAccessor
        Else
            mMSFileReader = New MSDataFileReader.clsMzDataFileReader
        End If

        objSpectrumInfo = New MSDataFileReader.clsSpectrumInfoMzData
        objSpectrumInfo.AutoShrinkDataLists = False

        TestReader(strInputFilePath, mMSFileReader, objSpectrumInfo, eDataReaderMode)
    End Sub

    Private Sub TestReader(ByVal strInputFilePath As String, ByRef objMSFileReader As MSDataFileReader.clsMSDataFileReaderBaseClass, ByRef objSpectrumInfo As MSDataFileReader.clsSpectrumInfo, ByVal eDataReaderMode As MSDataFileReader.clsMSDataFileReaderBaseClass.drmDataReaderModeConstants)
        Const USE_TEXTSTREAM_READER As Boolean = False

        Dim intIndex As Integer
        Dim strMessage As String
        Dim strText As String

        Dim srInFile As System.IO.StreamReader
        Dim intScanNumberList() As Integer

        Dim dtStartTime As DateTime
        Dim dtEndTime As DateTime

        mProgressForm.InitializeProgressForm("Reading " & System.IO.Path.GetFileName(strInputFilePath), 0, 100)
        mProgressForm.Show()
        System.Windows.Forms.Application.DoEvents()

        Console.WriteLine()
        Console.WriteLine("Reading " & strInputFilePath & "; Reader = " & objMSFileReader.GetType.ToString & ", DataReaderMode = " & eDataReaderMode.ToString)

        srInFile = New System.IO.StreamReader(strInputFilePath)

        objMSFileReader.ParseFilesWithUnknownVersion = False

        If USE_TEXTSTREAM_READER Then
            objMSFileReader.OpenTextStream(srInFile.ReadToEnd)
        Else
            objMSFileReader.OpenFile(strInputFilePath)
        End If

        Select Case eDataReaderMode
            Case MSDataFileReader.clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Cached, _
                 MSDataFileReader.clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Indexed

                dtStartTime = System.DateTime.UtcNow
                objMSFileReader.ReadAndCacheEntireFile()
                dtEndTime = System.DateTime.UtcNow
                LogFileReadEvent(strInputFilePath, "ReadAndCacheEntireFile", dtEndTime.Subtract(dtStartTime), "Reader = " & objMSFileReader.GetType.ToString & ControlChars.Tab & "DataReaderMode = " & eDataReaderMode.ToString)

                Console.WriteLine("Scan Count: " & objMSFileReader.ScanCount.ToString)

                strMessage = "Calling GetSpectrumByIndex; SpectrumCount = " & objMSFileReader.CachedSpectrumCount.ToString
                Console.WriteLine(strMessage)
                mProgressForm.InitializeProgressForm(strMessage, 0, objMSFileReader.CachedSpectrumCount)

                dtStartTime = System.DateTime.UtcNow
                For intIndex = 0 To objMSFileReader.CachedSpectrumCount - 1
                    If objMSFileReader.GetSpectrumByIndex(intIndex, objSpectrumInfo) Then
                        TestReaderShowSpectrumInfo(objSpectrumInfo)
                    End If

                    If intIndex Mod 10 = 0 Then
                        mProgressForm.UpdateProgressBar(intIndex)
                        System.Windows.Forms.Application.DoEvents()
                        If mProgressForm.KeyPressAbortProcess Then Exit For
                    End If
                Next intIndex
                dtEndTime = System.DateTime.UtcNow
                LogFileReadEvent(strInputFilePath, "Calling GetSpectrumByIndex", dtEndTime.Subtract(dtStartTime), "SpectrumCount = " & objMSFileReader.CachedSpectrumCount.ToString)

                strMessage = "Calling GetSpectrumByScanNumber; Minimum = " & objMSFileReader.CachedSpectraScanNumberMinimum.ToString & "; Maximum = " & objMSFileReader.CachedSpectraScanNumberMaximum.ToString
                Console.WriteLine(strMessage)
                If objMSFileReader.CachedSpectraScanNumberMaximum() - objMSFileReader.CachedSpectraScanNumberMinimum() > 0 Then
                    For intIndex = objMSFileReader.CachedSpectraScanNumberMinimum To objMSFileReader.CachedSpectraScanNumberMaximum() Step objMSFileReader.CachedSpectraScanNumberMaximum() - objMSFileReader.CachedSpectraScanNumberMinimum()
                        If objMSFileReader.GetSpectrumByScanNumber(intIndex, objSpectrumInfo) Then
                            TestReaderShowSpectrumInfo(objSpectrumInfo)
                        End If
                    Next intIndex
                End If

                If objMSFileReader.GetScanNumberList(intScanNumberList) Then
                    strMessage = "Calling GetSpectrumByScanNumber using ScanNumberList; SpectrumCount = " & intScanNumberList.Length
                    Console.WriteLine(strMessage)
                    mProgressForm.InitializeProgressForm(strMessage, 0, intScanNumberList.Length)

                    dtStartTime = System.DateTime.UtcNow
                    For intIndex = 0 To intScanNumberList.Length - 1
                        If objMSFileReader.GetSpectrumByScanNumber(intScanNumberList(intIndex), objSpectrumInfo) Then
                            TestReaderShowSpectrumInfo(objSpectrumInfo)
                        End If

                        If intIndex Mod 10 = 0 Then
                            mProgressForm.UpdateProgressBar(intIndex)
                            System.Windows.Forms.Application.DoEvents()
                            If mProgressForm.KeyPressAbortProcess Then Exit For
                        End If
                    Next intIndex
                    dtEndTime = System.DateTime.UtcNow
                    LogFileReadEvent(strInputFilePath, "Calling GetSpectrumByScanNumber using ScanNumberList", dtEndTime.Subtract(dtStartTime), "SpectrumCount = " & intScanNumberList.Length)
                End If

                If eDataReaderMode = MSDataFileReader.clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Indexed Then
                    Dim blnSuccess As Boolean
                    With CType(mMSFileReader, MSDataFileReader.clsMSDataFileAccessorBaseClass)
                        blnSuccess = .GetSpectrumHeaderInfoByIndex(0, objSpectrumInfo)

                        strText = .GetSourceXMLHeader(5, 3.002234, 20.34234324)
                        strText = .GetSourceXMLFooter()

                        For intIndex = 0 To objMSFileReader.CachedSpectrumCount - 1
                            blnSuccess = .GetSourceXMLByIndex(intIndex, strText)
                        Next intIndex

                    End With

                End If

            Case Else
                ' Includes MSDataFileReader.clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Sequential

                strMessage = "Calling ReadNextSpectrum (no caching)"
                Console.WriteLine(strMessage)

                dtStartTime = System.DateTime.UtcNow
                intIndex = 0
                Do While objMSFileReader.ReadNextSpectrum(objSpectrumInfo)
                    If intIndex = 0 Then
                        Console.WriteLine("Scan Count: " & objMSFileReader.ScanCount.ToString)
                    End If

                    TestReaderShowSpectrumInfo(objSpectrumInfo)
                    intIndex += 1
                Loop
                dtEndTime = System.DateTime.UtcNow
                LogFileReadEvent(strInputFilePath, strMessage, dtEndTime.Subtract(dtStartTime), "SpectrumCount = " & intIndex)

        End Select

        objMSFileReader.CloseFile()
    End Sub

    Private Sub TestReaderShowSpectrumInfo(ByVal objSpectrumInfo As MSDataFileReader.clsSpectrumInfo)
        With objSpectrumInfo
            If .DataCount = 0 Then
                Console.WriteLine(.ScanNumber & ControlChars.Tab & .MSLevel & ControlChars.Tab & .DataCount)
            Else
                Console.WriteLine(.ScanNumber & ControlChars.Tab & .MSLevel & ControlChars.Tab & .DataCount & ControlChars.Tab & .MZList(0) & ControlChars.Tab & .IntensityList(0))
            End If
        End With
    End Sub

    Private Sub mMSFileReader_ProgressChanged(ByVal taskDescription As String, ByVal percentComplete As Single) Handles mMSFileReader.ProgressChanged
        mProgressForm.UpdateCurrentTask(taskDescription)
        mProgressForm.UpdateProgressBar(percentComplete)
        System.Windows.Forms.Application.DoEvents()

        If mProgressForm.KeyPressAbortProcess Then
            mMSFileReader.AbortProcessingNow()
        End If
    End Sub

    Private Sub mMSFileReader_ProgressComplete() Handles mMSFileReader.ProgressComplete
        mProgressForm.UpdateProgressBar(100)
        System.Windows.Forms.Application.DoEvents()
    End Sub

    Private Sub mMSFileReader_ProgressReset() Handles mMSFileReader.ProgressReset
        mProgressForm.UpdateProgressBar(0)
        System.Windows.Forms.Application.DoEvents()
    End Sub
End Module
