using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MSDataFileReader
{

    // This class can be used to open an MS Data file (currently .mzXML and .mzData) and
    // index the location of all of the spectra present.  This does not cache the mass spectra
    // data in memory, and therefore uses little memory, but once the indexing is complete,
    // random access to the spectra is possible.  After the indexing is complete, spectra
    // can be obtained using GetSpectrumByScanNumber or GetSpectrumByIndex

    // -------------------------------------------------------------------------------
    // Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    // Copyright 2006, Battelle Memorial Institute.  All Rights Reserved.
    // Program started April 16, 2006
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

    public abstract class clsMSDataFileAccessorBaseClass : clsMSDataFileReaderBaseClass
    {
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

        #region Constants and Enums

        protected const int INITIAL_SCAN_RESERVE_COUNT = 1000;

        protected enum emmElementMatchModeConstants
        {
            StartElement = 0,
            EndElement = 1
        }

        #endregion

        #region Structures

        protected struct udtIndexedSpectrumInfoType
        {
            public int ScanNumber;
            public int SpectrumID;        // Only used by mzData files
            public long ByteOffsetStart;
            public long ByteOffsetEnd;

            public override string ToString()
            {
                return "Scan " + ScanNumber + ", bytes " + ByteOffsetStart + " to " + ByteOffsetEnd;
            }
        }

        #endregion

        #region Classwide Variables

        protected clsBinaryTextReader.InputFileEncodingConstants mInputFileEncoding;
        protected byte mCharSize;
        protected bool mIndexingComplete;
        protected FileStream mBinaryReader;
        protected clsBinaryTextReader mBinaryTextReader;
        protected string mInFileCurrentLineText;
        protected int mInFileCurrentCharIndex;

        // This dictionary maps scan number to index in mIndexedSpectrumInfo()
        // If more than one spectrum comes from the same scan, then tracks the first one read
        protected readonly Dictionary<int, int> mIndexedSpectraScanToIndex = new();
        protected int mLastSpectrumIndexRead;

        // These variables are used when mDataReaderMode = Indexed
        protected int mIndexedSpectrumInfoCount;
        protected udtIndexedSpectrumInfoType[] mIndexedSpectrumInfo;

        #endregion

        #region Processing Options and Interface Functions

        public override int CachedSpectrumCount
        {
            get
            {
                if (mDataReaderMode == drmDataReaderModeConstants.Cached)
                {
                    return base.CachedSpectrumCount;
                }
                else
                {
                    return mIndexedSpectrumInfoCount;
                }
            }
        }

        #endregion

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
    /// <returns></returns>
    /// <remarks></remarks>
        protected virtual string ExtractTextBetweenOffsets(string strFilePath, long lngStartByteOffset, long lngEndByteOffset)
        {
            byte[] bytData;
            int intBytesToRead;
            try
            {
                if (mBinaryReader != null && mBinaryReader.CanRead)
                {
                    mBinaryReader.Seek(lngStartByteOffset, SeekOrigin.Begin);
                    intBytesToRead = (int)(lngEndByteOffset - lngStartByteOffset + 1L);
                    if (intBytesToRead > 0)
                    {
                        bytData = new byte[intBytesToRead];
                        intBytesToRead = mBinaryReader.Read(bytData, 0, intBytesToRead);
                        switch (mInputFileEncoding)
                        {
                            case clsBinaryTextReader.InputFileEncodingConstants.Ascii:
                                {
                                    return new string(Encoding.ASCII.GetChars(bytData, 0, intBytesToRead));
                                }

                            case clsBinaryTextReader.InputFileEncodingConstants.UTF8:
                                {
                                    return new string(Encoding.UTF8.GetChars(bytData, 0, intBytesToRead));
                                }

                            case clsBinaryTextReader.InputFileEncodingConstants.UnicodeNormal:
                                {
                                    return new string(Encoding.Unicode.GetChars(bytData, 0, intBytesToRead));
                                }

                            case var @case when @case == clsBinaryTextReader.InputFileEncodingConstants.UnicodeNormal:
                                {
                                    return new string(Encoding.BigEndianUnicode.GetChars(bytData, 0, intBytesToRead));
                                }

                            default:
                                {
                                    // Unknown encoding
                                    return string.Empty;
                                }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ExtractXMLText", ex);
            }

            // If we get here, then no match was found, so return an empty string
            return string.Empty;
        }

        /// <summary>
    /// Extract the text between lngStartByteOffset and lngEndByteOffset in strFilePath, then append it to
    /// mXmlFileHeader, add the closing element tags, and return ByRef in strExtractedText
    /// </summary>
    /// <param name="strFilePath"></param>
    /// <param name="lngStartByteOffset"></param>
    /// <param name="lngEndByteOffset"></param>
    /// <param name="strExtractedText"></param>
    /// <param name="intScanCountTotal"></param>
    /// <param name="sngStartTimeMinutesAllScans"></param>
    /// <param name="sngEndTimeMinutesAllScans"></param>
    /// <returns></returns>
    /// <remarks>Note that sngStartTimeMinutesAllScans and sngEndTimeMinutesAllScans are really only appropriate for mzXML files</remarks>
        [Obsolete("Superseded by wrapping mBinaryReader with an XmlTextReader; see GetSpectrumByIndexWork")]
        protected bool ExtractTextFromFile(string strFilePath, long lngStartByteOffset, long lngEndByteOffset, out string strExtractedText, int intScanCountTotal, float sngStartTimeMinutesAllScans, float sngEndTimeMinutesAllScans)
        {
            var blnSuccess = default(bool);
            try
            {
                strExtractedText = GetSourceXMLHeader(intScanCountTotal, sngStartTimeMinutesAllScans, sngEndTimeMinutesAllScans) + Environment.NewLine + ExtractTextBetweenOffsets(strFilePath, lngStartByteOffset, lngEndByteOffset) + Environment.NewLine + GetSourceXMLFooter();
                blnSuccess = true;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ExtractTextFromFile", ex);
                strExtractedText = string.Empty;
            }

            return blnSuccess;
        }

        protected override string GetInputFileLocation()
        {
            try
            {
                if (mBinaryTextReader is null)
                {
                    return string.Empty;
                }
                else
                {
                    return "Line " + mBinaryTextReader.LineNumber + ", Byte Offset " + mBinaryTextReader.CurrentLineByteOffsetStart;
                }
            }
            catch (Exception ex)
            {
                // Ignore errors here
                return string.Empty;
            }
        }

        public override bool GetScanNumberList(out int[] ScanNumberList)
        {
            // Return the list of indexed scan numbers (aka acquisition numbers)

            int intSpectrumIndex;
            var blnSuccess = default(bool);
            try
            {
                blnSuccess = false;
                if (mDataReaderMode == drmDataReaderModeConstants.Cached)
                {
                    blnSuccess = base.GetScanNumberList(out ScanNumberList);
                }
                else if (GetSpectrumReadyStatus(true))
                {
                    if (mIndexedSpectrumInfo is null || mIndexedSpectrumInfoCount == 0)
                    {
                        ScanNumberList = new int[0];
                    }
                    else
                    {
                        ScanNumberList = new int[mIndexedSpectrumInfoCount];
                        var loopTo = ScanNumberList.Length - 1;
                        for (intSpectrumIndex = 0; intSpectrumIndex <= loopTo; intSpectrumIndex++)
                            ScanNumberList[intSpectrumIndex] = mIndexedSpectrumInfo[intSpectrumIndex].ScanNumber;
                        blnSuccess = true;
                    }
                }
                else
                {
                    ScanNumberList = new int[0];
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in GetScanNumberList", ex);
                ScanNumberList = new int[0];
            }

            return blnSuccess;
        }

        public abstract string GetSourceXMLFooter();
        public abstract string GetSourceXMLHeader(int intScanCountTotal, float sngStartTimeMinutesAllScans, float sngEndTimeMinutesAllScans);

        public bool GetSourceXMLByIndex(int intSpectrumIndex, out string strSourceXML)
        {
            // Returns the XML for the given spectrum
            // This does not include the header or footer XML for the file
            // Only valid if we have Indexed data in memory

            var blnSuccess = default(bool);
            strSourceXML = string.Empty;
            try
            {
                blnSuccess = false;
                mErrorMessage = string.Empty;
                if (mDataReaderMode == drmDataReaderModeConstants.Indexed)
                {
                    if (GetSpectrumReadyStatus(true))
                    {
                        if (mIndexedSpectrumInfoCount == 0)
                        {
                            mErrorMessage = "Indexed data not in memory";
                        }
                        else if (intSpectrumIndex >= 0 & intSpectrumIndex < mIndexedSpectrumInfoCount)
                        {
                            // Move the binary file reader to .ByteOffsetStart and populate strXMLText with the text for the given spectrum
                            strSourceXML = ExtractTextBetweenOffsets(mInputFilePath, mIndexedSpectrumInfo[intSpectrumIndex].ByteOffsetStart, mIndexedSpectrumInfo[intSpectrumIndex].ByteOffsetEnd);
                            if (string.IsNullOrWhiteSpace(strSourceXML))
                            {
                                blnSuccess = false;
                            }
                            else
                            {
                                blnSuccess = true;
                            }
                        }
                        else
                        {
                            mErrorMessage = "Invalid spectrum index: " + intSpectrumIndex.ToString();
                        }
                    }
                }
                else
                {
                    mErrorMessage = "Indexed data not in memory";
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in GetSourceXMLByIndex", ex);
            }

            return blnSuccess;
        }

        public bool GetSourceXMLByScanNumber(int intScanNumber, out string strSourceXML)
        {
            // Returns the XML for the given spectrum
            // This does not include the header or footer XML for the file
            // Only valid if we have Indexed data in memory

            var blnSuccess = default(bool);
            strSourceXML = string.Empty;
            try
            {
                blnSuccess = false;
                mErrorMessage = string.Empty;
                if (mDataReaderMode == drmDataReaderModeConstants.Indexed)
                {
                    if (GetSpectrumReadyStatus(true))
                    {
                        if (mIndexedSpectraScanToIndex.Count == 0)
                        {
                            for (int intSpectrumIndex = 0, loopTo = mIndexedSpectrumInfoCount - 1; intSpectrumIndex <= loopTo; intSpectrumIndex++)
                            {
                                if (mIndexedSpectrumInfo[intSpectrumIndex].ScanNumber == intScanNumber)
                                {
                                    blnSuccess = GetSourceXMLByIndex(intSpectrumIndex, out strSourceXML);
                                    break;
                                }
                            }
                        }
                        else
                        {
                            // Look for intScanNumber in mIndexedSpectraScanToIndex
                            var index = mIndexedSpectraScanToIndex[intScanNumber];
                            blnSuccess = GetSourceXMLByIndex(index, out strSourceXML);
                        }

                        if (!blnSuccess && mErrorMessage.Length == 0)
                        {
                            mErrorMessage = "Invalid scan number: " + intScanNumber.ToString();
                        }
                    }
                }
                else
                {
                    mErrorMessage = "Indexed data not in memory";
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in GetSourceXMLByScanNumber", ex);
            }

            return blnSuccess;
        }

        public override bool GetSpectrumByIndex(int intSpectrumIndex, out clsSpectrumInfo objSpectrumInfo)
        {
            // Returns True if success, False if failure
            // Only valid if we have Cached or Indexed data in memory

            var blnSuccess = default(bool);
            try
            {
                if (mDataReaderMode == drmDataReaderModeConstants.Cached)
                {
                    blnSuccess = base.GetSpectrumByIndex(intSpectrumIndex, out objSpectrumInfo);
                }
                else if (mDataReaderMode == drmDataReaderModeConstants.Indexed)
                {
                    blnSuccess = GetSpectrumByIndexWork(intSpectrumIndex, out objSpectrumInfo, false);
                }
                else
                {
                    mErrorMessage = "Cached or indexed data not in memory";
                    blnSuccess = false;
                    objSpectrumInfo = null;
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in GetSpectrumByIndex", ex);
                objSpectrumInfo = null;
            }

            return blnSuccess;
        }

        protected abstract bool GetSpectrumByIndexWork(int intSpectrumIndex, out clsSpectrumInfo objSpectrumInfo, bool blnHeaderInfoOnly);

        public override bool GetSpectrumByScanNumber(int intScanNumber, out clsSpectrumInfo objSpectrumInfo)
        {
            return GetSpectrumByScanNumberWork(intScanNumber, out objSpectrumInfo, false);
        }

        protected bool GetSpectrumByScanNumberWork(int intScanNumber, out clsSpectrumInfo objSpectrumInfo, bool blnHeaderInfoOnly)
        {

            // Return the data for scan intScanNumber in mIndexedSpectrumInfo
            // Returns True if success, False if failure
            // Only valid if we have Cached or Indexed data in memory

            var blnSuccess = default(bool);
            objSpectrumInfo = null;
            try
            {
                blnSuccess = false;
                mErrorMessage = string.Empty;
                if (mDataReaderMode == drmDataReaderModeConstants.Cached)
                {
                    blnSuccess = base.GetSpectrumByScanNumber(intScanNumber, out objSpectrumInfo);
                }
                else if (mDataReaderMode == drmDataReaderModeConstants.Indexed)
                {
                    if (GetSpectrumReadyStatus(true))
                    {
                        if (mIndexedSpectraScanToIndex.Count == 0)
                        {
                            for (int intSpectrumIndex = 0, loopTo = mIndexedSpectrumInfoCount - 1; intSpectrumIndex <= loopTo; intSpectrumIndex++)
                            {
                                if (mIndexedSpectrumInfo[intSpectrumIndex].ScanNumber == intScanNumber)
                                {
                                    blnSuccess = GetSpectrumByIndex(intSpectrumIndex, out objSpectrumInfo);
                                    break;
                                }
                            }
                        }
                        else
                        {
                            // Look for intScanNumber in mIndexedSpectraScanToIndex
                            var index = mIndexedSpectraScanToIndex[intScanNumber];
                            blnSuccess = GetSpectrumByIndexWork(index, out objSpectrumInfo, blnHeaderInfoOnly);
                        }

                        if (!blnSuccess && mErrorMessage.Length == 0)
                        {
                            mErrorMessage = "Invalid scan number: " + intScanNumber.ToString();
                        }
                    }
                }
                else
                {
                    mErrorMessage = "Cached or indexed data not in memory";
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in GetSpectrumByScanNumberWork", ex);
            }

            return blnSuccess;
        }

        public bool GetSpectrumHeaderInfoByIndex(int intSpectrumIndex, out clsSpectrumInfo objSpectrumInfo)
        {
            return GetSpectrumByIndexWork(intSpectrumIndex, out objSpectrumInfo, true);
        }

        public bool GetSpectrumHeaderInfoByScanNumber(int intScanNumber, out clsSpectrumInfo objSpectrumInfo)
        {
            return GetSpectrumByScanNumberWork(intScanNumber, out objSpectrumInfo, true);
        }

        protected bool GetSpectrumReadyStatus(bool blnAllowConcurrentReading)
        {
            // If blnAllowConcurrentReading = True, then returns True if mBinaryReader is ready for reading
            // If blnAllowConcurrentReading = False, then Returns True only after the file is fully indexed
            // Otherwise, returns false

            bool blnReady;
            if (mBinaryReader is null || !mBinaryReader.CanRead)
            {
                mErrorMessage = "Data file not currently open";
                blnReady = false;
            }
            else if (blnAllowConcurrentReading)
            {
                blnReady = true;
            }
            else
            {
                blnReady = !mReadingAndStoringSpectra;
            }

            return blnReady;
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
            mInputFileEncoding = clsBinaryTextReader.InputFileEncodingConstants.Ascii;
            mCharSize = 1;
            mIndexingComplete = false;
        }

        protected Regex InitializeRegEx(string strPattern)
        {
            return new Regex(strPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        // This function should be defined to look for an existing byte offset index and, if found,
        // populate mIndexedSpectrumInfo() and set mIndexingComplete = True
        protected abstract bool LoadExistingIndex();

        public override bool OpenFile(string strInputFilePath)
        {
            // Returns true if the file is successfully opened

            bool blnSuccess;
            try
            {
                blnSuccess = OpenFileInit(strInputFilePath);
                if (!blnSuccess)
                    return false;
                InitializeFileTrackingVariables();
                mDataReaderMode = drmDataReaderModeConstants.Indexed;
                mInputFilePath = string.Copy(strInputFilePath);

                // Initialize the binary text reader
                // Even if an existing index is present, this is needed to determine
                // the input file encoding and the character size
                mBinaryTextReader = new clsBinaryTextReader();
                blnSuccess = false;
                if (mBinaryTextReader.OpenFile(mInputFilePath, FileShare.ReadWrite))
                {
                    mInputFileEncoding = mBinaryTextReader.InputFileEncoding;
                    mCharSize = mBinaryTextReader.CharSize;
                    blnSuccess = true;

                    // Initialize the binary reader (which is used to extract individual spectra from the XML file)
                    mBinaryReader = new FileStream(mInputFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                    // Look for a byte offset index, present either inside the .XML file (e.g. .mzXML)
                    // or in a separate file (future capability)
                    // If an index is found, then set mIndexingComplete to True
                    if (LoadExistingIndex())
                    {
                        mIndexingComplete = true;
                    }

                    if (mBinaryTextReader.ByteBufferFileOffsetStart > 0L || mBinaryTextReader.CurrentLineByteOffsetStart > mBinaryTextReader.ByteOrderMarkLength)
                    {
                        mBinaryTextReader.MoveToBeginning();
                    }

                    mErrorMessage = string.Empty;
                }
            }
            catch (Exception ex)
            {
                mErrorMessage = "Error opening file: " + strInputFilePath + "; " + ex.Message;
                blnSuccess = false;
            }

            return blnSuccess;
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

        public bool ReadAndCacheEntireFileNonIndexed()
        {
            // Provides the option to cache the entire file rather than indexing it and accessing it with a binary reader

            bool blnSuccess;
            blnSuccess = base.ReadAndCacheEntireFile();
            if (blnSuccess)
            {
                mDataReaderMode = drmDataReaderModeConstants.Cached;
            }

            return blnSuccess;
        }

        public override bool ReadNextSpectrum(out clsSpectrumInfo objSpectrumInfo)
        {
            if (GetSpectrumReadyStatus(false) && mLastSpectrumIndexRead < mIndexedSpectrumInfoCount)
            {
                mLastSpectrumIndexRead += 1;
                return GetSpectrumByIndex(mLastSpectrumIndexRead, out objSpectrumInfo);
            }
            else
            {
                objSpectrumInfo = null;
                return false;
            }
        }

        protected void StoreIndexEntry(int intScanNumber, long lngByteOffsetStart, long lngByteOffsetEnd)
        {
            if (mIndexedSpectrumInfoCount >= mIndexedSpectrumInfo.Length)
            {
                // Double the amount of space reserved for mIndexedSpectrumInfo
                Array.Resize(ref mIndexedSpectrumInfo, mIndexedSpectrumInfo.Length * 2);
            }

            {
                ref var withBlock = ref mIndexedSpectrumInfo[mIndexedSpectrumInfoCount];
                withBlock.ScanNumber = intScanNumber;
                withBlock.ByteOffsetStart = lngByteOffsetStart;
                withBlock.ByteOffsetEnd = lngByteOffsetEnd;
                UpdateFileStats(mIndexedSpectrumInfoCount + 1, withBlock.ScanNumber);
                if (!mIndexedSpectraScanToIndex.ContainsKey(withBlock.ScanNumber))
                {
                    mIndexedSpectraScanToIndex.Add(withBlock.ScanNumber, mIndexedSpectrumInfoCount);
                }
            }

            // Increment mIndexedSpectrumInfoCount
            mIndexedSpectrumInfoCount += 1;
        }
    }
}