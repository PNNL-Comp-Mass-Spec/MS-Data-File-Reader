Option Strict On

Imports System.IO
Imports System.Text

' This class can be used to open a Text file and read each of the lines from the file,
'  where a line of text ends with CRLF or simply LF
' In addition, the byte offset at the start and end of the line is also returned
'
' Note that this class is compatible with UTF-16 Unicode files; it looks for byte order mark
'  FF FE or FE FF in the first two bytes of the file to determine if a file is Unicode 
' (though you can override this using the InputFileEncoding property after calling .OpenFile()
' This class will also look for the byte order mark for UTF-8 files (EF BB BF) though it may not
'  properly decode UTF-8 characters (not fully tested)
'
' You can change the expected line terminator character using Property FileSystemMode
'  If FileSystemMode = FileSystemModeConstants.Windows, then the Line Terminator = LF, optionally preceded by CR
'  If FileSystemMode = FileSystemModeConstants.Unix, then the Line Terminator = LF, optionally preceded by CR
'  If FileSystemMode = FileSystemModeConstants.Macintosh, then the Line Terminator = CR, previous character is not considered
'
' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2006, Battelle Memorial Institute.  All Rights Reserved.
' Program started April 18, 2006
'
' E-mail: matthew.monroe@pnl.gov or matt@alchemistmatt.com
' Website: http://omics.pnl.gov/ or http://www.sysbio.org/resources/staff/
' -------------------------------------------------------------------------------
'
' Licensed under the Apache License, Version 2.0; you may not use this file except
' in compliance with the License.  You may obtain a copy of the License at
' http://www.apache.org/licenses/LICENSE-2.0
'
' Notice: This computer software was prepared by Battelle Memorial Institute,
' hereinafter the Contractor, under Contract No. DE-AC05-76RL0 1830 with the
' Department of Energy (DOE).  All rights in the computer software are reserved
' by DOE on behalf of the United States Government and the Contractor as
' provided in the Contract.  NEITHER THE GOVERNMENT NOR THE CONTRACTOR MAKES ANY
' WARRANTY, EXPRESS OR IMPLIED, OR ASSUMES ANY LIABILITY FOR THE USE OF THIS
' SOFTWARE.  This notice including this sentence must appear on any copies of
' this computer software.
'

Public Class clsBinaryTextReader
    Public Sub New()
        ' Note: This property will also update mLineTerminator1Code and mLineTerminator2Code
        Me.FileSystemMode = FileSystemModeConstants.Windows

        InitializeLocalVariables()
    End Sub

    Protected Overrides Sub Finalize()
        MyBase.Finalize()
        Me.Close()
    End Sub

#Region "Constants and Enums"
    ' In order to support Unicode files, it is important that the buffer length always be a power of 2
    Protected Const INITIAL_BUFFER_LENGTH As Integer = 10000

    Protected Const LINE_TERMINATOR_CODE_LF As Byte = 10
    Protected Const LINE_TERMINATOR_CODE_CR As Byte = 13

    Public Enum FileSystemModeConstants
        Windows = 0
        Unix = 1
        Macintosh = 2
    End Enum

    Public Enum InputFileEncodingConstants
        Ascii = 0                   ' No Byte Order Mark
        UTF8 = 1                    ' Byte Order Mark: EF BB BF (UTF-8)
        UnicodeNormal = 2           ' Byte Order Mark: FF FE (Little Endian Unicode)
        UnicodeBigEndian = 3        ' Byte Order Mark: FE FF (Big Endian Unicode)
    End Enum

    Public Enum ReadDirectionConstants
        Forward = 0
        Reverse = 1
    End Enum

#End Region

#Region "Structures"

#End Region

#Region "Classwide Variables"

    Protected mInputFilePath As String

    Protected mInputFileEncoding As InputFileEncodingConstants = InputFileEncodingConstants.Ascii
    Protected mCharSize As Byte = 1
    Protected mByteOrderMarkLength As Byte

    ' Note: Use Me.FileSystemMode to set this variable so that mLineTerminator1Code and mLineTerminator2Code also get updated
    Protected mFileSystemMode As FileSystemModeConstants
    Protected mLineTerminator1Code As Byte
    Protected mLineTerminator2Code As Byte

    Protected mErrorMessage As String

    Protected mBinaryReader As FileStream
    Protected mLineNumber As Integer

    Protected mByteBufferCount As Integer
    Protected mByteBuffer() As Byte

    ' Note: The first byte in the file is Byte 0
    Protected mByteBufferFileOffsetStart As Long

    ' This variable defines the index in mByteBuffer() at which the next line starts
    Protected mByteBufferNextLineStartIndex As Integer

    Protected mCurrentLineText As String
    Protected mCurrentLineByteOffsetStart As Long
    Protected mCurrentLineByteOffsetEnd As Long
    Protected mCurrentLineByteOffsetEndWithTerminator As Long

    Protected mReadLineDirectionSaved As ReadDirectionConstants
    Protected mCurrentLineByteOffsetStartSaved As Long
    Protected mCurrentLineTextSaved As String

    Protected mCurrentLineTerminator As String

#End Region

