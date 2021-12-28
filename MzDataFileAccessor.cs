using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;

namespace MSDataFileReader
{

    // This class can be used to open a .mzData file and index the location
    // of all of the spectra present.  This does not cache the mass spectra data in
    // memory, and therefore uses little memory, but once the indexing is complete,
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

    public class clsMzDataFileAccessor : clsMSDataFileAccessorBaseClass
    {
        public clsMzDataFileAccessor()
        {
            InitializeObjectVariables();
            InitializeLocalVariables();
        }

        ~clsMzDataFileAccessor()
        {
            if (mXmlFileReader != null)
            {
                mXmlFileReader = null;
            }
        }

        #region Constants and Enums

        private const string SPECTRUM_LIST_START_ELEMENT = "<spectrumList";
        private const string SPECTRUM_LIST_END_ELEMENT = "</spectrumList>";
        private const string SPECTRUM_START_ELEMENT = "<spectrum";
        private const string SPECTRUM_END_ELEMENT = "</spectrum>";
        private const string MZDATA_START_ELEMENT = "<mzData";
        private const string MZDATA_END_ELEMENT = "</mzData>";

        #endregion

        #region Classwide Variables

        private clsMzDataFileReader mXmlFileReader;
        private clsSpectrumInfoMzData mCurrentSpectrumInfo;
        private int mInputFileStatsSpectrumIDMinimum;
        private int mInputFileStatsSpectrumIDMaximum;
        private string mXmlFileHeader;
        private bool mAddNewLinesToHeader;
        private Regex mSpectrumStartElementRegEx;
        private Regex mSpectrumEndElementRegEx;
        private Regex mSpectrumListRegEx;
        private Regex mAcquisitionNumberRegEx;
        private Regex mSpectrumIDRegEx;

        // This dictionary maps spectrum ID to index in mCachedSpectra()
        // If more than one spectrum has the same spectrum ID, then tracks the first one read
        private readonly Dictionary<int, int> mIndexedSpectraSpectrumIDToIndex = new();
        private XmlReaderSettings mXMLReaderSettings;

        #endregion

        #region Processing Options and Interface Functions

        public int CachedSpectraSpectrumIDMinimum
        {
            get
            {
                return mInputFileStatsSpectrumIDMinimum;
            }
        }

        public int CachedSpectraSpectrumIDMaximum
        {
            get
            {
                return mInputFileStatsSpectrumIDMaximum;
            }
        }

        public override bool ParseFilesWithUnknownVersion
        {
            get
            {
                return base.ParseFilesWithUnknownVersion;
            }

            set
            {
                base.ParseFilesWithUnknownVersion = value;
                if (mXmlFileReader != null)
                {
                    mXmlFileReader.ParseFilesWithUnknownVersion = value;
                }
            }
        }

        #endregion

