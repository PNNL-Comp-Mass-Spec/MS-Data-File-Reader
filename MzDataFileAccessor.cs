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
using System.Text.RegularExpressions;
using System.Xml;

// ReSharper disable UnusedMember.Global

namespace MSDataFileReader
{
    /// <summary>
    /// This class can be used to open a .mzData file and index the location
    /// of all of the spectra present.  This does not cache the mass spectra data in
    /// memory, and therefore uses little memory, but once the indexing is complete,
    /// random access to the spectra is possible.  After the indexing is complete, spectra
    /// can be obtained using GetSpectrumByScanNumber or GetSpectrumByIndex
    /// </summary>
    public class MzDataFileAccessor : MsDataFileAccessorBaseClass
    {
        public MzDataFileAccessor()
        {
            InitializeObjectVariables();
            InitializeLocalVariables();
        }

        ~MzDataFileAccessor()
        {
            mXmlFileReader = null;
        }

        // ReSharper disable UnusedMember.Local

        private const string SPECTRUM_LIST_START_ELEMENT = "<spectrumList";

        private const string SPECTRUM_LIST_END_ELEMENT = "</spectrumList>";

        private const string SPECTRUM_START_ELEMENT = "<spectrum";

        private const string SPECTRUM_END_ELEMENT = "</spectrum>";

        private const string MZDATA_START_ELEMENT = "<mzData";

        private const string MZDATA_END_ELEMENT = "</mzData>";

        // ReSharper restore UnusedMember.Local

        private MzDataFileReader mXmlFileReader;

        private SpectrumInfoMzData mCurrentSpectrumInfo;

        private int mInputFileStatsSpectrumIDMinimum;

        private int mInputFileStatsSpectrumIDMaximum;

        // ReSharper disable once NotAccessedField.Local
        private string mXmlFileHeader;

        private bool mAddNewLinesToHeader;

        private Regex mSpectrumStartElementRegEx;

        private Regex mSpectrumEndElementRegEx;

        private Regex mSpectrumListRegEx;

        private Regex mAcquisitionNumberRegEx;

        private Regex mSpectrumIDRegEx;

        // This dictionary maps spectrum ID to index in mCachedSpectra()
        // If more than one spectrum has the same spectrum ID, tracks the first one read
        private readonly Dictionary<int, int> mIndexedSpectraSpectrumIDToIndex = new();

        private XmlReaderSettings mXMLReaderSettings;

        public int CachedSpectraSpectrumIDMinimum => mInputFileStatsSpectrumIDMinimum;

        public int CachedSpectraSpectrumIDMaximum => mInputFileStatsSpectrumIDMaximum;

        public override bool ParseFilesWithUnknownVersion
        {
            get => base.ParseFilesWithUnknownVersion;

            set
            {
                base.ParseFilesWithUnknownVersion = value;

                if (mXmlFileReader != null)
                {
                    mXmlFileReader.ParseFilesWithUnknownVersion = value;
                }
            }
        }

