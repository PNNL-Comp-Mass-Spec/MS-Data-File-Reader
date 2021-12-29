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
using System.Text.RegularExpressions;
using System.Xml;

namespace MSDataFileReader
{
    /// <summary>
    /// This class can be used to open a .mzXML file and index the location
    /// of all of the spectra present.  This does not cache the mass spectra data in
    /// memory, and therefore uses little memory, but once the indexing is complete,
    /// random access to the spectra is possible.  After the indexing is complete, spectra
    /// can be obtained using GetSpectrumByScanNumber or GetSpectrumByIndex
    /// </summary>
    public class clsMzXMLFileAccessor : clsMSDataFileAccessorBaseClass
    {
        public clsMzXMLFileAccessor()
        {
            InitializeObjectVariables();
            InitializeLocalVariables();
        }

        ~clsMzXMLFileAccessor()
        {
            mXmlFileReader = null;
        }

        private const string MSRUN_START_ELEMENT = "<msRun";

        private const string MSRUN_END_ELEMENT = "</msRun>";

        private const string SCAN_START_ELEMENT = "<scan";

        public const string SCAN_END_ELEMENT = "</scan>";

        private const string PEAKS_END_ELEMENT = "</peaks>";

        private const string MZXML_START_ELEMENT = "<mzXML";

        private const string MZXML_END_ELEMENT = "</mzXML>";

        private const string INDEX_OFFSET_ELEMENT_NAME = "indexOffset";

        private const string INDEX_OFFSET_START_ELEMENT = "<" + INDEX_OFFSET_ELEMENT_NAME;

        private const string INDEX_OFFSET_END_ELEMENT = "</indexOffset>";

        private const string INDEX_ELEMENT_NAME = "index";

        private const string INDEX_START_ELEMENT = "<" + INDEX_ELEMENT_NAME;

        private const string INDEX_END_ELEMENT = "</index>";

        private const string INDEX_ATTRIBUTE_NAME = "name";

        private const string OFFSET_ELEMENT_NAME = "offset";

        private const string OFFSET_ATTRIBUTE_ID = "id";

        private clsMzXMLFileReader mXmlFileReader;

        private clsSpectrumInfoMzXML mCurrentSpectrumInfo;

        private string mXmlFileHeader;

        private bool mAddNewLinesToHeader;

        private bool mMSRunFound;

        private bool mIgnoreEmbeddedIndex;

        private Regex mMSRunRegEx;

        private Regex mScanStartElementRegEx;

        private Regex mPeaksEndElementRegEx;

        private Regex mScanNumberRegEx;

        private XmlReaderSettings mXMLReaderSettings;

