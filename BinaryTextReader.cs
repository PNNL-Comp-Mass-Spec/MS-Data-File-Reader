// -------------------------------------------------------------------------------
// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
// Copyright 2021, Battelle Memorial Institute.  All Rights Reserved.
//
// E-mail: matthew.monroe@pnl.gov or proteomics@pnnl.gov
// Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://panomics.pnnl.gov/ or https://www.pnnl.gov/integrative-omics
// -------------------------------------------------------------------------------
//
// Licensed under the Apache License, Version 2.0; you may not use this file except
// in compliance with the License.  You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
//
// Notice: This computer software was prepared by Battelle Memorial Institute,
// hereinafter the Contractor, under Contract No. DE-AC05-76RL0 1830 with the
// Department of Energy (DOE).  All rights in the computer software are reserved
// by DOE on behalf of the United States Government and the Contractor as
// provided in the Contract.  NEITHER THE GOVERNMENT NOR THE CONTRACTOR MAKES ANY
// WARRANTY, EXPRESS OR IMPLIED, OR ASSUMES ANY LIABILITY FOR THE USE OF THIS
// SOFTWARE.  This notice including this sentence must appear on any copies of
// this computer software.

using System;
using System.IO;
using System.Text;
using PRISM;

namespace MSDataFileReader
{
    /// <summary>
    /// <para>
    /// This class can be used to open a Text file and read each of the lines from the file,
    /// where a line of text ends with CRLF or simply LF
    /// In addition, the byte offset at the start and end of the line is also returned
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Note that this class is compatible with UTF-16 Unicode files; it looks for byte order mark
    /// FF FE or FE FF in the first two bytes of the file to determine if a file is Unicode
    /// (though you can override this using the InputFileEncoding property after calling .OpenFile()
    /// This class will also look for the byte order mark for UTF-8 files (EF BB BF) though it may not
    /// properly decode UTF-8 characters (not fully tested)
    /// </para>
    /// <para>
    /// You can change the expected line terminator character using Property FileSystemMode
    /// If FileSystemMode = FileSystemModeConstants.Windows, the Line Terminator = LF, optionally preceded by CR
    /// If FileSystemMode = FileSystemModeConstants.Linux, the Line Terminator = LF, optionally preceded by CR
    /// If FileSystemMode = FileSystemModeConstants.Macintosh, the Line Terminator = CR, previous character is not considered
    /// </para>
    /// </remarks>
    public class BinaryTextReader : EventNotifier
    {
        // Ignore Spelling: endian

        public BinaryTextReader()
        {
            // Note: Setting this property's value will also update mLineTerminator1Code and mLineTerminator2Code
            FileSystemMode = FileSystemModes.Windows;

            InitializeLocalVariables();
        }

        ~BinaryTextReader()
        {
            Close();
        }

        private const byte LINE_TERMINATOR_CODE_CR = 13;

        private const byte LINE_TERMINATOR_CODE_LF = 10;

        public enum FileSystemModes
        {
            Windows = 0,
            Linux = 1,
            Macintosh = 2
        }

        public enum InputFileEncodings
        {
            /// <summary>
            /// No Byte Order Mark
            /// </summary>
            ASCII = 0,

            /// <summary>
            /// Byte Order Mark: EF BB BF (UTF-8)
            /// </summary>
            UTF8 = 1,

            /// <summary>
            /// Byte Order Mark: FF FE (Little Endian Unicode)
            /// </summary>
            UnicodeNormal = 2,

            /// <summary>
            /// Byte Order Mark: FE FF (Big Endian Unicode)
            /// </summary>
            UnicodeBigEndian = 3
        }

        public enum ReadDirection
        {
            Forward = 0,
            Reverse = 1
        }

        private FileStream mBinaryReader;

        private int mByteBufferCount;

        private byte[] mByteBuffer;

        // Note: The first byte in the file is Byte 0
        private long mByteBufferFileOffsetStart;

        // This variable defines the index in mByteBuffer() at which the next line starts
        private int mByteBufferNextLineStartIndex;

        private byte mByteOrderMarkLength;

        private byte mCharSize = 1;

        private long mCurrentLineByteOffsetStart;

        private long mCurrentLineByteOffsetStartSaved;

        private long mCurrentLineByteOffsetEnd;

        private long mCurrentLineByteOffsetEndWithTerminator;

        private string mCurrentLineTerminator;

        private string mCurrentLineText;

        private string mCurrentLineTextSaved;

        private string mErrorMessage;

        // Note: Use Me.FileSystemMode to set this variable so that mLineTerminator1Code and mLineTerminator2Code also get updated
        private FileSystemModes mFileSystemMode;

        private InputFileEncodings mInputFileEncoding;

        private string mInputFilePath;

        private int mLineNumber;

        private byte mLineTerminator1Code;

        private byte mLineTerminator2Code;

        private ReadDirection mReadLineDirectionSaved;

        public long ByteBufferFileOffsetStart => mByteBufferFileOffsetStart;

        public byte ByteOrderMarkLength => mByteOrderMarkLength;

        public byte CharSize => mCharSize;

        public string CurrentLine => mCurrentLineText ?? string.Empty;

        // ReSharper disable once UnusedMember.Global
        public int CurrentLineLength => mCurrentLineText?.Length ?? 0;

        public long CurrentLineByteOffsetStart => mCurrentLineByteOffsetStart;

