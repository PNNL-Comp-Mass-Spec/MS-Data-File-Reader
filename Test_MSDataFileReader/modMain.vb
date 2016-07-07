Option Strict On

' This module can be used to test the data file readers
'
' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2006, Battelle Memorial Institute.  All Rights Reserved.
' Program started March 24, 2006
'
' E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com
' Website: http://omics.pnl.gov/ or http://www.sysbio.org/resources/staff/ or http://panomics.pnnl.gov/
' -------------------------------------------------------------------------------
'
' Last modified April 3, 2014

Imports System.IO
Imports System.Reflection
Imports System.Windows.Forms
Imports MSDataFileReader
Imports ProgressFormNET

Module modMain

    Private WithEvents mMSFileReader As clsMSDataFileReaderBaseClass
    Private mProgressForm As frmProgress

    Public Sub Main()
        Try
            mProgressForm = New frmProgress

            'TestDTATextReader(clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Indexed)
            'TestDTATextReader(clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Cached)
            'TestDTATextReader(clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Sequential)

            'TestMGFReader(clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Cached)
            'TestMGFReader(clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Sequential)

            TestMZXmlReader(clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Cached)
            TestMZXmlReader(clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Sequential)
            TestMZXmlReader(clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Indexed)

            TestMZDataReader(clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Cached)
            TestMZDataReader(clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Sequential)
            TestMZDataReader(clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Indexed)

            TestBinaryTextReader("SampleData_QC_Standards_Excerpt.mzXML")
            TestBinaryTextReader("Unicode_SampleData_myo_excerpt_1.05cv.mzdata")
            TestBinaryTextReader("Combined_BigEndian.mzXml")

        Catch ex As Exception
            Console.WriteLine("Error: " & ex.Message)
            Console.WriteLine(ex.StackTrace)
        Finally
            mProgressForm.Close()
        End Try

    End Sub

    ''Private Sub TestRegEx()
    ''    Dim text As String = "One car red car blue car"
    ''    Dim pat As String = "(\w+)\s+(car)"

    ''    ' Compile the regular expression.
    ''    Dim r As Text.RegularExpressions.Regex = New Text.RegularExpressions.Regex(pat, Text.RegularExpressions.RegexOptions.IgnoreCase)
    ''    ' Match the regular expression pattern against a text string.
    ''    Dim m As Text.RegularExpressions.Match = r.Match(text)

    ''    Dim matchcount As Integer = 0
    ''    While (m.Success)
    ''        matchcount += 1
    ''        Console.WriteLine("Match" & (matchcount))
    ''        Dim i As Integer
    ''        For i = 1 To 2
    ''            Dim g As Text.RegularExpressions.Group = m.Groups(i)
    ''            Console.WriteLine("Group" & i & "='" & g.ToString() & "'")

    ''            Dim cc As Text.RegularExpressions.CaptureCollection = g.Captures
    ''            Dim j As Integer
    ''            For j = 0 To cc.Count - 1

    ''                Dim c As Text.RegularExpressions.Capture = cc(j)
    ''                Console.WriteLine("Capture" & j & "='" & c.ToString() _
    ''                   & "', Position=" & c.Index)
    ''            Next j
    ''        Next i
    ''        m = m.NextMatch()
    ''    End While

    ''End Sub

    Private Sub LogFileReadEvent(strInputFilePath As String, strTask As String, tsElapsedTime As TimeSpan, strAdditionalInfo As String)
        Dim strLogFilePath As String

        Dim objFileInfo As FileInfo
        Dim dblFileSizeMB As Double

        Dim swOutFile As StreamWriter

        Try
            objFileInfo = New FileInfo(strInputFilePath)
            dblFileSizeMB = objFileInfo.Length / 1024.0 / 1024
        Catch ex As Exception
            dblFileSizeMB = 0
        End Try

        Try
            strLogFilePath = Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location) & "_EventLog.txt"

            swOutFile = New StreamWriter(strLogFilePath, True)
            swOutFile.WriteLine(DateTime.Now().ToString & ControlChars.Tab & _
                 Path.GetFileName(strInputFilePath) & ControlChars.Tab & _
                 Math.Round(dblFileSizeMB, 2) & ControlChars.Tab & _
                 strTask & ControlChars.Tab & _
                 tsElapsedTime.TotalSeconds & ControlChars.Tab & _
                 strAdditionalInfo)
            swOutFile.Close()

        Catch ex As Exception

        End Try

    End Sub

    Private Sub TestBinaryTextReader(strInputFilePath As String)
        Const BYTE_OFFSET_JUMP_COUNT = 10

        Dim intIndex As Integer
        Dim eDirection As clsBinaryTextReader.ReadDirectionConstants
        Dim strMessage As String
        Dim blnAtEnd As Boolean

        Dim objBinaryReader As clsBinaryTextReader

        objBinaryReader = New clsBinaryTextReader

        If objBinaryReader.OpenFile(strInputFilePath) Then

            objBinaryReader.MoveToByteOffset(18307)
            eDirection = clsBinaryTextReader.ReadDirectionConstants.Reverse
            Do
                If objBinaryReader.ReadLine(eDirection) Then
                    strMessage = "Bytes " & objBinaryReader.CurrentLineByteOffsetStart & " to " & objBinaryReader.CurrentLineByteOffsetEnd & "; " & objBinaryReader.CurrentLine
                    Console.WriteLine(strMessage)
                Else
                    Exit Do
                End If
            Loop

            eDirection = clsBinaryTextReader.ReadDirectionConstants.Forward
            Do
                For intIndex = 0 To 1
                    If objBinaryReader.ReadLine(eDirection) Then
                        strMessage = "Bytes " & objBinaryReader.CurrentLineByteOffsetStart & " to " & objBinaryReader.CurrentLineByteOffsetEnd & "; " & objBinaryReader.CurrentLine
                        Console.WriteLine(strMessage)
                    End If
                Next intIndex

                If eDirection = clsBinaryTextReader.ReadDirectionConstants.Forward Then
                    eDirection = clsBinaryTextReader.ReadDirectionConstants.Reverse
                ElseIf Not blnAtEnd Then
                    objBinaryReader.MoveToEnd()
                    blnAtEnd = True
                Else
                    eDirection = clsBinaryTextReader.ReadDirectionConstants.Reverse
                    For intIndex = BYTE_OFFSET_JUMP_COUNT To 0 Step -1
                        objBinaryReader.MoveToByteOffset(CInt(intIndex * objBinaryReader.FileLengthBytes / BYTE_OFFSET_JUMP_COUNT))

                        objBinaryReader.ReadLine(eDirection)
                        strMessage = "Bytes " & objBinaryReader.CurrentLineByteOffsetStart & " to " & objBinaryReader.CurrentLineByteOffsetEnd & "; " & objBinaryReader.CurrentLine
                        Console.WriteLine(strMessage)

                        objBinaryReader.ReadLine(eDirection)
                        strMessage = "Bytes " & objBinaryReader.CurrentLineByteOffsetStart & " to " & objBinaryReader.CurrentLineByteOffsetEnd & "; " & objBinaryReader.CurrentLine
                        Console.WriteLine(strMessage)

                    Next intIndex

                    Exit Do
                End If
            Loop
        End If

    End Sub

    Private Sub TestDTATextReader(eDataReaderMode As clsMSDataFileReaderBaseClass.drmDataReaderModeConstants)
        TestDTATextReader("Shew220a_16May03_pegasus_0306-01_4-20_dta.txt", eDataReaderMode)
    End Sub

    Private Sub TestDTATextReader(strInputFilePath As String, eDataReaderMode As clsMSDataFileReaderBaseClass.drmDataReaderModeConstants)

        mMSFileReader = New clsDtaTextFileReader()
        mMSFileReader.AutoShrinkDataLists = False

        TestReader(strInputFilePath, mMSFileReader, eDataReaderMode)

    End Sub
    Private Sub TestMGFReader(eDataReaderMode As clsMSDataFileReaderBaseClass.drmDataReaderModeConstants)
        TestMGFReader("Kolker10percentlessTFA3.mgf", eDataReaderMode)
        'TestMGFReader("CPTAC_Peptidome_Test1_P1_R2_Poroshell_03Feb12_Frodo_Poroshell300SB.mgf", eDataReaderMode)
    End Sub

    Private Sub TestMGFReader(strInputFilePath As String, eDataReaderMode As clsMSDataFileReaderBaseClass.drmDataReaderModeConstants)

        mMSFileReader = New clsMGFFileReader()
        mMSFileReader.AutoShrinkDataLists = False

        TestReader(strInputFilePath, mMSFileReader, eDataReaderMode)
    End Sub

    Private Sub TestMZXmlReader(eDataReaderMode As clsMSDataFileReaderBaseClass.drmDataReaderModeConstants)
        'TestMZXmlReader("SRM_HeavyPeptide_5nM_buffer2_mazama.mzXML", eDataReaderMode)

        'TestMZXmlReader("SampleData_QC_Standards_Excerpt.mzXML", eDataReaderMode)
        'TestMZXmlReader("Mini_proteome_CytochromeC02-LCQ-1_Profile.mzXML", eDataReaderMode)
        'TestMZXmlReader("MSFMS_018_Agilent_Fusion_031305.mzXML", eDataReaderMode)

        'TestMZXmlReader("Gsulf326_LTQFT_run2_23Aug05_Andro_0705-06.mzXML", eDataReaderMode)
        'TestMZXmlReader("Unicode_Gsulf326_LTQFT.mzxml", eDataReaderMode)
        'TestMZXmlReader("Ding-UG-G-IMAC-Label-60.mzXML", eDataReaderMode)
        TestMZXmlReader("Mortierella_iTRAQ4_test_28Mar14_Samwise_13-07-17.mzxml", eDataReaderMode)
    End Sub

    Private Sub TestMZXmlReader(strInputFilePath As String, eDataReaderMode As clsMSDataFileReaderBaseClass.drmDataReaderModeConstants)
        
        If eDataReaderMode = clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Indexed Then
            mMSFileReader = New clsMzXMLFileAccessor()
        Else
            mMSFileReader = New clsMzXMLFileReader()
        End If

        mMSFileReader.AutoShrinkDataLists = False

        If TypeOf mMSFileReader Is clsMzXMLFileAccessor Then
            CType(mMSFileReader, clsMzXMLFileAccessor).IgnoreEmbeddedIndex = False
        End If

        TestReader(strInputFilePath, mMSFileReader, eDataReaderMode)
    End Sub

    Private Sub TestMZDataReader(eDataReaderMode As clsMSDataFileReaderBaseClass.drmDataReaderModeConstants)
        TestMZDataReader("SampleData_myo_excerpt_1.05cv.mzdata", eDataReaderMode)
        'TestMZDataReader("SampleData_myo_excerpt_1.05cv_OddFormatting.mzdata", eDataReaderMode)
        'TestMZDataReader("Unicode_SampleData_myo_excerpt_1.05cv.mzdata", eDataReaderMode)
    End Sub

    Private Sub TestMZDataReader(strInputFilePath As String, eDataReaderMode As clsMSDataFileReaderBaseClass.drmDataReaderModeConstants)
        
        If eDataReaderMode = clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Indexed Then
            mMSFileReader = New clsMzDataFileAccessor()
        Else
            mMSFileReader = New clsMzDataFileReader()
        End If

        mMSFileReader.AutoShrinkDataLists = False

        TestReader(strInputFilePath, mMSFileReader, eDataReaderMode)
    End Sub

    Private Sub TestReader(
      strInputFilePath As String,
      objMSFileReader As clsMSDataFileReaderBaseClass,
      eDataReaderMode As clsMSDataFileReaderBaseClass.drmDataReaderModeConstants)

        Const USE_TEXTSTREAM_READER = False

        Dim intIndex As Integer
        Dim strMessage As String
        Dim strText As String

        Dim intScanNumberList() As Integer = Nothing

        Dim dtStartTime As DateTime
        Dim dtEndTime As DateTime

        mProgressForm.InitializeProgressForm("Reading " & Path.GetFileName(strInputFilePath), 0, 100)
        mProgressForm.Show()
        Application.DoEvents()

        Console.WriteLine()
        Console.WriteLine("Reading " & strInputFilePath & "; Reader = " & objMSFileReader.GetType.ToString & ", DataReaderMode = " & eDataReaderMode.ToString)

        objMSFileReader.ParseFilesWithUnknownVersion = False

        If USE_TEXTSTREAM_READER Then
            Using srInFile = New StreamReader(strInputFilePath)
                objMSFileReader.OpenTextStream(srInFile.ReadToEnd)
            End Using
        Else
            objMSFileReader.OpenFile(strInputFilePath)
        End If

        Dim objSpectrumInfo As clsSpectrumInfo = Nothing

        Select Case eDataReaderMode
            Case clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Cached,
                 clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Indexed

                dtStartTime = DateTime.UtcNow
                objMSFileReader.ReadAndCacheEntireFile()
                dtEndTime = DateTime.UtcNow
                LogFileReadEvent(strInputFilePath, "ReadAndCacheEntireFile", dtEndTime.Subtract(dtStartTime),
                                 "Reader = " & objMSFileReader.GetType.ToString & ControlChars.Tab & "DataReaderMode = " & eDataReaderMode.ToString)

                Console.WriteLine("Scan Count: " & objMSFileReader.ScanCount.ToString)

                strMessage = "Calling GetSpectrumByIndex; SpectrumCount = " & objMSFileReader.CachedSpectrumCount.ToString
                Console.WriteLine(strMessage)
                mProgressForm.InitializeProgressForm(strMessage, 0, objMSFileReader.CachedSpectrumCount)

                dtStartTime = DateTime.UtcNow

                ' Read all of the spectra using .GetSpectrumByIndex()
                ' Show 25 of them
                Dim modValue = CInt(objMSFileReader.CachedSpectrumCount / 25)
                If modValue < 1 Then modValue = 1

                For intIndex = 0 To objMSFileReader.CachedSpectrumCount - 1
                    If objMSFileReader.GetSpectrumByIndex(intIndex, objSpectrumInfo) Then
                        If intIndex Mod modValue = 0 Then
                            TestReaderShowSpectrumInfo(objSpectrumInfo)
                        End If
                    End If

                    If intIndex Mod 10 = 0 Then
                        mProgressForm.UpdateProgressBar(intIndex)
                        Application.DoEvents()
                        If mProgressForm.KeyPressAbortProcess Then Exit For
                    End If
                Next intIndex
                dtEndTime = DateTime.UtcNow
                LogFileReadEvent(strInputFilePath, "Calling GetSpectrumByIndex", dtEndTime.Subtract(dtStartTime),
                                 "SpectrumCount = " & objMSFileReader.CachedSpectrumCount.ToString)

                strMessage = "Calling GetSpectrumByScanNumber; Minimum = " & objMSFileReader.CachedSpectraScanNumberMinimum.ToString & "; Maximum = " & objMSFileReader.CachedSpectraScanNumberMaximum.ToString
                Console.WriteLine(strMessage)
                If objMSFileReader.CachedSpectraScanNumberMaximum() - objMSFileReader.CachedSpectraScanNumberMinimum() > 0 Then

                    ' Call .GetSpectrumByScanNumber() for 20 spectra in the file
                    Dim stepSize = CInt((objMSFileReader.CachedSpectraScanNumberMaximum - objMSFileReader.CachedSpectraScanNumberMinimum) / 20)
                    If stepSize < 1 Then stepSize = 1

                    For intIndex = objMSFileReader.CachedSpectraScanNumberMinimum To objMSFileReader.CachedSpectraScanNumberMaximum Step stepSize
                        If objMSFileReader.GetSpectrumByScanNumber(intIndex, objSpectrumInfo) Then
                            TestReaderShowSpectrumInfo(objSpectrumInfo)
                        End If
                    Next intIndex
                End If

                If objMSFileReader.GetScanNumberList(intScanNumberList) Then
                    strMessage = "Calling GetSpectrumByScanNumber using ScanNumberList; SpectrumCount = " & intScanNumberList.Length
                    Console.WriteLine(strMessage)
                    mProgressForm.InitializeProgressForm(strMessage, 0, intScanNumberList.Length)

                    dtStartTime = DateTime.UtcNow

                    ' Read all of the spectra using .GetSpectrumByScanNumber()
                    ' Show 30 of them
                    modValue = CInt(intScanNumberList.Length / 30)
                    If modValue < 1 Then modValue = 1

                    For intIndex = 0 To intScanNumberList.Length - 1
                        If objMSFileReader.GetSpectrumByScanNumber(intScanNumberList(intIndex), objSpectrumInfo) Then
                            If intIndex Mod modValue = 0 Then
                                TestReaderShowSpectrumInfo(objSpectrumInfo)
                            End If
                        End If

                        If intIndex Mod 10 = 0 Then
                            mProgressForm.UpdateProgressBar(intIndex)
                            Application.DoEvents()
                            If mProgressForm.KeyPressAbortProcess Then Exit For
                        End If
                    Next intIndex

                    dtEndTime = DateTime.UtcNow
                    LogFileReadEvent(strInputFilePath, "Calling GetSpectrumByScanNumber using ScanNumberList", dtEndTime.Subtract(dtStartTime),
                                     "SpectrumCount = " & intScanNumberList.Length)
                End If

                If eDataReaderMode = clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Indexed Then

                    If TypeOf mMSFileReader Is clsMzXMLFileAccessor Or TypeOf mMSFileReader Is clsMzDataFileAccessor Then
                        Dim objMSDataFileAccessor = CType(mMSFileReader, clsMSDataFileAccessorBaseClass)

                        Dim blnSuccess = objMSDataFileAccessor.GetSpectrumHeaderInfoByIndex(0, objSpectrumInfo)

                        strText = objMSDataFileAccessor.GetSourceXMLHeader(5, 3.002234, 20.34234324)
                        strText = objMSDataFileAccessor.GetSourceXMLFooter()

                        For intIndex = 0 To objMSFileReader.CachedSpectrumCount - 1
                            blnSuccess = objMSDataFileAccessor.GetSourceXMLByIndex(intIndex, strText)
                        Next intIndex

                    End If

                End If

            Case Else
                ' Includes MSDataFileReader.clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Sequential

                ' Read all of the spectra using .ReadNextSpectrum()
                ' Show every 100th spectrum for the first 20 spectra
                ' Then show every 200th for the next 20, etc.

                Dim modValue = 100
                Dim spectraShown = 0

                strMessage = "Calling ReadNextSpectrum (no caching)"
                Console.WriteLine(strMessage)

                dtStartTime = DateTime.UtcNow
                intIndex = 0
                Do While objMSFileReader.ReadNextSpectrum(objSpectrumInfo)
                    If intIndex = 0 Then
                        Console.WriteLine("Scan Count: " & objMSFileReader.ScanCount.ToString)
                    End If

                    If intIndex Mod modValue = 0 Then
                        TestReaderShowSpectrumInfo(objSpectrumInfo)
                        spectraShown += 1
                        If spectraShown Mod 20 = 0 Then
                            modValue *= 2
                        End If
                    End If

                    intIndex += 1
                Loop
                dtEndTime = DateTime.UtcNow
                LogFileReadEvent(strInputFilePath, strMessage, dtEndTime.Subtract(dtStartTime), "SpectrumCount = " & intIndex)

        End Select

        objMSFileReader.CloseFile()
    End Sub

    Private Sub TestReaderShowSpectrumInfo(objSpectrumInfo As clsSpectrumInfo)
        Dim blnShowDetails = False

        With objSpectrumInfo
            If .DataCount = 0 Then
                Console.WriteLine(.ScanNumber & ControlChars.Tab & .MSLevel & ControlChars.Tab & .DataCount)
            Else
                Console.WriteLine(.ScanNumber & ControlChars.Tab & .MSLevel & ControlChars.Tab & .DataCount & ControlChars.Tab & .MZList(0) & ControlChars.Tab & .IntensityList(0))
                For intIndex = 0 To .DataCount - 1
                    If .MZList(intIndex) <= 0 OrElse .IntensityList(intIndex) < 0 Then
                        Console.WriteLine("Possibly invalid point: m/z " & .MZList(intIndex).ToString("0.0000") & "   " & .IntensityList(intIndex).ToString("0.0"))
                        If Not blnShowDetails Then
                            blnShowDetails = True
                        End If
                    ElseIf intIndex <= 10 OrElse blnShowDetails Then
                        Console.WriteLine("    m/z " & .MZList(intIndex).ToString("0.0000") & "   " & .IntensityList(intIndex).ToString("0.0"))
                    End If

                    If intIndex > 0 AndAlso blnShowDetails AndAlso intIndex Mod 50 = 0 Then
                        Console.WriteLine("...")
                    End If
                Next
            End If
        End With
        Console.WriteLine()
    End Sub

    Private Sub mMSFileReader_ProgressChanged(taskDescription As String, percentComplete As Single) Handles mMSFileReader.ProgressChanged
        mProgressForm.UpdateCurrentTask(taskDescription)
        mProgressForm.UpdateProgressBar(percentComplete)
        Application.DoEvents()

        If mProgressForm.KeyPressAbortProcess Then
            mMSFileReader.AbortProcessingNow()
        End If
    End Sub

    Private Sub mMSFileReader_ProgressComplete() Handles mMSFileReader.ProgressComplete
        mProgressForm.UpdateProgressBar(100)
        Application.DoEvents()
    End Sub

    Private Sub mMSFileReader_ProgressReset() Handles mMSFileReader.ProgressReset
        mProgressForm.UpdateProgressBar(0)
        Application.DoEvents()
    End Sub
End Module
