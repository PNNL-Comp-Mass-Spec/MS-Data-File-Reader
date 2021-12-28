using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace MSDataFileReader
{

    // This class can be used to open a .mzXML file and index the location
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

    public class clsMzXMLFileAccessor : clsMSDataFileAccessorBaseClass
    {
        public clsMzXMLFileAccessor()
        {
            InitializeObjectVariables();
            InitializeLocalVariables();
        }

        ~clsMzXMLFileAccessor()
        {
            if (mXmlFileReader is object)
            {
                mXmlFileReader = null;
            }
        }

        #region Constants and Enums

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

        #endregion

        #region Classwide Variables

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

        #endregion

        #region Processing Options and Interface Functions

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
                if (mXmlFileReader is object)
                {
                    mXmlFileReader.ParseFilesWithUnknownVersion = value;
                }
            }
        }

        #endregion

        protected override bool AdvanceFileReaders(emmElementMatchModeConstants eElementMatchMode)
        {
            // Uses the BinaryTextReader to look for the given element type (as specified by eElementMatchMode)

            bool blnMatchFound;
            bool blnAppendingText;
            var lngByteOffsetForRewind = default(long);
            int intCharIndex;
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
                blnMatchFound = false;
                while (!(blnMatchFound | mAbortProcessing))
                {
                    if (mInFileCurrentCharIndex + 1 < mInFileCurrentLineText.Length)
                    {
                        if (blnAppendingText)
                        {
                            strInFileCurrentLineSubstring += ControlChars.NewLine + mInFileCurrentLineText.Substring(mInFileCurrentCharIndex + 1);
                        }
                        else
                        {
                            strInFileCurrentLineSubstring = mInFileCurrentLineText.Substring(mInFileCurrentCharIndex + 1);
                        }

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
                                mXmlFileHeader += mInFileCurrentLineText + ControlChars.NewLine;
                            }
                        }

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
                                        mInputFileStats.ScanCount = Conversions.ToInteger(objMatch.Groups[1].Captures[0].Value);
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
                                {
                                    objMatch = mScanStartElementRegEx.Match(strInFileCurrentLineSubstring);
                                    break;
                                }

                            case emmElementMatchModeConstants.EndElement:
                                {
                                    // Since mzXml files can have scans embedded within another scan, we'll look for </peaks>
                                    // rather than looking for </scan>
                                    objMatch = mPeaksEndElementRegEx.Match(strInFileCurrentLineSubstring);
                                    break;
                                }

                            default:
                                {
                                    // Unknown mode
                                    LogErrors("AdvanceFileReaders", "Unknown mode for eElementMatchMode: " + eElementMatchMode.ToString());
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
                                // Look for the scan number after <scan
                                objMatch = mScanNumberRegEx.Match(strInFileCurrentLineSubstring);
                                if (objMatch.Success)
                                {
                                    if (objMatch.Groups.Count > 1)
                                    {
                                        try
                                        {
                                            mCurrentSpectrumInfo.ScanNumber = Conversions.ToInteger(objMatch.Groups[1].Captures[0].Value);
                                        }
                                        catch (Exception ex)
                                        {
                                        }
                                    }
                                }
                                // Could not find the num attribute
                                // If strInFileCurrentLineSubstring does not contain PEAKS_END_ELEMENT, then
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
                            }
                            else if (eElementMatchMode == emmElementMatchModeConstants.EndElement)
                            {
                                // Move to the end of the element
                                intCharIndex += objMatch.Value.Length - 1;
                                if (intCharIndex >= mInFileCurrentLineText.Length)
                                {
                                    // This shouldn't happen
                                    LogErrors("AdvanceFileReaders", "Unexpected condition: intCharIndex >= mInFileCurrentLineText.Length");
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
                LogErrors("AdvanceFileReaders", ex.Message);
                blnMatchFound = false;
            }

            return blnMatchFound;
        }

        private long ExtractIndexOffsetFromTextStream(string strTextStream)
        {
            // Looks for the number between "<indexOffset" and "</indexOffset" in strTextStream
            // Returns the number if found, or 0 if an error

            int intMatchIndex;
            int intIndex;
            string strNumber;
            var lngIndexOffset = default(long);
            try
            {
                // Look for <indexOffset in strTextStream
                intMatchIndex = strTextStream.IndexOf(INDEX_OFFSET_START_ELEMENT, StringComparison.Ordinal);
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
                                for (intIndex = 0; intIndex <= loopTo; intIndex++)
                                {
                                    if (IsNumber(Conversions.ToString(strTextStream[intIndex])))
                                    {
                                        // First number found
                                        strNumber = Conversions.ToString(strTextStream[intIndex]);

                                        // Append any additional numbers to strNumber
                                        while (intIndex + 1 < strTextStream.Length)
                                        {
                                            intIndex += 1;
                                            if (IsNumber(Conversions.ToString(strTextStream[intIndex])))
                                            {
                                                strNumber += Conversions.ToString(strTextStream[intIndex]);
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
                LogErrors("ExtractIndexOffsetFromTextStream", ex.Message);
                lngIndexOffset = 0L;
            }

            return lngIndexOffset;
        }

        [Obsolete("No longer used")]
        protected override string ExtractTextBetweenOffsets(string strFilePath, long lngStartByteOffset, long lngEndByteOffset)
        {
            string strExtractedText;
            int intMatchIndex;
            bool blnAddScanEndElement;
            int intAsciiValue;
            strExtractedText = base.ExtractTextBetweenOffsets(strFilePath, lngStartByteOffset, lngEndByteOffset);
            if (!string.IsNullOrWhiteSpace(strExtractedText))
            {
                blnAddScanEndElement = false;

                // Check for the occurrence of two or more </scan> elements after </peaks>
                // This will be the case if the byte offset values were read from the end of the mzXML file
                intMatchIndex = strExtractedText.IndexOf(PEAKS_END_ELEMENT, StringComparison.Ordinal);
                if (intMatchIndex >= 0)
                {
                    intMatchIndex = strExtractedText.IndexOf(SCAN_END_ELEMENT, intMatchIndex, StringComparison.Ordinal);
                    if (intMatchIndex >= 0)
                    {
                        // Replace all but the first occurrence of </scan> with ""
                        strExtractedText = strExtractedText.Substring(0, intMatchIndex + 1) + strExtractedText.Substring(intMatchIndex + 1).Replace(SCAN_END_ELEMENT, string.Empty);
                    }
                    else
                    {
                        blnAddScanEndElement = true;
                    }
                }
                else
                {
                    blnAddScanEndElement = true;
                }

                if (blnAddScanEndElement)
                {
                    intAsciiValue = Convert.ToInt32(strExtractedText[strExtractedText.Length - 1]);
                    if (!(intAsciiValue == 10 || intAsciiValue == 13 || intAsciiValue == 9 || intAsciiValue == 32))
                    {
                        strExtractedText += ControlChars.NewLine + SCAN_END_ELEMENT;
                    }
                    else
                    {
                        strExtractedText += SCAN_END_ELEMENT;
                    }
                }

                strExtractedText += ControlChars.NewLine;
            }

            return strExtractedText;
        }

        [Obsolete("No longer used")]
        public override string GetSourceXMLFooter()
        {
            return MSRUN_END_ELEMENT + ControlChars.NewLine + MZXML_END_ELEMENT + ControlChars.NewLine;
        }

        [Obsolete("No longer used")]
        public override string GetSourceXMLHeader(int intScanCountTotal, float sngStartTimeMinutesAllScans, float sngEndTimeMinutesAllScans)
        {
            string strHeaderText;
            int intAsciiValue;
            Regex reStartTime;
            Regex reEndTime;
            Match objMatch;
            string strStartTimeSOAP;
            string strEndTimeSOAP;
            try
            {
                if (mXmlFileHeader is null)
                    mXmlFileHeader = string.Empty;
                strHeaderText = string.Copy(mXmlFileHeader);
                strStartTimeSOAP = clsMSXMLFileReaderBaseClass.ConvertTimeFromTimespanToXmlDuration(new TimeSpan((long)Math.Round(sngStartTimeMinutesAllScans * TimeSpan.TicksPerMinute)), true);
                strEndTimeSOAP = clsMSXMLFileReaderBaseClass.ConvertTimeFromTimespanToXmlDuration(new TimeSpan((long)Math.Round(sngEndTimeMinutesAllScans * TimeSpan.TicksPerMinute)), true);
                if (strHeaderText.Length == 0)
                {
                    strHeaderText = "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>" + ControlChars.NewLine + MZXML_START_ELEMENT + " xmlns=\"http://sashimi.sourceforge.net/schema_revision/mzXML_2.0\"" + " xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"" + " xsi:schemaLocation=\"http://sashimi.sourceforge.net/schema_revision/mzXML_2.0" + " http://sashimi.sourceforge.net/schema_revision/mzXML_2.0/mzXML_idx_2.0.xsd\">" + ControlChars.NewLine + MSRUN_START_ELEMENT + " scanCount=\"" + intScanCountTotal.ToString() + "\"" + " startTime = \"" + strStartTimeSOAP + "\"" + " endTime = \"" + strEndTimeSOAP + "\">" + ControlChars.NewLine;
                }
                else
                {
                    // Replace ScanCount, StartTime, and EndTime values with intScanCountTotal, sngStartTimeMinutesAllScans, and sngEndTimeMinutesAllScans

                    UpdateXmlFileHeaderScanCount(ref strHeaderText, intScanCountTotal);
                    reStartTime = InitializeRegEx(@"startTime\s*=\s*""[A-Z0-9.]+""");
                    reEndTime = InitializeRegEx(@"endTime\s*=\s*""[A-Z0-9.]+""");
                    objMatch = reStartTime.Match(strHeaderText);
                    if (objMatch.Success)
                    {
                        // Replace the start time with strStartTimeSOAP
                        strHeaderText = strHeaderText.Substring(0, objMatch.Index) + "startTime=\"" + strStartTimeSOAP + "\"" + strHeaderText.Substring(objMatch.Index + objMatch.Value.Length);
                    }

                    objMatch = reEndTime.Match(strHeaderText);
                    if (objMatch.Success)
                    {
                        // Replace the start time with strEndTimeSOAP
                        strHeaderText = strHeaderText.Substring(0, objMatch.Index) + "endTime=\"" + strEndTimeSOAP + "\"" + strHeaderText.Substring(objMatch.Index + objMatch.Value.Length);
                    }
                }

                intAsciiValue = Convert.ToInt32(strHeaderText[strHeaderText.Length - 1]);
                if (!(intAsciiValue == 10 || intAsciiValue == 13 || intAsciiValue == 9 || intAsciiValue == 32))
                {
                    strHeaderText += ControlChars.NewLine;
                }
            }
            catch (Exception ex)
            {
                mErrorMessage = "Error opening obtaining source XML header: " + ex.Message;
                strHeaderText = string.Empty;
            }

            return strHeaderText;
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
                        mXmlFileReader = new clsMzXMLFileReader() { ParseFilesWithUnknownVersion = mParseFilesWithUnknownVersion };
                    }

                    if (mIndexedSpectrumInfoCount == 0)
                    {
                        mErrorMessage = "Indexed data not in memory";
                    }
                    else if (intSpectrumIndex >= 0 & intSpectrumIndex < mIndexedSpectrumInfoCount)
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
                        else if (string.IsNullOrWhiteSpace(mFileVersion) && !string.IsNullOrWhiteSpace(mXmlFileHeader))
                        {
                            if (!clsMzXMLFileReader.ExtractMzXmlFileVersion(mXmlFileHeader, out mFileVersion))
                            {
                                LogErrors("ValidateMZXmlFileVersion", "Unknown mzXML file version; expected text not found in mXmlFileHeader");
                            }
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
                LogErrors("GetSpectrumByIndexWork", ex.Message);
            }

            return blnSuccess;
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

        protected override bool LoadExistingIndex()
        {
            // Use the mBinaryTextReader to jump to the end of the file and read the data line-by-line backward
            // looking for the <indexOffset> element or the <index
            // If found, and if the index elements are successfully loaded, then returns True
            // Otherwise, returns False

            string strCurrentLine;
            int intCharIndex;
            int intCharIndexEnd;
            long lngByteOffsetSaved;
            long lngIndexOffset;
            var blnExtractTextToEOF = default(bool);
            bool blnIndexLoaded;
            string strExtractedText;
            int intStartElementIndex;
            int intFirstBracketIndex;
            StringBuilder objStringBuilder;
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
                    strCurrentLine = mBinaryTextReader.CurrentLine;
                    intCharIndex = strCurrentLine.IndexOf(INDEX_OFFSET_START_ELEMENT, StringComparison.Ordinal);
                    if (intCharIndex >= 0)
                    {
                        // The offset to the index has been specified
                        // Parse out the number between <indexOffset> and </indexOffset>
                        // (normally on the same line, though this code can handle white space between the tags)

                        lngByteOffsetSaved = mBinaryTextReader.CurrentLineByteOffsetStart + intCharIndex * mBinaryTextReader.CharSize;
                        intCharIndexEnd = strCurrentLine.IndexOf(INDEX_OFFSET_END_ELEMENT, intCharIndex + INDEX_OFFSET_START_ELEMENT.Length, StringComparison.Ordinal);
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
                            lngIndexOffset = ExtractIndexOffsetFromTextStream(strCurrentLine);
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
                                    strCurrentLine = MZXML_START_ELEMENT + ">" + ControlChars.NewLine + strCurrentLine;
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
                        objStringBuilder = new StringBuilder() { Length = 0 };
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
                            // If the test read works, then we'll set blnIndexLoaded to True
                            blnIndexLoaded = false;
                            if (mIndexedSpectrumInfoCount > 0)
                            {
                                // Set up the default error message
                                mErrorMessage = "Index embedded in the input file (" + Path.GetFileName(mInputFilePath) + ") is corrupt: first byte offset (" + mIndexedSpectrumInfo[0].ByteOffsetStart.ToString() + ") does not point to a " + SCAN_START_ELEMENT + " element";
                                strExtractedText = base.ExtractTextBetweenOffsets(mInputFilePath, mIndexedSpectrumInfo[0].ByteOffsetStart, mIndexedSpectrumInfo[0].ByteOffsetEnd);
                                if (strExtractedText is object && strExtractedText.Length > 0)
                                {
                                    // Make sure the first text in strExtractedText is <scan
                                    intStartElementIndex = strExtractedText.IndexOf(SCAN_START_ELEMENT, StringComparison.Ordinal);
                                    if (intStartElementIndex >= 0)
                                    {
                                        intFirstBracketIndex = strExtractedText.IndexOf('<');
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
                                    mXmlFileHeader += strCurrentLine + ControlChars.NewLine;
                                }
                            }
                        }
                        else
                        {
                            // Index not loaded (or not valid)

                            if (mErrorMessage is object && mErrorMessage.Length > 0)
                            {
                                LogErrors("LoadExistingIndex", mErrorMessage);
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
                LogErrors("LoadExistingIndex", ex.Message);
                blnIndexLoaded = false;
            }

            return blnIndexLoaded;
        }

        protected override void LogErrors(string strCallingFunction, string strErrorDescription)
        {
            base.LogErrors("clsMzXMLFileAccessor." + strCallingFunction, strErrorDescription);
        }

        private bool ParseMzXMLOffsetIndex(string strTextStream)
        {
            bool blnIndexLoaded = false;
            try
            {
                bool blnParseIndexValues = true;
                int intCurrentScanNumber = -1;
                int intPreviousScanNumber = -1;
                long lngCurrentScanByteOffsetStart = -1;
                long lngPreviousScanByteOffsetStart;
                string strCurrentElement = string.Empty;
                using (var objXMLReader = new XmlTextReader(new StringReader(strTextStream)))
                {

                    // Skip all whitespace
                    objXMLReader.WhitespaceHandling = WhitespaceHandling.None;
                    bool blnReadSuccessful = true;
                    while (blnReadSuccessful && objXMLReader.ReadState == ReadState.Initial | objXMLReader.ReadState == ReadState.Interactive)
                    {
                        blnReadSuccessful = objXMLReader.Read();
                        if (blnReadSuccessful && objXMLReader.ReadState == ReadState.Interactive)
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

                                        if (strValue is object && strValue.Length > 0)
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
                                            intCurrentScanNumber = Conversions.ToInteger(objXMLReader.GetAttribute(OFFSET_ATTRIBUTE_ID));
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
                                    if (!(objXMLReader.NodeType == XmlNodeType.Whitespace) & objXMLReader.HasValue)
                                    {
                                        try
                                        {
                                            lngPreviousScanByteOffsetStart = lngCurrentScanByteOffsetStart;
                                            lngCurrentScanByteOffsetStart = Conversions.ToLong(objXMLReader.Value);
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
                LogErrors("ParseMzXMLOffsetIndex", ex.Message);
                blnIndexLoaded = false;
            }

            return blnIndexLoaded;
        }

        public override bool ReadAndCacheEntireFile()
        {
            // Indexes the location of each of the spectra in the input file

            bool blnSuccess;
            try
            {
                if (mBinaryTextReader is null)
                {
                    blnSuccess = mIndexingComplete;
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

                    blnSuccess = ReadMZXmlFile();
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

                        mIndexingComplete = true;
                    }
                }
            }
            catch (Exception ex)
            {
                LogErrors("ReadAndCacheEntireFile", ex.Message);
                blnSuccess = false;
            }
            finally
            {
                mReadingAndStoringSpectra = false;
            }

            return blnSuccess;
        }

        private bool ReadMZXmlFile()
        {
            // This function uses the Binary Text Reader to determine
            // the location of the "<scan" and "</peaks>" elements in the .Xml file
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
                            LogErrors("ReadMZXmlFile", "Unexpected condition: mInFileCurrentCharIndex < 0");
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
                            LogErrors("ReadMZXmlFile", "Unexpected condition: mAddNewLinesToHeader was True; changing to False");
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
                }
                while (blnSpectrumFound);
                blnSuccess = true;
            }
            catch (Exception ex)
            {
                LogErrors("ReadMZXmlFile", ex.Message);
                blnSuccess = false;
            }

            return blnSuccess;
        }

        private void StoreFinalIndexEntry(int intScanNumber, long lngByteOffsetStart)
        {
            // Use the binary text reader to jump to lngByteOffsetStart, then read line-by-line until the next </peaks> tag is found
            // The end of this tag is equal to lngByteOffsetEnd

            string strCurrentLine;
            int intMatchIndex;
            long lngByteOffsetEnd;
            mBinaryTextReader.MoveToByteOffset(lngByteOffsetStart);
            while (mBinaryTextReader.ReadLine(clsBinaryTextReader.ReadDirectionConstants.Forward))
            {
                strCurrentLine = mBinaryTextReader.CurrentLine;
                intMatchIndex = strCurrentLine.IndexOf(PEAKS_END_ELEMENT, StringComparison.Ordinal);
                if (intMatchIndex >= 0)
                {
                    lngByteOffsetEnd = mBinaryTextReader.CurrentLineByteOffsetStart + (intMatchIndex + PEAKS_END_ELEMENT.Length) * mBinaryTextReader.CharSize - 1L;
                    StoreIndexEntry(intScanNumber, lngByteOffsetStart, lngByteOffsetEnd);
                    break;
                }
            }
        }

        private void UpdateXmlFileHeaderScanCount(ref string strHeaderText)
        {
            UpdateXmlFileHeaderScanCount(ref strHeaderText, 1);
        }

        private void UpdateXmlFileHeaderScanCount(ref string strHeaderText, int intScanCountTotal)
        {
            // Examine strHeaderText to look for the number after the scanCount attribute of msRun
            // Replace the number with intScanCountTotal

            Match objMatch;
            if (strHeaderText is object && strHeaderText.Length > 0)
            {
                objMatch = mMSRunRegEx.Match(strHeaderText);
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