        public long CurrentLineByteOffsetEnd => mCurrentLineByteOffsetEnd;

        public long CurrentLineByteOffsetEndWithTerminator => mCurrentLineByteOffsetEndWithTerminator;

        public string CurrentLineTerminator => mCurrentLineTerminator ?? string.Empty;

        // ReSharper disable once UnusedMember.Global
        public string ErrorMessage => mErrorMessage ?? string.Empty;

        public long FileLengthBytes
        {
            get
            {
                try
                {
                    return mBinaryReader?.Length ?? 0L;
                }
                catch (Exception)
                {
                    return 0L;
                }
            }
        }

        public FileSystemModes FileSystemMode
        {
            get => mFileSystemMode;

            set
            {
                mFileSystemMode = value;

                switch (mFileSystemMode)
                {
                    case FileSystemModes.Windows:
                    case FileSystemModes.Linux:
                        // Normally present for Windows; normally not present for Linux
                        mLineTerminator1Code = LINE_TERMINATOR_CODE_CR;
                        mLineTerminator2Code = LINE_TERMINATOR_CODE_LF;
                        break;

                    case FileSystemModes.Macintosh:
                        mLineTerminator1Code = 0;
                        mLineTerminator2Code = LINE_TERMINATOR_CODE_CR;
                        break;
                }
            }
        }

        public InputFileEncodings InputFileEncoding
        {
            get => mInputFileEncoding;

            set => SetInputFileEncoding(value);
        }

        public string InputFilePath => mInputFilePath;

        public int LineNumber => mLineNumber;

        public bool ByteAtBOF(long bytePosition)
        {
            return bytePosition <= mByteOrderMarkLength;
        }

        /// <summary>
        /// Check whether the reader is at the end of the file
        /// </summary>
        /// <param name="bytePosition"></param>
        /// <returns>True if bytePosition is at or beyond the end of the file</returns>
        public bool ByteAtEOF(long bytePosition)
        {
            return bytePosition >= mBinaryReader.Length;
        }

        public void Close()
        {
            try
            {
                mBinaryReader?.Close();
            }
            catch (Exception)
            {
                // Ignore errors here
            }

            mInputFilePath = string.Empty;
            mLineNumber = 0;
            mByteBufferCount = 0;
            mByteBufferFileOffsetStart = 0L;
            mByteBufferNextLineStartIndex = 0;
        }

        private void InitializeCurrentLine()
        {
            mCurrentLineText = string.Empty;
            mCurrentLineByteOffsetStart = 0L;
            mCurrentLineByteOffsetEnd = 0L;
            mCurrentLineByteOffsetEndWithTerminator = 0L;
            mCurrentLineTerminator = string.Empty;
        }

        /// <summary>
        /// Initialize local variables
        /// </summary>
        /// <remarks>
        /// Do not update mFileSystemMode, mLineTerminator1Code, mLineTerminator2Code, or mInputFileEncoding in this method
        /// </remarks>
        private void InitializeLocalVariables()
        {
            mInputFilePath = string.Empty;
            mErrorMessage = string.Empty;
            mLineNumber = 0;
            mByteOrderMarkLength = 0;
            mByteBufferCount = 0;

            if (mByteBuffer is null)
            {
                // In order to support Unicode files, it is important that the buffer length always be a power of 2
                mByteBuffer = new byte[10000];
            }
            else
            {
                // Clear the buffer
                Array.Clear(mByteBuffer, 0, mByteBuffer.Length);
            }

            mReadLineDirectionSaved = ReadDirection.Forward;
            mCurrentLineByteOffsetStartSaved = -1;
            mCurrentLineTextSaved = string.Empty;
            InitializeCurrentLine();
        }

