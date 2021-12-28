﻿using System;
using System.IO;
using System.Text;
using PRISM;

namespace MSDataFileReader
{

    // This class can be used to open a Text file and read each of the lines from the file,
    // where a line of text ends with CRLF or simply LF
    // In addition, the byte offset at the start and end of the line is also returned
    // 
    // Note that this class is compatible with UTF-16 Unicode files; it looks for byte order mark
    // FF FE or FE FF in the first two bytes of the file to determine if a file is Unicode
    // (though you can override this using the InputFileEncoding property after calling .OpenFile()
    // This class will also look for the byte order mark for UTF-8 files (EF BB BF) though it may not
    // properly decode UTF-8 characters (not fully tested)
    // 
    // You can change the expected line terminator character using Property FileSystemMode
    // If FileSystemMode = FileSystemModeConstants.Windows, then the Line Terminator = LF, optionally preceded by CR
    // If FileSystemMode = FileSystemModeConstants.Unix, then the Line Terminator = LF, optionally preceded by CR
    // If FileSystemMode = FileSystemModeConstants.Macintosh, then the Line Terminator = CR, previous character is not considered
    // 
    // -------------------------------------------------------------------------------
    // Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    // Copyright 2006, Battelle Memorial Institute.  All Rights Reserved.
    // Program started April 18, 2006
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
    // 

    public class clsBinaryTextReader : EventNotifier
    {
        public clsBinaryTextReader()
        {
            // Note: This property will also update mLineTerminator1Code and mLineTerminator2Code
            FileSystemMode = FileSystemModeConstants.Windows;
            InitializeLocalVariables();
        }

        ~clsBinaryTextReader()
        {
            Close();
        }

        #region Constants and Enums
        // In order to support Unicode files, it is important that the buffer length always be a power of 2
        private const int INITIAL_BUFFER_LENGTH = 10000;
        private const byte LINE_TERMINATOR_CODE_LF = 10;
        private const byte LINE_TERMINATOR_CODE_CR = 13;

        public enum FileSystemModeConstants
        {
            Windows = 0,
            Unix = 1,
            Macintosh = 2
        }

        public enum InputFileEncodingConstants
        {
            Ascii = 0,                   // No Byte Order Mark
            UTF8 = 1,                    // Byte Order Mark: EF BB BF (UTF-8)
            UnicodeNormal = 2,           // Byte Order Mark: FF FE (Little Endian Unicode)
            UnicodeBigEndian = 3        // Byte Order Mark: FE FF (Big Endian Unicode)
        }

        public enum ReadDirectionConstants
        {
            Forward = 0,
            Reverse = 1
        }

        #endregion

        #region Structures

        #endregion

        #region Classwide Variables

        private string mInputFilePath;
        private InputFileEncodingConstants mInputFileEncoding = InputFileEncodingConstants.Ascii;
        private byte mCharSize = 1;
        private byte mByteOrderMarkLength;

        // Note: Use Me.FileSystemMode to set this variable so that mLineTerminator1Code and mLineTerminator2Code also get updated
        private FileSystemModeConstants mFileSystemMode;
        private byte mLineTerminator1Code;
        private byte mLineTerminator2Code;
        private string mErrorMessage;
        private FileStream mBinaryReader;
        private int mLineNumber;
        private int mByteBufferCount;
        private byte[] mByteBuffer;

        // Note: The first byte in the file is Byte 0
        private long mByteBufferFileOffsetStart;

        // This variable defines the index in mByteBuffer() at which the next line starts
        private int mByteBufferNextLineStartIndex;
        private string mCurrentLineText;
        private long mCurrentLineByteOffsetStart;
        private long mCurrentLineByteOffsetEnd;
        private long mCurrentLineByteOffsetEndWithTerminator;
        private ReadDirectionConstants mReadLineDirectionSaved;
        private long mCurrentLineByteOffsetStartSaved;
        private string mCurrentLineTextSaved;
        private string mCurrentLineTerminator;

        #endregion

        #region Processing Options and Interface Functions

        public long ByteBufferFileOffsetStart
        {
            get
            {
                return mByteBufferFileOffsetStart;
            }
        }

        public byte ByteOrderMarkLength
        {
            get
            {
                return mByteOrderMarkLength;
            }
        }

        public byte CharSize
        {
            get
            {
                return mCharSize;
            }
        }

        public string CurrentLine
        {
            get
            {
                if (mCurrentLineText is null)
                {
                    return string.Empty;
                }
                else
                {
                    return mCurrentLineText;
                }
            }
        }

        public int CurrentLineLength
        {
            get
            {
                if (mCurrentLineText is null)
                {
                    return 0;
                }
                else
                {
                    return mCurrentLineText.Length;
                }
            }
        }

        public long CurrentLineByteOffsetStart
        {
            get
            {
                return mCurrentLineByteOffsetStart;
            }
        }

        public long CurrentLineByteOffsetEnd
        {
            get
            {
                return mCurrentLineByteOffsetEnd;
            }
        }

