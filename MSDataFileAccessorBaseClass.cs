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
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

// ReSharper disable UnusedMember.Global

namespace MSDataFileReader
{
    /// <summary>
    ///
    /// This class can be used to open an MS Data file (currently .mzXML and .mzData) and
    /// index the location of all of the spectra present.  This does not cache the mass spectra
    /// data in memory, and therefore uses little memory, but once the indexing is complete,
    /// random access to the spectra is possible.  After the indexing is complete, spectra
    /// can be obtained using GetSpectrumByScanNumber or GetSpectrumByIndex
    /// </summary>
    public abstract class MsDataFileAccessorBaseClass : MsDataFileReaderBaseClass
    {
        // Ignore Spelling: Accessor

        protected MsDataFileAccessorBaseClass()
        {
            // ReSharper disable once VirtualMemberCallInConstructor
            InitializeLocalVariables();
        }

        ~MsDataFileAccessorBaseClass()
        {
            try
            {
                CloseFile();
            }
            catch (Exception)
            {
                // Ignore errors here
            }
        }

        protected enum ElementMatchMode
        {
            StartElement = 0,
            EndElement = 1
        }

        protected BinaryTextReader.InputFileEncodings mInputFileEncoding;

        protected byte mCharSize;

        protected bool mIndexingComplete;

        protected FileStream mBinaryReader;

        protected BinaryTextReader mBinaryTextReader;

        protected string mInFileCurrentLineText;

        protected int mInFileCurrentCharIndex;

        // This dictionary maps scan number to index in mIndexedSpectrumInfo()
        // If more than one spectrum comes from the same scan, tracks the first one read
        protected readonly Dictionary<int, int> mIndexedSpectraScanToIndex = new();

        protected int mLastSpectrumIndexRead;

        // These variables are used when mDataReaderMode = Indexed
        protected int mIndexedSpectrumInfoCount;

        protected IndexedSpectrumInfoType[] mIndexedSpectrumInfo;

        public override int CachedSpectrumCount
        {
            get
            {
                if (mDataReaderMode == DataReaderMode.Cached)
                {
                    return base.CachedSpectrumCount;
                }

                return mIndexedSpectrumInfoCount;
            }
        }

        // ReSharper disable once UnusedMemberInSuper.Global
        protected abstract bool AdvanceFileReaders(ElementMatchMode elementMatchMode);

        public override void CloseFile()
        {
            if (mBinaryReader != null)
            {
                mBinaryReader.Close();
                mBinaryReader = null;
            }

            if (mBinaryTextReader != null)
            {
                mBinaryTextReader.Close();
                mBinaryTextReader = null;
            }

            mInputFilePath = string.Empty;
            mReadingAndStoringSpectra = false;
        }