        protected override bool AdvanceFileReaders(emmElementMatchModeConstants eElementMatchMode)
        {
            // Uses the BinaryTextReader to look for strTextToFind

            bool blnMatchFound;
            bool blnAppendingText;
            var lngByteOffsetForRewind = default(long);
            var blnLookForScanCountOnNextRead = default(bool);
            string strScanCountSearchText = string.Empty;
            int intCharIndex;
            string strAcqNumberSearchText;
            bool blnAcqNumberFound;
            string strInFileCurrentLineSubstring;
            Match objMatch;
            try
            {
                if (mInFileCurrentLineText is null)
                {
                    mInFileCurrentLineText = string.Empty;
                }

                strInFileCurrentLineSubstring = string.Empty;
                blnAppendingText = false;
                strAcqNumberSearchText = string.Empty;
                blnAcqNumberFound = false;
                blnMatchFound = false;
                while (!(blnMatchFound || mAbortProcessing))
                {
                    if (mInFileCurrentCharIndex + 1 < mInFileCurrentLineText.Length)
                    {
                        if (blnAppendingText)
                        {
                            strInFileCurrentLineSubstring += Environment.NewLine + mInFileCurrentLineText.Substring(mInFileCurrentCharIndex + 1);
                        }
                        else
                        {
                            strInFileCurrentLineSubstring = mInFileCurrentLineText.Substring(mInFileCurrentCharIndex + 1);
                        }

                        if (mAddNewLinesToHeader)
                        {
                            // We haven't yet found the first scan; look for "<spectrumList"
                            intCharIndex = mInFileCurrentLineText.IndexOf(SPECTRUM_LIST_START_ELEMENT, mInFileCurrentCharIndex + 1, StringComparison.Ordinal);
                            if (intCharIndex >= 0)
                            {
                                // Only add a portion of mInFileCurrentLineText to mXmlFileHeader
                                // since it contains SPECTRUM_LIST_START_ELEMENT

                                if (intCharIndex > 0)
                                {
                                    mXmlFileHeader += mInFileCurrentLineText.Substring(0, intCharIndex);
                                }

                                mAddNewLinesToHeader = false;
                                strScanCountSearchText = strInFileCurrentLineSubstring.Substring(intCharIndex);
                                blnLookForScanCountOnNextRead = true;
                            }
                            else
                            {
                                // Append mInFileCurrentLineText to mXmlFileHeader
                                mXmlFileHeader += mInFileCurrentLineText + Environment.NewLine;
                            }
                        }
                        else if (blnLookForScanCountOnNextRead)
                        {
                            strScanCountSearchText += Environment.NewLine + strInFileCurrentLineSubstring;
                        }

                        if (blnLookForScanCountOnNextRead)
                        {
                            // Look for the Scan Count value in strScanCountSearchText
                            objMatch = mSpectrumListRegEx.Match(strScanCountSearchText);
                            if (objMatch.Success)
                            {
                                // Record the Scan Count value
                                if (objMatch.Groups.Count > 1)
                                {
                                    try
                                    {
                                        mInputFileStats.ScanCount = int.Parse(objMatch.Groups[1].Captures[0].Value);
                                    }
                                    catch (Exception ex)
                                    {
                                    }
                                }

                                blnLookForScanCountOnNextRead = false;
                            }
                            // The count attribute is not on the same line as the <spectrumList element
                            // Set blnLookForScanCountOnNextRead to true if strScanCountSearchText does not contain the end element symbol, i.e. >
                            else if (strScanCountSearchText.IndexOf('>') >= 0)
                            {
                                blnLookForScanCountOnNextRead = false;
                            }
                        }

                        if (eElementMatchMode == emmElementMatchModeConstants.EndElement && !blnAcqNumberFound)
                        {
                            strAcqNumberSearchText += Environment.NewLine + strInFileCurrentLineSubstring;

                            // Look for the acquisition number
                            // Because strAcqNumberSearchText contains all of the text from <spectrum on (i.e. not just the text for the current line)
                            // the test by mAcquisitionNumberRegEx should match the acqNumber attribute even if it is not
                            // on the same line as <acquisition or if it is not the first attribute following <acquisition
                            objMatch = mAcquisitionNumberRegEx.Match(strAcqNumberSearchText);
                            if (objMatch.Success)
                            {
                                if (objMatch.Groups.Count > 1)
                                {
                                    try
                                    {
                                        blnAcqNumberFound = true;
                                        mCurrentSpectrumInfo.ScanNumber = int.Parse(objMatch.Groups[1].Captures[0].Value);
                                    }
                                    catch (Exception ex)
                                    {
                                    }
                                }
                            }
                        }

                        // Look for the appropriate search text in mInFileCurrentLineText, starting at mInFileCurrentCharIndex + 1
                        switch (eElementMatchMode)
                        {
                            case emmElementMatchModeConstants.StartElement:
                                {
                                    objMatch = mSpectrumStartElementRegEx.Match(strInFileCurrentLineSubstring);
                                    break;
                                }

                            case emmElementMatchModeConstants.EndElement:
                                {
                                    objMatch = mSpectrumEndElementRegEx.Match(strInFileCurrentLineSubstring);
                                    break;
                                }

                            default:
                                {
                                    // Unknown mode
                                    OnErrorEvent("Unknown mode for eElementMatchMode in AdvanceFileReaders: {0}", eElementMatchMode);
                                    return false;
                                }
                        }

                        if (objMatch.Success)
                        {
                            // Match Found
                            blnMatchFound = true;
                            intCharIndex = objMatch.Index + 1 + mInFileCurrentCharIndex;
                            if (eElementMatchMode == emmElementMatchModeConstants.StartElement)
                            {
                                // Look for the id value after <spectrum
                                objMatch = mSpectrumIDRegEx.Match(strInFileCurrentLineSubstring);
                                if (objMatch.Success)
                                {
                                    if (objMatch.Groups.Count > 1)
                                    {
                                        try
                                        {
                                            mCurrentSpectrumInfo.SpectrumID = int.Parse(objMatch.Groups[1].Captures[0].Value);
                                        }
                                        catch (Exception ex)
                                        {
                                        }
                                    }
                                }
                                // Could not find the id attribute
                                // If strInFileCurrentLineSubstring does not contain SPECTRUM_END_ELEMENT, then
                                // set blnAppendingText to True and continue reading
                                else if (strInFileCurrentLineSubstring.IndexOf(SPECTRUM_END_ELEMENT, StringComparison.Ordinal) < 0)
                                {
                                    blnMatchFound = false;
                                    if (!blnAppendingText)
                                    {
                                        blnAppendingText = true;
                                        // Record the byte offset of the start of the current line
                                        // We will use this offset to "rewind" the file pointer once the id attribute is found
                                        lngByteOffsetForRewind = mBinaryTextReader.CurrentLineByteOffsetStart;
                                    }
                                }
                            }
                            else if (eElementMatchMode == emmElementMatchModeConstants.EndElement)
                            {
                                // Move to the end of the element
                                intCharIndex += objMatch.Value.Length - 1;
                                if (intCharIndex >= mInFileCurrentLineText.Length)
                                {
                                    // This shouldn't happen
                                    OnErrorEvent("Unexpected condition in AdvanceFileReaders: intCharIndex >= mInFileCurrentLineText.Length");
                                    intCharIndex = mInFileCurrentLineText.Length - 1;
                                }
                            }

                            mInFileCurrentCharIndex = intCharIndex;
                            if (blnMatchFound)
                            {
                                if (blnAppendingText)
                                {
                                    mBinaryTextReader.MoveToByteOffset(lngByteOffsetForRewind);
                                    mBinaryTextReader.ReadLine();
                                    mInFileCurrentLineText = mBinaryTextReader.CurrentLine;
                                }

                                break;
                            }
                        }
                    }

                    // Read the next line from the BinaryTextReader
                    if (!mBinaryTextReader.ReadLine())
                    {
                        break;
                    }

                    mInFileCurrentLineText = mBinaryTextReader.CurrentLine;
                    mInFileCurrentCharIndex = -1;
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in AdvanceFileReaders", ex);
                blnMatchFound = false;
            }

            return blnMatchFound;
        }

        [Obsolete("No longer used")]
        public override string GetSourceXMLFooter()
        {
            return SPECTRUM_LIST_END_ELEMENT + Environment.NewLine + MZDATA_END_ELEMENT + Environment.NewLine;
        }

        [Obsolete("No longer used")]
        public override string GetSourceXMLHeader(int intScanCountTotal, float sngStartTimeMinutesAllScans, float sngEndTimeMinutesAllScans)
        {
            string strHeaderText;
            int intAsciiValue;
            if (mXmlFileHeader is null)
                mXmlFileHeader = string.Empty;
            strHeaderText = string.Copy(mXmlFileHeader);
            if (strHeaderText.Length == 0)
            {
                strHeaderText = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + Environment.NewLine + MZDATA_START_ELEMENT + " version=\"1.05\" accessionNumber=\"psi-ms:100\"" + " xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" + Environment.NewLine;
            }

            intAsciiValue = Convert.ToInt32(strHeaderText[strHeaderText.Length - 1]);
            if (!(intAsciiValue == 10 || intAsciiValue == 13 || intAsciiValue == 9 || intAsciiValue == 32))
            {
                strHeaderText += Environment.NewLine;
            }

            return strHeaderText + " " + SPECTRUM_LIST_START_ELEMENT + " count=\"" + intScanCountTotal + "\">";
        }

        protected override bool GetSpectrumByIndexWork(int intSpectrumIndex, out clsSpectrumInfo objCurrentSpectrumInfo, bool blnHeaderInfoOnly)
        {
            var blnSuccess = default(bool);
            objCurrentSpectrumInfo = null;
            try
            {
                blnSuccess = false;
                if (GetSpectrumReadyStatus(true))
                {
                    if (mXmlFileReader is null)
                    {
                        mXmlFileReader = new clsMzDataFileReader() { ParseFilesWithUnknownVersion = mParseFilesWithUnknownVersion };
                    }

                    if (mIndexedSpectrumInfoCount == 0)
                    {
                        mErrorMessage = "Indexed data not in memory";
                    }
                    else if (intSpectrumIndex >= 0 && intSpectrumIndex < mIndexedSpectrumInfoCount)
                    {
                        // Move the binary file reader to .ByteOffsetStart and instantiate an XMLReader at that position
                        mBinaryReader.Position = mIndexedSpectrumInfo[intSpectrumIndex].ByteOffsetStart;
                        UpdateProgress(mBinaryReader.Position / (double)mBinaryReader.Length * 100.0d);

                        // Create a new XmlTextReader
                        using (var reader = XmlReader.Create(mBinaryReader, mXMLReaderSettings))
                        {
                            reader.MoveToContent();
                            mXmlFileReader.SetXMLReaderForSpectrum(reader.ReadSubtree());
                            blnSuccess = mXmlFileReader.ReadNextSpectrum(out objCurrentSpectrumInfo);
                        }

                        if (!string.IsNullOrWhiteSpace(mXmlFileReader.FileVersion))
                        {
                            mFileVersion = mXmlFileReader.FileVersion;
                        }
                    }
                    else
                    {
                        mErrorMessage = "Invalid spectrum index: " + intSpectrumIndex.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in GetSpectrumByIndexWork", ex);
            }

            return blnSuccess;
        }

        public bool GetSpectrumBySpectrumID(int intSpectrumID, out clsSpectrumInfo objSpectrumInfo)
        {
            return GetSpectrumBySpectrumIDWork(intSpectrumID, out objSpectrumInfo, false);
        }

        private bool GetSpectrumBySpectrumIDWork(int intSpectrumID, out clsSpectrumInfo objSpectrumInfo, bool blnHeaderInfoOnly)
        {

            // Returns True if success, False if failure
            // Only valid if we have Indexed data in memory

            var blnSuccess = default(bool);
            objSpectrumInfo = null;

            try
            {
                blnSuccess = false;
                mErrorMessage = string.Empty;
                if (mDataReaderMode == drmDataReaderModeConstants.Cached)
                {
                    mErrorMessage = "Cannot obtain spectrum by spectrum ID when data is cached in memory; only valid when the data is indexed";
                }
                else if (mDataReaderMode == drmDataReaderModeConstants.Indexed)
                {
                    if (GetSpectrumReadyStatus(true))
                    {
                        if (mIndexedSpectraSpectrumIDToIndex.Count == 0)
                        {
                            var loopTo = mIndexedSpectrumInfoCount - 1;
                            for (var intSpectrumIndex = 0; intSpectrumIndex <= loopTo; intSpectrumIndex++)
                            {
                                if (mIndexedSpectrumInfo[intSpectrumIndex].SpectrumID == intSpectrumID)
                                {
                                    blnSuccess = GetSpectrumByIndexWork(intSpectrumIndex, out objSpectrumInfo, blnHeaderInfoOnly);
                                    break;
                                }
                            }
                        }
                        else
                        {
                            // Look for intSpectrumID in mIndexedSpectraSpectrumIDToIndex
                            var index = mIndexedSpectraSpectrumIDToIndex[intSpectrumID];
                            blnSuccess = GetSpectrumByIndexWork(index, out objSpectrumInfo, blnHeaderInfoOnly);
                        }

                        if (!blnSuccess && mErrorMessage.Length == 0)
                        {
                            mErrorMessage = "Invalid spectrum ID: " + intSpectrumID.ToString();
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
                OnErrorEvent("Error in GetSpectrumBySpectrumID", ex);
            }

            return blnSuccess;
        }

        public bool GetSpectrumHeaderInfoBySpectrumID(int intSpectrumID, out clsSpectrumInfo objSpectrumInfo)
        {
            return GetSpectrumBySpectrumIDWork(intSpectrumID, out objSpectrumInfo, true);
        }

        public bool GetSpectrumIDList(out int[] SpectrumIDList)
        {
            // Return the list of indexed spectrumID values

            int intSpectrumIndex;
            var blnSuccess = default(bool);
            try
            {
                blnSuccess = false;
                if (mDataReaderMode == drmDataReaderModeConstants.Cached)
                {
                    // Cannot get the spectrum ID list when mDataReaderMode = Cached
                    SpectrumIDList = new int[0];
                }
                else if (GetSpectrumReadyStatus(true))
                {
                    if (mIndexedSpectrumInfo is null || mIndexedSpectrumInfoCount == 0)
                    {
                        SpectrumIDList = new int[0];
                    }
                    else
                    {
                        SpectrumIDList = new int[mIndexedSpectrumInfoCount];
                        var loopTo = SpectrumIDList.Length - 1;
                        for (intSpectrumIndex = 0; intSpectrumIndex <= loopTo; intSpectrumIndex++)
                            SpectrumIDList[intSpectrumIndex] = mIndexedSpectrumInfo[intSpectrumIndex].SpectrumID;
                        blnSuccess = true;
                    }
                }
                else
                {
                    SpectrumIDList = new int[0];
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in GetSpectrumIDList", ex);
                SpectrumIDList = new int[0];
            }

            return blnSuccess;
        }

        protected override void InitializeLocalVariables()
        {
            base.InitializeLocalVariables();
            mInputFileStatsSpectrumIDMinimum = 0;
            mInputFileStatsSpectrumIDMaximum = 0;
            mXmlFileHeader = string.Empty;
            mAddNewLinesToHeader = true;
            mIndexedSpectraSpectrumIDToIndex.Clear();
        }

        private void InitializeObjectVariables()
        {
            // Note: This form of the RegEx allows the <spectrum element to be followed by a space or present at the end of the line
            mSpectrumStartElementRegEx = InitializeRegEx(SPECTRUM_START_ELEMENT + @"\s+|" + SPECTRUM_START_ELEMENT + "$");
            mSpectrumEndElementRegEx = InitializeRegEx(SPECTRUM_END_ELEMENT);

            // Note: This form of the RegEx allows for the count attribute to occur on a separate line from <spectrumList
            // It also allows for other attributes to be present between <spectrumList and the count attribute
            mSpectrumListRegEx = InitializeRegEx(SPECTRUM_LIST_START_ELEMENT + @"[^/]+count\s*=\s*""([0-9]+)""");

            // Note: This form of the RegEx allows for the id attribute to occur on a separate line from <spectrum
            // It also allows for other attributes to be present between <spectrum and the id attribute
            mSpectrumIDRegEx = InitializeRegEx(SPECTRUM_START_ELEMENT + @"[^/]+id\s*=\s*""([0-9]+)""");

            // Note: This form of the RegEx allows for the acqNumber attribute to occur on a separate line from <acquisition
            // It also allows for other attributes to be present between <acquisition and the acqNumber attribute
            mAcquisitionNumberRegEx = InitializeRegEx(@"<acquisition[^/]+acqNumber\s*=\s*""([0-9]+)""");
            mXMLReaderSettings = new XmlReaderSettings() { IgnoreWhitespace = true };
        }

        protected override bool LoadExistingIndex()
        {
            // Returns True if an existing index is found, False if not
            // mzData files do not have existing indices so always return False
            return false;
        }

        public override bool ReadAndCacheEntireFile()
        {
            // Indexes the location of each of the spectra in the input file

            bool blnSuccess;
            try
            {
                if (mBinaryTextReader is null)
                {
                    blnSuccess = false;
                }
                else
                {
                    mReadingAndStoringSpectra = true;
                    mErrorMessage = string.Empty;
                    ResetProgress("Indexing " + Path.GetFileName(mInputFilePath));

                    // Read and parse the input file to determine:
                    // a) The header XML (text before <spectrumList)
                    // b) The start and end byte offset of each spectrum
                    // (text between "<spectrum" and "</spectrum>")

                    blnSuccess = ReadMZDataFile();
                    mBinaryTextReader.Close();
                    mBinaryTextReader = null;
                    if (blnSuccess)
                    {
                        // Note: Even if we aborted reading the data mid-file, the cached information is still valid
                        if (mAbortProcessing)
                        {
                            mErrorMessage = "Aborted processing";
                        }
                        else
                        {
                            UpdateProgress(100f);
                            OperationComplete();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ReadAndCacheEntireFile", ex);
                blnSuccess = false;
            }
            finally
            {
                mReadingAndStoringSpectra = false;
            }

            return blnSuccess;
        }

        private bool ReadMZDataFile()
        {
            // This function uses the Binary Text Reader to determine
            // the location of the "<spectrum" and "</spectrum>" elements in the .Xml file
            // If mIndexingComplete is already True, then simply returns True

            var lngCurrentSpectrumByteOffsetStart = default(long);
            var lngCurrentSpectrumByteOffsetEnd = default(long);
            bool blnSuccess;
            bool blnSpectrumFound;
            try
            {
                if (mIndexingComplete)
                {
                    return true;
                }

                do
                {
                    if (mCurrentSpectrumInfo is null)
                    {
                        mCurrentSpectrumInfo = new clsSpectrumInfoMzData();
                    }
                    else
                    {
                        mCurrentSpectrumInfo.Clear();
                    }

                    blnSpectrumFound = AdvanceFileReaders(emmElementMatchModeConstants.StartElement);
                    if (blnSpectrumFound)
                    {
                        if (mInFileCurrentCharIndex < 0)
                        {
                            // This shouldn't normally happen
                            lngCurrentSpectrumByteOffsetStart = mBinaryTextReader.CurrentLineByteOffsetStart;
                            OnErrorEvent("Unexpected condition in ReadMZDataFile: mInFileCurrentCharIndex < 0");
                        }
                        else
                        {
                            lngCurrentSpectrumByteOffsetStart = mBinaryTextReader.CurrentLineByteOffsetStart + mInFileCurrentCharIndex * mCharSize;
                        }

                        blnSpectrumFound = AdvanceFileReaders(emmElementMatchModeConstants.EndElement);
                        if (blnSpectrumFound)
                        {
                            if (mCharSize > 1)
                            {
                                lngCurrentSpectrumByteOffsetEnd = mBinaryTextReader.CurrentLineByteOffsetStart + mInFileCurrentCharIndex * mCharSize + (mCharSize - 1);
                            }
                            else
                            {
                                lngCurrentSpectrumByteOffsetEnd = mBinaryTextReader.CurrentLineByteOffsetStart + mInFileCurrentCharIndex;
                            }
                        }
                    }

                    if (blnSpectrumFound)
                    {
                        // Make sure mAddNewLinesToHeader is now false
                        if (mAddNewLinesToHeader)
                        {
                            OnErrorEvent("Unexpected condition in ReadMZDataFile: mAddNewLinesToHeader was True; changing to False");
                            mAddNewLinesToHeader = false;
                        }

                        StoreIndexEntry(mCurrentSpectrumInfo.ScanNumber, lngCurrentSpectrumByteOffsetStart, lngCurrentSpectrumByteOffsetEnd);

                        // Note that StoreIndexEntry will have incremented mIndexedSpectrumInfoCount
                        {
                            ref var withBlock = ref mIndexedSpectrumInfo[mIndexedSpectrumInfoCount - 1];
                            withBlock.SpectrumID = mCurrentSpectrumInfo.SpectrumID;
                            UpdateFileStats(mIndexedSpectrumInfoCount, withBlock.ScanNumber, withBlock.SpectrumID);
                            if (!mIndexedSpectraSpectrumIDToIndex.ContainsKey(withBlock.SpectrumID))
                            {
                                mIndexedSpectraSpectrumIDToIndex.Add(withBlock.SpectrumID, mIndexedSpectrumInfoCount - 1);
                            }
                        }

                        // Update the progress
                        if (mBinaryTextReader.FileLengthBytes > 0L)
                        {
                            UpdateProgress(mBinaryTextReader.CurrentLineByteOffsetEnd / (double)mBinaryTextReader.FileLengthBytes * 100d);
                        }

                        if (mAbortProcessing)
                        {
                            break;
                        }
                    }
                }
                while (blnSpectrumFound);
                blnSuccess = true;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ReadMZDataFile", ex);
                blnSuccess = false;
            }

            return blnSuccess;
        }

        private void UpdateFileStats(int intScanCount, int intScanNumber, int intSpectrumID)
        {
            UpdateFileStats(intScanCount, intScanNumber);
            if (intScanCount <= 1)
            {
                mInputFileStatsSpectrumIDMinimum = intSpectrumID;
                mInputFileStatsSpectrumIDMaximum = intSpectrumID;
            }
            else
            {
                if (intSpectrumID < mInputFileStatsSpectrumIDMinimum)
                {
                    mInputFileStatsSpectrumIDMinimum = intSpectrumID;
                }

                if (intSpectrumID > mInputFileStatsSpectrumIDMaximum)
                {
                    mInputFileStatsSpectrumIDMaximum = intSpectrumID;
                }
            }
        }
    }
}