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
    public abstract class clsMSDataFileAccessorBaseClass : clsMSDataFileReaderBaseClass
    {
        // Ignore Spelling: Accessor

        public clsMSDataFileAccessorBaseClass()
        {
            InitializeLocalVariables();
        }

        ~clsMSDataFileAccessorBaseClass()
        {
            try
            {
                CloseFile();
            }
            catch (Exception ex)
            {
                // Ignore errors here
            }
        }

        protected const int INITIAL_SCAN_RESERVE_COUNT = 1000;

        protected enum emmElementMatchModeConstants
        {
            StartElement = 0,
            EndElement = 1
        }

        protected struct udtIndexedSpectrumInfoType
        {
            public int ScanNumber;

            /// <summary>
            /// Spectrum ID
            /// </summary>
            /// <remarks>
            /// Only used by mzData files
            /// </remarks>
            public int SpectrumID;

            public long ByteOffsetStart;

            public long ByteOffsetEnd;

            public override string ToString()
            {
                return "Scan " + ScanNumber + ", bytes " + ByteOffsetStart + " to " + ByteOffsetEnd;
            }
        }

        protected clsBinaryTextReader.InputFileEncodingConstants mInputFileEncoding;

        protected byte mCharSize;

        protected bool mIndexingComplete;

        protected FileStream mBinaryReader;

        protected clsBinaryTextReader mBinaryTextReader;

        protected string mInFileCurrentLineText;

        protected int mInFileCurrentCharIndex;

        // This dictionary maps scan number to index in mIndexedSpectrumInfo()
        // If more than one spectrum comes from the same scan, tracks the first one read
        protected readonly Dictionary<int, int> mIndexedSpectraScanToIndex = new();

        protected int mLastSpectrumIndexRead;

        // These variables are used when mDataReaderMode = Indexed
        protected int mIndexedSpectrumInfoCount;

        protected udtIndexedSpectrumInfoType[] mIndexedSpectrumInfo;

        public override int CachedSpectrumCount
        {
            get
            {
                if (mDataReaderMode == drmDataReaderModeConstants.Cached)
                {
                    return base.CachedSpectrumCount;
                }

                return mIndexedSpectrumInfoCount;
            }
        }

        protected abstract bool AdvanceFileReaders(emmElementMatchModeConstants eElementMatchMode);

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
        /// Extracts the text between lngStartByteOffset and lngEndByteOffset in strFilePath and returns it
        /// </summary>
        /// <param name="strFilePath"></param>
        /// <param name="lngStartByteOffset"></param>
        /// <param name="lngEndByteOffset"></param>
        /// <returns>Extracted text</returns>
        protected string ExtractTextBetweenOffsets(string strFilePath, long lngStartByteOffset, long lngEndByteOffset)
        {
            try
            {
                if (mBinaryReader != null && mBinaryReader.CanRead)
                {
                    mBinaryReader.Seek(lngStartByteOffset, SeekOrigin.Begin);
                    var intBytesToRead = (int)(lngEndByteOffset - lngStartByteOffset + 1L);

                    if (intBytesToRead > 0)
                    {
                        var bytData = new byte[intBytesToRead];
                        intBytesToRead = mBinaryReader.Read(bytData, 0, intBytesToRead);

                        switch (mInputFileEncoding)
                        {
                            case clsBinaryTextReader.InputFileEncodingConstants.ASCII:
                                return new string(Encoding.ASCII.GetChars(bytData, 0, intBytesToRead));

                            case clsBinaryTextReader.InputFileEncodingConstants.UTF8:
                                return new string(Encoding.UTF8.GetChars(bytData, 0, intBytesToRead));

                            case clsBinaryTextReader.InputFileEncodingConstants.UnicodeNormal:
                                return new string(Encoding.Unicode.GetChars(bytData, 0, intBytesToRead));

                            case clsBinaryTextReader.InputFileEncodingConstants.UnicodeBigEndian:
                                return new string(Encoding.BigEndianUnicode.GetChars(bytData, 0, intBytesToRead));

                            default:
                                // Unknown encoding
                                return string.Empty;
                        }
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
            catch (Exception ex)
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
                if (mDataReaderMode == drmDataReaderModeConstants.Cached)
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
                var loopTo = ScanNumberList.Length - 1;
                int intSpectrumIndex;
                for (intSpectrumIndex = 0; intSpectrumIndex <= loopTo; intSpectrumIndex++)
                {
                    ScanNumberList[intSpectrumIndex] = mIndexedSpectrumInfo[intSpectrumIndex].ScanNumber;
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
        /// <param name="intSpectrumIndex"></param>
        /// <param name="strSourceXML"></param>
        /// <returns>True if successful, false if an error or invalid spectrum</returns>
        public bool GetSourceXMLByIndex(int intSpectrumIndex, out string strSourceXML)
        {
            strSourceXML = string.Empty;

            try
            {
                mErrorMessage = string.Empty;

                if (mDataReaderMode != drmDataReaderModeConstants.Indexed)
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

                if (intSpectrumIndex < 0 || intSpectrumIndex >= mIndexedSpectrumInfoCount)
                {
                    mErrorMessage = "Invalid spectrum index: " + intSpectrumIndex.ToString();
                    return false;
                }

                // Move the binary file reader to .ByteOffsetStart and populate strXMLText with the text for the given spectrum
                strSourceXML = ExtractTextBetweenOffsets(
                    mInputFilePath,
                    mIndexedSpectrumInfo[intSpectrumIndex].ByteOffsetStart,
                    mIndexedSpectrumInfo[intSpectrumIndex].ByteOffsetEnd);

                return !string.IsNullOrWhiteSpace(strSourceXML);
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
        /// <param name="intScanNumber"></param>
        /// <param name="strSourceXML"></param>
        /// <returns>True if successful, false if an error or invalid spectrum</returns>
        public bool GetSourceXMLByScanNumber(int intScanNumber, out string strSourceXML)
        {
            strSourceXML = string.Empty;

            try
            {
                mErrorMessage = string.Empty;
                var success = false;

                if (mDataReaderMode != drmDataReaderModeConstants.Indexed)
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
                    for (int intSpectrumIndex = 0, loopTo = mIndexedSpectrumInfoCount - 1; intSpectrumIndex <= loopTo; intSpectrumIndex++)
                    {
                        if (mIndexedSpectrumInfo[intSpectrumIndex].ScanNumber == intScanNumber)
                        {
                            success = GetSourceXMLByIndex(intSpectrumIndex, out strSourceXML);
                            break;
                        }
                    }
                }
                else
                {
                    // Look for intScanNumber in mIndexedSpectraScanToIndex
                    var index = mIndexedSpectraScanToIndex[intScanNumber];
                    success = GetSourceXMLByIndex(index, out strSourceXML);
                }

                if (!success && mErrorMessage.Length == 0)
                {
                    mErrorMessage = "Invalid scan number: " + intScanNumber;
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
        /// <param name="intSpectrumIndex"></param>
        /// <param name="objSpectrumInfo"></param>
        /// <returns>True if success, False if failure</returns>
        public override bool GetSpectrumByIndex(int intSpectrumIndex, out clsSpectrumInfo objSpectrumInfo)
        {
            try
            {
                if (mDataReaderMode == drmDataReaderModeConstants.Cached)
                {
                    return base.GetSpectrumByIndex(intSpectrumIndex, out objSpectrumInfo);
                }

                if (mDataReaderMode == drmDataReaderModeConstants.Indexed)
                {
                    return GetSpectrumByIndexWork(intSpectrumIndex, out objSpectrumInfo, false);
                }

                mErrorMessage = "Cached or indexed data not in memory";
                objSpectrumInfo = null;
                return false;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in GetSpectrumByIndex", ex);
                objSpectrumInfo = null;
                return false;
            }
        }

        protected abstract bool GetSpectrumByIndexWork(int intSpectrumIndex, out clsSpectrumInfo objSpectrumInfo, bool blnHeaderInfoOnly);

        public override bool GetSpectrumByScanNumber(int intScanNumber, out clsSpectrumInfo objSpectrumInfo)
        {
            return GetSpectrumByScanNumberWork(intScanNumber, out objSpectrumInfo, false);
        }

        /// <summary>
        /// Obtain the spectrum data for the given scan
        /// </summary>
        /// <remarks>
        /// Only valid if we have Cached or Indexed data in memory
        /// </remarks>
        /// <param name="intScanNumber"></param>
        /// <param name="objSpectrumInfo"></param>
        /// <param name="blnHeaderInfoOnly"></param>
        /// <returns>True if success, False if failure</returns>
        protected bool GetSpectrumByScanNumberWork(int intScanNumber, out clsSpectrumInfo objSpectrumInfo, bool blnHeaderInfoOnly)
        {
            objSpectrumInfo = null;

            try
            {
                mErrorMessage = string.Empty;

                if (mDataReaderMode == drmDataReaderModeConstants.Cached)
                {
                    return base.GetSpectrumByScanNumber(intScanNumber, out objSpectrumInfo);
                }

                if (mDataReaderMode != drmDataReaderModeConstants.Indexed)
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
                    for (int intSpectrumIndex = 0, loopTo = mIndexedSpectrumInfoCount - 1; intSpectrumIndex <= loopTo; intSpectrumIndex++)
                    {
                        if (mIndexedSpectrumInfo[intSpectrumIndex].ScanNumber == intScanNumber)
                        {
                            success = GetSpectrumByIndex(intSpectrumIndex, out objSpectrumInfo);
                            break;
                        }
                    }
                }
                else
                {
                    // Look for intScanNumber in mIndexedSpectraScanToIndex
                    var index = mIndexedSpectraScanToIndex[intScanNumber];
                    success = GetSpectrumByIndexWork(index, out objSpectrumInfo, blnHeaderInfoOnly);
                }

                if (!success && string.IsNullOrWhiteSpace(mErrorMessage))
                {
                    mErrorMessage = "Invalid scan number: " + intScanNumber;
                }

                return success;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in GetSpectrumByScanNumberWork", ex);
                return false;
            }
        }

        public bool GetSpectrumHeaderInfoByIndex(int intSpectrumIndex, out clsSpectrumInfo objSpectrumInfo)
        {
            return GetSpectrumByIndexWork(intSpectrumIndex, out objSpectrumInfo, true);
        }

        public bool GetSpectrumHeaderInfoByScanNumber(int intScanNumber, out clsSpectrumInfo objSpectrumInfo)
        {
            return GetSpectrumByScanNumberWork(intScanNumber, out objSpectrumInfo, true);
        }

        /// <summary>
        /// Check whether the reader is open and spectra can be obtained
        /// </summary>
        /// <remarks>
        /// If blnAllowConcurrentReading = True, returns True if mBinaryReader is ready for reading
        /// If blnAllowConcurrentReading = False, returns True only after the file is fully indexed
        /// Otherwise, returns false
        /// </remarks>
        /// <param name="blnAllowConcurrentReading"></param>
        protected bool GetSpectrumReadyStatus(bool blnAllowConcurrentReading)
        {
            if (mBinaryReader is null || !mBinaryReader.CanRead)
            {
                mErrorMessage = "Data file not currently open";
                return false;
            }

            if (blnAllowConcurrentReading)
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
            mIndexedSpectrumInfo = new udtIndexedSpectrumInfoType[1000];
            mIndexedSpectraScanToIndex.Clear();
        }

        protected override void InitializeLocalVariables()
        {
            base.InitializeLocalVariables();
            mErrorMessage = string.Empty;
            mLastSpectrumIndexRead = 0;
            mDataReaderMode = drmDataReaderModeConstants.Indexed;
            mInputFileEncoding = clsBinaryTextReader.InputFileEncodingConstants.ASCII;
            mCharSize = 1;
            mIndexingComplete = false;
        }

        protected Regex InitializeRegEx(string strPattern)
        {
            return new Regex(strPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// This method should be defined to look for an existing byte offset index and, if found,
        /// populate mIndexedSpectrumInfo() and set mIndexingComplete = True
        /// </summary>
        protected abstract bool LoadExistingIndex();

        /// <summary>
        /// Open the given file
        /// </summary>
        /// <param name="strInputFilePath"></param>
        /// <returns>True if the file is successfully opened</returns>
        public override bool OpenFile(string strInputFilePath)
        {
            try
            {
                var initSuccess = OpenFileInit(strInputFilePath);

                if (!initSuccess)
                    return false;

                InitializeFileTrackingVariables();
                mDataReaderMode = drmDataReaderModeConstants.Indexed;
                mInputFilePath = string.Copy(strInputFilePath);

                // Initialize the binary text reader
                // Even if an existing index is present, this is needed to determine
                // the input file encoding and the character size
                mBinaryTextReader = new clsBinaryTextReader();

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
                mErrorMessage = "Error opening file: " + strInputFilePath + "; " + ex.Message;
                OnErrorEvent(mErrorMessage, ex);
                return false;
            }
        }

        /// <summary>
        /// This reading mode is not appropriate for the MS Data File Accessor
        /// </summary>
        /// <param name="strTextStream"></param>
        /// <returns>Always returns false</returns>
        /// <remarks></remarks>
        public override bool OpenTextStream(string strTextStream)
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
            var success= base.ReadAndCacheEntireFile();

            if (success)
            {
                mDataReaderMode = drmDataReaderModeConstants.Cached;
            }

            return success;
        }

        public override bool ReadNextSpectrum(out clsSpectrumInfo objSpectrumInfo)
        {
            if (GetSpectrumReadyStatus(false) && mLastSpectrumIndexRead < mIndexedSpectrumInfoCount)
            {
                mLastSpectrumIndexRead++;
                return GetSpectrumByIndex(mLastSpectrumIndexRead, out objSpectrumInfo);
            }

            objSpectrumInfo = null;
            return false;
        }

        protected void StoreIndexEntry(int intScanNumber, long lngByteOffsetStart, long lngByteOffsetEnd)
        {
            if (mIndexedSpectrumInfoCount >= mIndexedSpectrumInfo.Length)
            {
                // Double the amount of space reserved for mIndexedSpectrumInfo
                Array.Resize(ref mIndexedSpectrumInfo, mIndexedSpectrumInfo.Length * 2);
            }

            mIndexedSpectrumInfo[mIndexedSpectrumInfoCount].ScanNumber = intScanNumber;
            mIndexedSpectrumInfo[mIndexedSpectrumInfoCount].ByteOffsetStart = lngByteOffsetStart;
            mIndexedSpectrumInfo[mIndexedSpectrumInfoCount].ByteOffsetEnd = lngByteOffsetEnd;

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