        public long CurrentLineByteOffsetEndWithTerminator
        {
            get
            {
                return mCurrentLineByteOffsetEndWithTerminator;
            }
        }

        public string CurrentLineTerminator
        {
            get
            {
                if (mCurrentLineTerminator is null)
                {
                    return string.Empty;
                }
                else
                {
                    return mCurrentLineTerminator;
                }
            }
        }

        public string ErrorMessage
        {
            get
            {
                return mErrorMessage;
            }
        }

        public long FileLengthBytes
        {
            get
            {
                try
                {
                    if (mBinaryReader is null)
                    {
                        return 0L;
                    }
                    else
                    {
                        return mBinaryReader.Length;
                    }
                }
                catch (Exception ex)
                {
                    return 0L;
                }
            }
        }

        public FileSystemModeConstants FileSystemMode
        {
            get
            {
                return mFileSystemMode;
            }

            set
            {
                mFileSystemMode = value;
                switch (mFileSystemMode)
                {
                    case FileSystemModeConstants.Windows:
                    case FileSystemModeConstants.Unix:
                        {
                            // Normally present for Windows; normally not present for Unix
                            mLineTerminator1Code = LINE_TERMINATOR_CODE_CR;
                            mLineTerminator2Code = LINE_TERMINATOR_CODE_LF;
                            break;
                        }

                    case FileSystemModeConstants.Macintosh:
                        {
                            mLineTerminator1Code = 0;
                            mLineTerminator2Code = LINE_TERMINATOR_CODE_CR;
                            break;
                        }
                }
            }
        }

        public string InputFilePath
        {
            get
            {
                return mInputFilePath;
            }
        }

        public int LineNumber
        {
            get
            {
                return mLineNumber;
            }
        }

        public InputFileEncodingConstants InputFileEncoding
        {
            get
            {
                return mInputFileEncoding;
            }

            set
            {
                SetInputFileEncoding(value);
            }
        }

        #endregion