#Region "Processing Options and Interface Functions"

    Public ReadOnly Property ByteBufferFileOffsetStart() As Long
        Get
            Return mByteBufferFileOffsetStart
        End Get
    End Property

    Public ReadOnly Property ByteOrderMarkLength() As Byte
        Get
            Return mByteOrderMarkLength
        End Get
    End Property

    Public ReadOnly Property CharSize() As Byte
        Get
            Return mCharSize
        End Get
    End Property

    Public ReadOnly Property CurrentLine() As String
        Get
            If mCurrentLineText Is Nothing Then
                Return String.Empty
            Else
                Return mCurrentLineText
            End If
        End Get
    End Property

    Public ReadOnly Property CurrentLineLength() As Integer
        Get
            If mCurrentLineText Is Nothing Then
                Return 0
            Else
                Return mCurrentLineText.Length
            End If
        End Get
    End Property

    Public ReadOnly Property CurrentLineByteOffsetStart() As Long
        Get
            Return mCurrentLineByteOffsetStart
        End Get
    End Property

    Public ReadOnly Property CurrentLineByteOffsetEnd() As Long
        Get
            Return mCurrentLineByteOffsetEnd
        End Get
    End Property

    Public ReadOnly Property CurrentLineByteOffsetEndWithTerminator() As Long
        Get
            Return mCurrentLineByteOffsetEndWithTerminator
        End Get
    End Property

    Public ReadOnly Property CurrentLineTerminator() As String
        Get
            If mCurrentLineTerminator Is Nothing Then
                Return String.Empty
            Else
                Return mCurrentLineTerminator
            End If
        End Get
    End Property

    Public ReadOnly Property ErrorMessage() As String
        Get
            Return mErrorMessage
        End Get
    End Property

    Public ReadOnly Property FileLengthBytes() As Long
        Get
            Try
                If mBinaryReader Is Nothing Then
                    Return 0
                Else
                    Return mBinaryReader.Length
                End If
            Catch ex As Exception
                Return 0
            End Try
        End Get
    End Property

    Public Property FileSystemMode() As FileSystemModeConstants
        Get
            Return mFileSystemMode
        End Get
        Set(Value As FileSystemModeConstants)
            mFileSystemMode = Value
            Select Case mFileSystemMode
                Case FileSystemModeConstants.Windows, FileSystemModeConstants.Unix
                    ' Normally present for Windows; normally not present for Unix
                    mLineTerminator1Code = LINE_TERMINATOR_CODE_CR
                    mLineTerminator2Code = LINE_TERMINATOR_CODE_LF
                Case FileSystemModeConstants.Macintosh
                    mLineTerminator1Code = 0
                    mLineTerminator2Code = LINE_TERMINATOR_CODE_CR
            End Select
        End Set
    End Property

    Public ReadOnly Property InputFilePath() As String
        Get
            Return mInputFilePath
        End Get
    End Property

    Public ReadOnly Property LineNumber() As Integer
        Get
            Return mLineNumber
        End Get
    End Property

    Public Property InputFileEncoding() As InputFileEncodingConstants
        Get
            Return mInputFileEncoding
        End Get
        Set(Value As InputFileEncodingConstants)
            SetInputFileEncoding(Value)
        End Set
    End Property