        public void MoveToByteOffset(long byteOffset)
        {
            try
            {
                // ReSharper disable once MergeIntoNegatedPattern
                if (mBinaryReader == null || !mBinaryReader.CanRead)
                    return;

                if (byteOffset < 0L)
                {
                    byteOffset = 0L;
                }
                else if (byteOffset > mBinaryReader.Length)
                {
                    byteOffset = mBinaryReader.Length;
                }

                int bytesRead;

                if (byteOffset < mByteBufferFileOffsetStart)
                {
                    // Need to slide the buffer window backward
                    do
                    {
                        mByteBufferFileOffsetStart -= mByteBuffer.Length;
                    } while (byteOffset < mByteBufferFileOffsetStart);

                    if (mByteBufferFileOffsetStart < 0L)
                    {
                        mByteBufferFileOffsetStart = 0L;
                    }

                    mBinaryReader.Seek(mByteBufferFileOffsetStart, SeekOrigin.Begin);

                    // Clear the buffer
                    Array.Clear(mByteBuffer, 0, mByteBuffer.Length);
                    bytesRead = mBinaryReader.Read(mByteBuffer, 0, mByteBuffer.Length);
                    mByteBufferCount = bytesRead;
                    mByteBufferNextLineStartIndex = (int)(byteOffset - mByteBufferFileOffsetStart);
                }
                else if (byteOffset > mByteBufferFileOffsetStart + mByteBufferCount)
                {
                    if (mByteBufferFileOffsetStart < mBinaryReader.Length)
                    {
                        // Possibly slide the buffer window forward (note that if
                        // mByteBufferCount < mByteBuffer.Length then we may not need to update mByteBufferFileOffsetStart)
                        while (byteOffset > mByteBufferFileOffsetStart + mByteBuffer.Length)
                        {
                            mByteBufferFileOffsetStart += mByteBuffer.Length;
                        }

                        if (mByteBufferFileOffsetStart >= mBinaryReader.Length)
                        {
                            // This shouldn't normally happen
                            mByteBufferFileOffsetStart -= mByteBuffer.Length;

                            if (mByteBufferFileOffsetStart < 0L)
                            {
                                mByteBufferFileOffsetStart = 0L;
                            }
                        }

                        mBinaryReader.Seek(mByteBufferFileOffsetStart, SeekOrigin.Begin);

                        // Clear the buffer
                        Array.Clear(mByteBuffer, 0, mByteBuffer.Length);
                        bytesRead = mBinaryReader.Read(mByteBuffer, 0, mByteBuffer.Length);
                        mByteBufferCount = bytesRead;
                    }

                    mByteBufferNextLineStartIndex = (int)(byteOffset - mByteBufferFileOffsetStart);

                    if (mByteBufferNextLineStartIndex > mByteBufferCount)
                    {
                        // This shouldn't normally happen
                        mByteBufferNextLineStartIndex = mByteBufferCount;
                    }
                }
                else
                {
                    // The desired byte offset is already present in mByteBuffer
                    mByteBufferNextLineStartIndex = (int)(byteOffset - mByteBufferFileOffsetStart);

                    if (mByteBufferNextLineStartIndex > mByteBufferCount)
                    {
                        // This shouldn't normally happen, but is possible if jumping around a file and reading forward and
                        mByteBufferNextLineStartIndex = mByteBufferCount;
                    }
                }
            }
            catch (Exception ex)
            {
                mInputFilePath ??= string.Empty;
                OnErrorEvent(string.Format("Error moving to byte offset {0} in file {1}", byteOffset, mInputFilePath), ex);
            }
        }