        public bool ByteAtBOF(long lngBytePosition)
        {
            if (lngBytePosition <= mByteOrderMarkLength)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool ByteAtEOF(long lngBytePosition)
        {
            // Returns True if lngBytePosition is >= the end of the file
            if (lngBytePosition >= mBinaryReader.Length)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void Close()
        {
            try
            {
                if (mBinaryReader != null)
                {
                    mBinaryReader.Close();
                }
            }
            catch (Exception ex)
            {
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

        private void InitializeLocalVariables()
        {
            // Note: Do Not update mFileSystemMode, mLineTerminator1Code, mLineTerminator2Code, or mInputFileEncoding in this sub

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

            mReadLineDirectionSaved = ReadDirectionConstants.Forward;
            mCurrentLineByteOffsetStartSaved = -1;
            mCurrentLineTextSaved = string.Empty;
            InitializeCurrentLine();
        }

        public void MoveToByteOffset(long lngByteOffset)
        {
            int intBytesRead;
            try
            {
                if (mBinaryReader != null && mBinaryReader.CanRead)
                {
                    if (lngByteOffset < 0L)
                    {
                        lngByteOffset = 0L;
                    }
                    else if (lngByteOffset > mBinaryReader.Length)
                    {
                        lngByteOffset = mBinaryReader.Length;
                    }

                    if (lngByteOffset < mByteBufferFileOffsetStart)
                    {
                        // Need to slide the buffer window backward
                        do
                            mByteBufferFileOffsetStart -= mByteBuffer.Length;
                        while (lngByteOffset < mByteBufferFileOffsetStart);
                        if (mByteBufferFileOffsetStart < 0L)
                        {
                            mByteBufferFileOffsetStart = 0L;
                        }

                        mBinaryReader.Seek(mByteBufferFileOffsetStart, SeekOrigin.Begin);

                        // Clear the buffer
                        Array.Clear(mByteBuffer, 0, mByteBuffer.Length);
                        intBytesRead = mBinaryReader.Read(mByteBuffer, 0, mByteBuffer.Length);
                        mByteBufferCount = intBytesRead;
                        mByteBufferNextLineStartIndex = (int)(lngByteOffset - mByteBufferFileOffsetStart);
                    }
                    else if (lngByteOffset > mByteBufferFileOffsetStart + mByteBufferCount)
                    {
                        if (mByteBufferFileOffsetStart < mBinaryReader.Length)
                        {
                            // Possibly slide the buffer window forward (note that if
                            // mByteBufferCount < mByteBuffer.Length then we may not need to update mByteBufferFileOffsetStart)
                            while (lngByteOffset > mByteBufferFileOffsetStart + mByteBuffer.Length)
                                mByteBufferFileOffsetStart += mByteBuffer.Length;
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
                            intBytesRead = mBinaryReader.Read(mByteBuffer, 0, mByteBuffer.Length);
                            mByteBufferCount = intBytesRead;
                        }

                        mByteBufferNextLineStartIndex = (int)(lngByteOffset - mByteBufferFileOffsetStart);
                        if (mByteBufferNextLineStartIndex > mByteBufferCount)
                        {
                            // This shouldn't normally happen
                            mByteBufferNextLineStartIndex = mByteBufferCount;
                        }
                    }
                    else
                    {
                        // The desired byte offset is already present in mByteBuffer
                        mByteBufferNextLineStartIndex = (int)(lngByteOffset - mByteBufferFileOffsetStart);
                        if (mByteBufferNextLineStartIndex > mByteBufferCount)
                        {
                            // This shouldn't normally happen, but is possible if jumping around a file and reading forward and
                            mByteBufferNextLineStartIndex = mByteBufferCount;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (mInputFilePath is null)
                    mInputFilePath = string.Empty;
                OnErrorEvent(string.Format("Error moving to byte offset {0} in file {1}", lngByteOffset, mInputFilePath), ex);
            }
        }

        public void MoveToBeginning()
        {
            // Move to the beginning of the file and freshly populate the byte buffer

            int intIndex;
            int intIndexStart;
            int intIndexEnd;
            int intCharCheckCount;
            int intAlternatedZeroMatchCount;
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
                if (mByteBufferCount >= 2)
                {
                    if (mByteBuffer[0] == 255 & mByteBuffer[1] == 254)
                    {
                        // Unicode (Little Endian)
                        // Note that this sets mCharSize to 2
                        SetInputFileEncoding(InputFileEncodingConstants.UnicodeNormal);

                        // Skip the first 2 bytes
                        mByteBufferNextLineStartIndex = 2;
                        mByteOrderMarkLength = 2;
                    }
                    else if (mByteBuffer[0] == 254 & mByteBuffer[1] == 255)
                    {
                        // Unicode (Big Endian)
                        // Note that this sets mCharSize to 2
                        SetInputFileEncoding(InputFileEncodingConstants.UnicodeBigEndian);
                        // Skip the first 2 bytes
                        mByteBufferNextLineStartIndex = 2;
                        mByteOrderMarkLength = 2;
                    }
                    else if (mByteBufferCount >= 3)
                    {
                        if (mByteBuffer[0] == 239 & mByteBuffer[1] == 187 & mByteBuffer[2] == 191)
                        {
                            // UTF8
                            // Note that this sets mCharSize to 1
                            SetInputFileEncoding(InputFileEncodingConstants.UTF8);
                            // Skip the first 3 bytes
                            mByteBufferNextLineStartIndex = 3;
                            mByteOrderMarkLength = 3;
                        }
                        else
                        {
                            // Examine the first 2000 bytes and check whether or not
                            // every other byte is 0 for at least 95% of the data
                            // If it is, then assume the appropriate Unicode format

                            intIndexEnd = 2000;
                            if (intIndexEnd >= mByteBufferCount - 1)
                            {
                                intIndexEnd = mByteBufferCount - 2;
                            }

                            for (intIndexStart = 0; intIndexStart <= 1; intIndexStart++)
                            {
                                intCharCheckCount = 0;
                                intAlternatedZeroMatchCount = 0;
                                var loopTo = mByteBufferCount - 2;
                                for (intIndex = intIndexStart; intIndex <= loopTo; intIndex += 2)
                                {
                                    intCharCheckCount += 1;
                                    if (mByteBuffer[intIndex] != 0 & mByteBuffer[intIndex + 1] == 0)
                                    {
                                        intAlternatedZeroMatchCount += 1;
                                    }
                                }

                                if (intCharCheckCount > 0)
                                {
                                    if (intAlternatedZeroMatchCount / (double)intCharCheckCount >= 0.95d)
                                    {
                                        // Assume this is a Unicode file
                                        if (intIndexStart == 0)
                                        {
                                            // Unicode (Little Endian)
                                            SetInputFileEncoding(InputFileEncodingConstants.UnicodeNormal);
                                        }
                                        else
                                        {
                                            // Unicode (Big Endian)
                                            SetInputFileEncoding(InputFileEncodingConstants.UnicodeBigEndian);
                                        }

                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (mInputFilePath is null)
                    mInputFilePath = string.Empty;
                OnErrorEvent("Error moving to beginning of file " + mInputFilePath, ex);
            }
        }

        public void MoveToEnd()
        {
            try
            {
                if (mBinaryReader != null && mBinaryReader.CanRead)
                {
                    MoveToByteOffset(mBinaryReader.Length);
                }
            }
            catch (Exception ex)
            {
                if (mInputFilePath is null)
                    mInputFilePath = string.Empty;
                OnErrorEvent("Error moving to end of file " + mInputFilePath, ex);
            }
        }

        public bool OpenFile(string dataFilePath)
        {
            return OpenFile(dataFilePath, FileShare.Read);
        }

        public bool OpenFile(string dataFilePath, FileShare share)
        {
            // Returns true if the file is successfully opened

            bool blnSuccess;
            mErrorMessage = string.Empty;

            // Make sure any open file or text stream is closed
            Close();
            try
            {
                blnSuccess = false;
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
                mInputFilePath = string.Copy(dataFilePath);

                // Note that this sets mCharSize to 1
                SetInputFileEncoding(InputFileEncodingConstants.Ascii);

                // Initialize the binary reader
                mBinaryReader = new FileStream(mInputFilePath, FileMode.Open, FileAccess.Read, share);
                if (mBinaryReader.Length == 0L)
                {
                    Close();
                    mErrorMessage = "File is zero-length";
                    blnSuccess = false;
                }
                else
                {
                    MoveToBeginning();
                    blnSuccess = true;
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error opening file " + InputFilePath, ex);
                blnSuccess = false;
            }

            return blnSuccess;
        }

        public bool ReadLine()
        {
            return ReadLine(ReadDirectionConstants.Forward);
        }

        public bool ReadLine(ReadDirectionConstants eDirection)
        {
            // Looks for the next line in the file (by looking for the next LF symbol in the binary file)
            // Returns True if success, False if failure
            // Use Property CurrentLine to obtain the text for the line

            int intSearchIndexStartOffset;
            int intBytesRead;
            int intBytesToRead;
            int intIndex;
            int intIndexMinimum;
            int intIndexMaximum;
            int intShiftIncrement;
            int intMatchingTextIndexStart;
            int intMatchingTextIndexEnd;
            int intLineTerminatorLength;
            int intTerminatorCheckCount;
            int intTerminatorCheckCountValueZero;
            double dblValueZeroFraction;
            int intStartIndexShiftCount;
            var intStartIndexShiftIncrement = default(int);
            bool blnStartIndexShifted;
            bool blnMatchFound;
            bool blnTerminatorFound;
            try
            {
                blnMatchFound = false;
                blnTerminatorFound = false;
                intStartIndexShiftCount = 0;
                InitializeCurrentLine();
                if (mBinaryReader != null && mBinaryReader.CanRead)
                {
                    switch (mInputFileEncoding)
                    {
                        case InputFileEncodingConstants.Ascii:
                        case InputFileEncodingConstants.UTF8:
                            {
                                // Ascii or UTF-8 encoding; Assure mCharSize = 1
                                mCharSize = 1;
                                break;
                            }

                        case InputFileEncodingConstants.UnicodeNormal:
                            {
                                // Unicode (Little Endian) encoding; Assure mCharSize = 2
                                mCharSize = 2;
                                break;
                            }

                        case InputFileEncodingConstants.UnicodeBigEndian:
                            {
                                // Unicode (Big Endian) encoding; Assure mCharSize = 2
                                mCharSize = 2;
                                break;
                            }

                        default:
                            {
                                // Unknown encoding
                                mCurrentLineText = string.Empty;
                                return false;
                            }
                    }

                    if (eDirection == ReadDirectionConstants.Forward)
                    {
                        intSearchIndexStartOffset = 0;
                        if (ByteAtEOF(mByteBufferFileOffsetStart + mByteBufferNextLineStartIndex + intSearchIndexStartOffset))
                        {
                            mCurrentLineByteOffsetStart = mBinaryReader.Length;
                            mCurrentLineByteOffsetEnd = mBinaryReader.Length;
                            mCurrentLineByteOffsetEndWithTerminator = mBinaryReader.Length;
                            return false;
                        }
                    }
                    else
                    {
                        intSearchIndexStartOffset = -mCharSize * 2;
                        if (ByteAtBOF(mByteBufferFileOffsetStart + mByteBufferNextLineStartIndex + intSearchIndexStartOffset))
                        {
                            return false;
                        }
                    }

                    while (!blnMatchFound)
                    {
                        // Note that intSearchIndexStartOffset will be >=0 if searching forward and <=-2 if searching backward
                        intIndex = mByteBufferNextLineStartIndex + intSearchIndexStartOffset;

                        // Define the minimum and maximum allowable indices for searching for mLineTerminator2Code
                        intIndexMinimum = mCharSize - 1;                     // This is only used when searching backward
                        intIndexMaximum = mByteBufferCount - mCharSize;      // This is only used when searching forward
                        if (eDirection == ReadDirectionConstants.Reverse && mLineTerminator1Code != 0 && mByteBufferFileOffsetStart > 0L)
                        {
                            // We're looking for a two-character line terminator (though the
                            // presence of mLineTerminator1Code is not required)
                            // Need to increment intIndexMinimum to guarantee we'll be able to find both line terminators if the
                            // second line terminator happens to be at the start of mByteBuffer
                            intIndexMinimum += mCharSize;
                        }

                        // Reset the terminator check counters
                        intTerminatorCheckCount = 0;
                        intTerminatorCheckCountValueZero = 0;
                        blnStartIndexShifted = false;
                        if (eDirection == ReadDirectionConstants.Reverse && intIndex >= intIndexMinimum || eDirection == ReadDirectionConstants.Forward && intIndex <= intIndexMaximum)
                        {
                            do
                            {
                                switch (mInputFileEncoding)
                                {
                                    case InputFileEncodingConstants.Ascii:
                                    case InputFileEncodingConstants.UTF8:
                                        {
                                            // Ascii or UTF-8 encoding; Assure mCharSize = 1
                                            if (mByteBuffer[intIndex] == mLineTerminator2Code)
                                            {
                                                blnTerminatorFound = true;
                                                break;
                                            }

                                            break;
                                        }

                                    case InputFileEncodingConstants.UnicodeNormal:
                                        {
                                            // Look for the LF symbol followed by a byte with value 0 in mByteBuffer
                                            if (mByteBuffer[intIndex] == mLineTerminator2Code && mByteBuffer[intIndex + 1] == 0)
                                            {
                                                blnTerminatorFound = true;
                                                break;
                                            }
                                            else if (mByteBuffer[intIndex] == 0)
                                            {
                                                intTerminatorCheckCountValueZero += 1;
                                            }

                                            intTerminatorCheckCount += 1;
                                            break;
                                        }

                                    case InputFileEncodingConstants.UnicodeBigEndian:
                                        {
                                            // Unicode (Big Endian) encoding; Assure mCharSize = 2
                                            if (mByteBuffer[intIndex] == 0 && mByteBuffer[intIndex + 1] == mLineTerminator2Code)
                                            {
                                                blnTerminatorFound = true;
                                                break;
                                            }
                                            else if (mByteBuffer[intIndex + 1] == 0)
                                            {
                                                intTerminatorCheckCountValueZero += 1;
                                            }

                                            intTerminatorCheckCount += 1;
                                            break;
                                        }
                                }

                                if (eDirection == ReadDirectionConstants.Forward)
                                {
                                    if (intIndex + mCharSize <= intIndexMaximum)
                                    {
                                        intIndex += mCharSize;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                                else if (intIndex - mCharSize >= intIndexMinimum)
                                {
                                    intIndex -= mCharSize;
                                }
                                else
                                {
                                    break;
                                }
                            }
                            while (!blnTerminatorFound);
                        }

                        if (!blnTerminatorFound)
                        {
                            if (intTerminatorCheckCount > 0)
                            {
                                dblValueZeroFraction = intTerminatorCheckCountValueZero / (double)intTerminatorCheckCount;
                            }
                            else
                            {
                                dblValueZeroFraction = 0d;
                            }

                            if (mCharSize > 1 && intStartIndexShiftCount < mCharSize - 1 && dblValueZeroFraction >= 0.95d)
                            {

                                // mByteBufferNextLineStartIndex is most likely off by 1
                                // This could happen due to an inappropriate byte value being sent to MoveToByteOffset()
                                // or due to a corrupted Unicode file

                                // Shift mByteBufferNextLineStartIndex by 1 and try again
                                if (intStartIndexShiftCount == 0)
                                {
                                    // First attempt to shift; determine the shift direction
                                    if (eDirection == ReadDirectionConstants.Forward)
                                    {
                                        // Searching forward
                                        if (mByteBufferNextLineStartIndex > mCharSize - 2)
                                        {
                                            intStartIndexShiftIncrement = -1;
                                        }
                                        else
                                        {
                                            intStartIndexShiftIncrement = 1;
                                        }
                                    }
                                    // Searching reverse
                                    else if (mByteBufferNextLineStartIndex < mByteBufferCount - (mCharSize - 2))
                                    {
                                        intStartIndexShiftIncrement = 1;
                                    }
                                    else
                                    {
                                        intStartIndexShiftIncrement = -1;
                                    }
                                }

                                mByteBufferNextLineStartIndex += intStartIndexShiftIncrement;
                                intStartIndexShiftCount += 1;
                                blnStartIndexShifted = true;
                            }
                            else if (eDirection == ReadDirectionConstants.Forward)
                            {
                                // Searching forward; are we at the end of the file?
                                if (ByteAtEOF(mByteBufferFileOffsetStart + intIndex + mCharSize))
                                {
                                    // Yes, we're at the end of the file
                                    blnTerminatorFound = true;
                                    intIndex = mByteBufferCount - 1;
                                }
                            }
                            // Searching backward; are we at the beginning of the file?
                            else if (ByteAtBOF(mByteBufferFileOffsetStart + intIndex))
                            {
                                // Yes, we're at the beginning of the file
                                blnTerminatorFound = true;
                                intIndex = mByteOrderMarkLength - mCharSize;
                            }
                        }

                        if (blnTerminatorFound)
                        {
                            if (eDirection == ReadDirectionConstants.Forward)
                            {
                                intMatchingTextIndexStart = mByteBufferNextLineStartIndex;
                                intMatchingTextIndexEnd = intIndex;
                            }
                            else
                            {
                                intMatchingTextIndexStart = intIndex + mCharSize;
                                intMatchingTextIndexEnd = mByteBufferNextLineStartIndex - mCharSize;
                            }

                            // Determine the line terminator length
                            switch (mInputFileEncoding)
                            {
                                case InputFileEncodingConstants.Ascii:
                                case InputFileEncodingConstants.UTF8:
                                    {
                                        // Ascii encoding
                                        if (mLineTerminator1Code != 0 && intMatchingTextIndexEnd - mCharSize >= 0 && mByteBuffer[intMatchingTextIndexEnd - mCharSize] == mLineTerminator1Code)
                                        {
                                            intLineTerminatorLength = 2;
                                            mCurrentLineTerminator = Convert.ToChar(mByteBuffer[intMatchingTextIndexEnd - mCharSize]).ToString() + Convert.ToChar(mByteBuffer[intMatchingTextIndexEnd]);
                                        }
                                        else if (mByteBuffer[intMatchingTextIndexEnd] == mLineTerminator2Code)
                                        {
                                            intLineTerminatorLength = 1;
                                            mCurrentLineTerminator = Convert.ToChar(mByteBuffer[intMatchingTextIndexEnd]).ToString();
                                        }
                                        else
                                        {
                                            // No line terminator (this is probably the last line of the file or else the user called MoveToByteOffset with a location in the middle of a line)
                                            intLineTerminatorLength = 0;
                                            mCurrentLineTerminator = string.Empty;
                                        }

                                        intBytesToRead = intMatchingTextIndexEnd - intMatchingTextIndexStart - mCharSize * (intLineTerminatorLength - 1);
                                        if (intBytesToRead <= 0)
                                        {
                                            // Blank line
                                            mCurrentLineText = string.Empty;
                                        }
                                        else if (mInputFileEncoding == InputFileEncodingConstants.UTF8)
                                        {
                                            // Extract the data between intMatchingTextIndexStart and intMatchingTextIndexEnd, excluding any line terminator characters
                                            mCurrentLineText = new string(Encoding.UTF8.GetChars(mByteBuffer, intMatchingTextIndexStart, intBytesToRead));
                                        }
                                        else
                                        {
                                            // Extract the data between intMatchingTextIndexStart and intMatchingTextIndexEnd, excluding any line terminator characters
                                            mCurrentLineText = new string(Encoding.ASCII.GetChars(mByteBuffer, intMatchingTextIndexStart, intBytesToRead));
                                        }

                                        break;
                                    }

                                case InputFileEncodingConstants.UnicodeNormal:
                                    {
                                        // Unicode (Little Endian) encoding
                                        if (mLineTerminator1Code != 0 && intMatchingTextIndexEnd - mCharSize >= 0 && mByteBuffer[intMatchingTextIndexEnd - mCharSize] == mLineTerminator1Code && mByteBuffer[intMatchingTextIndexEnd - mCharSize + 1] == 0)
                                        {
                                            intLineTerminatorLength = 2;
                                            mCurrentLineTerminator = Convert.ToChar(mByteBuffer[intMatchingTextIndexEnd - mCharSize]).ToString() + Convert.ToChar(mByteBuffer[intMatchingTextIndexEnd]);
                                        }
                                        else if (mByteBuffer[intMatchingTextIndexEnd] == mLineTerminator2Code && intMatchingTextIndexEnd + 1 < mByteBufferCount && mByteBuffer[intMatchingTextIndexEnd + 1] == 0)
                                        {
                                            intLineTerminatorLength = 1;
                                            mCurrentLineTerminator = Convert.ToChar(mByteBuffer[intMatchingTextIndexEnd]).ToString();
                                        }
                                        else
                                        {
                                            // No line terminator (this is probably the last line of the file or else the user called MoveToByteOffset with a location in the middle of a line)
                                            intLineTerminatorLength = 0;
                                            mCurrentLineTerminator = string.Empty;
                                        }

                                        // Extract the data between intMatchingTextIndexStart and intMatchingTextIndexEnd, excluding any line terminator characters
                                        intBytesToRead = intMatchingTextIndexEnd - intMatchingTextIndexStart - mCharSize * (intLineTerminatorLength - 1);
                                        if (intBytesToRead <= 0)
                                        {
                                            // Blank line
                                            mCurrentLineText = string.Empty;
                                        }
                                        else
                                        {
                                            mCurrentLineText = new string(Encoding.Unicode.GetChars(mByteBuffer, intMatchingTextIndexStart, intBytesToRead));
                                        }

                                        break;
                                    }

                                case InputFileEncodingConstants.UnicodeBigEndian:
                                    {
                                        // Unicode (Big Endian) encoding
                                        if (mLineTerminator1Code != 0 && intMatchingTextIndexEnd - mCharSize >= 0 && mByteBuffer[intMatchingTextIndexEnd - mCharSize] == 0 && mByteBuffer[intMatchingTextIndexEnd - mCharSize + 1] == mLineTerminator1Code)
                                        {
                                            intLineTerminatorLength = 2;
                                            mCurrentLineTerminator = Convert.ToChar(mByteBuffer[intMatchingTextIndexEnd - mCharSize + 1]).ToString() + Convert.ToChar(mByteBuffer[intMatchingTextIndexEnd + 1]);
                                        }
                                        else if (mByteBuffer[intMatchingTextIndexEnd] == 0 && intMatchingTextIndexEnd + 1 < mByteBufferCount && mByteBuffer[intMatchingTextIndexEnd + 1] == mLineTerminator2Code)
                                        {
                                            intLineTerminatorLength = 1;
                                            mCurrentLineTerminator = Convert.ToChar(mByteBuffer[intMatchingTextIndexEnd + 1]).ToString();
                                        }
                                        else
                                        {
                                            // No line terminator (this is probably the last line of the file or else the user called MoveToByteOffset with a location in the middle of a line)
                                            intLineTerminatorLength = 0;
                                            mCurrentLineTerminator = string.Empty;
                                        }

                                        // Extract the data between intMatchingTextIndexStart and intMatchingTextIndexEnd, excluding any line terminator characters
                                        intBytesToRead = intMatchingTextIndexEnd - intMatchingTextIndexStart - mCharSize * (intLineTerminatorLength - 1);
                                        if (intBytesToRead <= 0)
                                        {
                                            // Blank line
                                            mCurrentLineText = string.Empty;
                                        }
                                        else
                                        {
                                            mCurrentLineText = new string(Encoding.BigEndianUnicode.GetChars(mByteBuffer, intMatchingTextIndexStart, intBytesToRead));
                                        }

                                        break;
                                    }

                                default:
                                    {
                                        // Unknown/unsupported encoding
                                        mCurrentLineText = string.Empty;
                                        blnMatchFound = false;
                                        break;
                                    }
                            }

                            if (mCharSize > 1 && !ByteAtEOF(mByteBufferFileOffsetStart + intMatchingTextIndexEnd))
                            {
                                intMatchingTextIndexEnd += mCharSize - 1;
                            }

                            mCurrentLineByteOffsetStart = mByteBufferFileOffsetStart + intMatchingTextIndexStart;
                            mCurrentLineByteOffsetEndWithTerminator = mByteBufferFileOffsetStart + intMatchingTextIndexEnd;
                            mCurrentLineByteOffsetEnd = mByteBufferFileOffsetStart + intMatchingTextIndexEnd - intLineTerminatorLength * mCharSize;
                            if (mCurrentLineByteOffsetEnd < mCurrentLineByteOffsetStart)
                            {
                                // Zero-length line
                                mCurrentLineByteOffsetEnd = mCurrentLineByteOffsetStart;
                            }

                            if (eDirection == ReadDirectionConstants.Forward)
                            {
                                mByteBufferNextLineStartIndex = intMatchingTextIndexEnd + 1;
                                mLineNumber += 1;
                            }
                            else
                            {
                                mByteBufferNextLineStartIndex = intMatchingTextIndexStart;
                                if (mLineNumber > 0)
                                {
                                    mLineNumber -= 1;
                                }
                            }

                            // Check whether the user just changed reading direction
                            // If they did, then it is possible that this function will return the exact same line
                            // as was previously read.  Check for this, and if true, then read the next line (in direction eDiretion)
                            if (eDirection != mReadLineDirectionSaved &&
                                mCurrentLineByteOffsetStartSaved >= 0L &&
                                mCurrentLineByteOffsetStart == mCurrentLineByteOffsetStartSaved &&
                                mCurrentLineTextSaved != null &&
                                (mCurrentLineText ?? "") == (mCurrentLineTextSaved ?? ""))
                            {

                                // Recursively call this function to read the next line
                                // To avoid infinite loops, set mCurrentLineByteOffsetStartSaved to -1
                                mCurrentLineByteOffsetStartSaved = -1;
                                blnMatchFound = ReadLine(eDirection);
                            }
                            else
                            {
                                blnMatchFound = true;
                            }

                            break;
                        }

                        if (!blnMatchFound && !blnStartIndexShifted)
                        {
                            // Need to add more data to the buffer (or shift the data in the buffer)
                            if (eDirection == ReadDirectionConstants.Forward)
                            {
                                if (mBinaryReader.Position >= mBinaryReader.Length)
                                {
                                    // Already at the end of the file; cannot move forward
                                    break;
                                }

                                if (mByteBufferNextLineStartIndex > 0)
                                {
                                    // First, shift all of the data so that element mByteBufferNextLineStartIndex moves to element 0
                                    var loopTo = mByteBufferCount - 1;
                                    for (intIndex = mByteBufferNextLineStartIndex; intIndex <= loopTo; intIndex++)
                                        mByteBuffer[intIndex - mByteBufferNextLineStartIndex] = mByteBuffer[intIndex];
                                    mByteBufferCount -= mByteBufferNextLineStartIndex;
                                    mByteBufferFileOffsetStart += mByteBufferNextLineStartIndex;
                                    intSearchIndexStartOffset = mByteBufferCount;
                                    mByteBufferNextLineStartIndex = 0;
                                    if (mByteBufferFileOffsetStart + mByteBufferCount != mBinaryReader.Position)
                                    {
                                        // The file read-position is out-of-sync with mByteBufferFileOffsetStart; this can happen
                                        // if we used MoveToByteOffset, read backward, and are now reading forward
                                        mBinaryReader.Seek(mByteBufferFileOffsetStart + mByteBufferCount, SeekOrigin.Begin);
                                    }
                                }
                                else
                                {
                                    intSearchIndexStartOffset = mByteBufferCount;
                                    if (mByteBufferCount >= mByteBuffer.Length)
                                    {
                                        // Need to expand the buffer
                                        // In order to support Unicode files, it is important that the buffer length always be a power of 2
                                        Array.Resize(ref mByteBuffer, mByteBuffer.Length * 2);
                                    }
                                }

                                intBytesRead = mBinaryReader.Read(mByteBuffer, intSearchIndexStartOffset, mByteBuffer.Length - intSearchIndexStartOffset);
                                if (intBytesRead == 0)
                                {
                                    // No data could be read; exit the loop
                                    break;
                                }
                                else
                                {
                                    mByteBufferCount += intBytesRead;
                                }
                            }
                            else
                            {
                                if (mByteBufferFileOffsetStart <= mByteOrderMarkLength || mBinaryReader.Position <= 0L)
                                {
                                    // Already at the beginning of the file; cannot move backward
                                    break;
                                }

                                if (mByteBufferCount >= mByteBuffer.Length & mByteBufferNextLineStartIndex >= mByteBuffer.Length)
                                {
                                    // The byte buffer is full and mByteBufferNextLineStartIndex is past the end of the buffer
                                    // Need to double its size, shift the data from the first half to the second half, and
                                    // populate the first half

                                    // Expand the buffer
                                    // In order to support Unicode files, it is important that the buffer length always be a power of 2
                                    Array.Resize(ref mByteBuffer, mByteBuffer.Length * 2);
                                }

                                if (mByteBufferCount < mByteBuffer.Length)
                                {
                                    intShiftIncrement = mByteBuffer.Length - mByteBufferCount;
                                }
                                else
                                {
                                    intShiftIncrement = mByteBuffer.Length - mByteBufferNextLineStartIndex;
                                }

                                if (mByteBufferFileOffsetStart - intShiftIncrement < mByteOrderMarkLength)
                                {
                                    intShiftIncrement = (int)mByteBufferFileOffsetStart - mByteOrderMarkLength;
                                }

                                // Possibly update mByteBufferCount
                                if (mByteBufferCount < mByteBuffer.Length)
                                {
                                    mByteBufferCount += intShiftIncrement;
                                }

                                // Shift the data
                                for (intIndex = mByteBufferCount - intShiftIncrement - 1; intIndex >= 0; intIndex -= 1)
                                    mByteBuffer[intShiftIncrement + intIndex] = mByteBuffer[intIndex];

                                // Update the tracking variables
                                mByteBufferFileOffsetStart -= intShiftIncrement;
                                mByteBufferNextLineStartIndex += intShiftIncrement;

                                // Populate the first portion of the byte buffer with new data
                                mBinaryReader.Seek(mByteBufferFileOffsetStart, SeekOrigin.Begin);
                                intBytesRead = mBinaryReader.Read(mByteBuffer, 0, intShiftIncrement);
                                if (intBytesRead == 0)
                                {
                                    // No data could be read; this shouldn't ever happen
                                    // Move to the beginning of the file and re-populate mByteBuffer
                                    MoveToBeginning();
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error reading data line", ex);
                blnMatchFound = false;
            }

            if (blnMatchFound)
            {
                mReadLineDirectionSaved = eDirection;
                mCurrentLineByteOffsetStartSaved = mCurrentLineByteOffsetStart;
                mCurrentLineTextSaved = string.Copy(mCurrentLineText);
            }
            else
            {
                mReadLineDirectionSaved = eDirection;
                mCurrentLineByteOffsetStartSaved = -1;
                mCurrentLineTextSaved = string.Empty;
            }

            return blnMatchFound;
        }

        private void SetInputFileEncoding(InputFileEncodingConstants EncodingMode)
        {
            mInputFileEncoding = EncodingMode;
            switch (mInputFileEncoding)
            {
                case InputFileEncodingConstants.Ascii:
                case InputFileEncodingConstants.UTF8:
                    {
                        mCharSize = 1;
                        break;
                    }

                case InputFileEncodingConstants.UnicodeNormal:
                    {
                        mCharSize = 2;
                        break;
                    }

                case InputFileEncodingConstants.UnicodeBigEndian:
                    {
                        mCharSize = 2;
                        break;
                    }

                default:
                    {
                        // Unknown mode; assume mCharSize = 1
                        mCharSize = 1;
                        break;
                    }
            }
        }
    }
}