#End Region

    Public Function ByteAtBOF(lngBytePosition As Long) As Boolean
        If lngBytePosition <= mByteOrderMarkLength Then
            Return True
        Else
            Return False
        End If
    End Function

    Public Function ByteAtEOF(lngBytePosition As Long) As Boolean
        ' Returns True if lngBytePosition is >= the end of the file
        If lngBytePosition >= mBinaryReader.Length Then
            Return True
        Else
            Return False
        End If
    End Function

    Public Sub Close()
        Try
            If Not mBinaryReader Is Nothing Then
                mBinaryReader.Close()
            End If
        Catch ex As Exception
        End Try

        mInputFilePath = String.Empty
        mLineNumber = 0
        mByteBufferCount = 0
        mByteBufferFileOffsetStart = 0
        mByteBufferNextLineStartIndex = 0
    End Sub

    Protected Sub InitializeCurrentLine()
        mCurrentLineText = String.Empty
        mCurrentLineByteOffsetStart = 0
        mCurrentLineByteOffsetEnd = 0
        mCurrentLineByteOffsetEndWithTerminator = 0
        mCurrentLineTerminator = String.Empty
    End Sub

    Protected Sub InitializeLocalVariables()
        ' Note: Do Not update mFileSystemMode, mLineTerminator1Code, mLineTerminator2Code, or mInputFileEncoding in this sub

        mInputFilePath = String.Empty
        mErrorMessage = String.Empty

        mLineNumber = 0
        mByteOrderMarkLength = 0

        mByteBufferCount = 0
        If mByteBuffer Is Nothing Then
            ' In order to support Unicode files, it is important that the buffer length always be a power of 2
            ReDim mByteBuffer(INITIAL_BUFFER_LENGTH - 1)
        Else
            ' Clear the buffer
            Array.Clear(mByteBuffer, 0, mByteBuffer.Length)
        End If

        mReadLineDirectionSaved = ReadDirectionConstants.Forward
        mCurrentLineByteOffsetStartSaved = -1
        mCurrentLineTextSaved = String.Empty

        InitializeCurrentLine()
    End Sub

    Protected Sub LogErrors(strCallingFunction As String, strErrorDescription As String)

        Static LastCallingFunction As String
        Static LastErrorMessage As String
        Static LastSaveTime As DateTime

        Try
            If Not strErrorDescription Is Nothing Then
                mErrorMessage = String.Copy(strErrorDescription)
            Else
                mErrorMessage = "Unknown error"
            End If

            If Not LastCallingFunction Is Nothing Then
                If LastCallingFunction = strCallingFunction AndAlso
                   LastErrorMessage = strErrorDescription Then
                    If DateTime.UtcNow.Subtract(LastSaveTime).TotalSeconds < 0.5 Then
                        ' Duplicate message, less than 500 milliseconds since the last save
                        ' Do not update the log file
                        Exit Sub
                    End If
                End If
            End If

            LastCallingFunction = String.Copy(strCallingFunction)
            LastErrorMessage = String.Copy(strErrorDescription)
            LastSaveTime = DateTime.UtcNow

            Dim strLogFilePath = "MSDataFileReader_ErrorLog.txt"
            Using swErrorLog = New StreamWriter(New FileStream(strLogFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))

                swErrorLog.WriteLine(DateTime.Now & ControlChars.Tab &
                                     strCallingFunction & ControlChars.Tab &
                                     mErrorMessage)

            End Using

        Catch ex As Exception
            ' Ignore errors that occur while logging errors
        End Try
    End Sub

    Public Sub MoveToByteOffset(lngByteOffset As Long)
        Dim intBytesRead As Integer

        Try
            If Not mBinaryReader Is Nothing AndAlso mBinaryReader.CanRead Then
                If lngByteOffset < 0 Then
                    lngByteOffset = 0
                ElseIf lngByteOffset > mBinaryReader.Length Then
                    lngByteOffset = mBinaryReader.Length
                End If

                If lngByteOffset < mByteBufferFileOffsetStart Then
                    ' Need to slide the buffer window backward
                    Do
                        mByteBufferFileOffsetStart -= mByteBuffer.Length
                    Loop While lngByteOffset < mByteBufferFileOffsetStart

                    If mByteBufferFileOffsetStart < 0 Then
                        mByteBufferFileOffsetStart = 0
                    End If

                    mBinaryReader.Seek(mByteBufferFileOffsetStart, SeekOrigin.Begin)

                    ' Clear the buffer
                    Array.Clear(mByteBuffer, 0, mByteBuffer.Length)

                    intBytesRead = mBinaryReader.Read(mByteBuffer, 0, mByteBuffer.Length)
                    mByteBufferCount = intBytesRead

                    mByteBufferNextLineStartIndex = CInt(lngByteOffset - mByteBufferFileOffsetStart)

                ElseIf lngByteOffset > mByteBufferFileOffsetStart + mByteBufferCount Then
                    If mByteBufferFileOffsetStart < mBinaryReader.Length Then
                        ' Possibly slide the buffer window forward (note that if 
                        '  mByteBufferCount < mByteBuffer.Length then we may not need to update mByteBufferFileOffsetStart)
                        Do While lngByteOffset > mByteBufferFileOffsetStart + mByteBuffer.Length
                            mByteBufferFileOffsetStart += mByteBuffer.Length
                        Loop

                        If mByteBufferFileOffsetStart >= mBinaryReader.Length Then
                            ' This shouldn't normally happen
                            mByteBufferFileOffsetStart -= mByteBuffer.Length
                            If mByteBufferFileOffsetStart < 0 Then
                                mByteBufferFileOffsetStart = 0
                            End If
                        End If

                        mBinaryReader.Seek(mByteBufferFileOffsetStart, SeekOrigin.Begin)

                        ' Clear the buffer
                        Array.Clear(mByteBuffer, 0, mByteBuffer.Length)

                        intBytesRead = mBinaryReader.Read(mByteBuffer, 0, mByteBuffer.Length)
                        mByteBufferCount = intBytesRead
                    End If

                    mByteBufferNextLineStartIndex = CInt(lngByteOffset - mByteBufferFileOffsetStart)
                    If mByteBufferNextLineStartIndex > mByteBufferCount Then
                        ' This shouldn't normally happen
                        mByteBufferNextLineStartIndex = mByteBufferCount
                    End If
                Else
                    ' The desired byte offset is already present in mByteBuffer
                    mByteBufferNextLineStartIndex = CInt(lngByteOffset - mByteBufferFileOffsetStart)
                    If mByteBufferNextLineStartIndex > mByteBufferCount Then
                        ' This shouldn't normally happen, but is possible if jumping around a file and reading forward and 
                        mByteBufferNextLineStartIndex = mByteBufferCount
                    End If
                End If
            End If

        Catch ex As Exception
            If mInputFilePath Is Nothing Then mInputFilePath = String.Empty
            LogErrors("MoveToByteOffset",
                      "Error moving to byte offset " & lngByteOffset.ToString & " in file " & mInputFilePath & "; " &
                      ex.Message)
        End Try
    End Sub

    Public Sub MoveToBeginning()
        ' Move to the beginning of the file and freshly populate the byte buffer

        Dim intIndex As Integer
        Dim intIndexStart As Integer
        Dim intIndexEnd As Integer

        Dim intCharCheckCount As Integer
        Dim intAlternatedZeroMatchCount As Integer

        Try
            mByteBufferFileOffsetStart = 0

            ' Clear the buffer
            Array.Clear(mByteBuffer, 0, mByteBuffer.Length)

            mBinaryReader.Seek(mByteBufferFileOffsetStart, SeekOrigin.Begin)
            mByteBufferCount = mBinaryReader.Read(mByteBuffer, 0, mByteBuffer.Length)
            mByteBufferNextLineStartIndex = 0

            ' Look for a byte order mark at the beginning of the file
            mByteOrderMarkLength = 0
            If mByteBufferCount >= 2 Then
                If mByteBuffer(0) = 255 And mByteBuffer(1) = 254 Then
                    ' Unicode (Little Endian)
                    ' Note that this sets mCharSize to 2
                    SetInputFileEncoding(InputFileEncodingConstants.UnicodeNormal)

                    ' Skip the first 2 bytes
                    mByteBufferNextLineStartIndex = 2
                    mByteOrderMarkLength = 2

                ElseIf mByteBuffer(0) = 254 And mByteBuffer(1) = 255 Then
                    ' Unicode (Big Endian)
                    ' Note that this sets mCharSize to 2
                    SetInputFileEncoding(InputFileEncodingConstants.UnicodeBigEndian)
                    ' Skip the first 2 bytes
                    mByteBufferNextLineStartIndex = 2
                    mByteOrderMarkLength = 2

                ElseIf mByteBufferCount >= 3 Then
                    If mByteBuffer(0) = 239 And mByteBuffer(1) = 187 And mByteBuffer(2) = 191 Then
                        ' UTF8
                        ' Note that this sets mCharSize to 1
                        SetInputFileEncoding(InputFileEncodingConstants.UTF8)
                        ' Skip the first 3 bytes
                        mByteBufferNextLineStartIndex = 3
                        mByteOrderMarkLength = 3
                    Else
                        ' Examine the first 2000 bytes and check whether or not 
                        '  every other byte is 0 for at least 95% of the data
                        ' If it is, then assume the appropriate Unicode format

                        intIndexEnd = 2000
                        If intIndexEnd >= mByteBufferCount - 1 Then
                            intIndexEnd = mByteBufferCount - 2
                        End If

                        For intIndexStart = 0 To 1
                            intCharCheckCount = 0
                            intAlternatedZeroMatchCount = 0

                            For intIndex = intIndexStart To mByteBufferCount - 2 Step 2
                                intCharCheckCount += 1
                                If mByteBuffer(intIndex) <> 0 And mByteBuffer(intIndex + 1) = 0 Then
                                    intAlternatedZeroMatchCount += 1
                                End If
                            Next intIndex

                            If intCharCheckCount > 0 Then
                                If intAlternatedZeroMatchCount / CDbl(intCharCheckCount) >= 0.95 Then
                                    ' Assume this is a Unicode file
                                    If intIndexStart = 0 Then
                                        ' Unicode (Little Endian)
                                        SetInputFileEncoding(InputFileEncodingConstants.UnicodeNormal)
                                    Else
                                        ' Unicode (Big Endian)
                                        SetInputFileEncoding(InputFileEncodingConstants.UnicodeBigEndian)
                                    End If
                                    Exit For
                                End If
                            End If
                        Next intIndexStart
                    End If
                End If
            End If

        Catch ex As Exception
            If mInputFilePath Is Nothing Then mInputFilePath = String.Empty
            LogErrors("MoveToBeginning", "Error moving to beginning of file " & mInputFilePath & "; " & ex.Message)
        End Try
    End Sub

    Public Sub MoveToEnd()
        Try
            If Not mBinaryReader Is Nothing AndAlso mBinaryReader.CanRead Then
                MoveToByteOffset(mBinaryReader.Length)
            End If
        Catch ex As Exception
            If mInputFilePath Is Nothing Then mInputFilePath = String.Empty
            LogErrors("MoveToEnd", "Error moving to end of file " & mInputFilePath & "; " & ex.Message)
        End Try
    End Sub

    Public Function OpenFile(dataFilePath As String) As Boolean
        Return OpenFile(dataFilePath, FileShare.Read)
    End Function

    Public Function OpenFile(dataFilePath As String, share As FileShare) As Boolean
        ' Returns true if the file is successfully opened

        Dim blnSuccess As Boolean

        mErrorMessage = String.Empty

        ' Make sure any open file or text stream is closed
        Me.Close()

        Try
            blnSuccess = False
            If String.IsNullOrEmpty(dataFilePath) Then
                mErrorMessage = "Error opening file: input file path is blank"
                Return False
            End If

            If Not File.Exists(dataFilePath) Then
                mErrorMessage = "File not found: " & InputFilePath
                Return False
            End If

            InitializeLocalVariables()

            mInputFilePath = String.Copy(dataFilePath)

            ' Note that this sets mCharSize to 1
            SetInputFileEncoding(InputFileEncodingConstants.Ascii)

            ' Initialize the binary reader
            mBinaryReader = New FileStream(mInputFilePath, FileMode.Open, FileAccess.Read, share)

            If mBinaryReader.Length = 0 Then
                Close()

                mErrorMessage = "File is zero-length"
                blnSuccess = False
            Else
                MoveToBeginning()
                blnSuccess = True
            End If

        Catch ex As Exception
            LogErrors("OpenFile", "Error opening file: " & InputFilePath & "; " & ex.Message)
            blnSuccess = False
        End Try

        Return blnSuccess
    End Function

    Public Function ReadLine() As Boolean
        Return ReadLine(ReadDirectionConstants.Forward)
    End Function

    Public Function ReadLine(eDirection As ReadDirectionConstants) As Boolean
        ' Looks for the next line in the file (by looking for the next LF symbol in the binary file)
        ' Returns True if success, False if failure
        ' Use Property CurrentLine to obtain the text for the line

        Dim intSearchIndexStartOffset As Integer
        Dim intBytesRead As Integer
        Dim intBytesToRead As Integer

        Dim intIndex As Integer
        Dim intIndexMinimum As Integer
        Dim intIndexMaximum As Integer
        Dim intShiftIncrement As Integer

        Dim intMatchingTextIndexStart As Integer
        Dim intMatchingTextIndexEnd As Integer

        Dim intLineTerminatorLength As Integer

        Dim intTerminatorCheckCount As Integer
        Dim intTerminatorCheckCountValueZero As Integer
        Dim dblValueZeroFraction As Double

        Dim intStartIndexShiftCount As Integer
        Dim intStartIndexShiftIncrement As Integer
        Dim blnStartIndexShifted As Boolean

        Dim blnMatchFound As Boolean
        Dim blnTerminatorFound As Boolean

        Try
            blnMatchFound = False
            blnTerminatorFound = False
            intStartIndexShiftCount = 0

            InitializeCurrentLine()

            If Not mBinaryReader Is Nothing AndAlso mBinaryReader.CanRead Then
                Select Case mInputFileEncoding
                    Case InputFileEncodingConstants.Ascii, InputFileEncodingConstants.UTF8
                        ' Ascii or UTF-8 encoding; Assure mCharSize = 1
                        mCharSize = 1
                    Case InputFileEncodingConstants.UnicodeNormal
                        ' Unicode (Little Endian) encoding; Assure mCharSize = 2
                        mCharSize = 2
                    Case InputFileEncodingConstants.UnicodeBigEndian
                        ' Unicode (Big Endian) encoding; Assure mCharSize = 2
                        mCharSize = 2
                    Case Else
                        ' Unknown encoding
                        mCurrentLineText = String.Empty
                        Return False
                End Select

                If eDirection = ReadDirectionConstants.Forward Then
                    intSearchIndexStartOffset = 0
                    If ByteAtEOF(mByteBufferFileOffsetStart + mByteBufferNextLineStartIndex + intSearchIndexStartOffset) Then
                        mCurrentLineByteOffsetStart = mBinaryReader.Length
                        mCurrentLineByteOffsetEnd = mBinaryReader.Length
                        mCurrentLineByteOffsetEndWithTerminator = mBinaryReader.Length
                        Return False
                    End If
                Else
                    intSearchIndexStartOffset = -mCharSize * 2
                    If ByteAtBOF(mByteBufferFileOffsetStart + mByteBufferNextLineStartIndex + intSearchIndexStartOffset) Then
                        Return False
                    End If
                End If

                Do While Not blnMatchFound
                    ' Note that intSearchIndexStartOffset will be >=0 if searching forward and <=-2 if searching backward
                    intIndex = mByteBufferNextLineStartIndex + intSearchIndexStartOffset

                    ' Define the minimum and maximum allowable indices for searching for mLineTerminator2Code
                    intIndexMinimum = mCharSize - 1                     ' This is only used when searching backward
                    intIndexMaximum = mByteBufferCount - mCharSize      ' This is only used when searching forward

                    If eDirection = ReadDirectionConstants.Reverse AndAlso
                       mLineTerminator1Code <> 0 AndAlso
                       mByteBufferFileOffsetStart > 0 Then
                        ' We're looking for a two-character line terminator (though the 
                        '  presence of mLineTerminator1Code is not required)
                        ' Need to increment intIndexMinimum to guarantee we'll be able to find both line terminators if the 
                        '  second line terminator happens to be at the start of mByteBuffer
                        intIndexMinimum += mCharSize
                    End If

                    ' Reset the terminator check counters
                    intTerminatorCheckCount = 0
                    intTerminatorCheckCountValueZero = 0
                    blnStartIndexShifted = False

                    If (eDirection = ReadDirectionConstants.Reverse AndAlso intIndex >= intIndexMinimum) OrElse
                       (eDirection = ReadDirectionConstants.Forward AndAlso intIndex <= intIndexMaximum) Then
                        Do
                            Select Case mInputFileEncoding
                                Case InputFileEncodingConstants.Ascii, InputFileEncodingConstants.UTF8
                                    ' Ascii or UTF-8 encoding; Assure mCharSize = 1
                                    If mByteBuffer(intIndex) = mLineTerminator2Code Then
                                        blnTerminatorFound = True
                                        Exit Do
                                    End If
                                Case InputFileEncodingConstants.UnicodeNormal
                                    ' Look for the LF symbol followed by a byte with value 0 in mByteBuffer
                                    If mByteBuffer(intIndex) = mLineTerminator2Code AndAlso
                                       mByteBuffer(intIndex + 1) = 0 Then
                                        blnTerminatorFound = True
                                        Exit Do
                                    ElseIf mByteBuffer(intIndex) = 0 Then
                                        intTerminatorCheckCountValueZero += 1
                                    End If
                                    intTerminatorCheckCount += 1

                                Case InputFileEncodingConstants.UnicodeBigEndian
                                    ' Unicode (Big Endian) encoding; Assure mCharSize = 2
                                    If mByteBuffer(intIndex) = 0 AndAlso
                                       mByteBuffer(intIndex + 1) = mLineTerminator2Code Then
                                        blnTerminatorFound = True
                                        Exit Do
                                    ElseIf mByteBuffer(intIndex + 1) = 0 Then
                                        intTerminatorCheckCountValueZero += 1
                                    End If
                                    intTerminatorCheckCount += 1

                            End Select

                            If eDirection = ReadDirectionConstants.Forward Then
                                If intIndex + mCharSize <= intIndexMaximum Then
                                    intIndex += mCharSize
                                Else
                                    Exit Do
                                End If
                            Else
                                If intIndex - mCharSize >= intIndexMinimum Then
                                    intIndex -= mCharSize
                                Else
                                    Exit Do
                                End If
                            End If

                        Loop While Not blnTerminatorFound
                    End If

                    If Not blnTerminatorFound Then
                        If intTerminatorCheckCount > 0 Then
                            dblValueZeroFraction = intTerminatorCheckCountValueZero / CDbl(intTerminatorCheckCount)
                        Else
                            dblValueZeroFraction = 0
                        End If

                        If mCharSize > 1 AndAlso
                           intStartIndexShiftCount < mCharSize - 1 AndAlso
                           dblValueZeroFraction >= 0.95 Then

                            ' mByteBufferNextLineStartIndex is most likely off by 1
                            ' This could happen due to an inappropriate byte value being sent to MoveToByteOffset()
                            '  or due to a corrupted Unicode file

                            ' Shift mByteBufferNextLineStartIndex by 1 and try again
                            If intStartIndexShiftCount = 0 Then
                                ' First attempt to shift; determine the shift direction
                                If eDirection = ReadDirectionConstants.Forward Then
                                    ' Searching forward
                                    If mByteBufferNextLineStartIndex > mCharSize - 2 Then
                                        intStartIndexShiftIncrement = -1
                                    Else
                                        intStartIndexShiftIncrement = 1
                                    End If
                                Else
                                    ' Searching reverse
                                    If mByteBufferNextLineStartIndex < mByteBufferCount - (mCharSize - 2) Then
                                        intStartIndexShiftIncrement = 1
                                    Else
                                        intStartIndexShiftIncrement = -1
                                    End If
                                End If
                            End If

                            mByteBufferNextLineStartIndex += intStartIndexShiftIncrement
                            intStartIndexShiftCount += 1
                            blnStartIndexShifted = True
                        Else
                            If eDirection = ReadDirectionConstants.Forward Then
                                ' Searching forward; are we at the end of the file?
                                If ByteAtEOF(mByteBufferFileOffsetStart + intIndex + mCharSize) Then
                                    ' Yes, we're at the end of the file
                                    blnTerminatorFound = True
                                    intIndex = CInt(mByteBufferCount - 1)
                                End If
                            Else
                                ' Searching backward; are we at the beginning of the file?
                                If ByteAtBOF(mByteBufferFileOffsetStart + intIndex) Then
                                    ' Yes, we're at the beginning of the file
                                    blnTerminatorFound = True
                                    intIndex = CInt(mByteOrderMarkLength) - mCharSize
                                End If
                            End If
                        End If

                    End If

                    If blnTerminatorFound Then
                        If eDirection = ReadDirectionConstants.Forward Then
                            intMatchingTextIndexStart = mByteBufferNextLineStartIndex
                            intMatchingTextIndexEnd = intIndex
                        Else
                            intMatchingTextIndexStart = intIndex + mCharSize
                            intMatchingTextIndexEnd = mByteBufferNextLineStartIndex - mCharSize
                        End If

                        ' Determine the line terminator length
                        Select Case mInputFileEncoding
                            Case InputFileEncodingConstants.Ascii, InputFileEncodingConstants.UTF8
                                ' Ascii encoding
                                If mLineTerminator1Code <> 0 AndAlso
                                   intMatchingTextIndexEnd - mCharSize >= 0 AndAlso
                                   mByteBuffer(intMatchingTextIndexEnd - mCharSize) = mLineTerminator1Code Then
                                    intLineTerminatorLength = 2
                                    mCurrentLineTerminator =
                                        Convert.ToChar(mByteBuffer(intMatchingTextIndexEnd - mCharSize)) &
                                        Convert.ToChar(mByteBuffer(intMatchingTextIndexEnd))
                                ElseIf mByteBuffer(intMatchingTextIndexEnd) = mLineTerminator2Code Then
                                    intLineTerminatorLength = 1
                                    mCurrentLineTerminator = Convert.ToChar(mByteBuffer(intMatchingTextIndexEnd))
                                Else
                                    ' No line terminator (this is probably the last line of the file or else the user called MoveToByteOffset with a location in the middle of a line)
                                    intLineTerminatorLength = 0
                                    mCurrentLineTerminator = String.Empty
                                End If

                                intBytesToRead = intMatchingTextIndexEnd - intMatchingTextIndexStart -
                                                 mCharSize * (intLineTerminatorLength - 1)
                                If intBytesToRead <= 0 Then
                                    ' Blank line
                                    mCurrentLineText = String.Empty
                                Else
                                    If mInputFileEncoding = InputFileEncodingConstants.UTF8 Then
                                        ' Extract the data between intMatchingTextIndexStart and intMatchingTextIndexEnd, excluding any line terminator characters
                                        mCurrentLineText = Convert.ToString(Encoding.UTF8.GetChars(mByteBuffer,
                                                                                                   intMatchingTextIndexStart,
                                                                                                   intBytesToRead))
                                    Else
                                        ' Extract the data between intMatchingTextIndexStart and intMatchingTextIndexEnd, excluding any line terminator characters
                                        mCurrentLineText = Convert.ToString(Encoding.ASCII.GetChars(mByteBuffer,
                                                                                                    intMatchingTextIndexStart,
                                                                                                    intBytesToRead))
                                    End If
                                End If

                            Case InputFileEncodingConstants.UnicodeNormal
                                ' Unicode (Little Endian) encoding
                                If mLineTerminator1Code <> 0 AndAlso
                                   intMatchingTextIndexEnd - mCharSize >= 0 AndAlso
                                   mByteBuffer(intMatchingTextIndexEnd - mCharSize) = mLineTerminator1Code AndAlso
                                   mByteBuffer(intMatchingTextIndexEnd - mCharSize + 1) = 0 Then
                                    intLineTerminatorLength = 2
                                    mCurrentLineTerminator =
                                        Convert.ToChar(mByteBuffer(intMatchingTextIndexEnd - mCharSize)) &
                                        Convert.ToChar(mByteBuffer(intMatchingTextIndexEnd))
                                ElseIf mByteBuffer(intMatchingTextIndexEnd) = mLineTerminator2Code AndAlso
                                       intMatchingTextIndexEnd + 1 < mByteBufferCount AndAlso
                                       mByteBuffer(intMatchingTextIndexEnd + 1) = 0 Then
                                    intLineTerminatorLength = 1
                                    mCurrentLineTerminator = Convert.ToChar(mByteBuffer(intMatchingTextIndexEnd))
                                Else
                                    ' No line terminator (this is probably the last line of the file or else the user called MoveToByteOffset with a location in the middle of a line)
                                    intLineTerminatorLength = 0
                                    mCurrentLineTerminator = String.Empty
                                End If

                                ' Extract the data between intMatchingTextIndexStart and intMatchingTextIndexEnd, excluding any line terminator characters
                                intBytesToRead = intMatchingTextIndexEnd - intMatchingTextIndexStart -
                                                 mCharSize * (intLineTerminatorLength - 1)
                                If intBytesToRead <= 0 Then
                                    ' Blank line
                                    mCurrentLineText = String.Empty
                                Else
                                    mCurrentLineText = Convert.ToString(Encoding.Unicode.GetChars(mByteBuffer,
                                                                                                  intMatchingTextIndexStart,
                                                                                                  intBytesToRead))
                                End If

                            Case InputFileEncodingConstants.UnicodeBigEndian
                                ' Unicode (Big Endian) encoding
                                If mLineTerminator1Code <> 0 AndAlso
                                   intMatchingTextIndexEnd - mCharSize >= 0 AndAlso
                                   mByteBuffer(intMatchingTextIndexEnd - mCharSize) = 0 AndAlso
                                   mByteBuffer(intMatchingTextIndexEnd - mCharSize + 1) = mLineTerminator1Code Then
                                    intLineTerminatorLength = 2
                                    mCurrentLineTerminator =
                                        Convert.ToChar(mByteBuffer(intMatchingTextIndexEnd - mCharSize + 1)) &
                                        Convert.ToChar(mByteBuffer(intMatchingTextIndexEnd + 1))
                                ElseIf mByteBuffer(intMatchingTextIndexEnd) = 0 AndAlso
                                       intMatchingTextIndexEnd + 1 < mByteBufferCount AndAlso
                                       mByteBuffer(intMatchingTextIndexEnd + 1) = mLineTerminator2Code Then
                                    intLineTerminatorLength = 1
                                    mCurrentLineTerminator = Convert.ToChar(mByteBuffer(intMatchingTextIndexEnd + 1))
                                Else
                                    ' No line terminator (this is probably the last line of the file or else the user called MoveToByteOffset with a location in the middle of a line)
                                    intLineTerminatorLength = 0
                                    mCurrentLineTerminator = String.Empty
                                End If

                                ' Extract the data between intMatchingTextIndexStart and intMatchingTextIndexEnd, excluding any line terminator characters
                                intBytesToRead = intMatchingTextIndexEnd - intMatchingTextIndexStart -
                                                 mCharSize * (intLineTerminatorLength - 1)
                                If intBytesToRead <= 0 Then
                                    ' Blank line
                                    mCurrentLineText = String.Empty
                                Else
                                    mCurrentLineText = Convert.ToString(Encoding.BigEndianUnicode.GetChars(mByteBuffer,
                                                                                                           intMatchingTextIndexStart,
                                                                                                           intBytesToRead))
                                End If

                            Case Else
                                ' Unknown/unsupported encoding
                                mCurrentLineText = String.Empty
                                blnMatchFound = False
                                Exit Do
                        End Select

                        If mCharSize > 1 AndAlso Not ByteAtEOF(mByteBufferFileOffsetStart + intMatchingTextIndexEnd) Then
                            intMatchingTextIndexEnd += (mCharSize - 1)
                        End If

                        mCurrentLineByteOffsetStart = mByteBufferFileOffsetStart + intMatchingTextIndexStart
                        mCurrentLineByteOffsetEndWithTerminator = mByteBufferFileOffsetStart + intMatchingTextIndexEnd

                        mCurrentLineByteOffsetEnd = mByteBufferFileOffsetStart + intMatchingTextIndexEnd -
                                                    intLineTerminatorLength * mCharSize
                        If mCurrentLineByteOffsetEnd < mCurrentLineByteOffsetStart Then
                            ' Zero-length line
                            mCurrentLineByteOffsetEnd = mCurrentLineByteOffsetStart
                        End If

                        If eDirection = ReadDirectionConstants.Forward Then
                            mByteBufferNextLineStartIndex = intMatchingTextIndexEnd + 1
                            mLineNumber += 1
                        Else
                            mByteBufferNextLineStartIndex = intMatchingTextIndexStart
                            If mLineNumber > 0 Then
                                mLineNumber -= 1
                            End If
                        End If

                        ' Check whether the user just changed reading direction
                        ' If they did, then it is possible that this function will return the exact same line
                        '  as was previously read.  Check for this, and if true, then read the next line (in direction eDiretion)
                        If eDirection <> mReadLineDirectionSaved AndAlso
                           mCurrentLineByteOffsetStartSaved >= 0 AndAlso
                           mCurrentLineByteOffsetStart = mCurrentLineByteOffsetStartSaved AndAlso
                           Not mCurrentLineTextSaved Is Nothing AndAlso
                           mCurrentLineText = mCurrentLineTextSaved Then

                            ' Recursively call this function to read the next line
                            ' To avoid infinite loops, set mCurrentLineByteOffsetStartSaved to -1
                            mCurrentLineByteOffsetStartSaved = -1
                            blnMatchFound = ReadLine(eDirection)
                        Else
                            blnMatchFound = True
                        End If

                        Exit Do
                    End If

                    If Not blnMatchFound AndAlso Not blnStartIndexShifted Then
                        ' Need to add more data to the buffer (or shift the data in the buffer)
                        If eDirection = ReadDirectionConstants.Forward Then
                            If mBinaryReader.Position >= mBinaryReader.Length Then
                                ' Already at the end of the file; cannot move forward
                                Exit Do
                            End If

                            If mByteBufferNextLineStartIndex > 0 Then
                                ' First, shift all of the data so that element mByteBufferNextLineStartIndex moves to element 0
                                For intIndex = mByteBufferNextLineStartIndex To mByteBufferCount - 1
                                    mByteBuffer(intIndex - mByteBufferNextLineStartIndex) = mByteBuffer(intIndex)
                                Next intIndex

                                mByteBufferCount -= mByteBufferNextLineStartIndex
                                mByteBufferFileOffsetStart += mByteBufferNextLineStartIndex
                                intSearchIndexStartOffset = mByteBufferCount

                                mByteBufferNextLineStartIndex = 0

                                If mByteBufferFileOffsetStart + mByteBufferCount <> mBinaryReader.Position Then
                                    ' The file read-position is out-of-sync with mByteBufferFileOffsetStart; this can happen
                                    '  if we used MoveToByteOffset, read backward, and are now reading forward
                                    mBinaryReader.Seek(mByteBufferFileOffsetStart + mByteBufferCount, SeekOrigin.Begin)
                                End If
                            Else
                                intSearchIndexStartOffset = mByteBufferCount

                                If mByteBufferCount >= mByteBuffer.Length Then
                                    ' Need to expand the buffer
                                    ' In order to support Unicode files, it is important that the buffer length always be a power of 2
                                    ReDim Preserve mByteBuffer(mByteBuffer.Length * 2 - 1)
                                End If
                            End If

                            intBytesRead = mBinaryReader.Read(mByteBuffer, intSearchIndexStartOffset,
                                                              mByteBuffer.Length - intSearchIndexStartOffset)
                            If intBytesRead = 0 Then
                                ' No data could be read; exit the loop
                                Exit Do
                            Else
                                mByteBufferCount += intBytesRead
                            End If
                        Else

                            If mByteBufferFileOffsetStart <= mByteOrderMarkLength OrElse mBinaryReader.Position <= 0 Then
                                ' Already at the beginning of the file; cannot move backward
                                Exit Do
                            End If

                            If mByteBufferCount >= mByteBuffer.Length And
                               mByteBufferNextLineStartIndex >= mByteBuffer.Length Then
                                ' The byte buffer is full and mByteBufferNextLineStartIndex is past the end of the buffer
                                ' Need to double its size, shift the data from the first half to the second half, and
                                '  populate the first half

                                ' Expand the buffer
                                ' In order to support Unicode files, it is important that the buffer length always be a power of 2
                                ReDim Preserve mByteBuffer(mByteBuffer.Length * 2 - 1)
                            End If

                            If mByteBufferCount < mByteBuffer.Length Then
                                intShiftIncrement = mByteBuffer.Length - mByteBufferCount
                            Else
                                intShiftIncrement = mByteBuffer.Length - mByteBufferNextLineStartIndex
                            End If

                            If mByteBufferFileOffsetStart - intShiftIncrement < mByteOrderMarkLength Then
                                intShiftIncrement = CInt(mByteBufferFileOffsetStart) - mByteOrderMarkLength
                            End If

                            ' Possibly update mByteBufferCount
                            If mByteBufferCount < mByteBuffer.Length Then
                                mByteBufferCount += intShiftIncrement
                            End If

                            ' Shift the data
                            For intIndex = mByteBufferCount - intShiftIncrement - 1 To 0 Step -1
                                mByteBuffer(intShiftIncrement + intIndex) = mByteBuffer(intIndex)
                            Next intIndex

                            ' Update the tracking variables
                            mByteBufferFileOffsetStart -= intShiftIncrement
                            mByteBufferNextLineStartIndex += intShiftIncrement

                            ' Populate the first portion of the byte buffer with new data
                            mBinaryReader.Seek(mByteBufferFileOffsetStart, SeekOrigin.Begin)
                            intBytesRead = mBinaryReader.Read(mByteBuffer, 0, intShiftIncrement)
                            If intBytesRead = 0 Then
                                ' No data could be read; this shouldn't ever happen
                                ' Move to the beginning of the file and re-populate mByteBuffer
                                MoveToBeginning()
                                Exit Do
                            End If

                        End If
                    End If
                Loop
            End If

        Catch ex As Exception
            LogErrors("ReadLine", ex.Message)
            blnMatchFound = False
        End Try

        If blnMatchFound Then
            mReadLineDirectionSaved = eDirection
            mCurrentLineByteOffsetStartSaved = mCurrentLineByteOffsetStart
            mCurrentLineTextSaved = String.Copy(mCurrentLineText)
        Else
            mReadLineDirectionSaved = eDirection
            mCurrentLineByteOffsetStartSaved = -1
            mCurrentLineTextSaved = String.Empty
        End If

        Return blnMatchFound
    End Function

    Protected Sub SetInputFileEncoding(EncodingMode As InputFileEncodingConstants)
        mInputFileEncoding = EncodingMode
        Select Case mInputFileEncoding
            Case InputFileEncodingConstants.Ascii, InputFileEncodingConstants.UTF8
                mCharSize = 1
            Case InputFileEncodingConstants.UnicodeNormal
                mCharSize = 2
            Case InputFileEncodingConstants.UnicodeBigEndian
                mCharSize = 2
            Case Else
                ' Unknown mode; assume mCharSize = 1
                mCharSize = 1
        End Select
    End Sub
End Class
