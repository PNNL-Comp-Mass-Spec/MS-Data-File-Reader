// -------------------------------------------------------------------------------
// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
// Copyright 2021, Battelle Memorial Institute.  All Rights Reserved.
//
// E-mail: matthew.monroe@pnl.gov or proteomics@pnnl.gov
// Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://panomics.pnnl.gov/ or https://www.pnnl.gov/integrative-omics
// -------------------------------------------------------------------------------

using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using PRISM;

namespace MSDataFileReader
{
    /// <summary>
    /// This is the base class for the mzXML and mzData readers
    /// </summary>
    public abstract class clsMSXMLFileReaderBaseClass : clsMSDataFileReaderBaseClass
    {
        public clsMSXMLFileReaderBaseClass()
        {
            InitializeLocalVariables();
        }

        ~clsMSXMLFileReaderBaseClass()
        {
            try
            {
                if (mDataFileOrTextStream != null)
                {
                    mDataFileOrTextStream.Close();
                }
            }
            catch (Exception ex)
            {
                // Ignore errors here
            }

            try
            {
                if (mXMLReader != null)
                {
                    mXMLReader.Close();
                }
            }
            catch (Exception ex)
            {
                // Ignore errors here
            }
        }

        private struct udtElementInfoType
        {
            public string Name;

            public int Depth;
        }

        protected TextReader mDataFileOrTextStream;

        protected XmlReader mXMLReader;

        protected bool mSpectrumFound;

        /// <summary>
        /// When true, the base-64 encoded data in the file is not parsed,
        /// thus speeding up the reader
        /// </summary>
        protected bool mSkipBinaryData;

        protected bool mSkipNextReaderAdvance;

        protected bool mSkippedStartElementAdvance;

        // Last element name handed off from reader; set to "" when an End Element is encountered
        protected string mCurrentElement;

        protected Stack mParentElementStack;

        public int SAXParserLineNumber
        {
            get
            {
                if (mXMLReader is XmlTextReader xmlReader)
                {
                    return xmlReader.LineNumber;
                }

                return 0;
            }
        }

        public int SAXParserColumnNumber
        {
            get
            {
                if (mXMLReader is XmlTextReader xmlReader)
                {
                    return xmlReader.LinePosition;
                }

                return 0;
            }
        }

        public bool SkipBinaryData
        {
            get => mSkipBinaryData;

            set => mSkipBinaryData = value;
        }

        public override void CloseFile()
        {
            if (mXMLReader != null)
            {
                mXMLReader.Close();
            }

            mDataFileOrTextStream = null;
            mInputFilePath = string.Empty;
        }

        /// <summary>
        /// Convert a timespan to an XML duration
        /// </summary>
        /// <remarks>
        /// <para>
        /// XML duration value is typically of the form "PT249.559S" or "PT4M9.559S"
        /// where the S indicates seconds and M indicates minutes
        /// Thus, "PT249.559S" means 249.559 seconds while
        /// "PT4M9.559S" means 4 minutes plus 9.559 seconds
        /// </para>
        /// <para>
        /// When blnTrimLeadingZeroValues is true, for TimeSpan 3 minutes, returns P3M0S rather than P0Y0M0DT0H3M0S
        /// </para>
        /// </remarks>
        /// <param name="dtTimeSpan"></param>
        /// <param name="blnTrimLeadingZeroValues">When false, return the full specification; otherwise, remove any leading zero-value entries</param>
        /// <param name="bytSecondsValueDigitsAfterDecimal"></param>
        /// <returns>XML duration string</returns>
        public static string ConvertTimeFromTimespanToXmlDuration(TimeSpan dtTimeSpan, bool blnTrimLeadingZeroValues, byte bytSecondsValueDigitsAfterDecimal = 3)
        {
            const string ZERO_SOAP_DURATION_FULL = "P0Y0M0DT0H0M0S";
            const string ZERO_SOAP_DURATION_SHORT = "PT0S";

            var success = ConvertTimeFromTimespanToXmlDuration(
                dtTimeSpan, blnTrimLeadingZeroValues, bytSecondsValueDigitsAfterDecimal, out var strXMLDuration, out var isZero);

            if (!success || isZero)
            {
                if (blnTrimLeadingZeroValues)
                {
                    return ZERO_SOAP_DURATION_SHORT;
                }
                else
                {
                    return ZERO_SOAP_DURATION_FULL;
                }
            }

            return strXMLDuration;
        }