        /// <summary>
        /// Use the binary reader to look for strTextToFind
        /// </summary>
        /// <param name="eElementMatchMode"></param>
        /// <returns>True if successful, false if an error</returns>
        protected override bool AdvanceFileReaders(emmElementMatchModeConstants eElementMatchMode)
        {
            bool blnMatchFound;
            var lngByteOffsetForRewind = 0L;
            var blnLookForScanCountOnNextRead = false;
            var strScanCountSearchText = string.Empty;

            try
            {
                mInFileCurrentLineText ??= string.Empty;

                var strInFileCurrentLineSubstring = string.Empty;
                var blnAppendingText = false;
                var strAcqNumberSearchText = string.Empty;
                var blnAcqNumberFound = false;
                blnMatchFound = false;

                while (!mAbortProcessing)
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

                        int intCharIndex;

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

                        Match objMatch;

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
                                        // Ignore errors here
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
                                        // Ignore errors here
                                    }
                                }
                            }
                        }

                        // Look for the appropriate search text in mInFileCurrentLineText, starting at mInFileCurrentCharIndex + 1
                        switch (eElementMatchMode)
                        {
                            case emmElementMatchModeConstants.StartElement:
                                objMatch = mSpectrumStartElementRegEx.Match(strInFileCurrentLineSubstring);
                                break;

                            case emmElementMatchModeConstants.EndElement:
                                objMatch = mSpectrumEndElementRegEx.Match(strInFileCurrentLineSubstring);
                                break;

                            default:
                                // Unknown mode
                                OnErrorEvent("Unknown mode for eElementMatchMode in AdvanceFileReaders: {0}", eElementMatchMode);
                                return false;
                        }

                        if (objMatch.Success)
                        {
                            // Match Found
                            blnMatchFound = true;
                            intCharIndex = objMatch.Index + 1 + mInFileCurrentCharIndex;

                            switch (eElementMatchMode)
                            {
                                case emmElementMatchModeConstants.StartElement:
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
                                                // Ignore errors here
                                            }
                                        }
                                    }

                                    // Could not find the id attribute
                                    // If strInFileCurrentLineSubstring does not contain SPECTRUM_END_ELEMENT,
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

                                    break;

                                case emmElementMatchModeConstants.EndElement:
                                    // Move to the end of the element
                                    intCharIndex += objMatch.Value.Length - 1;

                                    if (intCharIndex >= mInFileCurrentLineText.Length)
                                    {
                                        // This shouldn't happen
                                        OnErrorEvent("Unexpected condition in AdvanceFileReaders: intCharIndex >= mInFileCurrentLineText.Length");
                                        intCharIndex = mInFileCurrentLineText.Length - 1;
                                    }

                                    break;

                                default:
                                    throw new ArgumentOutOfRangeException(nameof(eElementMatchMode), eElementMatchMode, null);
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

        protected override bool GetSpectrumByIndexWork(int intSpectrumIndex, out SpectrumInfo objCurrentSpectrumInfo, bool blnHeaderInfoOnly)
        {
            objCurrentSpectrumInfo = null;

            try
            {
                if (!GetSpectrumReadyStatus(true))
                {
                    return false;
                }

                mXmlFileReader ??= new MzDataFileReader
                {
                    ParseFilesWithUnknownVersion = mParseFilesWithUnknownVersion
                };

                if (mIndexedSpectrumInfoCount == 0)
                {
                    mErrorMessage = "Indexed data not in memory";
                    return false;
                }

                if (intSpectrumIndex < 0 || intSpectrumIndex >= mIndexedSpectrumInfoCount)
                {
                    mErrorMessage = "Invalid spectrum index: " + intSpectrumIndex;
                    return false;
                }

                // Move the binary file reader to .ByteOffsetStart and instantiate an XMLReader at that position
                mBinaryReader.Position = mIndexedSpectrumInfo[intSpectrumIndex].ByteOffsetStart;
                UpdateProgress(mBinaryReader.Position / (double)mBinaryReader.Length * 100.0d);

                bool success;

                // Create a new XmlTextReader
                using (var reader = XmlReader.Create(mBinaryReader, mXMLReaderSettings))
                {
                    reader.MoveToContent();
                    mXmlFileReader.SetXMLReaderForSpectrum(reader.ReadSubtree());
                    success = mXmlFileReader.ReadNextSpectrum(out objCurrentSpectrumInfo);
                }

                if (!string.IsNullOrWhiteSpace(mXmlFileReader.FileVersion))
                {
                    mFileVersion = mXmlFileReader.FileVersion;
                }

                return success;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in GetSpectrumByIndexWork", ex);
                return false;
            }
        }

        /// <summary>
        /// Obtain a spectrum by spectrum ID
        /// </summary>
        /// <remarks>
        /// Only valid if we have Indexed data in memory
        /// </remarks>
        /// <param name="intSpectrumID"></param>
        /// <param name="objSpectrumInfo"></param>
        /// <returns>True if successful, false if an error or invalid spectrum ID</returns>
        public bool GetSpectrumBySpectrumID(int intSpectrumID, out SpectrumInfo objSpectrumInfo)
        {
            return GetSpectrumBySpectrumIDWork(intSpectrumID, out objSpectrumInfo, false);
        }

        /// <summary>
        /// Obtain a spectrum by spectrum ID
        /// </summary>
        /// <remarks>
        /// Only valid if we have Indexed data in memory
        /// </remarks>
        /// <param name="intSpectrumID"></param>
        /// <param name="objSpectrumInfo"></param>
        /// <param name="blnHeaderInfoOnly"></param>
        /// <returns>True if successful, false if an error or invalid spectrum ID</returns>
        private bool GetSpectrumBySpectrumIDWork(int intSpectrumID, out SpectrumInfo objSpectrumInfo, bool blnHeaderInfoOnly)
        {
            objSpectrumInfo = null;

            try
            {
                mErrorMessage = string.Empty;

                if (mDataReaderMode == drmDataReaderModeConstants.Cached)
                {
                    mErrorMessage = "Cannot obtain spectrum by spectrum ID when data is cached in memory; only valid when the data is indexed";
                    return false;
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

                if (mIndexedSpectraSpectrumIDToIndex.Count == 0)
                {
                    var intIndexEnd = mIndexedSpectrumInfoCount - 1;

                    for (var intSpectrumIndex = 0; intSpectrumIndex <= intIndexEnd; intSpectrumIndex++)
                    {
                        if (mIndexedSpectrumInfo[intSpectrumIndex].SpectrumID == intSpectrumID)
                        {
                            success = GetSpectrumByIndexWork(intSpectrumIndex, out objSpectrumInfo, blnHeaderInfoOnly);
                            break;
                        }
                    }
                }
                else
                {
                    // Look for intSpectrumID in mIndexedSpectraSpectrumIDToIndex
                    var index = mIndexedSpectraSpectrumIDToIndex[intSpectrumID];
                    success = GetSpectrumByIndexWork(index, out objSpectrumInfo, blnHeaderInfoOnly);
                }

                if (!success && string.IsNullOrWhiteSpace(mErrorMessage))
                {
                    mErrorMessage = "Invalid spectrum ID: " + intSpectrumID.ToString();
                }

                return success;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in GetSpectrumBySpectrumID", ex);
                return false;
            }
        }

        public bool GetSpectrumHeaderInfoBySpectrumID(int intSpectrumID, out SpectrumInfo objSpectrumInfo)
        {
            return GetSpectrumBySpectrumIDWork(intSpectrumID, out objSpectrumInfo, true);
        }

        /// <summary>
        /// Obtain the list of indexed spectrumID values
        /// </summary>
        /// <param name="SpectrumIDList"></param>
        /// <returns>True if successful, false if an error</returns>
        public bool GetSpectrumIDList(out int[] SpectrumIDList)
        {
            try
            {
                if (mDataReaderMode == drmDataReaderModeConstants.Cached)
                {
                    // Cannot get the spectrum ID list when mDataReaderMode = Cached
                    SpectrumIDList = Array.Empty<int>();
                }
                else if (GetSpectrumReadyStatus(true))
                {
                    if (mIndexedSpectrumInfo is null || mIndexedSpectrumInfoCount == 0)
                    {
                        SpectrumIDList = Array.Empty<int>();
                    }
                    else
                    {
                        SpectrumIDList = new int[mIndexedSpectrumInfoCount];
                        var intIndexEnd = SpectrumIDList.Length - 1;

                        for (var intSpectrumIndex = 0; intSpectrumIndex <= intIndexEnd; intSpectrumIndex++)
                        {
                            SpectrumIDList[intSpectrumIndex] = mIndexedSpectrumInfo[intSpectrumIndex].SpectrumID;
                        }

                        return true;
                    }
                }
                else
                {
                    SpectrumIDList = Array.Empty<int>();
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in GetSpectrumIDList", ex);
                SpectrumIDList = Array.Empty<int>();
            }

            return false;
        }

        protected sealed override void InitializeLocalVariables()
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

        /// <summary>
        /// Load the spectrum index from the data file
        /// </summary>
        /// <remarks>
        /// Returns True if an existing index is found, False if not
        /// </remarks>
        /// <returns>Always returns false since mzData files to not have a spectrum index</returns>
        protected override bool LoadExistingIndex()
        {
            return false;
        }

        /// <summary>
        /// Index the location of each of the spectra in the input file
        /// </summary>
        /// <returns>True if successful, false if an error</returns>
        public override bool ReadAndCacheEntireFile()
        {
            try
            {
                if (mBinaryTextReader is null)
                {
                    return false;
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

                    var success = ReadMZDataFile();
                    mBinaryTextReader.Close();
                    mBinaryTextReader = null;

                    if (!success)
                        return false;

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

                    return true;
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ReadAndCacheEntireFile", ex);
                return false;
            }
            finally
            {
                mReadingAndStoringSpectra = false;
            }
        }

        /// <summary>
        /// Use the binary reader to determine the location of the spectrum elements in the .xml file
        /// </summary>
        /// <remarks>
        /// Returns true if mIndexingComplete is already true
        /// </remarks>
        /// <returns>True if successful, false if an error</returns>
        private bool ReadMZDataFile()
        {
            var lngCurrentSpectrumByteOffsetStart = 0L;
            var lngCurrentSpectrumByteOffsetEnd = 0L;

            try
            {
                if (mIndexingComplete)
                {
                    return true;
                }

                bool blnSpectrumFound;
                do
                {
                    if (mCurrentSpectrumInfo is null)
                    {
                        mCurrentSpectrumInfo = new SpectrumInfoMzData();
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

                    if (!blnSpectrumFound)
                        continue;

                    // Make sure mAddNewLinesToHeader is now false
                    if (mAddNewLinesToHeader)
                    {
                        OnErrorEvent("Unexpected condition in ReadMZDataFile: mAddNewLinesToHeader was True; changing to False");
                        mAddNewLinesToHeader = false;
                    }

                    StoreIndexEntry(mCurrentSpectrumInfo.ScanNumber, lngCurrentSpectrumByteOffsetStart, lngCurrentSpectrumByteOffsetEnd);

                    // Note that StoreIndexEntry will have incremented mIndexedSpectrumInfoCount
                    mIndexedSpectrumInfo[mIndexedSpectrumInfoCount - 1].SpectrumID = mCurrentSpectrumInfo.SpectrumID;
                    UpdateFileStats(mIndexedSpectrumInfoCount, mIndexedSpectrumInfo[mIndexedSpectrumInfoCount - 1].ScanNumber,
                        mIndexedSpectrumInfo[mIndexedSpectrumInfoCount - 1].SpectrumID);

                    if (!mIndexedSpectraSpectrumIDToIndex.ContainsKey(mIndexedSpectrumInfo[mIndexedSpectrumInfoCount - 1].SpectrumID))
                    {
                        mIndexedSpectraSpectrumIDToIndex.Add(mIndexedSpectrumInfo[mIndexedSpectrumInfoCount - 1].SpectrumID,
                            mIndexedSpectrumInfoCount - 1);
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
                while (blnSpectrumFound);

                return true;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ReadMZDataFile", ex);
                return false;
            }
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