        public bool IgnoreEmbeddedIndex
        {
            get
            {
                return mIgnoreEmbeddedIndex;
            }

            set
            {
                mIgnoreEmbeddedIndex = value;
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

        /// <summary>
        /// Use the binary reader to look for the given element type (as specified by eElementMatchMode)
        /// </summary>
        /// <param name="eElementMatchMode"></param>
        /// <returns>True if successful, false if an error</returns>
        protected override bool AdvanceFileReaders(emmElementMatchModeConstants eElementMatchMode)
        {
            bool blnMatchFound;
            var lngByteOffsetForRewind = default(long);

            try
            {
                if (mInFileCurrentLineText is null)
                {
                    mInFileCurrentLineText = string.Empty;
                }

                var strInFileCurrentLineSubstring = string.Empty;
                var blnAppendingText = false;
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

                        int intCharIndex;

                        if (mAddNewLinesToHeader)
                        {
                            // We haven't yet found the first scan; look for "<scan"
                            intCharIndex = mInFileCurrentLineText.IndexOf(SCAN_START_ELEMENT, mInFileCurrentCharIndex + 1, StringComparison.Ordinal);

                            if (intCharIndex >= 0)
                            {
                                // Only add a portion of mInFileCurrentLineText to mXmlFileHeader
                                // since it contains SCAN_START_ELEMENT

                                if (intCharIndex > 0)
                                {
                                    mXmlFileHeader += mInFileCurrentLineText.Substring(0, intCharIndex);
                                }

                                mAddNewLinesToHeader = false;
                                mMSRunFound = true;
                                UpdateXmlFileHeaderScanCount(ref mXmlFileHeader);
                            }
                            else
                            {
                                // Append mInFileCurrentLineText to mXmlFileHeader
                                mXmlFileHeader += mInFileCurrentLineText + Environment.NewLine;
                            }
                        }

                        Match objMatch;

                        if (!mMSRunFound)
                        {
                            // We haven't yet found msRun; look for "<msRun" and the Scan Count value
                            objMatch = mMSRunRegEx.Match(mXmlFileHeader);

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

                                mMSRunFound = true;
                            }
                        }

                        // Look for the appropriate search text in mInFileCurrentLineText, starting at mInFileCurrentCharIndex + 1
                        switch (eElementMatchMode)
                        {
                            case emmElementMatchModeConstants.StartElement:
                                objMatch = mScanStartElementRegEx.Match(strInFileCurrentLineSubstring);
                                break;

                            case emmElementMatchModeConstants.EndElement:
                                // Since mzXml files can have scans embedded within another scan, we'll look for </peaks>
                                // rather than looking for </scan>
                                objMatch = mPeaksEndElementRegEx.Match(strInFileCurrentLineSubstring);
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
                                {
                                    // Look for the scan number after <scan
                                    objMatch = mScanNumberRegEx.Match(strInFileCurrentLineSubstring);

                                    if (objMatch.Success)
                                    {
                                        if (objMatch.Groups.Count > 1)
                                        {
                                            try
                                            {
                                                mCurrentSpectrumInfo.ScanNumber = int.Parse(objMatch.Groups[1].Captures[0].Value);
                                            }
                                            catch (Exception ex)
                                            {
                                                // Ignore errors here
                                            }
                                        }
                                    }

                                    // Could not find the num attribute
                                    // If strInFileCurrentLineSubstring does not contain PEAKS_END_ELEMENT,
                                    // set blnAppendingText to True and continue reading
                                    else if (strInFileCurrentLineSubstring.IndexOf(PEAKS_END_ELEMENT, StringComparison.Ordinal) < 0)
                                    {
                                        blnMatchFound = false;

                                        if (!blnAppendingText)
                                        {
                                            blnAppendingText = true;
                                            // Record the byte offset of the start of the current line
                                            // We will use this offset to "rewind" the file pointer once the num attribute is found
                                            lngByteOffsetForRewind = mBinaryTextReader.CurrentLineByteOffsetStart;
                                        }
                                    }

                                    break;
                                }

                                case emmElementMatchModeConstants.EndElement:
                                {
                                    // Move to the end of the element
                                    intCharIndex += objMatch.Value.Length - 1;

                                    if (intCharIndex >= mInFileCurrentLineText.Length)
                                    {
                                        // This shouldn't happen
                                        OnErrorEvent("Unexpected condition in AdvanceFileReaders: intCharIndex >= mInFileCurrentLineText.Length");
                                        intCharIndex = mInFileCurrentLineText.Length - 1;
                                    }

                                    break;
                                }

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

        /// <summary>
        /// Look for the number following the indexOffset tag
        /// </summary>
        /// <param name="strTextStream"></param>
        /// <returns>Byte offset if found, otherwise 0</returns>
        private long ExtractIndexOffsetFromTextStream(string strTextStream)
        {
            var lngIndexOffset = default(long);

            try
            {
                // Look for <indexOffset in strTextStream
                var intMatchIndex = strTextStream.IndexOf(INDEX_OFFSET_START_ELEMENT, StringComparison.Ordinal);

                if (intMatchIndex >= 0)
                {
                    // Look for the next >
                    intMatchIndex = strTextStream.IndexOf('>', intMatchIndex + 1);

                    if (intMatchIndex >= 0)
                    {
                        // Remove the leading text
                        strTextStream = strTextStream.Substring(intMatchIndex + 1);

                        // Look for the next <
                        intMatchIndex = strTextStream.IndexOf('<');

                        if (intMatchIndex >= 0)
                        {
                            strTextStream = strTextStream.Substring(0, intMatchIndex);

                            // Try to convert strTextStream to a number
                            try
                            {
                                lngIndexOffset = int.Parse(strTextStream);
                            }
                            catch (Exception ex)
                            {
                                lngIndexOffset = 0L;
                            }

                            if (lngIndexOffset == 0L)
                            {
                                // Number conversion failed; probably have carriage returns in the text
                                // Look for the next number in strTextStream
                                var loopTo = strTextStream.Length - 1;
                                int intIndex;
                                for (intIndex = 0; intIndex <= loopTo; intIndex++)
                                {
                                    if (IsNumber(strTextStream[intIndex].ToString()))
                                    {
                                        // First number found
                                        var strNumber = strTextStream[intIndex].ToString();

                                        // Append any additional numbers to strNumber
                                        while (intIndex + 1 < strTextStream.Length)
                                        {
                                            intIndex += 1;

                                            if (IsNumber(strTextStream[intIndex].ToString()))
                                            {
                                                strNumber += strTextStream[intIndex].ToString();
                                            }
                                            else
                                            {
                                                break;
                                            }
                                        }

                                        try
                                        {
                                            lngIndexOffset = int.Parse(strNumber);
                                        }
                                        catch (Exception ex)
                                        {
                                            lngIndexOffset = 0L;
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
                OnErrorEvent("Error in ExtractIndexOffsetFromTextStream", ex);
                lngIndexOffset = 0L;
            }

            return lngIndexOffset;
        }

        protected override bool GetSpectrumByIndexWork(int intSpectrumIndex, out clsSpectrumInfo objCurrentSpectrumInfo, bool blnHeaderInfoOnly)
        {
            objCurrentSpectrumInfo = null;

            try
            {
                if (!GetSpectrumReadyStatus(true))
                {
                    return false;
                }

                if (mXmlFileReader is null)
                {
                    mXmlFileReader = new clsMzXMLFileReader() { ParseFilesWithUnknownVersion = mParseFilesWithUnknownVersion };
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
                else if (string.IsNullOrWhiteSpace(mFileVersion) && !string.IsNullOrWhiteSpace(mXmlFileHeader))
                {
                    if (!clsMzXMLFileReader.ExtractMzXmlFileVersion(mXmlFileHeader, out mFileVersion))
                    {
                        OnErrorEvent("Unknown mzXML file version; expected text not found in mXmlFileHeader");
                    }
                }

                return success;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in GetSpectrumByIndexWork", ex);
                return false;
            }
        }

        protected override void InitializeLocalVariables()
        {
            base.InitializeLocalVariables();
            mXmlFileHeader = string.Empty;
            mAddNewLinesToHeader = true;
            mMSRunFound = false;
        }

        private void InitializeObjectVariables()
        {
            // Note: This form of the RegEx allows the <scan element to be followed by a space or present at the end of the line
            mScanStartElementRegEx = InitializeRegEx(SCAN_START_ELEMENT + @"\s+|" + SCAN_START_ELEMENT + "$");
            mPeaksEndElementRegEx = InitializeRegEx(PEAKS_END_ELEMENT);

            // Note: This form of the RegEx allows for the scanCount attribute to occur on a separate line from <msRun
            // It also allows for other attributes to be present between <msRun and the scanCount attribute
            mMSRunRegEx = InitializeRegEx(MSRUN_START_ELEMENT + @"[^/]+scanCount\s*=\s*""([0-9]+)""");

            // Note: This form of the RegEx allows for the num attribute to occur on a separate line from <scan
            // It also allows for other attributes to be present between <scan and the num attribute
            mScanNumberRegEx = InitializeRegEx(SCAN_START_ELEMENT + @"[^/]+num\s*=\s*""([0-9]+)""");
            mXMLReaderSettings = new XmlReaderSettings() { IgnoreWhitespace = true };
        }

        /// <summary>
        /// Load the spectrum index from the data file
        /// </summary>
        /// <remarks>
        /// Use the mBinaryTextReader to jump to the end of the file and read the data line-by-line backward
        /// looking for the indexOffset element
        /// </remarks>
        /// <returns>True if index elements are successfully loaded, otherwise false</returns>
        protected override bool LoadExistingIndex()
        {
            var blnExtractTextToEOF = false;
            bool blnIndexLoaded;

            try
            {
                if (mIgnoreEmbeddedIndex)
                {
                    return false;
                }

                blnIndexLoaded = false;

                // Move to the end of the file
                mBinaryTextReader.MoveToEnd();

                while (mBinaryTextReader.ReadLine(clsBinaryTextReader.ReadDirectionConstants.Reverse))
                {
                    var strCurrentLine = mBinaryTextReader.CurrentLine;
                    var intCharIndex = strCurrentLine.IndexOf(INDEX_OFFSET_START_ELEMENT, StringComparison.Ordinal);
                    var intCharIndexEnd = strCurrentLine.IndexOf(INDEX_OFFSET_END_ELEMENT, intCharIndex + INDEX_OFFSET_START_ELEMENT.Length, StringComparison.Ordinal);

                    if (intCharIndex >= 0)
                    {
                        // The offset to the index has been specified
                        // Parse out the number between <indexOffset> and </indexOffset>
                        // (normally on the same line, though this code can handle white space between the tags)

                        var lngByteOffsetSaved = mBinaryTextReader.CurrentLineByteOffsetStart + intCharIndex * mBinaryTextReader.CharSize;

                        if (intCharIndexEnd <= 0)
                        {
                            // Need to read the next few lines to find </indexOffset>
                            mBinaryTextReader.MoveToByteOffset(mBinaryTextReader.CurrentLineByteOffsetEndWithTerminator + 1L);

                            while (mBinaryTextReader.ReadLine(clsBinaryTextReader.ReadDirectionConstants.Forward))
                            {
                                strCurrentLine += " " + mBinaryTextReader.CurrentLine;
                                intCharIndexEnd = strCurrentLine.IndexOf(INDEX_OFFSET_END_ELEMENT, intCharIndex + INDEX_OFFSET_START_ELEMENT.Length, StringComparison.Ordinal);

                                if (intCharIndexEnd > 0)
                                {
                                    break;
                                }
                            }
                        }

                        if (intCharIndexEnd > 0)
                        {
                            var lngIndexOffset = ExtractIndexOffsetFromTextStream(strCurrentLine);

                            if (lngIndexOffset > 0L)
                            {
                                // Move the binary reader to lngIndexOffset
                                mBinaryTextReader.MoveToByteOffset(lngIndexOffset);

                                // Read the text at offset lngIndexOffset
                                mBinaryTextReader.ReadLine(clsBinaryTextReader.ReadDirectionConstants.Forward);
                                strCurrentLine = mBinaryTextReader.CurrentLine;

                                // Verify that strCurrentLine contains "<index"
                                if (strCurrentLine.IndexOf(INDEX_START_ELEMENT, StringComparison.Ordinal) >= 0)
                                {
                                    strCurrentLine = MZXML_START_ELEMENT + ">" + Environment.NewLine + strCurrentLine;
                                    blnExtractTextToEOF = true;
                                }
                                else
                                {
                                    // Corrupt index offset value; move back to byte offset
                                }
                            }
                        }

                        if (!blnExtractTextToEOF)
                        {
                            // Move the reader back to byte lngByteOffsetSaved
                            mBinaryTextReader.MoveToByteOffset(lngByteOffsetSaved);
                            strCurrentLine = string.Empty;
                        }
                    }

                    if (!blnExtractTextToEOF)
                    {
                        intCharIndex = strCurrentLine.IndexOf(MSRUN_END_ELEMENT, StringComparison.Ordinal);

                        if (intCharIndex >= 0)
                        {
                            // </msRun> element found
                            // Extract the text from here to the end of the file and parse with ParseMzXMLOffsetIndex
                            blnExtractTextToEOF = true;
                            strCurrentLine = MZXML_START_ELEMENT + ">";
                        }
                    }

                    if (blnExtractTextToEOF)
                    {
                        var objStringBuilder = new StringBuilder() { Length = 0 };

                        if (strCurrentLine.Length > 0)
                        {
                            objStringBuilder.Append(strCurrentLine + mBinaryTextReader.CurrentLineTerminator);
                        }

                        // Read all of the lines to the end of the file
                        while (mBinaryTextReader.ReadLine(clsBinaryTextReader.ReadDirectionConstants.Forward))
                        {
                            strCurrentLine = mBinaryTextReader.CurrentLine;
                            objStringBuilder.Append(strCurrentLine + mBinaryTextReader.CurrentLineTerminator);
                        }

                        blnIndexLoaded = ParseMzXMLOffsetIndex(objStringBuilder.ToString());

                        if (blnIndexLoaded)
                        {
                            // Validate the first entry of the index to make sure the index is valid

                            // For now, set blnIndexLoaded to False
                            // If the test read works, we'll set blnIndexLoaded to True
                            blnIndexLoaded = false;

                            if (mIndexedSpectrumInfoCount > 0)
                            {
                                // Set up the default error message
                                mErrorMessage = "Index embedded in the input file (" + Path.GetFileName(mInputFilePath) + ") is corrupt: first byte offset (" + mIndexedSpectrumInfo[0].ByteOffsetStart.ToString() + ") does not point to a " + SCAN_START_ELEMENT + " element";
                                var strExtractedText = ExtractTextBetweenOffsets(mInputFilePath, mIndexedSpectrumInfo[0].ByteOffsetStart, mIndexedSpectrumInfo[0].ByteOffsetEnd);

                                if (!string.IsNullOrEmpty(strExtractedText))
                                {
                                    // Make sure the first text in strExtractedText is <scan
                                    var intStartElementIndex = strExtractedText.IndexOf(SCAN_START_ELEMENT, StringComparison.Ordinal);

                                    if (intStartElementIndex >= 0)
                                    {
                                        var intFirstBracketIndex = strExtractedText.IndexOf('<');

                                        if (intFirstBracketIndex == intStartElementIndex)
                                        {
                                            blnIndexLoaded = true;
                                            mErrorMessage = string.Empty;
                                        }
                                    }
                                }
                            }
                        }

                        if (blnIndexLoaded)
                        {
                            // Move back to the beginning of the file and extract the header tags
                            mBinaryTextReader.MoveToBeginning();
                            mXmlFileHeader = string.Empty;

                            while (mBinaryTextReader.ReadLine(clsBinaryTextReader.ReadDirectionConstants.Forward))
                            {
                                strCurrentLine = mBinaryTextReader.CurrentLine;
                                intCharIndex = strCurrentLine.IndexOf(SCAN_START_ELEMENT, StringComparison.Ordinal);

                                if (intCharIndex >= 0)
                                {
                                    // SCAN_START_ELEMENT found
                                    if (intCharIndex > 0)
                                    {
                                        // Only add a portion of strCurrentLine to mXmlFileHeader
                                        // since it contains SCAN_START_ELEMENT
                                        mXmlFileHeader += strCurrentLine.Substring(0, intCharIndex);
                                    }

                                    break;
                                }
                                else
                                {
                                    // Append strCurrentLine to mXmlFileHeader
                                    mXmlFileHeader += strCurrentLine + Environment.NewLine;
                                }
                            }
                        }
                        else
                        {
                            // Index not loaded (or not valid)

                            if (!string.IsNullOrEmpty(mErrorMessage))
                            {
                                OnErrorEvent("Error in LoadExistingIndex: {0}", mErrorMessage);
                            }

                            if (mIndexedSpectrumInfoCount > 0)
                            {
                                // Reset the indexed spectrum info
                                mIndexedSpectrumInfoCount = 0;
                                mIndexedSpectrumInfo = new udtIndexedSpectrumInfoType[1000];
                            }
                        }

                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in LoadExistingIndex", ex);
                blnIndexLoaded = false;
            }

            return blnIndexLoaded;
        }

        private bool ParseMzXMLOffsetIndex(string strTextStream)
        {
            var blnIndexLoaded = false;

            try
            {
                var blnParseIndexValues = true;
                var intCurrentScanNumber = -1;
                var intPreviousScanNumber = -1;
                long lngCurrentScanByteOffsetStart = -1;
                var strCurrentElement = string.Empty;

                using (var objXMLReader = new XmlTextReader(new StringReader(strTextStream)))
                {
                    // Skip all whitespace
                    objXMLReader.WhitespaceHandling = WhitespaceHandling.None;
                    var validData = true;

                    while (validData && objXMLReader.ReadState == ReadState.Initial || objXMLReader.ReadState == ReadState.Interactive)
                    {
                        validData = objXMLReader.Read();

                        if (validData && objXMLReader.ReadState == ReadState.Interactive)
                        {
                            if (objXMLReader.NodeType == XmlNodeType.Element)
                            {
                                strCurrentElement = objXMLReader.Name;

                                if ((strCurrentElement ?? "") == INDEX_ELEMENT_NAME)
                                {
                                    if (objXMLReader.HasAttributes)
                                    {
                                        // Validate that this is the "scan" index

                                        string strValue;

                                        try
                                        {
                                            strValue = objXMLReader.GetAttribute(INDEX_ATTRIBUTE_NAME);
                                        }
                                        catch (Exception ex)
                                        {
                                            strValue = string.Empty;
                                        }

                                        if (!string.IsNullOrEmpty(strValue))
                                        {
                                            if (strValue == "scan")
                                            {
                                                blnParseIndexValues = true;
                                            }
                                            else
                                            {
                                                blnParseIndexValues = false;
                                            }
                                        }
                                    }
                                }
                                else if ((strCurrentElement ?? "") == OFFSET_ELEMENT_NAME)
                                {
                                    if (blnParseIndexValues && objXMLReader.HasAttributes)
                                    {
                                        // Extract the scan number from the id attribute
                                        try
                                        {
                                            intPreviousScanNumber = intCurrentScanNumber;
                                            intCurrentScanNumber = int.Parse(objXMLReader.GetAttribute(OFFSET_ATTRIBUTE_ID));
                                        }
                                        catch (Exception ex)
                                        {
                                            // Index is corrupted (or of an unknown format); do not continue parsing
                                            break;
                                        }
                                    }
                                }
                            }
                            else if (objXMLReader.NodeType == XmlNodeType.EndElement)
                            {
                                if (blnParseIndexValues && (objXMLReader.Name ?? "") == INDEX_ELEMENT_NAME)
                                {
                                    // Store the final index value
                                    // This is tricky since we don't know the ending offset for the given scan
                                    // Thus, need to use the binary text reader to jump to lngCurrentScanByteOffsetStart and then read line-by-line until the next </peaks> tag is found
                                    StoreFinalIndexEntry(intCurrentScanNumber, lngCurrentScanByteOffsetStart);
                                    blnIndexLoaded = true;
                                    break;
                                }

                                strCurrentElement = string.Empty;
                            }
                            else if (objXMLReader.NodeType == XmlNodeType.Text)
                            {
                                if (blnParseIndexValues && (strCurrentElement ?? "") == OFFSET_ELEMENT_NAME)
                                {
                                    if (objXMLReader.NodeType != XmlNodeType.Whitespace && objXMLReader.HasValue)
                                    {
                                        try
                                        {
                                            var lngPreviousScanByteOffsetStart = lngCurrentScanByteOffsetStart;
                                            lngCurrentScanByteOffsetStart = long.Parse(objXMLReader.Value);

                                            if (lngPreviousScanByteOffsetStart >= 0L && intCurrentScanNumber >= 0)
                                            {
                                                // Store the previous scan info
                                                StoreIndexEntry(intPreviousScanNumber, lngPreviousScanByteOffsetStart, lngCurrentScanByteOffsetStart - 1L);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            // Index is corrupted (or of an unknown format); do not continue parsing
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ParseMzXMLOffsetIndex", ex);
                blnIndexLoaded = false;
            }

            return blnIndexLoaded;
        }

        /// <summary>
        /// Indexes the location of each of the spectra in the input file
        /// </summary>
        /// <remarks>
        /// Returns true if mIndexingComplete is true
        /// </remarks>
        /// <returns>True if successful, false if an error</returns>
        public override bool ReadAndCacheEntireFile()
        {
            try
            {
                if (mBinaryTextReader is null)
                {
                    return mIndexingComplete;
                }
                else
                {
                    mReadingAndStoringSpectra = true;
                    mErrorMessage = string.Empty;
                    ResetProgress("Indexing " + Path.GetFileName(mInputFilePath));

                    // Read and parse the input file to determine:
                    // a) The header XML (text before the first occurrence of <scan)
                    // b) The start and end byte offset of each spectrum
                    // (text between "<scan" and "</peaks>")

                    var success = ReadMZXmlFile();
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

                    mIndexingComplete = true;
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
        /// Use the binary reader to determine the location of the scan and peaks elements in the .xml file
        /// </summary>
        /// <remarks>
        /// Returns true if mIndexingComplete is true
        /// </remarks>
        /// <returns>True if successful, false if an error</returns>
        private bool ReadMZXmlFile()
        {
            var lngCurrentSpectrumByteOffsetStart = default(long);
            var lngCurrentSpectrumByteOffsetEnd = default(long);

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
                        mCurrentSpectrumInfo = new clsSpectrumInfoMzXML();
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
                            OnErrorEvent("Unexpected condition in ReadMZXmlFile: mInFileCurrentCharIndex < 0");
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
                        OnErrorEvent("Unexpected condition in ReadMZXmlFile: mAddNewLinesToHeader was True; changing to False");
                        mAddNewLinesToHeader = false;
                    }

                    StoreIndexEntry(mCurrentSpectrumInfo.ScanNumber, lngCurrentSpectrumByteOffsetStart, lngCurrentSpectrumByteOffsetEnd);

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
                OnErrorEvent("Error in ReadMZXmlFile", ex);
                return false;
            }
        }

        /// <summary>
        /// Use the binary reader to jump to lngByteOffsetStart, then read line-by-line until the next closing peaks tag is found
        /// </summary>
        /// <param name="intScanNumber"></param>
        /// <param name="lngByteOffsetStart"></param>
        private void StoreFinalIndexEntry(int intScanNumber, long lngByteOffsetStart)
        {
            // The byte offset of the end of </peaks>

            mBinaryTextReader.MoveToByteOffset(lngByteOffsetStart);

            while (mBinaryTextReader.ReadLine(clsBinaryTextReader.ReadDirectionConstants.Forward))
            {
                var strCurrentLine = mBinaryTextReader.CurrentLine;
                var intMatchIndex = strCurrentLine.IndexOf(PEAKS_END_ELEMENT, StringComparison.Ordinal);

                if (intMatchIndex >= 0)
                {
                    var lngByteOffsetEnd = mBinaryTextReader.CurrentLineByteOffsetStart + (intMatchIndex + PEAKS_END_ELEMENT.Length) * mBinaryTextReader.CharSize - 1L;
                    StoreIndexEntry(intScanNumber, lngByteOffsetStart, lngByteOffsetEnd);
                    break;
                }
            }
        }

        private void UpdateXmlFileHeaderScanCount(ref string strHeaderText)
        {
            UpdateXmlFileHeaderScanCount(ref strHeaderText, 1);
        }

        /// <summary>
        /// Examine strHeaderText to look for the number after the scanCount attribute of msRun,
        /// then replace the number with intScanCountTotal
        /// </summary>
        /// <param name="strHeaderText"></param>
        /// <param name="intScanCountTotal"></param>
        private void UpdateXmlFileHeaderScanCount(ref string strHeaderText, int intScanCountTotal)
        {
            if (!string.IsNullOrWhiteSpace(strHeaderText))
            {
                var objMatch = mMSRunRegEx.Match(strHeaderText);

                if (objMatch.Success)
                {
                    // Replace the scan count value with intScanCountTotal
                    if (objMatch.Groups.Count > 1)
                    {
                        try
                        {
                            strHeaderText = strHeaderText.Substring(0, objMatch.Groups[1].Index) + intScanCountTotal.ToString() + strHeaderText.Substring(objMatch.Groups[1].Index + objMatch.Groups[1].Length);
                        }
                        catch (Exception ex)
                        {
                        }
                    }
                }
            }
        }
    }
}