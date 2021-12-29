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
    public class MzXMLFileAccessor : MsDataFileAccessorBaseClass
    {
        // Ignore Spelling: num

        public MzXMLFileAccessor()
        {
            InitializeObjectVariables();
            InitializeLocalVariables();
        }

        ~MzXMLFileAccessor()
        {
            mXmlFileReader = null;
        }

        // ReSharper disable UnusedMember.Local

        private const string MSRUN_START_ELEMENT = "<msRun";

        private const string MSRUN_END_ELEMENT = "</msRun>";

        private const string SCAN_START_ELEMENT = "<scan";

        /// <summary>
        /// Scan end element
        /// </summary>
        /// <remarks>
        /// Used by the MSDataFileTrimmer
        /// </remarks>
        // ReSharper disable once UnusedMember.Global
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

        // ReSharper restore UnusedMember.Local

        private MzXMLFileReader mXmlFileReader;

        private SpectrumInfoMzXML mCurrentSpectrumInfo;

        private string mXmlFileHeader;

        private bool mAddNewLinesToHeader;

        private bool mMSRunFound;

        private Regex mMSRunRegEx;

        private Regex mScanStartElementRegEx;

        private Regex mPeaksEndElementRegEx;

        private Regex mScanNumberRegEx;

        private XmlReaderSettings mXMLReaderSettings;

        public bool IgnoreEmbeddedIndex { get; set; }

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
        /// Use the binary reader to look for the given element type (as specified by elementMatchMode)
        /// </summary>
        /// <param name="elementMatchMode"></param>
        /// <returns>True if successful, false if an error</returns>
        protected override bool AdvanceFileReaders(ElementMatchMode elementMatchMode)
        {
            bool matchFound;
            var byteOffsetForRewind = default(long);

            try
            {
                mInFileCurrentLineText ??= string.Empty;

                var inFileCurrentLineSubstring = string.Empty;
                var appendingText = false;
                matchFound = false;

                while (!mAbortProcessing)
                {
                    if (mInFileCurrentCharIndex + 1 < mInFileCurrentLineText.Length)
                    {
                        if (appendingText)
                        {
                            inFileCurrentLineSubstring += Environment.NewLine + mInFileCurrentLineText.Substring(mInFileCurrentCharIndex + 1);
                        }
                        else
                        {
                            inFileCurrentLineSubstring = mInFileCurrentLineText.Substring(mInFileCurrentCharIndex + 1);
                        }

                        int charIndex;

                        if (mAddNewLinesToHeader)
                        {
                            // We haven't yet found the first scan; look for "<scan"
                            charIndex = mInFileCurrentLineText.IndexOf(SCAN_START_ELEMENT, mInFileCurrentCharIndex + 1, StringComparison.Ordinal);

                            if (charIndex >= 0)
                            {
                                // Only add a portion of mInFileCurrentLineText to mXmlFileHeader
                                // since it contains SCAN_START_ELEMENT

                                if (charIndex > 0)
                                {
                                    mXmlFileHeader += mInFileCurrentLineText.Substring(0, charIndex);
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

                        Match match;

                        if (!mMSRunFound)
                        {
                            // We haven't yet found msRun; look for "<msRun" and the Scan Count value
                            match = mMSRunRegEx.Match(mXmlFileHeader);

                            if (match.Success)
                            {
                                // Record the Scan Count value
                                if (match.Groups.Count > 1)
                                {
                                    try
                                    {
                                        mInputFileStats.ScanCount = int.Parse(match.Groups[1].Captures[0].Value);
                                    }
                                    catch (Exception ex)
                                    {
                                        // Ignore errors here
                                    }
                                }

                                mMSRunFound = true;
                            }
                        }

                        // Look for the appropriate search text in mInFileCurrentLineText, starting at mInFileCurrentCharIndex + 1
                        switch (elementMatchMode)
                        {
                            case ElementMatchMode.StartElement:
                                match = mScanStartElementRegEx.Match(inFileCurrentLineSubstring);
                                break;

                            case ElementMatchMode.EndElement:
                                // Since mzXml files can have scans embedded within another scan, we'll look for </peaks>
                                // rather than looking for </scan>
                                match = mPeaksEndElementRegEx.Match(inFileCurrentLineSubstring);
                                break;

                            default:
                                // Unknown mode
                                OnErrorEvent("Unknown mode for elementMatchMode in AdvanceFileReaders: {0}", elementMatchMode);
                                return false;
                        }

                        if (match.Success)
                        {
                            // Match Found
                            matchFound = true;
                            charIndex = match.Index + 1 + mInFileCurrentCharIndex;

                            switch (elementMatchMode)
                            {
                                case ElementMatchMode.StartElement:
                                    // Look for the scan number after <scan
                                    match = mScanNumberRegEx.Match(inFileCurrentLineSubstring);

                                    if (match.Success)
                                    {
                                        if (match.Groups.Count > 1)
                                        {
                                            try
                                            {
                                                mCurrentSpectrumInfo.ScanNumber = int.Parse(match.Groups[1].Captures[0].Value);
                                            }
                                            catch (Exception ex)
                                            {
                                                // Ignore errors here
                                            }
                                        }
                                    }

                                    // Could not find the num attribute
                                    // If inFileCurrentLineSubstring does not contain PEAKS_END_ELEMENT,
                                    // set appendingText to True and continue reading
                                    else if (inFileCurrentLineSubstring.IndexOf(PEAKS_END_ELEMENT, StringComparison.Ordinal) < 0)
                                    {
                                        matchFound = false;

                                        if (!appendingText)
                                        {
                                            appendingText = true;
                                            // Record the byte offset of the start of the current line
                                            // We will use this offset to "rewind" the file pointer once the num attribute is found
                                            byteOffsetForRewind = mBinaryTextReader.CurrentLineByteOffsetStart;
                                        }
                                    }

                                    break;

                                case ElementMatchMode.EndElement:
                                    // Move to the end of the element
                                    charIndex += match.Value.Length - 1;

                                    if (charIndex >= mInFileCurrentLineText.Length)
                                    {
                                        // This shouldn't happen
                                        OnErrorEvent("Unexpected condition in AdvanceFileReaders: charIndex >= mInFileCurrentLineText.Length");
                                        charIndex = mInFileCurrentLineText.Length - 1;
                                    }

                                    break;

                                default:
                                    throw new ArgumentOutOfRangeException(nameof(elementMatchMode), elementMatchMode, null);
                            }

                            mInFileCurrentCharIndex = charIndex;

                            if (matchFound)
                            {
                                if (appendingText)
                                {
                                    mBinaryTextReader.MoveToByteOffset(byteOffsetForRewind);
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
                matchFound = false;
            }

            return matchFound;
        }

        /// <summary>
        /// Look for the number following the indexOffset tag
        /// </summary>
        /// <param name="textStream"></param>
        /// <returns>Byte offset if found, otherwise 0</returns>
        private long ExtractIndexOffsetFromTextStream(string textStream)
        {
            var indexOffset = default(long);

            try
            {
                // Look for <indexOffset in textStream
                var matchIndex = textStream.IndexOf(INDEX_OFFSET_START_ELEMENT, StringComparison.Ordinal);

                if (matchIndex >= 0)
                {
                    // Look for the next >
                    matchIndex = textStream.IndexOf('>', matchIndex + 1);

                    if (matchIndex >= 0)
                    {
                        // Remove the leading text
                        textStream = textStream.Substring(matchIndex + 1);

                        // Look for the next <
                        matchIndex = textStream.IndexOf('<');

                        if (matchIndex >= 0)
                        {
                            textStream = textStream.Substring(0, matchIndex);

                            // Try to convert textStream to a number
                            try
                            {
                                indexOffset = int.Parse(textStream);
                            }
                            catch (Exception ex)
                            {
                                indexOffset = 0L;
                            }

                            if (indexOffset == 0L)
                            {
                                // Number conversion failed; probably have carriage returns in the text
                                // Look for the next number in textStream
                                var indexEnd = textStream.Length - 1;

                                for (var index = 0; index <= indexEnd; index++)
                                {
                                    if (IsNumber(textStream[index].ToString()))
                                    {
                                        // First number found
                                        var number = textStream[index].ToString();

                                        // Append any additional numbers to number
                                        while (index + 1 < textStream.Length)
                                        {
                                            index++;

                                            if (IsNumber(textStream[index].ToString()))
                                            {
                                                number += textStream[index].ToString();
                                            }
                                            else
                                            {
                                                break;
                                            }
                                        }

                                        try
                                        {
                                            indexOffset = int.Parse(number);
                                        }
                                        catch (Exception ex)
                                        {
                                            indexOffset = 0L;
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
                indexOffset = 0L;
            }

            return indexOffset;
        }

        protected override bool GetSpectrumByIndexWork(int spectrumIndex, out SpectrumInfo currentSpectrumInfo, bool headerInfoOnly)
        {
            currentSpectrumInfo = null;

            try
            {
                if (!GetSpectrumReadyStatus(true))
                {
                    return false;
                }

                mXmlFileReader ??= new MzXMLFileReader
                {
                    ParseFilesWithUnknownVersion = mParseFilesWithUnknownVersion
                };

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

                // Move the binary file reader to .ByteOffsetStart and instantiate an XMLReader at that position
                mBinaryReader.Position = mIndexedSpectrumInfo[spectrumIndex].ByteOffsetStart;
                UpdateProgress(mBinaryReader.Position / (double)mBinaryReader.Length * 100.0d);

                bool success;

                // Create a new XmlTextReader
                using (var reader = XmlReader.Create(mBinaryReader, mXMLReaderSettings))
                {
                    reader.MoveToContent();
                    mXmlFileReader.SetXMLReaderForSpectrum(reader.ReadSubtree());
                    success = mXmlFileReader.ReadNextSpectrum(out currentSpectrumInfo);
                }

                if (!string.IsNullOrWhiteSpace(mXmlFileReader.FileVersion))
                {
                    mFileVersion = mXmlFileReader.FileVersion;
                }
                else if (string.IsNullOrWhiteSpace(mFileVersion) && !string.IsNullOrWhiteSpace(mXmlFileHeader))
                {
                    if (!MzXMLFileReader.ExtractMzXmlFileVersion(mXmlFileHeader, out mFileVersion))
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

        protected sealed override void InitializeLocalVariables()
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
            var extractTextToEOF = false;
            bool indexLoaded;

            try
            {
                if (IgnoreEmbeddedIndex)
                {
                    return false;
                }

                indexLoaded = false;

                // Move to the end of the file
                mBinaryTextReader.MoveToEnd();

                while (mBinaryTextReader.ReadLine(BinaryTextReader.ReadDirection.Reverse))
                {
                    var currentLine = mBinaryTextReader.CurrentLine;
                    var charIndex = currentLine.IndexOf(INDEX_OFFSET_START_ELEMENT, StringComparison.Ordinal);
                    var charIndexEnd = currentLine.IndexOf(INDEX_OFFSET_END_ELEMENT, charIndex + INDEX_OFFSET_START_ELEMENT.Length, StringComparison.Ordinal);

                    if (charIndex >= 0)
                    {
                        // The offset to the index has been specified
                        // Parse out the number between <indexOffset> and </indexOffset>
                        // (normally on the same line, though this code can handle white space between the tags)

                        var byteOffsetSaved = mBinaryTextReader.CurrentLineByteOffsetStart + charIndex * mBinaryTextReader.CharSize;

                        if (charIndexEnd <= 0)
                        {
                            // Need to read the next few lines to find </indexOffset>
                            mBinaryTextReader.MoveToByteOffset(mBinaryTextReader.CurrentLineByteOffsetEndWithTerminator + 1L);

                            while (mBinaryTextReader.ReadLine(BinaryTextReader.ReadDirection.Forward))
                            {
                                currentLine += " " + mBinaryTextReader.CurrentLine;
                                charIndexEnd = currentLine.IndexOf(INDEX_OFFSET_END_ELEMENT, charIndex + INDEX_OFFSET_START_ELEMENT.Length, StringComparison.Ordinal);

                                if (charIndexEnd > 0)
                                {
                                    break;
                                }
                            }
                        }

                        if (charIndexEnd > 0)
                        {
                            var indexOffset = ExtractIndexOffsetFromTextStream(currentLine);

                            if (indexOffset > 0L)
                            {
                                // Move the binary reader to indexOffset
                                mBinaryTextReader.MoveToByteOffset(indexOffset);

                                // Read the text at offset indexOffset
                                mBinaryTextReader.ReadLine(BinaryTextReader.ReadDirection.Forward);
                                currentLine = mBinaryTextReader.CurrentLine;

                                // Verify that currentLine contains "<index"
                                if (currentLine.IndexOf(INDEX_START_ELEMENT, StringComparison.Ordinal) >= 0)
                                {
                                    currentLine = MZXML_START_ELEMENT + ">" + Environment.NewLine + currentLine;
                                    extractTextToEOF = true;
                                }
                                else
                                {
                                    // Corrupt index offset value; move back to byte offset
                                }
                            }
                        }

                        if (!extractTextToEOF)
                        {
                            // Move the reader back to byte byteOffsetSaved
                            mBinaryTextReader.MoveToByteOffset(byteOffsetSaved);
                            currentLine = string.Empty;
                        }
                    }

                    if (!extractTextToEOF)
                    {
                        charIndex = currentLine.IndexOf(MSRUN_END_ELEMENT, StringComparison.Ordinal);

                        if (charIndex >= 0)
                        {
                            // </msRun> element found
                            // Extract the text from here to the end of the file and parse with ParseMzXMLOffsetIndex
                            extractTextToEOF = true;
                            currentLine = MZXML_START_ELEMENT + ">";
                        }
                    }

                    if (extractTextToEOF)
                    {
                        var stringBuilder = new StringBuilder() { Length = 0 };

                        if (currentLine.Length > 0)
                        {
                            stringBuilder.Append(currentLine + mBinaryTextReader.CurrentLineTerminator);
                        }

                        // Read all of the lines to the end of the file
                        while (mBinaryTextReader.ReadLine(BinaryTextReader.ReadDirection.Forward))
                        {
                            currentLine = mBinaryTextReader.CurrentLine;
                            stringBuilder.Append(currentLine + mBinaryTextReader.CurrentLineTerminator);
                        }

                        indexLoaded = ParseMzXMLOffsetIndex(stringBuilder.ToString());

                        if (indexLoaded)
                        {
                            // Validate the first entry of the index to make sure the index is valid

                            // For now, set indexLoaded to False
                            // If the test read works, we'll set indexLoaded to True
                            indexLoaded = false;

                            if (mIndexedSpectrumInfoCount > 0)
                            {
                                // Set up the default error message
                                mErrorMessage = "Index embedded in the input file (" + Path.GetFileName(mInputFilePath) + ") is corrupt: first byte offset (" + mIndexedSpectrumInfo[0].ByteOffsetStart + ") does not point to a " + SCAN_START_ELEMENT + " element";
                                var extractedText = ExtractTextBetweenOffsets(mInputFilePath, mIndexedSpectrumInfo[0].ByteOffsetStart, mIndexedSpectrumInfo[0].ByteOffsetEnd);

                                if (!string.IsNullOrEmpty(extractedText))
                                {
                                    // Make sure the first text in extractedText is <scan
                                    var startElementIndex = extractedText.IndexOf(SCAN_START_ELEMENT, StringComparison.Ordinal);

                                    if (startElementIndex >= 0)
                                    {
                                        var firstBracketIndex = extractedText.IndexOf('<');

                                        if (firstBracketIndex == startElementIndex)
                                        {
                                            indexLoaded = true;
                                            mErrorMessage = string.Empty;
                                        }
                                    }
                                }
                            }
                        }

                        if (indexLoaded)
                        {
                            // Move back to the beginning of the file and extract the header tags
                            mBinaryTextReader.MoveToBeginning();
                            mXmlFileHeader = string.Empty;

                            while (mBinaryTextReader.ReadLine(BinaryTextReader.ReadDirection.Forward))
                            {
                                currentLine = mBinaryTextReader.CurrentLine;
                                charIndex = currentLine.IndexOf(SCAN_START_ELEMENT, StringComparison.Ordinal);

                                if (charIndex >= 0)
                                {
                                    // SCAN_START_ELEMENT found
                                    if (charIndex > 0)
                                    {
                                        // Only add a portion of currentLine to mXmlFileHeader
                                        // since it contains SCAN_START_ELEMENT
                                        mXmlFileHeader += currentLine.Substring(0, charIndex);
                                    }

                                    break;
                                }

                                // Append currentLine to mXmlFileHeader
                                mXmlFileHeader += currentLine + Environment.NewLine;
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
                                mIndexedSpectrumInfo = new IndexedSpectrumInfoType[1000];
                            }
                        }

                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in LoadExistingIndex", ex);
                indexLoaded = false;
            }

            return indexLoaded;
        }

        private bool ParseMzXMLOffsetIndex(string textStream)
        {
            var indexLoaded = false;

            try
            {
                var parseIndexValues = true;
                var currentScanNumber = -1;
                var previousScanNumber = -1;
                long currentScanByteOffsetStart = -1;
                var currentElement = string.Empty;

                using var xmlreader = new XmlTextReader(new StringReader(textStream));

                // Skip all whitespace
                xmlreader.WhitespaceHandling = WhitespaceHandling.None;
                var validData = true;

                while (validData && xmlreader.ReadState == ReadState.Initial || xmlreader.ReadState == ReadState.Interactive)
                {
                    validData = xmlreader.Read();

                    if (validData && xmlreader.ReadState == ReadState.Interactive)
                    {
                        if (xmlreader.NodeType == XmlNodeType.Element)
                        {
                            currentElement = xmlreader.Name;

                            if (currentElement == INDEX_ELEMENT_NAME)
                            {
                                if (xmlreader.HasAttributes)
                                {
                                    // Validate that this is the "scan" index

                                    string value;

                                    try
                                    {
                                        value = xmlreader.GetAttribute(INDEX_ATTRIBUTE_NAME);
                                    }
                                    catch (Exception ex)
                                    {
                                        value = string.Empty;
                                    }

                                    if (!string.IsNullOrEmpty(value))
                                    {
                                        if (value == "scan")
                                        {
                                            parseIndexValues = true;
                                        }
                                        else
                                        {
                                            parseIndexValues = false;
                                        }
                                    }
                                }
                            }
                            else if (currentElement == OFFSET_ELEMENT_NAME)
                            {
                                if (parseIndexValues && xmlreader.HasAttributes)
                                {
                                    // Extract the scan number from the id attribute
                                    previousScanNumber = currentScanNumber;

                                    if (!int.TryParse(xmlreader.GetAttribute(OFFSET_ATTRIBUTE_ID), out currentScanNumber))
                                    {
                                        // Index is corrupted (or of an unknown format); do not continue parsing
                                        break;
                                    }
                                }
                            }
                        }
                        else if (xmlreader.NodeType == XmlNodeType.EndElement)
                        {
                            if (parseIndexValues && xmlreader.Name == INDEX_ELEMENT_NAME)
                            {
                                // Store the final index value
                                // This is tricky since we don't know the ending offset for the given scan
                                // Thus, need to use the binary text reader to jump to currentScanByteOffsetStart and then read line-by-line until the next </peaks> tag is found
                                StoreFinalIndexEntry(currentScanNumber, currentScanByteOffsetStart);
                                indexLoaded = true;
                                break;
                            }

                            currentElement = string.Empty;
                        }
                        else if (xmlreader.NodeType == XmlNodeType.Text)
                        {
                            if (parseIndexValues && currentElement == OFFSET_ELEMENT_NAME)
                            {
                                if (xmlreader.NodeType != XmlNodeType.Whitespace && xmlreader.HasValue)
                                {
                                    try
                                    {
                                        var previousScanByteOffsetStart = currentScanByteOffsetStart;
                                        currentScanByteOffsetStart = long.Parse(xmlreader.Value);

                                        if (previousScanByteOffsetStart >= 0L && currentScanNumber >= 0)
                                        {
                                            // Store the previous scan info
                                            StoreIndexEntry(previousScanNumber, previousScanByteOffsetStart, currentScanByteOffsetStart - 1L);
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
            catch (Exception ex)
            {
                OnErrorEvent("Error in ParseMzXMLOffsetIndex", ex);
                indexLoaded = false;
            }

            return indexLoaded;
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
            var currentSpectrumByteOffsetStart = default(long);
            var currentSpectrumByteOffsetEnd = default(long);

            try
            {
                if (mIndexingComplete)
                {
                    return true;
                }

                bool spectrumFound;

                do
                {
                    if (mCurrentSpectrumInfo is null)
                    {
                        mCurrentSpectrumInfo = new SpectrumInfoMzXML();
                    }
                    else
                    {
                        mCurrentSpectrumInfo.Clear();
                    }

                    spectrumFound = AdvanceFileReaders(ElementMatchMode.StartElement);

                    if (spectrumFound)
                    {
                        if (mInFileCurrentCharIndex < 0)
                        {
                            // This shouldn't normally happen
                            currentSpectrumByteOffsetStart = mBinaryTextReader.CurrentLineByteOffsetStart;
                            OnErrorEvent("Unexpected condition in ReadMZXmlFile: mInFileCurrentCharIndex < 0");
                        }
                        else
                        {
                            currentSpectrumByteOffsetStart = mBinaryTextReader.CurrentLineByteOffsetStart + mInFileCurrentCharIndex * mCharSize;
                        }

                        spectrumFound = AdvanceFileReaders(ElementMatchMode.EndElement);

                        if (spectrumFound)
                        {
                            if (mCharSize > 1)
                            {
                                currentSpectrumByteOffsetEnd = mBinaryTextReader.CurrentLineByteOffsetStart + mInFileCurrentCharIndex * mCharSize + (mCharSize - 1);
                            }
                            else
                            {
                                currentSpectrumByteOffsetEnd = mBinaryTextReader.CurrentLineByteOffsetStart + mInFileCurrentCharIndex;
                            }
                        }
                    }

                    if (!spectrumFound)
                        continue;

                    // Make sure mAddNewLinesToHeader is now false
                    if (mAddNewLinesToHeader)
                    {
                        OnErrorEvent("Unexpected condition in ReadMZXmlFile: mAddNewLinesToHeader was True; changing to False");
                        mAddNewLinesToHeader = false;
                    }

                    StoreIndexEntry(mCurrentSpectrumInfo.ScanNumber, currentSpectrumByteOffsetStart, currentSpectrumByteOffsetEnd);

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
                while (spectrumFound);
                return true;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ReadMZXmlFile", ex);
                return false;
            }
        }

        /// <summary>
        /// Use the binary reader to jump to byteOffsetStart, then read line-by-line until the next closing peaks tag is found
        /// </summary>
        /// <param name="scanNumber"></param>
        /// <param name="byteOffsetStart"></param>
        private void StoreFinalIndexEntry(int scanNumber, long byteOffsetStart)
        {
            // The byte offset of the end of </peaks>

            mBinaryTextReader.MoveToByteOffset(byteOffsetStart);

            while (mBinaryTextReader.ReadLine(BinaryTextReader.ReadDirection.Forward))
            {
                var currentLine = mBinaryTextReader.CurrentLine;
                var matchIndex = currentLine.IndexOf(PEAKS_END_ELEMENT, StringComparison.Ordinal);

                if (matchIndex >= 0)
                {
                    var byteOffsetEnd = mBinaryTextReader.CurrentLineByteOffsetStart + (matchIndex + PEAKS_END_ELEMENT.Length) * mBinaryTextReader.CharSize - 1L;
                    StoreIndexEntry(scanNumber, byteOffsetStart, byteOffsetEnd);
                    break;
                }
            }
        }

        private void UpdateXmlFileHeaderScanCount(ref string headerText)
        {
            UpdateXmlFileHeaderScanCount(ref headerText, 1);
        }

        /// <summary>
        /// Examine headerText to look for the number after the scanCount attribute of msRun,
        /// then replace the number with scanCountTotal
        /// </summary>
        /// <param name="headerText"></param>
        /// <param name="scanCountTotal"></param>
        private void UpdateXmlFileHeaderScanCount(ref string headerText, int scanCountTotal)
        {
            if (!string.IsNullOrWhiteSpace(headerText))
            {
                var match = mMSRunRegEx.Match(headerText);

                if (match.Success)
                {
                    // Replace the scan count value with scanCountTotal
                    if (match.Groups.Count > 1)
                    {
                        try
                        {
                            headerText = headerText.Substring(0, match.Groups[1].Index) + scanCountTotal + headerText.Substring(match.Groups[1].Index + match.Groups[1].Length);
                        }
                        catch (Exception ex)
                        {
                            // Ignore errors here
                        }
                    }
                }
            }
        }
    }
}