        /// <summary>
        /// Move to the beginning of the file and freshly populate the byte buffer
        /// </summary>
        public void MoveToBeginning()
        {
            try
            {
                mByteBufferFileOffsetStart = 0L;

                // Clear the buffer
                Array.Clear(mByteBuffer, 0, mByteBuffer.Length);
                mBinaryReader.Seek(mByteBufferFileOffsetStart, SeekOrigin.Begin);
                mByteBufferCount = mBinaryReader.Read(mByteBuffer, 0, mByteBuffer.Length);
                mByteBufferNextLineStartIndex = 0;

                // Look for a byte order mark at the beginning of the file
                mByteOrderMarkLength = 0;

                if (mByteBufferCount < 2)
                    return;

                if (mByteBuffer[0] == 255 && mByteBuffer[1] == 254)
                {
                    // Unicode (Little Endian)
                    // Note that this sets mCharSize to 2
                    SetInputFileEncoding(InputFileEncodings.UnicodeNormal);

                    // Skip the first 2 bytes
                    mByteBufferNextLineStartIndex = 2;
                    mByteOrderMarkLength = 2;
                }
                else if (mByteBuffer[0] == 254 && mByteBuffer[1] == 255)
                {
                    // Unicode (Big Endian)
                    // Note that this sets mCharSize to 2
                    SetInputFileEncoding(InputFileEncodings.UnicodeBigEndian);
                    // Skip the first 2 bytes
                    mByteBufferNextLineStartIndex = 2;
                    mByteOrderMarkLength = 2;
                }
                else if (mByteBufferCount >= 3)
                {
                    if (mByteBuffer[0] == 239 && mByteBuffer[1] == 187 && mByteBuffer[2] == 191)
                    {
                        // UTF-8
                        // Note that this sets mCharSize to 1
                        SetInputFileEncoding(InputFileEncodings.UTF8);
                        // Skip the first 3 bytes
                        mByteBufferNextLineStartIndex = 3;
                        mByteOrderMarkLength = 3;
                    }
                    else
                    {
                        // Examine the data in the byte buffer to check whether or not
                        // every other byte is 0 for at least 95% of the data
                        // If it is, assume the appropriate Unicode format

                        int indexStart;
                        for (indexStart = 0; indexStart <= 1; indexStart++)
                        {
                            var charCheckCount = 0;
                            var alternatedZeroMatchCount = 0;
                            var indexEnd = mByteBufferCount - 2;

                            for (var index = indexStart; index <= indexEnd; index += 2)
                            {
                                charCheckCount++;

                                if (mByteBuffer[index] != 0 && mByteBuffer[index + 1] == 0)
                                {
                                    alternatedZeroMatchCount++;
                                }
                            }

                            if (charCheckCount <= 0)
                                continue;

                            if (!(alternatedZeroMatchCount / (double)charCheckCount >= 0.95d))
                                continue;

                            // Assume this is a Unicode file
                            if (indexStart == 0)
                            {
                                // Unicode (Little Endian)
                                SetInputFileEncoding(InputFileEncodings.UnicodeNormal);
                            }
                            else
                            {
                                // Unicode (Big Endian)
                                SetInputFileEncoding(InputFileEncodings.UnicodeBigEndian);
                            }

                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                mInputFilePath ??= string.Empty;
                OnErrorEvent("Error moving to beginning of file " + mInputFilePath, ex);
            }
        }

        public void MoveToEnd()
        {
            try
            {
                // ReSharper disable once MergeIntoPattern
                if (mBinaryReader != null && mBinaryReader.CanRead)
                {
                    MoveToByteOffset(mBinaryReader.Length);
                }
            }
            catch (Exception ex)
            {
                mInputFilePath ??= string.Empty;
                OnErrorEvent("Error moving to end of file " + mInputFilePath, ex);
            }
        }

        /// <summary>
        /// Open the data file, granting other programs read-access
        /// </summary>
        /// <param name="dataFilePath"></param>
        /// <returns>True if successful, false if an error</returns>
        // ReSharper disable once UnusedMember.Global
        public bool OpenFile(string dataFilePath)
        {
            return OpenFile(dataFilePath, FileShare.Read);
        }

        /// <summary>
        /// Open the data file, granting other programs the given FileShare access
        /// </summary>
        /// <param name="dataFilePath"></param>
        /// <param name="share"></param>
        /// <returns>True if successful, false if an error</returns>
        public bool OpenFile(string dataFilePath, FileShare share)
        {
            mErrorMessage = string.Empty;

            // Make sure any open file or text stream is closed
            Close();

            try
            {
                if (string.IsNullOrEmpty(dataFilePath))
                {
                    mErrorMessage = "Error opening file: input file path is blank";
                    return false;
                }

                if (!File.Exists(dataFilePath))
                {
                    mErrorMessage = "File not found: " + InputFilePath;
                    return false;
                }

                InitializeLocalVariables();
                mInputFilePath = dataFilePath;

                // Note that this sets mCharSize to 1
                SetInputFileEncoding(InputFileEncodings.ASCII);

                // Initialize the binary reader
                mBinaryReader = new FileStream(mInputFilePath, FileMode.Open, FileAccess.Read, share);

                if (mBinaryReader.Length == 0L)
                {
                    Close();
                    mErrorMessage = "File is zero-length";
                    return false;
                }

                MoveToBeginning();
                return true;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error opening file " + InputFilePath, ex);
                return false;
            }
        }

        /// <summary>
        /// Read the next line in the data file (by looking for the next LF symbol in the binary file)
        /// </summary>
        /// <remarks>
        /// Use Property CurrentLine to obtain the text for the line
        /// </remarks>
        /// <returns>True if successful, False if an error</returns>
        public bool ReadLine()
        {
            return ReadLine(ReadDirection.Forward);
        }

        /// <summary>
        /// Read the next (or previous) line in the data file (by looking for the next LF symbol in the binary file)
        /// </summary>
        /// <remarks>
        /// Use Property CurrentLine to obtain the text for the line
        /// </remarks>
        /// <param name="readDirection"></param>
        /// <returns>True if successful, False if an error</returns>
        public bool ReadLine(ReadDirection readDirection)
        {
            var startIndexShiftIncrement = 0;
            var matchFound = false;

            try
            {
                var terminatorFound = false;
                var startIndexShiftCount = 0;
                InitializeCurrentLine();

                // ReSharper disable once MergeIntoPattern
                if (mBinaryReader != null && mBinaryReader.CanRead)
                {
                    switch (mInputFileEncoding)
                    {
                        case InputFileEncodings.ASCII:
                        case InputFileEncodings.UTF8:
                            // ASCII or UTF-8 encoding; Assure mCharSize = 1
                            mCharSize = 1;
                            break;

                        case InputFileEncodings.UnicodeNormal:
                            // Unicode (Little Endian) encoding; Assure mCharSize = 2
                            mCharSize = 2;
                            break;

                        case InputFileEncodings.UnicodeBigEndian:
                            // Unicode (Big Endian) encoding; Assure mCharSize = 2
                            mCharSize = 2;
                            break;

                        default:
                            // Unknown encoding
                            mCurrentLineText = string.Empty;
                            return false;
                    }

                    int searchIndexStartOffset;

                    if (readDirection == ReadDirection.Forward)
                    {
                        searchIndexStartOffset = 0;

                        if (ByteAtEOF(mByteBufferFileOffsetStart + mByteBufferNextLineStartIndex))
                        {
                            mCurrentLineByteOffsetStart = mBinaryReader.Length;
                            mCurrentLineByteOffsetEnd = mBinaryReader.Length;
                            mCurrentLineByteOffsetEndWithTerminator = mBinaryReader.Length;
                            return false;
                        }
                    }
                    else
                    {
                        searchIndexStartOffset = -mCharSize * 2;

                        if (ByteAtBOF(mByteBufferFileOffsetStart + mByteBufferNextLineStartIndex + searchIndexStartOffset))
                        {
                            return false;
                        }
                    }

                    while (true)
                    {
                        // Note that searchIndexStartOffset will be >=0 if searching forward and <=-2 if searching backward
                        var currentIndex = mByteBufferNextLineStartIndex + searchIndexStartOffset;

                        // Define the minimum and maximum allowable indices for searching for mLineTerminator2Code
                        var indexMinimum = mCharSize - 1;
                        var indexMaximum = mByteBufferCount - mCharSize;

                        if (readDirection == ReadDirection.Reverse && mLineTerminator1Code != 0 && mByteBufferFileOffsetStart > 0L)
                        {
                            // We're looking for a two-character line terminator (though the
                            // presence of mLineTerminator1Code is not required)
                            // Need to increment indexMinimum to guarantee we'll be able to find both line terminators if the
                            // second line terminator happens to be at the start of mByteBuffer
                            indexMinimum += mCharSize;
                        }

                        // Reset the terminator check counters
                        int terminatorCheckCount;
                        int terminatorCheckCountValueZero;

                        if (readDirection == ReadDirection.Reverse && currentIndex >= indexMinimum ||
                            readDirection == ReadDirection.Forward && currentIndex <= indexMaximum)
                        {
                            terminatorFound = ReadLineFindTerminator(
                                readDirection,
                                ref currentIndex,
                                indexMinimum,
                                indexMaximum,
                                out terminatorCheckCountValueZero,
                                out terminatorCheckCount);
                        }
                        else
                        {
                            terminatorCheckCount = 0;
                            terminatorCheckCountValueZero = 0;
                        }

                        bool startIndexShifted;

                        if (terminatorFound)
                        {
                            startIndexShifted = false;
                        }
                        else
                        {
                            terminatorFound = ReadLineFindTerminatorShifted(
                                readDirection,
                                ref currentIndex,
                                ref startIndexShiftCount,
                                ref startIndexShiftIncrement,
                                out startIndexShifted,
                                terminatorCheckCount,
                                terminatorCheckCountValueZero);
                        }

                        if (terminatorFound)
                        {
                            var matchingTextIndexStart = ReadLineContent(
                                readDirection,
                                currentIndex,
                                out var lineTerminatorLength,
                                out var matchingTextIndexEnd,
                                out var validEncoding);

                            if (!validEncoding)
                            {
                                // Exit the while loop
                                break;
                            }

                            matchFound = ReadLineFinalize(readDirection, lineTerminatorLength, matchingTextIndexStart, matchingTextIndexEnd);

                            // Exit the while loop
                            break;
                        }

                        if (startIndexShifted)
                            continue;

                        var dataAdded = ReadLineAppendToBuffer(readDirection, ref searchIndexStartOffset);

                        if (!dataAdded)
                        {
                            // Exit the while loop
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error reading data line", ex);
                matchFound = false;
            }

            if (matchFound)
            {
                mReadLineDirectionSaved = readDirection;
                mCurrentLineByteOffsetStartSaved = CurrentLineByteOffsetStart;
                mCurrentLineTextSaved = mCurrentLineText;
                return true;
            }

            mReadLineDirectionSaved = readDirection;
            mCurrentLineByteOffsetStartSaved = -1;
            mCurrentLineTextSaved = string.Empty;
            return false;
        }

        private bool ReadLineAppendToBuffer(ReadDirection readDirection, ref int searchIndexStartOffset)
        {
            try
            {
                // Need to add more data to the buffer (or shift the data in the buffer)
                int bytesRead;

                if (readDirection == ReadDirection.Forward)
                {
                    if (mBinaryReader.Position >= mBinaryReader.Length)
                    {
                        // Already at the end of the file; cannot move forward
                        return false;
                    }

                    if (mByteBufferNextLineStartIndex > 0)
                    {
                        // First, shift all of the data so that element mByteBufferNextLineStartIndex moves to element 0
                        var indexEnd = mByteBufferCount - 1;

                        for (var index = mByteBufferNextLineStartIndex; index <= indexEnd; index++)
                        {
                            mByteBuffer[index - mByteBufferNextLineStartIndex] = mByteBuffer[index];
                        }

                        mByteBufferCount -= mByteBufferNextLineStartIndex;
                        mByteBufferFileOffsetStart += mByteBufferNextLineStartIndex;
                        searchIndexStartOffset = mByteBufferCount;
                        mByteBufferNextLineStartIndex = 0;

                        if (ByteBufferFileOffsetStart + mByteBufferCount != mBinaryReader.Position)
                        {
                            // The file read-position is out-of-sync with mByteBufferFileOffsetStart; this can happen
                            // if we used MoveToByteOffset, read backward, and are now reading forward
                            mBinaryReader.Seek(ByteBufferFileOffsetStart + mByteBufferCount, SeekOrigin.Begin);
                        }
                    }
                    else
                    {
                        searchIndexStartOffset = mByteBufferCount;

                        if (mByteBufferCount >= mByteBuffer.Length)
                        {
                            // Need to expand the buffer
                            // In order to support Unicode files, it is important that the buffer length always be a power of 2
                            Array.Resize(ref mByteBuffer, mByteBuffer.Length * 2);
                        }
                    }

                    bytesRead = mBinaryReader.Read(mByteBuffer, searchIndexStartOffset, mByteBuffer.Length - searchIndexStartOffset);

                    if (bytesRead == 0)
                    {
                        // No data could be read
                        return false;
                    }

                    mByteBufferCount += bytesRead;
                }
                else
                {
                    if (ByteBufferFileOffsetStart <= ByteOrderMarkLength || mBinaryReader.Position <= 0L)
                    {
                        // Already at the beginning of the file; cannot move backward
                        return false;
                    }

                    if (mByteBufferCount >= mByteBuffer.Length && mByteBufferNextLineStartIndex >= mByteBuffer.Length)
                    {
                        // The byte buffer is full and mByteBufferNextLineStartIndex is past the end of the buffer
                        // Need to double its size, shift the data from the first half to the second half, and
                        // populate the first half

                        // Expand the buffer
                        // In order to support Unicode files, it is important that the buffer length always be a power of 2
                        Array.Resize(ref mByteBuffer, mByteBuffer.Length * 2);
                    }

                    int shiftIncrement;

                    if (mByteBufferCount < mByteBuffer.Length)
                    {
                        shiftIncrement = mByteBuffer.Length - mByteBufferCount;
                    }
                    else
                    {
                        shiftIncrement = mByteBuffer.Length - mByteBufferNextLineStartIndex;
                    }

                    if (ByteBufferFileOffsetStart - shiftIncrement < ByteOrderMarkLength)
                    {
                        shiftIncrement = (int)ByteBufferFileOffsetStart - ByteOrderMarkLength;
                    }

                    // Possibly update mByteBufferCount
                    if (mByteBufferCount < mByteBuffer.Length)
                    {
                        mByteBufferCount += shiftIncrement;
                    }

                    // Shift the data
                    for (var index = mByteBufferCount - shiftIncrement - 1; index >= 0; index--)
                    {
                        mByteBuffer[shiftIncrement + index] = mByteBuffer[index];
                    }

                    // Update the tracking variables
                    mByteBufferFileOffsetStart -= shiftIncrement;
                    mByteBufferNextLineStartIndex += shiftIncrement;

                    // Populate the first portion of the byte buffer with new data
                    mBinaryReader.Seek(ByteBufferFileOffsetStart, SeekOrigin.Begin);
                    bytesRead = mBinaryReader.Read(mByteBuffer, 0, shiftIncrement);

                    if (bytesRead == 0)
                    {
                        // No data could be read; this shouldn't ever happen
                        // Move to the beginning of the file and re-populate mByteBuffer
                        MoveToBeginning();

                        return false;
                    }
                }

                // Successfully added data to the buffer
                return true;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ReadLineAppendToBuffer", ex);
                return false;
            }
        }

        private int ReadLineContent(
            ReadDirection readDirection,
            int currentIndex,
            out int lineTerminatorLength,
            out int matchingTextIndexEnd,
            out bool validEncoding)
        {
            try
            {
                int matchingTextIndexStart;

                if (readDirection == ReadDirection.Forward)
                {
                    matchingTextIndexStart = mByteBufferNextLineStartIndex;
                    matchingTextIndexEnd = currentIndex;
                }
                else
                {
                    matchingTextIndexStart = currentIndex + CharSize;
                    matchingTextIndexEnd = mByteBufferNextLineStartIndex - CharSize;
                }

                // Determine the line terminator length
                int bytesToRead;

                switch (mInputFileEncoding)
                {
                    case InputFileEncodings.ASCII:
                    case InputFileEncodings.UTF8:
                        // ASCII encoding
                        if (mLineTerminator1Code != 0 && matchingTextIndexEnd - CharSize >= 0 &&
                            mByteBuffer[matchingTextIndexEnd - CharSize] == mLineTerminator1Code)
                        {
                            lineTerminatorLength = 2;
                            mCurrentLineTerminator = Convert.ToChar(mByteBuffer[matchingTextIndexEnd - CharSize]).ToString() +
                                                     Convert.ToChar(mByteBuffer[matchingTextIndexEnd]);
                        }
                        else if (mByteBuffer[matchingTextIndexEnd] == mLineTerminator2Code)
                        {
                            lineTerminatorLength = 1;
                            mCurrentLineTerminator = Convert.ToChar(mByteBuffer[matchingTextIndexEnd]).ToString();
                        }
                        else
                        {
                            // No line terminator (this is probably the last line of the file or else the user called MoveToByteOffset with a location in the middle of a line)
                            lineTerminatorLength = 0;
                            mCurrentLineTerminator = string.Empty;
                        }

                        bytesToRead = matchingTextIndexEnd - matchingTextIndexStart - CharSize * (lineTerminatorLength - 1);

                        if (bytesToRead <= 0)
                        {
                            // Blank line
                            mCurrentLineText = string.Empty;
                        }
                        else if (mInputFileEncoding == InputFileEncodings.UTF8)
                        {
                            // Extract the data between matchingTextIndexStart and matchingTextIndexEnd, excluding any line terminator characters
                            mCurrentLineText = new string(Encoding.UTF8.GetChars(mByteBuffer, matchingTextIndexStart, bytesToRead));
                        }
                        else
                        {
                            // Extract the data between matchingTextIndexStart and matchingTextIndexEnd, excluding any line terminator characters
                            mCurrentLineText = new string(Encoding.ASCII.GetChars(mByteBuffer, matchingTextIndexStart, bytesToRead));
                        }

                        validEncoding = true;
                        break;

                    case InputFileEncodings.UnicodeNormal:
                        // Unicode (Little Endian) encoding
                        if (mLineTerminator1Code != 0 && matchingTextIndexEnd - CharSize >= 0 &&
                            mByteBuffer[matchingTextIndexEnd - CharSize] == mLineTerminator1Code && mByteBuffer[matchingTextIndexEnd - CharSize + 1] == 0)
                        {
                            lineTerminatorLength = 2;
                            mCurrentLineTerminator = Convert.ToChar(mByteBuffer[matchingTextIndexEnd - CharSize]).ToString() +
                                                     Convert.ToChar(mByteBuffer[matchingTextIndexEnd]);
                        }
                        else if (mByteBuffer[matchingTextIndexEnd] == mLineTerminator2Code && matchingTextIndexEnd + 1 < mByteBufferCount &&
                                 mByteBuffer[matchingTextIndexEnd + 1] == 0)
                        {
                            lineTerminatorLength = 1;
                            mCurrentLineTerminator = Convert.ToChar(mByteBuffer[matchingTextIndexEnd]).ToString();
                        }
                        else
                        {
                            // No line terminator (this is probably the last line of the file or else the user called MoveToByteOffset with a location in the middle of a line)
                            lineTerminatorLength = 0;
                            mCurrentLineTerminator = string.Empty;
                        }

                        // Extract the data between matchingTextIndexStart and matchingTextIndexEnd, excluding any line terminator characters
                        bytesToRead = matchingTextIndexEnd - matchingTextIndexStart - CharSize * (lineTerminatorLength - 1);

                        if (bytesToRead <= 0)
                        {
                            // Blank line
                            mCurrentLineText = string.Empty;
                        }
                        else
                        {
                            mCurrentLineText = new string(Encoding.Unicode.GetChars(mByteBuffer, matchingTextIndexStart, bytesToRead));
                        }

                        validEncoding = true;
                        break;

                    case InputFileEncodings.UnicodeBigEndian:
                        // Unicode (Big Endian) encoding
                        if (mLineTerminator1Code != 0 && matchingTextIndexEnd - CharSize >= 0 && mByteBuffer[matchingTextIndexEnd - CharSize] == 0 &&
                            mByteBuffer[matchingTextIndexEnd - CharSize + 1] == mLineTerminator1Code)
                        {
                            lineTerminatorLength = 2;
                            mCurrentLineTerminator = Convert.ToChar(mByteBuffer[matchingTextIndexEnd - CharSize + 1]).ToString() +
                                                     Convert.ToChar(mByteBuffer[matchingTextIndexEnd + 1]);
                        }
                        else if (mByteBuffer[matchingTextIndexEnd] == 0 && matchingTextIndexEnd + 1 < mByteBufferCount &&
                                 mByteBuffer[matchingTextIndexEnd + 1] == mLineTerminator2Code)
                        {
                            lineTerminatorLength = 1;
                            mCurrentLineTerminator = Convert.ToChar(mByteBuffer[matchingTextIndexEnd + 1]).ToString();
                        }
                        else
                        {
                            // No line terminator (this is probably the last line of the file or else the user called MoveToByteOffset with a location in the middle of a line)
                            lineTerminatorLength = 0;
                            mCurrentLineTerminator = string.Empty;
                        }

                        // Extract the data between matchingTextIndexStart and matchingTextIndexEnd, excluding any line terminator characters
                        bytesToRead = matchingTextIndexEnd - matchingTextIndexStart - CharSize * (lineTerminatorLength - 1);

                        if (bytesToRead <= 0)
                        {
                            // Blank line
                            mCurrentLineText = string.Empty;
                        }
                        else
                        {
                            mCurrentLineText = new string(Encoding.BigEndianUnicode.GetChars(mByteBuffer, matchingTextIndexStart, bytesToRead));
                        }

                        validEncoding = true;
                        break;

                    default:
                        // Unknown/unsupported encoding
                        mCurrentLineText = string.Empty;
                        lineTerminatorLength = 0;
                        validEncoding = false;
                        break;
                }

                return matchingTextIndexStart;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ReadLineContent: " + ex.Message);
                throw;
            }
        }

        private bool ReadLineFinalize(ReadDirection readDirection, int lineTerminatorLength, int matchingTextIndexStart, int matchingTextIndexEnd)
        {
            try
            {
                bool matchFound;
                if (CharSize > 1 && !ByteAtEOF(ByteBufferFileOffsetStart + matchingTextIndexEnd))
                {
                    matchingTextIndexEnd += CharSize - 1;
                }

                mCurrentLineByteOffsetStart = ByteBufferFileOffsetStart + matchingTextIndexStart;
                mCurrentLineByteOffsetEndWithTerminator = ByteBufferFileOffsetStart + matchingTextIndexEnd;
                mCurrentLineByteOffsetEnd = ByteBufferFileOffsetStart + matchingTextIndexEnd - lineTerminatorLength * CharSize;

                if (CurrentLineByteOffsetEnd < CurrentLineByteOffsetStart)
                {
                    // Zero-length line
                    mCurrentLineByteOffsetEnd = CurrentLineByteOffsetStart;
                }

                if (readDirection == ReadDirection.Forward)
                {
                    mByteBufferNextLineStartIndex = matchingTextIndexEnd + 1;
                    mLineNumber++;
                }
                else
                {
                    mByteBufferNextLineStartIndex = matchingTextIndexStart;

                    if (LineNumber > 0)
                    {
                        mLineNumber--;
                    }
                }

                // Check whether the user just changed reading direction
                // If they did, it is possible that this method will return the exact same line
                // as was previously read.  Check for this, and if true, read the next line (in direction readDirection)
                if (readDirection != mReadLineDirectionSaved &&
                    mCurrentLineByteOffsetStartSaved >= 0L &&
                    CurrentLineByteOffsetStart == mCurrentLineByteOffsetStartSaved &&
                    mCurrentLineTextSaved != null &&
                    mCurrentLineText.Equals(mCurrentLineTextSaved))
                {
                    // Recursively call this method to read the next line
                    // To avoid infinite loops, set mCurrentLineByteOffsetStartSaved to -1
                    mCurrentLineByteOffsetStartSaved = -1;
                    return ReadLine(readDirection);
                }

                return true;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ReadLineFinalize: " + ex.Message);
                throw;
            }
        }

        private bool ReadLineFindTerminator(
            ReadDirection readDirection,
            ref int currentIndex,
            int indexMinimum,
            int indexMaximum,
            out int terminatorCheckCountValueZero,
            out int terminatorCheckCount)
        {
            try
            {
                terminatorCheckCountValueZero = 0;
                terminatorCheckCount = 0;

                while (true)
                {
                    switch (mInputFileEncoding)
                    {
                        case InputFileEncodings.ASCII:
                        case InputFileEncodings.UTF8:
                            // ASCII or UTF-8 encoding; Assure mCharSize = 1
                            if (mByteBuffer[currentIndex] == mLineTerminator2Code)
                            {
                                return true;
                            }

                            break;

                        case InputFileEncodings.UnicodeNormal:
                            // Look for the LF symbol followed by a byte with value 0 in mByteBuffer
                            if (mByteBuffer[currentIndex] == mLineTerminator2Code && mByteBuffer[currentIndex + 1] == 0)
                            {
                                return true;
                            }
                            else if (mByteBuffer[currentIndex] == 0)
                            {
                                terminatorCheckCountValueZero++;
                            }

                            terminatorCheckCount++;
                            break;

                        case InputFileEncodings.UnicodeBigEndian:
                            // Unicode (Big Endian) encoding; Assure mCharSize = 2
                            if (mByteBuffer[currentIndex] == 0 && mByteBuffer[currentIndex + 1] == mLineTerminator2Code)
                            {
                                return true;
                            }
                            else if (mByteBuffer[currentIndex + 1] == 0)
                            {
                                terminatorCheckCountValueZero++;
                            }

                            terminatorCheckCount++;
                            break;
                    }

                    if (readDirection == ReadDirection.Forward)
                    {
                        if (currentIndex + CharSize <= indexMaximum)
                        {
                            currentIndex += CharSize;
                        }
                        else
                        {
                            // Exit the TerminatorFound do loop
                            break;
                        }
                    }
                    else if (currentIndex - CharSize >= indexMinimum)
                    {
                        currentIndex -= CharSize;
                    }
                    else
                    {
                        // Exit the TerminatorFound do loop
                        break;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ReadLineFindTerminator: " + ex.Message);
                throw;
            }
        }

        private bool ReadLineFindTerminatorShifted(
            ReadDirection readDirection,
            ref int currentIndex,
            ref int startIndexShiftCount,
            ref int startIndexShiftIncrement,
            out bool startIndexShifted,
            int terminatorCheckCount,
            int terminatorCheckCountValueZero)
        {
            try
            {
                double valueZeroFraction;

                if (terminatorCheckCount > 0)
                {
                    valueZeroFraction = terminatorCheckCountValueZero / (double)terminatorCheckCount;
                }
                else
                {
                    valueZeroFraction = 0d;
                }

                startIndexShifted = false;

                if (CharSize > 1 && startIndexShiftCount < CharSize - 1 && valueZeroFraction >= 0.95d)
                {
                    // mByteBufferNextLineStartIndex is most likely off by 1
                    // This could happen due to an inappropriate byte value being sent to MoveToByteOffset()
                    // or due to a corrupted Unicode file

                    // Shift mByteBufferNextLineStartIndex by 1 and try again
                    if (startIndexShiftCount == 0)
                    {
                        // First attempt to shift; determine the shift direction
                        if (readDirection == ReadDirection.Forward)
                        {
                            // Searching forward
                            if (mByteBufferNextLineStartIndex > CharSize - 2)
                            {
                                startIndexShiftIncrement = -1;
                            }
                            else
                            {
                                startIndexShiftIncrement = 1;
                            }
                        }

                        // Searching reverse
                        else if (mByteBufferNextLineStartIndex < mByteBufferCount - (CharSize - 2))
                        {
                            startIndexShiftIncrement = 1;
                        }
                        else
                        {
                            startIndexShiftIncrement = -1;
                        }
                    }

                    mByteBufferNextLineStartIndex += startIndexShiftIncrement;
                    startIndexShiftCount++;
                    startIndexShifted = true;
                }
                else if (readDirection == ReadDirection.Forward)
                {
                    // Searching forward; are we at the end of the file?
                    if (ByteAtEOF(ByteBufferFileOffsetStart + currentIndex + CharSize))
                    {
                        // Yes, we're at the end of the file
                        currentIndex = mByteBufferCount - 1;
                        return true;
                    }
                }
                else if (ByteAtBOF(ByteBufferFileOffsetStart + currentIndex))
                {
                    // Searching backward; are we at the beginning of the file?
                    // Yes, we're at the beginning of the file
                    currentIndex = ByteOrderMarkLength - CharSize;
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ReadLineFindTerminatorShifted: " + ex.Message);
                throw;
            }
        }

        private void SetInputFileEncoding(InputFileEncodings EncodingMode)
        {
            mInputFileEncoding = EncodingMode;

            mCharSize = mInputFileEncoding switch
            {
                InputFileEncodings.ASCII => 1,
                InputFileEncodings.UTF8 => 1,
                InputFileEncodings.UnicodeNormal => 2,
                InputFileEncodings.UnicodeBigEndian => 2,
                _ => 1
            };
        }
    }
}