        /// <summary>
        /// Extracts the text between startByteOffset and endByteOffset in filePath and returns it
        /// </summary>
        /// <param name="startByteOffset"></param>
        /// <param name="endByteOffset"></param>
        /// <returns>Extracted text</returns>
        protected string ExtractTextBetweenOffsets(long startByteOffset, long endByteOffset)
        {
            try
            {
                if (mBinaryReader is { CanRead: true })
                {
                    mBinaryReader.Seek(startByteOffset, SeekOrigin.Begin);
                    var bytesToRead = (int)(endByteOffset - startByteOffset + 1L);

                    if (bytesToRead > 0)
                    {
                        var data = new byte[bytesToRead];
                        bytesToRead = mBinaryReader.Read(data, 0, bytesToRead);

                        return mInputFileEncoding switch
                        {
                            BinaryTextReader.InputFileEncodings.ASCII => new string(Encoding.ASCII.GetChars(data, 0, bytesToRead)),
                            BinaryTextReader.InputFileEncodings.UTF8 => new string(Encoding.UTF8.GetChars(data, 0, bytesToRead)),
                            BinaryTextReader.InputFileEncodings.UnicodeNormal => new string(Encoding.Unicode.GetChars(data, 0, bytesToRead)),
                            BinaryTextReader.InputFileEncodings.UnicodeBigEndian => new string(Encoding.BigEndianUnicode.GetChars(data, 0, bytesToRead)),
                            _ => string.Empty
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ExtractXMLText", ex);
            }

            // If we get here, no match was found, so return an empty string
            return string.Empty;
        }

        protected override string GetInputFileLocation()
        {
            try
            {
                if (mBinaryTextReader is null)
                {
                    return string.Empty;
                }

                return "Line " + mBinaryTextReader.LineNumber + ", Byte Offset " + mBinaryTextReader.CurrentLineByteOffsetStart;
            }
            catch (Exception)
            {
                // Ignore errors here
                return string.Empty;
            }
        }

        /// <summary>
        /// Obtain the list of scan numbers (aka acquisition numbers)
        /// </summary>
        /// <param name="ScanNumberList"></param>
        /// <returns>True if successful, false if an error or no cached spectra</returns>
        public override bool GetScanNumberList(out int[] ScanNumberList)
        {
            try
            {
                if (mDataReaderMode == DataReaderMode.Cached)
                {
                    return base.GetScanNumberList(out ScanNumberList);
                }

                if (!GetSpectrumReadyStatus(true))
                {
                    ScanNumberList = Array.Empty<int>();
                    return false;
                }

                if (mIndexedSpectrumInfo is null || mIndexedSpectrumInfoCount == 0)
                {
                    ScanNumberList = Array.Empty<int>();
                    return false;
                }

                ScanNumberList = new int[mIndexedSpectrumInfoCount];
                var indexEnd = ScanNumberList.Length - 1;

                for (var spectrumIndex = 0; spectrumIndex <= indexEnd; spectrumIndex++)
                {
                    ScanNumberList[spectrumIndex] = mIndexedSpectrumInfo[spectrumIndex].ScanNumber;
                }

                return true;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in GetScanNumberList", ex);
                ScanNumberList = Array.Empty<int>();
                return false;
            }
        }

        /// <summary>
        /// Obtain the source XML for the given spectrum index
        /// </summary>
        /// <remarks>
        /// <para>
        /// This does not include the header or footer XML for the file
        /// </para>
        /// <para>
        /// Only valid if we have Indexed data in memory
        /// </para>
        /// </remarks>
        /// <param name="spectrumIndex"></param>
        /// <param name="sourceXML"></param>
        /// <returns>True if successful, false if an error or invalid spectrum</returns>
        public bool GetSourceXMLByIndex(int spectrumIndex, out string sourceXML)
        {
            sourceXML = string.Empty;

            try
            {
                mErrorMessage = string.Empty;

                if (mDataReaderMode != DataReaderMode.Indexed)
                {
                    mErrorMessage = "Indexed data not in memory";
                    return false;
                }

                if (!GetSpectrumReadyStatus(true))
                {
                    return false;
                }

                if (mIndexedSpectrumInfoCount == 0)
                {
                    mErrorMessage = "Indexed data not in memory";
                    return false;
                }

                if (spectrumIndex < 0 || spectrumIndex >= mIndexedSpectrumInfoCount)
                {
                    mErrorMessage = "Invalid spectrum index: " + spectrumIndex;
                    return false;
                }

                // Move the binary file reader to .ByteOffsetStart and populate sourceXML with the text for the given spectrum
                sourceXML = ExtractTextBetweenOffsets(mIndexedSpectrumInfo[spectrumIndex].ByteOffsetStart, mIndexedSpectrumInfo[spectrumIndex].ByteOffsetEnd);

                return !string.IsNullOrWhiteSpace(sourceXML);
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in GetSourceXMLByIndex", ex);
                return false;
            }
        }

        /// <summary>
        /// Obtain the source XML for the given scan number
        /// </summary>
        /// <remarks>
        /// <para>
        /// This does not include the header or footer XML for the file
        /// </para>
        /// <para>
        /// Only valid if we have Indexed data in memory
        /// </para>
        /// </remarks>
        /// <param name="scanNumber"></param>
        /// <param name="sourceXML"></param>
        /// <returns>True if successful, false if an error or invalid spectrum</returns>
        public bool GetSourceXMLByScanNumber(int scanNumber, out string sourceXML)
        {
            sourceXML = string.Empty;

            try
            {
                mErrorMessage = string.Empty;
                var success = false;

                if (mDataReaderMode != DataReaderMode.Indexed)
                {
                    mErrorMessage = "Indexed data not in memory";
                    return false;
                }

                if (!GetSpectrumReadyStatus(true))
                {
                    return false;
                }

                if (mIndexedSpectraScanToIndex.Count == 0)
                {
                    var indexEnd = mIndexedSpectrumInfoCount - 1;

                    for (var spectrumIndex = 0; spectrumIndex <= indexEnd; spectrumIndex++)
                    {
                        if (mIndexedSpectrumInfo[spectrumIndex].ScanNumber == scanNumber)
                        {
                            success = GetSourceXMLByIndex(spectrumIndex, out sourceXML);
                            break;
                        }
                    }
                }
                else
                {
                    // Look for scanNumber in mIndexedSpectraScanToIndex
                    var index = mIndexedSpectraScanToIndex[scanNumber];
                    success = GetSourceXMLByIndex(index, out sourceXML);
                }

                if (!success && mErrorMessage.Length == 0)
                {
                    mErrorMessage = "Invalid scan number: " + scanNumber;
                }

                return success;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in GetSourceXMLByScanNumber", ex);
                return false;
            }
        }

        /// <summary>
        /// Get the spectrum for the given index
        /// </summary>
        /// <remarks>
        /// Only valid if we have Cached or Indexed data in memory
        /// </remarks>
        /// <param name="spectrumIndex"></param>
        /// <param name="spectrumInfo"></param>
        /// <returns>True if success, False if failure</returns>
        public override bool GetSpectrumByIndex(int spectrumIndex, out SpectrumInfo spectrumInfo)
        {
            try
            {
                if (mDataReaderMode == DataReaderMode.Cached)
                {
                    return base.GetSpectrumByIndex(spectrumIndex, out spectrumInfo);
                }

                if (mDataReaderMode == DataReaderMode.Indexed)
                {
                    return GetSpectrumByIndexWork(spectrumIndex, out spectrumInfo, false);
                }

                mErrorMessage = "Cached or indexed data not in memory";
                spectrumInfo = null;
                return false;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in GetSpectrumByIndex", ex);
                spectrumInfo = null;
                return false;
            }
        }

        protected abstract bool GetSpectrumByIndexWork(int spectrumIndex, out SpectrumInfo spectrumInfo, bool headerInfoOnly);

        public override bool GetSpectrumByScanNumber(int scanNumber, out SpectrumInfo spectrumInfo)
        {
            return GetSpectrumByScanNumberWork(scanNumber, out spectrumInfo, false);
        }

        /// <summary>
        /// Obtain the spectrum data for the given scan
        /// </summary>
        /// <remarks>
        /// Only valid if we have Cached or Indexed data in memory
        /// </remarks>
        /// <param name="scanNumber"></param>
        /// <param name="spectrumInfo"></param>
        /// <param name="headerInfoOnly"></param>
        /// <returns>True if success, False if failure</returns>
        protected bool GetSpectrumByScanNumberWork(int scanNumber, out SpectrumInfo spectrumInfo, bool headerInfoOnly)
        {
            spectrumInfo = null;

            try
            {
                mErrorMessage = string.Empty;

                if (mDataReaderMode == DataReaderMode.Cached)
                {
                    return base.GetSpectrumByScanNumber(scanNumber, out spectrumInfo);
                }

                if (mDataReaderMode != DataReaderMode.Indexed)
                {
                    mErrorMessage = "Cached or indexed data not in memory";
                    return false;
                }

                if (!GetSpectrumReadyStatus(true))
                {
                    return false;
                }

                var success = false;

                if (mIndexedSpectraScanToIndex.Count == 0)
                {
                    var indexEnd = mIndexedSpectrumInfoCount - 1;

                    for (var spectrumIndex = 0; spectrumIndex <= indexEnd; spectrumIndex++)
                    {
                        if (mIndexedSpectrumInfo[spectrumIndex].ScanNumber == scanNumber)
                        {
                            success = GetSpectrumByIndex(spectrumIndex, out spectrumInfo);
                            break;
                        }
                    }
                }
                else
                {
                    // Look for scanNumber in mIndexedSpectraScanToIndex
                    var index = mIndexedSpectraScanToIndex[scanNumber];
                    success = GetSpectrumByIndexWork(index, out spectrumInfo, headerInfoOnly);
                }

                if (!success && string.IsNullOrWhiteSpace(mErrorMessage))
                {
                    mErrorMessage = "Invalid scan number: " + scanNumber;
                }

                return success;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in GetSpectrumByScanNumberWork", ex);
                return false;
            }
        }

        public bool GetSpectrumHeaderInfoByIndex(int spectrumIndex, out SpectrumInfo spectrumInfo)
        {
            return GetSpectrumByIndexWork(spectrumIndex, out spectrumInfo, true);
        }

        public bool GetSpectrumHeaderInfoByScanNumber(int scanNumber, out SpectrumInfo spectrumInfo)
        {
            return GetSpectrumByScanNumberWork(scanNumber, out spectrumInfo, true);
        }

        /// <summary>
        /// Check whether the reader is open and spectra can be obtained
        /// </summary>
        /// <remarks>
        /// If allowConcurrentReading = True, returns True if mBinaryReader is ready for reading
        /// If allowConcurrentReading = False, returns True only after the file is fully indexed
        /// Otherwise, returns false
        /// </remarks>
        /// <param name="allowConcurrentReading"></param>
        protected bool GetSpectrumReadyStatus(bool allowConcurrentReading)
        {
            if (mBinaryReader is null || !mBinaryReader.CanRead)
            {
                mErrorMessage = "Data file not currently open";
                return false;
            }

            if (allowConcurrentReading)
            {
                return true;
            }

            return !mReadingAndStoringSpectra;
        }

        protected void InitializeFileTrackingVariables()
        {
            InitializeLocalVariables();

            // Reset the tracking variables for the text file
            mInFileCurrentLineText = string.Empty;
            mInFileCurrentCharIndex = -1;

            // Reset the indexed spectrum info
            mIndexedSpectrumInfoCount = 0;
            mIndexedSpectrumInfo = new IndexedSpectrumInfoType[1000];
            mIndexedSpectraScanToIndex.Clear();
        }

        protected override void InitializeLocalVariables()
        {
            base.InitializeLocalVariables();
            mErrorMessage = string.Empty;
            mLastSpectrumIndexRead = 0;
            mDataReaderMode = DataReaderMode.Indexed;
            mInputFileEncoding = BinaryTextReader.InputFileEncodings.ASCII;
            mCharSize = 1;
            mIndexingComplete = false;
        }

        protected Regex InitializeRegEx(string pattern)
        {
            return new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// This method should be defined to look for an existing byte offset index and, if found,
        /// populate mIndexedSpectrumInfo() and set mIndexingComplete = True
        /// </summary>
        protected abstract bool LoadExistingIndex();

        /// <summary>
        /// Open the given file
        /// </summary>
        /// <param name="inputFilePath"></param>
        /// <returns>True if the file is successfully opened</returns>
        public override bool OpenFile(string inputFilePath)
        {
            try
            {
                var initSuccess = OpenFileInit(inputFilePath);

                if (!initSuccess)
                    return false;

                InitializeFileTrackingVariables();
                mDataReaderMode = DataReaderMode.Indexed;
                mInputFilePath = string.Copy(inputFilePath);

                // Initialize the binary text reader
                // Even if an existing index is present, this is needed to determine
                // the input file encoding and the character size
                mBinaryTextReader = new BinaryTextReader();

                if (!mBinaryTextReader.OpenFile(mInputFilePath, FileShare.ReadWrite))
                {
                    return false;
                }

                mInputFileEncoding = mBinaryTextReader.InputFileEncoding;
                mCharSize = mBinaryTextReader.CharSize;

                // Initialize the binary reader (which is used to extract individual spectra from the XML file)
                mBinaryReader = new FileStream(mInputFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                // Look for a byte offset index, present either inside the .XML file (e.g. .mzXML)
                // or in a separate file (future capability)
                // If an index is found, set mIndexingComplete to True
                if (LoadExistingIndex())
                {
                    mIndexingComplete = true;
                }

                if (mBinaryTextReader.ByteBufferFileOffsetStart > 0L ||
                    mBinaryTextReader.CurrentLineByteOffsetStart > mBinaryTextReader.ByteOrderMarkLength)
                {
                    mBinaryTextReader.MoveToBeginning();
                }

                mErrorMessage = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                mErrorMessage = "Error opening file: " + inputFilePath + "; " + ex.Message;
                OnErrorEvent(mErrorMessage, ex);
                return false;
            }
        }

        /// <summary>
        /// This reading mode is not appropriate for the MS Data File Accessor
        /// </summary>
        /// <param name="textStream"></param>
        /// <returns>Always returns false</returns>
        public override bool OpenTextStream(string textStream)
        {
            mErrorMessage = "The OpenTextStream method is not valid for clsMSDataFileAccessorBaseClass";
            CloseFile();
            return false;
        }

        /// <summary>
        /// Cache the entire file rather than indexing it and accessing it with a binary reader
        /// </summary>
        /// <returns>True if successful, false if an error</returns>
        public bool ReadAndCacheEntireFileNonIndexed()
        {
            var success = base.ReadAndCacheEntireFile();

            if (success)
            {
                mDataReaderMode = DataReaderMode.Cached;
            }

            return success;
        }

        public override bool ReadNextSpectrum(out SpectrumInfo spectrumInfo)
        {
            if (GetSpectrumReadyStatus(false) && mLastSpectrumIndexRead < mIndexedSpectrumInfoCount)
            {
                mLastSpectrumIndexRead++;
                return GetSpectrumByIndex(mLastSpectrumIndexRead, out spectrumInfo);
            }

            spectrumInfo = null;
            return false;
        }

        protected void StoreIndexEntry(int scanNumber, long byteOffsetStart, long byteOffsetEnd)
        {
            if (mIndexedSpectrumInfoCount >= mIndexedSpectrumInfo.Length)
            {
                // Double the amount of space reserved for mIndexedSpectrumInfo
                Array.Resize(ref mIndexedSpectrumInfo, mIndexedSpectrumInfo.Length * 2);
            }

            mIndexedSpectrumInfo[mIndexedSpectrumInfoCount].ScanNumber = scanNumber;
            mIndexedSpectrumInfo[mIndexedSpectrumInfoCount].ByteOffsetStart = byteOffsetStart;
            mIndexedSpectrumInfo[mIndexedSpectrumInfoCount].ByteOffsetEnd = byteOffsetEnd;

            UpdateFileStats(mIndexedSpectrumInfoCount + 1, mIndexedSpectrumInfo[mIndexedSpectrumInfoCount].ScanNumber);

            if (!mIndexedSpectraScanToIndex.ContainsKey(mIndexedSpectrumInfo[mIndexedSpectrumInfoCount].ScanNumber))
            {
                mIndexedSpectraScanToIndex.Add(mIndexedSpectrumInfo[mIndexedSpectrumInfoCount].ScanNumber, mIndexedSpectrumInfoCount);
            }

            // Increment mIndexedSpectrumInfoCount
            mIndexedSpectrumInfoCount++;
        }
    }
}