        private static bool ConvertTimeFromTimespanToXmlDuration(
            TimeSpan dtTimeSpan,
            bool blnTrimLeadingZeroValues,
            byte bytSecondsValueDigitsAfterDecimal,
            out string strXMLDuration,
            out bool isZero)
        {
            isZero = false;

            try
            {
                if (dtTimeSpan.Equals(TimeSpan.Zero))
                {
                    isZero = true;
                    strXMLDuration = string.Empty;
                    return true;
                }

                strXMLDuration = System.Runtime.Remoting.Metadata.W3cXsd2001.SoapDuration.ToString(dtTimeSpan);

                if (strXMLDuration.Length == 0)
                {
                    isZero = true;
                    return true;
                }

                if (strXMLDuration[0] == '-')
                {
                    strXMLDuration = strXMLDuration.Substring(1);
                }

                if (bytSecondsValueDigitsAfterDecimal < 9)
                {
                    // Look for "M\.\d+S"
                    var reSecondsRegEx = new Regex(@"M(\d+\.\d+)S");
                    var objMatch = reSecondsRegEx.Match(strXMLDuration);

                    if (objMatch.Success && objMatch.Groups.Count > 1 && IsNumber(objMatch.Groups[1].Captures[0].Value))
                    {
                        var dblSeconds = double.Parse(objMatch.Groups[1].Captures[0].Value);
                        strXMLDuration = strXMLDuration.Substring(0, objMatch.Groups[1].Index) +
                                         Math.Round(dblSeconds, bytSecondsValueDigitsAfterDecimal) + "S";
                    }
                }

                if (blnTrimLeadingZeroValues)
                {
                    var intDateIndex = strXMLDuration.IndexOf('P');
                    var intTimeIndex = strXMLDuration.IndexOf('T');
                    var intCharIndex = strXMLDuration.IndexOf("P0Y", StringComparison.Ordinal);

                    if (intCharIndex >= 0 && intCharIndex < intTimeIndex)
                    {
                        intCharIndex += 1;
                        var intCharIndex2 = strXMLDuration.IndexOf("Y0M", intCharIndex, StringComparison.Ordinal);

                        if (intCharIndex2 > 0 && intCharIndex < intTimeIndex)
                        {
                            intCharIndex = intCharIndex2 + 1;
                            intCharIndex2 = strXMLDuration.IndexOf("M0D", intCharIndex, StringComparison.Ordinal);

                            if (intCharIndex2 > 0 && intCharIndex < intTimeIndex)
                            {
                                intCharIndex = intCharIndex2 + 1;
                            }
                        }
                    }

                    if (intCharIndex > 0)
                    {
                        strXMLDuration = strXMLDuration.Substring(0, intDateIndex + 1) + strXMLDuration.Substring(intCharIndex + 2);
                        intTimeIndex = strXMLDuration.IndexOf('T');
                        intCharIndex = strXMLDuration.IndexOf("T0H", intTimeIndex, StringComparison.Ordinal);

                        if (intCharIndex > 0)
                        {
                            intCharIndex += 1;
                            var intCharIndex2 = strXMLDuration.IndexOf("H0M", intCharIndex, StringComparison.Ordinal);

                            if (intCharIndex2 > 0)
                            {
                                intCharIndex = intCharIndex2 + 1;
                            }
                        }

                        if (intCharIndex > 0)
                        {
                            strXMLDuration = strXMLDuration.Substring(0, intTimeIndex + 1) + strXMLDuration.Substring(intCharIndex + 2);
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowWarning("Error in ConvertTimeFromTimespanToXmlDuration: {0}", ex.Message);
                strXMLDuration = string.Empty;
                return false;
            }
        }

        /// <summary>
        /// Convert an XML duration to a TimeSpan
        /// </summary>
        /// <remarks>
        /// XML duration value is typically of the form "PT249.559S" or "PT4M9.559S"
        /// where the S indicates seconds and M indicates minutes
        /// Thus, "PT249.559S" means 249.559 seconds while
        /// "PT4M9.559S" means 4 minutes plus 9.559 seconds
        /// </remarks>
        /// <param name="strTime"></param>
        /// <param name="dtDefaultTimeSpan"></param>
        /// <returns>TimeSpan, or dtDefaultTimeSpan if an error</returns>
        public static TimeSpan ConvertTimeFromXmlDurationToTimespan(string strTime, TimeSpan dtDefaultTimeSpan)
        {
            // Official definition:
            // A length of time given in the ISO 8601 extended format: PnYnMnDTnHnMnS. The number of seconds
            // can be a decimal or an integer. All the other values must be non-negative integers. For example,
            // P1Y2M3DT4H5M6.7S is one year, two months, three days, four hours, five minutes, and 6.7 seconds.

            TimeSpan dtTimeSpan;

            try
            {
                dtTimeSpan = System.Runtime.Remoting.Metadata.W3cXsd2001.SoapDuration.Parse(strTime);
            }
            catch (Exception ex)
            {
                dtTimeSpan = dtDefaultTimeSpan;
            }

            return dtTimeSpan;
        }

        protected float GetAttribTimeValueMinutes(string strAttributeName)
        {
            try
            {
                var dtTimeSpan = ConvertTimeFromXmlDurationToTimespan(GetAttribValue(strAttributeName, "PT0S"), new TimeSpan(0L));
                return (float)dtTimeSpan.TotalMinutes;
            }
            catch (Exception ex)
            {
                return 0f;
            }
        }

        protected string GetAttribValue(string strAttributeName, string DefaultValue)
        {
            try
            {
                string strValue;

                if (mXMLReader.HasAttributes)
                {
                    strValue = mXMLReader.GetAttribute(strAttributeName);

                    if (strValue is null)
                        strValue = string.Copy(DefaultValue);
                }
                else
                {
                    strValue = string.Copy(DefaultValue);
                }

                return strValue;
            }
            catch (Exception ex)
            {
                return DefaultValue;
            }
        }

        protected int GetAttribValue(string strAttributeName, int DefaultValue)
        {
            try
            {
                return int.Parse(GetAttribValue(strAttributeName, DefaultValue.ToString()));
            }
            catch (Exception ex)
            {
                return DefaultValue;
            }
        }

        protected float GetAttribValue(string strAttributeName, float DefaultValue)
        {
            try
            {
                return float.Parse(GetAttribValue(strAttributeName, DefaultValue.ToString()));
            }
            catch (Exception ex)
            {
                return DefaultValue;
            }
        }

        protected bool GetAttribValue(string strAttributeName, bool DefaultValue)
        {
            try
            {
                return CBoolSafe(GetAttribValue(strAttributeName, DefaultValue.ToString()), DefaultValue);
            }
            catch (Exception ex)
            {
                return DefaultValue;
            }
        }

        protected double GetAttribValue(string strAttributeName, double DefaultValue)
        {
            try
            {
                return double.Parse(GetAttribValue(strAttributeName, DefaultValue.ToString()));
            }
            catch (Exception ex)
            {
                return DefaultValue;
            }
        }

        protected abstract clsSpectrumInfo GetCurrentSpectrum();

        /// <summary>
        /// Obtain the element name one level up from intDepth
        /// </summary>
        /// <remarks>
        /// If intDepth = 0, returns the element name one level up from the last entry in mParentElementStack
        /// </remarks>
        /// <param name="intElementDepth"></param>
        protected string GetParentElement(int intElementDepth = 0)
        {
            if (intElementDepth == 0)
            {
                intElementDepth = mParentElementStack.Count;
            }

            if (intElementDepth >= 2 && intElementDepth <= mParentElementStack.Count)
            {
                try
                {
                    var udtElementInfo = (udtElementInfoType)mParentElementStack.ToArray()[mParentElementStack.Count - intElementDepth + 1];
                    return udtElementInfo.Name;
                }
                catch (Exception ex)
                {
                    return string.Empty;
                }
            }
            else
            {
                return string.Empty;
            }
        }

        protected override string GetInputFileLocation()
        {
            return "Line " + SAXParserLineNumber.ToString() + ", Column " + SAXParserColumnNumber.ToString();
        }

        protected abstract void InitializeCurrentSpectrum(bool blnAutoShrinkDataLists);

        protected override void InitializeLocalVariables()
        {
            // This method is called from OpenFile and OpenTextStream;
            // thus, do not update mSkipBinaryData

            base.InitializeLocalVariables();
            mSkipNextReaderAdvance = false;
            mSkippedStartElementAdvance = false;
            mSpectrumFound = false;
            mCurrentElement = string.Empty;

            if (mParentElementStack is null)
            {
                mParentElementStack = new Stack();
            }
            else
            {
                mParentElementStack.Clear();
            }
        }

        /// <summary>
        /// Open the data file
        /// </summary>
        /// <param name="strInputFilePath"></param>
        /// <returns>True if successful, false if an error</returns>
        public override bool OpenFile(string strInputFilePath)
        {
            try
            {
                var success = OpenFileInit(strInputFilePath);

                if (!success)
                    return false;

                // Initialize the stream reader and the XML Text Reader (set to skip all whitespace)
                mDataFileOrTextStream = new StreamReader(strInputFilePath);
                var reader = new XmlTextReader(mDataFileOrTextStream) { WhitespaceHandling = WhitespaceHandling.None };
                mXMLReader = reader;
                mErrorMessage = string.Empty;
                InitializeLocalVariables();
                ResetProgress("Parsing " + Path.GetFileName(strInputFilePath));
                return true;
            }
            catch (Exception ex)
            {
                mErrorMessage = "Error opening file: " + strInputFilePath + "; " + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Open the text stream
        /// </summary>
        /// <param name="strTextStream"></param>
        /// <returns>True if successful, false if an error</returns>
        public override bool OpenTextStream(string strTextStream)
        {
            // Make sure any open file or text stream is closed
            CloseFile();

            try
            {
                mInputFilePath = "TextStream";

                // Initialize the stream reader and the XML Text Reader (set to skip all whitespace)
                mDataFileOrTextStream = new StringReader(strTextStream);
                var reader = new XmlTextReader(mDataFileOrTextStream) { WhitespaceHandling = WhitespaceHandling.None };
                mXMLReader = reader;
                mErrorMessage = string.Empty;
                InitializeLocalVariables();
                ResetProgress("Parsing text stream");
                return true;
            }
            catch (Exception ex)
            {
                mErrorMessage = "Error opening text stream";
                OnErrorEvent(mErrorMessage, ex);
                return false;
            }
        }

        protected string ParentElementStackRemove()
        {
            // Removes the most recent entry from mParentElementStack and returns it
            if (mParentElementStack.Count == 0)
            {
                return string.Empty;
            }
            else
            {
                var udtElementInfo = (udtElementInfoType)mParentElementStack.Pop();
                return udtElementInfo.Name;
            }
        }

        /// <summary>
        /// Adds a new entry to the end of mParentElementStack
        /// </summary>
        /// <param name="objXMLReader"></param>
        protected void ParentElementStackAdd(XmlReader objXMLReader)
        {
            // Since the XML Text Reader doesn't recognize implicit end elements (e.g. the "/>" characters at
            // the end of <City name="Laramie" />) we need to compare the depth of the current element with
            // the depth of the element at the top of the stack
            // If the depth values are the same, we pop the top element off and push the new element on
            // If the depth values are not the same, we push the new element on

            udtElementInfoType udtElementInfo;

            if (mParentElementStack.Count > 0)
            {
                udtElementInfo = (udtElementInfoType)mParentElementStack.Peek();

                if (udtElementInfo.Depth == objXMLReader.Depth)
                {
                    mParentElementStack.Pop();
                }
            }

            udtElementInfo.Name = objXMLReader.Name;
            udtElementInfo.Depth = objXMLReader.Depth;
            mParentElementStack.Push(udtElementInfo);
        }

        protected abstract void ParseStartElement();

        protected abstract void ParseElementContent();

        protected abstract void ParseEndElement();

        /// <summary>
        /// Read the next spectrum from an mzXML or mzData file
        /// </summary>
        /// <param name="objSpectrumInfo"></param>
        /// <returns>True if a spectrum is found, otherwise, returns False</returns>
        public override bool ReadNextSpectrum(out clsSpectrumInfo objSpectrumInfo)
        {
            try
            {
                InitializeCurrentSpectrum(mAutoShrinkDataLists);
                mSpectrumFound = false;

                if (mXMLReader is null)
                {
                    objSpectrumInfo = new clsSpectrumInfo();
                    mErrorMessage = "Data file not currently open";
                }
                else
                {
                    if (mDataFileOrTextStream != null)
                    {
                        if (mDataFileOrTextStream is StreamReader streamReader)
                        {
                            {
                                UpdateProgress(streamReader.BaseStream.Position / (double)streamReader.BaseStream.Length * 100.0d);
                            }
                        }
                        else
                        {
                            if (mXMLReader is XmlTextReader xmlReader)
                            {
                                // Note that 1000 is an arbitrary value for the number of lines in the input stream
                                // (only needed if mDataFileOrTextStream is a StringReader)
                                UpdateProgress(xmlReader.LineNumber % 1000 / 1000d * 100.0d);
                            }
                        }
                    }

                    var validData = true;

                    while (!mSpectrumFound && validData && !mAbortProcessing && mXMLReader.ReadState == ReadState.Initial || mXMLReader.ReadState == ReadState.Interactive)
                    {
                        mSpectrumFound = false;

                        if (mSkipNextReaderAdvance)
                        {
                            mSkipNextReaderAdvance = false;

                            try
                            {
                                if (mXMLReader.NodeType == XmlNodeType.Element)
                                {
                                    mSkippedStartElementAdvance = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                // Ignore Errors Here
                            }

                            validData = true;
                        }
                        else
                        {
                            mSkippedStartElementAdvance = false;
                            validData = mXMLReader.Read();
                            XMLTextReaderSkipWhitespace();
                        }

                        if (validData && mXMLReader.ReadState == ReadState.Interactive)
                        {
                            if (mXMLReader.NodeType == XmlNodeType.Element)
                            {
                                ParseStartElement();
                            }
                            else if (mXMLReader.NodeType == XmlNodeType.EndElement)
                            {
                                ParseEndElement();
                            }
                            else if (mXMLReader.NodeType == XmlNodeType.Text)
                            {
                                ParseElementContent();
                            }
                        }
                    }

                    objSpectrumInfo = GetCurrentSpectrum();

                    if (mSpectrumFound && !ReadingAndStoringSpectra)
                    {
                        if (mInputFileStats.ScanCount == 0)
                            mInputFileStats.ScanCount = 1;
                        UpdateFileStats(mInputFileStats.ScanCount, objSpectrumInfo.ScanNumber);
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ReadNextSpectrum", ex);
                objSpectrumInfo = new clsSpectrumInfo();
            }

            return mSpectrumFound;
        }

        protected string XMLTextReaderGetInnerText()
        {
            bool success;

            if (mXMLReader.NodeType == XmlNodeType.Element)
            {
                // Advance the reader so that we can read the value
                success = mXMLReader.Read();
            }
            else
            {
                success = true;
            }

            if (success && mXMLReader.NodeType != XmlNodeType.Whitespace && mXMLReader.HasValue)
            {
                return mXMLReader.Value;
            }

            return string.Empty;
        }

        private void XMLTextReaderSkipWhitespace()
        {
            try
            {
                if (mXMLReader.NodeType == XmlNodeType.Whitespace)
                {
                    // Whitespace; read the next node
                    mXMLReader.Read();
                }
            }
            catch (Exception ex)
            {
                // Ignore Errors Here
            }
        }
    }
}