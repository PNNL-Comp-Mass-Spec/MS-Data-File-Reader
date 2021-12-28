using System;
using System.Xml;

namespace MSDataFileReader
{

    // This class uses a SAX Parser to read an mzXML file

    // -------------------------------------------------------------------------------
    // Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    // Copyright 2006, Battelle Memorial Institute.  All Rights Reserved.
    // Started March 26, 2006
    // 
    // E-mail: matthew.monroe@pnl.gov or proteomics@pnnl.gov
    // Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://panomics.pnnl.gov/ or https://www.pnnl.gov/integrative-omics
    // -------------------------------------------------------------------------------

    public class clsMzXMLFileReader : clsMSXMLFileReaderBaseClass
    {
        public clsMzXMLFileReader()
        {
            InitializeLocalVariables();
        }

        #region Constants and Enums
        // Note: The extensions must be in all caps
        public const string MZXML_FILE_EXTENSION = ".MZXML";
        public const string MZXML_FILE_EXTENSION_XML = "_MZXML.XML";

        // Note that I'm using classes to group the constants
        private class XMLSectionNames
        {
            public const string RootName = "mzXML";
            public const string msRun = "msRun";
        }

        private class mzXMLRootAttrbuteNames
        {
            public const string xmlns = "xmlns";
            public const string xsi_schemaLocation = "xsi:schemaLocation";
        }

        private class HeaderSectionNames
        {
            public const string msInstrument = "msInstrument";
            public const string dataProcessing = "dataProcessing";
        }

        private class ScanSectionNames
        {
            public const string scan = "scan";
            public const string precursorMz = "precursorMz";
            public const string peaks = "peaks";
        }

        private class MSRunAttributeNames
        {
            public const string scanCount = "scanCount";
            public const string startTime = "startTime";
            public const string endTime = "endTime";
        }

        private class DataProcessingAttributeNames
        {
            public const string centroided = "centroided";
        }

        private class ScanAttributeNames
        {
            public const string num = "num";
            public const string msLevel = "msLevel";

            // 0 or 1
            public const string centroided = "centroided";
            public const string peaksCount = "peaksCount";
            public const string polarity = "polarity";

            // Options are: Full, zoom, SIM, SRM, MRM, CRM, Q1, or Q3; note that MRM and SRM and functionally equivalent; ReadW uses SRM
            public const string scanType = "scanType";

            // Thermo-specific filter-line text; added by ReadW
            public const string filterLine = "filterLine";

            // Example retention time: PT1.0373S
            public const string retentionTime = "retentionTime";

            // Collision energy used to fragment the parent ion
            public const string collisionEnergy = "collisionEnergy";

            // Setted low m/z boundary (this is the instrumetal setting); not present in .mzXML files created with ReadW
            public const string startMz = "startMz";

            // Setted high m/z boundary (this is the instrumetal setting); not present in .mzXML files created with ReadW
            public const string endMz = "endMz";

            // Observed low m/z (this is what the actual data looks like
            public const string lowMz = "lowMz";

            // Observed high m/z (this is what the actual data looks like
            public const string highMz = "highMz";

            // m/z of the base peak (most intense peak)
            public const string basePeakMz = "basePeakMz";

            // Intensity of the base peak (most intense peak)
            public const string basePeakIntensity = "basePeakIntensity";

            // Total ion current (total intensity in the scan)
            public const string totIonCurrent = "totIonCurrent";
            public const string msInstrumentID = "msInstrumentID";
        }

        private class PrecursorAttributeNames
        {
            // Scan number of the precursor
            public const string precursorScanNum = "precursorScanNum";

            // Intensity of the precursor ion
            public const string precursorIntensity = "precursorIntensity";

            // Charge of the precursor, typically determined at time of acquisition by the mass spectrometer
            public const string precursorCharge = "precursorCharge";

            // Fragmentation method, e.g. CID, ETD, or HCD
            public const string activationMethod = "activationMethod";

            // Isolation window width, e.g. 2.0
            public const string windowWideness = "windowWideness";
        }

        private class PeaksAttributeNames
        {
            public const string precision = "precision";
            public const string byteOrder = "byteOrder";

            // For example, "m/z-int"  ; superseded by "contentType" in mzXML 3
            public const string pairOrder = "pairOrder";

            // Allowed values are: "none" or "zlib"
            public const string compressionType = "compressionType";

            // Integer value required when using zlib compression
            public const string compressedLen = "compressedLen";

            // Allowed values are: "m/z-int", "m/z", "intensity", "S/N", "charge", "m/z ruler", "TOF"
            public const string contentType = "contentType";
        }

        private enum eCurrentMZXMLDataFileSectionConstants : int
        {
            UnknownFile = 0,
            Start = 1,
            msRun = 2,
            msInstrument = 3,
            dataProcessing = 4,
            ScanList = 5
        }

        #endregion

        #region Structures

        private struct udtFileStatsAddnlType
        {
            public float StartTimeMin;
            public float EndTimeMin;
            public bool IsCentroid;      // True if centroid (aka stick) data; False if profile (aka continuum) data
        }

        #endregion

        #region Classwide Variables

        private eCurrentMZXMLDataFileSectionConstants mCurrentXMLDataFileSection;
        private int mScanDepth;       // > 0 if we're inside a scan element
        private clsSpectrumInfoMzXML mCurrentSpectrum;
        private udtFileStatsAddnlType mInputFileStatsAddnl;

        #endregion

        #region Processing Options and Interface Functions

        public float FileInfoStartTimeMin
        {
            get
            {
                return mInputFileStatsAddnl.StartTimeMin;
            }
        }

        public float FileInfoEndTimeMin
        {
            get
            {
                return mInputFileStatsAddnl.EndTimeMin;
            }
        }

        public bool FileInfoIsCentroid
        {
            get
            {
                return mInputFileStatsAddnl.IsCentroid;
            }
        }

        #endregion

        protected override clsSpectrumInfo GetCurrentSpectrum()
        {
            return mCurrentSpectrum;
        }

        protected override void InitializeCurrentSpectrum(bool blnAutoShrinkDataLists)
        {
            if (ReadingAndStoringSpectra || mCurrentSpectrum is null)
            {
                mCurrentSpectrum = new clsSpectrumInfoMzXML();
            }
            else
            {
                mCurrentSpectrum.Clear();
            }

            mCurrentSpectrum.AutoShrinkDataLists = blnAutoShrinkDataLists;
        }

        protected override void InitializeLocalVariables()
        {
            base.InitializeLocalVariables();
            mCurrentXMLDataFileSection = eCurrentMZXMLDataFileSectionConstants.UnknownFile;
            mScanDepth = 0;
            {
                ref var withBlock = ref mInputFileStatsAddnl;
                withBlock.StartTimeMin = 0f;
                withBlock.EndTimeMin = 0f;
                withBlock.IsCentroid = false;
            }
        }

        public override bool OpenFile(string strInputFilePath)
        {
            bool blnSuccess;
            InitializeLocalVariables();
            blnSuccess = base.OpenFile(strInputFilePath);
            return blnSuccess;
        }

        private bool ParseBinaryData(string strMSMSDataBase64Encoded, string strCompressionType)
        {
            // Parses strMSMSDataBase64Encoded and stores the data in mIntensityList() and mMZList()

            float[] sngDataArray = null;
            double[] dblDataArray = null;
            bool zLibCompressed = false;
            var eEndianMode = clsBase64EncodeDecode.eEndianTypeConstants.BigEndian;
            int intIndex;
            bool blnSuccess;
            if (mCurrentSpectrum is null)
            {
                return false;
            }

            blnSuccess = false;
            if (strMSMSDataBase64Encoded is null || strMSMSDataBase64Encoded.Length == 0)
            {
                {
                    ref var withBlock = ref mCurrentSpectrum;
                    withBlock.DataCount = 0;
                    withBlock.MZList = new double[0];
                    withBlock.IntensityList = new float[0];
                }
            }
            else
            {
                try
                {
                    if ((strCompressionType ?? "") == clsSpectrumInfoMzXML.CompressionTypes.zlib)
                    {
                        zLibCompressed = true;
                    }
                    else
                    {
                        zLibCompressed = false;
                    }

                    switch (mCurrentSpectrum.NumericPrecisionOfData)
                    {
                        case 32:
                            {
                                if (clsBase64EncodeDecode.DecodeNumericArray(strMSMSDataBase64Encoded, out sngDataArray, zLibCompressed, eEndianMode))
                                {

                                    // sngDataArray now contains pairs of singles, either m/z and intensity or intensity and m/z
                                    // Need to split this apart into two arrays

                                    {
                                        ref var withBlock1 = ref mCurrentSpectrum;
                                        withBlock1.MZList = new double[((int)Math.Round(sngDataArray.Length / 2d))];
                                        withBlock1.IntensityList = new float[((int)Math.Round(sngDataArray.Length / 2d))];
                                        if ((mCurrentSpectrum.PeaksPairOrder ?? "") == clsSpectrumInfoMzXML.PairOrderTypes.IntensityAndMZ)
                                        {
                                            var loopTo = sngDataArray.Length - 1;
                                            for (intIndex = 0; intIndex <= loopTo; intIndex += 2)
                                            {
                                                withBlock1.IntensityList[(int)Math.Round(intIndex / 2d)] = sngDataArray[intIndex];
                                                withBlock1.MZList[(int)Math.Round(intIndex / 2d)] = sngDataArray[intIndex + 1];
                                            }
                                        }
                                        else
                                        {
                                            // Assume PairOrderTypes.MZandIntensity
                                            var loopTo1 = sngDataArray.Length - 1;
                                            for (intIndex = 0; intIndex <= loopTo1; intIndex += 2)
                                            {
                                                withBlock1.MZList[(int)Math.Round(intIndex / 2d)] = sngDataArray[intIndex];
                                                withBlock1.IntensityList[(int)Math.Round(intIndex / 2d)] = sngDataArray[intIndex + 1];
                                            }
                                        }
                                    }

                                    blnSuccess = true;
                                }

                                break;
                            }

                        case 64:
                            {
                                if (clsBase64EncodeDecode.DecodeNumericArray(strMSMSDataBase64Encoded, out dblDataArray, zLibCompressed, eEndianMode))
                                {
                                    // dblDataArray now contains pairs of doubles, either m/z and intensity or intensity and m/z
                                    // Need to split this apart into two arrays

                                    {
                                        ref var withBlock2 = ref mCurrentSpectrum;
                                        withBlock2.MZList = new double[((int)Math.Round(dblDataArray.Length / 2d))];
                                        withBlock2.IntensityList = new float[((int)Math.Round(dblDataArray.Length / 2d))];
                                        if ((mCurrentSpectrum.PeaksPairOrder ?? "") == clsSpectrumInfoMzXML.PairOrderTypes.IntensityAndMZ)
                                        {
                                            var loopTo2 = dblDataArray.Length - 1;
                                            for (intIndex = 0; intIndex <= loopTo2; intIndex += 2)
                                            {
                                                withBlock2.IntensityList[(int)Math.Round(intIndex / 2d)] = (float)dblDataArray[intIndex];
                                                withBlock2.MZList[(int)Math.Round(intIndex / 2d)] = dblDataArray[intIndex + 1];
                                            }
                                        }
                                        else
                                        {
                                            // Assume PairOrderTypes.MZandIntensity
                                            var loopTo3 = dblDataArray.Length - 1;
                                            for (intIndex = 0; intIndex <= loopTo3; intIndex += 2)
                                            {
                                                withBlock2.MZList[(int)Math.Round(intIndex / 2d)] = dblDataArray[intIndex];
                                                withBlock2.IntensityList[(int)Math.Round(intIndex / 2d)] = (float)dblDataArray[intIndex + 1];
                                            }
                                        }
                                    }

                                    blnSuccess = true;
                                }

                                break;
                            }

                        default:
                            {
                                break;
                            }
                            // Invalid numeric precision
                    }

                    if (blnSuccess)
                    {
                        {
                            ref var withBlock3 = ref mCurrentSpectrum;
                            if (withBlock3.MZList.Length != withBlock3.DataCount)
                            {
                                if (withBlock3.DataCount == 0 && withBlock3.MZList.Length > 0 && Math.Abs(withBlock3.MZList[0] - 0d) < float.Epsilon && Math.Abs(withBlock3.IntensityList[0] - 0f) < float.Epsilon)
                                {
                                }
                                // Leave .PeaksCount at 0
                                else
                                {
                                    if (withBlock3.MZList.Length > 1 && withBlock3.IntensityList.Length > 1)
                                    {
                                        // Check whether the last entry has a mass and intensity of 0
                                        if (Math.Abs(withBlock3.MZList[withBlock3.MZList.Length - 1]) < float.Epsilon && Math.Abs(withBlock3.IntensityList[withBlock3.MZList.Length - 1]) < float.Epsilon)
                                        {
                                            // Remove the final entry
                                            Array.Resize(ref withBlock3.MZList, withBlock3.MZList.Length - 2 + 1);
                                            Array.Resize(ref withBlock3.IntensityList, withBlock3.IntensityList.Length - 2 + 1);
                                        }
                                    }

                                    if (withBlock3.MZList.Length != withBlock3.DataCount)
                                    {
                                        // This shouldn't normally be necessary
                                        OnErrorEvent("Unexpected condition in ParseBinaryData: .MZList.Length <> .DataCount and .DataCount > 0");
                                        withBlock3.DataCount = withBlock3.MZList.Length;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    OnErrorEvent("Error in ParseBinaryData", ex);
                }
            }

            return blnSuccess;
        }

        protected override void ParseElementContent()
        {
            bool blnSuccess;
            if (mAbortProcessing)
                return;
            if (mCurrentSpectrum is null)
                return;
            try
            {
                // Skip the element if we aren't parsing a scan (inside a scan element)
                // This is an easy way to skip whitespace
                // We can do this since since we only care about the data inside the
                // ScanSectionNames.precursorMz and ScanSectionNames.peaks elements
                if (mScanDepth > 0)
                {
                    // Check the last element name sent to startElement to determine
                    // what to do with the data we just received
                    switch (mCurrentElement ?? "")
                    {
                        case ScanSectionNames.precursorMz:
                            {
                                try
                                {
                                    mCurrentSpectrum.ParentIonMZ = double.Parse(XMLTextReaderGetInnerText());
                                }
                                catch (Exception ex)
                                {
                                    mCurrentSpectrum.ParentIonMZ = 0d;
                                }

                                break;
                            }

                        case ScanSectionNames.peaks:
                            {
                                if (!mSkipBinaryData)
                                {
                                    blnSuccess = ParseBinaryData(XMLTextReaderGetInnerText(), mCurrentSpectrum.CompressionType);
                                }
                                else
                                {
                                    blnSuccess = true;
                                }

                                break;
                            }
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ParseElementContent", ex);
            }
        }

        protected override void ParseEndElement()
        {
            if (mAbortProcessing)
                return;
            if (mCurrentSpectrum is null)
                return;
            try
            {
                // If we just moved out of a scan element, then finalize the current scan
                if ((mXMLReader.Name ?? "") == ScanSectionNames.scan)
                {
                    if (mCurrentSpectrum.SpectrumStatus != clsSpectrumInfo.eSpectrumStatusConstants.Initialized && mCurrentSpectrum.SpectrumStatus != clsSpectrumInfo.eSpectrumStatusConstants.Validated)
                    {
                        mCurrentSpectrum.Validate();
                        mSpectrumFound = true;
                    }

                    mScanDepth -= 1;
                    if (mScanDepth < 0)
                    {
                        // This shouldn't happen
                        OnErrorEvent("Unexpected condition in ParseEndElement: mScanDepth < 0");
                        mScanDepth = 0;
                    }
                }

                ParentElementStackRemove();

                // Clear the current element name
                mCurrentElement = string.Empty;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ParseEndElement", ex);
            }
        }

        protected override void ParseStartElement()
        {
            string strValue;
            bool blnAttributeMissing;
            int intInstrumentID;
            if (mAbortProcessing)
                return;
            if (mCurrentSpectrum is null)
                return;
            if (!mSkippedStartElementAdvance)
            {
                // Add mXMLReader.Name to mParentElementStack
                ParentElementStackAdd(mXMLReader);
            }

            // Store name of the element we just entered
            mCurrentElement = mXMLReader.Name;
            switch (mXMLReader.Name ?? "")
            {
                case ScanSectionNames.scan:
                    {
                        mCurrentXMLDataFileSection = eCurrentMZXMLDataFileSectionConstants.ScanList;
                        if (mScanDepth > 0 && !mSkippedStartElementAdvance)
                        {
                            if (mCurrentSpectrum.SpectrumStatus != clsSpectrumInfo.eSpectrumStatusConstants.Initialized && mCurrentSpectrum.SpectrumStatus != clsSpectrumInfo.eSpectrumStatusConstants.Validated)
                            {
                                mCurrentSpectrum.Validate();
                                mSkipNextReaderAdvance = true;
                                mSpectrumFound = true;
                                return;
                            }
                        }

                        mCurrentSpectrum.Clear();
                        mScanDepth += 1;
                        if (!mXMLReader.HasAttributes)
                        {
                            blnAttributeMissing = true;
                        }
                        else
                        {
                            mCurrentSpectrum.ScanNumber = GetAttribValue(ScanAttributeNames.num, int.MinValue);
                            if (mCurrentSpectrum.ScanNumber == int.MinValue)
                            {
                                blnAttributeMissing = true;
                            }
                            else
                            {
                                blnAttributeMissing = false;
                                {
                                    ref var withBlock = ref mCurrentSpectrum;
                                    withBlock.ScanCount = 1;
                                    withBlock.ScanNumberEnd = withBlock.ScanNumber;
                                    withBlock.MSLevel = GetAttribValue(ScanAttributeNames.msLevel, 1);
                                    if (GetAttribValue(ScanAttributeNames.centroided, 0) == 0)
                                    {
                                        withBlock.Centroided = false;
                                    }
                                    else
                                    {
                                        withBlock.Centroided = true;
                                    }

                                    intInstrumentID = GetAttribValue(ScanAttributeNames.msInstrumentID, 1);
                                    withBlock.DataCount = GetAttribValue(ScanAttributeNames.peaksCount, 0);
                                    withBlock.Polarity = GetAttribValue(ScanAttributeNames.polarity, "+");
                                    withBlock.RetentionTimeMin = GetAttribTimeValueMinutes(ScanAttributeNames.retentionTime);
                                    withBlock.ScanType = GetAttribValue(ScanAttributeNames.scanType, "");
                                    withBlock.FilterLine = GetAttribValue(ScanAttributeNames.filterLine, "");
                                    withBlock.StartMZ = GetAttribValue(ScanAttributeNames.startMz, 0);
                                    withBlock.EndMZ = GetAttribValue(ScanAttributeNames.endMz, 0);
                                    withBlock.mzRangeStart = GetAttribValue(ScanAttributeNames.lowMz, 0);
                                    withBlock.mzRangeEnd = GetAttribValue(ScanAttributeNames.highMz, 0);
                                    withBlock.BasePeakMZ = GetAttribValue(ScanAttributeNames.basePeakMz, 0);
                                    withBlock.BasePeakIntensity = GetAttribValue(ScanAttributeNames.basePeakIntensity, 0);
                                    withBlock.TotalIonCurrent = GetAttribValue(ScanAttributeNames.totIonCurrent, 0);
                                }
                            }
                        }

                        if (blnAttributeMissing)
                        {
                            mCurrentSpectrum.ScanNumber = 0;
                            OnErrorEvent("Unable to read the 'num' attribute for the current scan since it is missing");
                        }

                        break;
                    }

                case ScanSectionNames.precursorMz:
                    {
                        if (mXMLReader.HasAttributes)
                        {
                            mCurrentSpectrum.ParentIonIntensity = GetAttribValue(PrecursorAttributeNames.precursorIntensity, 0);
                            mCurrentSpectrum.ActivationMethod = GetAttribValue(PrecursorAttributeNames.activationMethod, string.Empty);
                            mCurrentSpectrum.ParentIonCharge = GetAttribValue(PrecursorAttributeNames.precursorCharge, 0);
                            mCurrentSpectrum.PrecursorScanNum = GetAttribValue(PrecursorAttributeNames.precursorScanNum, 0);
                            mCurrentSpectrum.IsolationWindow = GetAttribValue(PrecursorAttributeNames.windowWideness, 0);
                        }

                        break;
                    }

                case ScanSectionNames.peaks:
                    {
                        if (mXMLReader.HasAttributes)
                        {
                            {
                                ref var withBlock1 = ref mCurrentSpectrum;
                                // mzXML 3.x files will have a contentType attribute
                                // Earlier versions will have a pairOrder attribute

                                withBlock1.PeaksPairOrder = GetAttribValue(PeaksAttributeNames.contentType, string.Empty);
                                if (!string.IsNullOrEmpty(withBlock1.PeaksPairOrder))
                                {
                                    // mzXML v3.x
                                    withBlock1.CompressionType = GetAttribValue(PeaksAttributeNames.compressionType, clsSpectrumInfoMzXML.CompressionTypes.none);
                                    withBlock1.CompressedLen = GetAttribValue(PeaksAttributeNames.compressedLen, 0);
                                }
                                else
                                {
                                    // mzXML v1.x or v2.x
                                    withBlock1.PeaksPairOrder = GetAttribValue(PeaksAttributeNames.pairOrder, clsSpectrumInfoMzXML.PairOrderTypes.MZandIntensity);
                                    withBlock1.CompressionType = clsSpectrumInfoMzXML.CompressionTypes.none;
                                    withBlock1.CompressedLen = 0;
                                }

                                withBlock1.NumericPrecisionOfData = GetAttribValue(PeaksAttributeNames.precision, 32);
                                withBlock1.PeaksByteOrder = GetAttribValue(PeaksAttributeNames.byteOrder, clsSpectrumInfoMzXML.ByteOrderTypes.network);
                            }
                        }

                        break;
                    }

                case XMLSectionNames.RootName:
                    {
                        mCurrentXMLDataFileSection = eCurrentMZXMLDataFileSectionConstants.Start;
                        if (mXMLReader.HasAttributes)
                        {
                            // First look for attribute xlmns
                            strValue = GetAttribValue(mzXMLRootAttrbuteNames.xmlns, string.Empty);
                            if (strValue is null || strValue.Length == 0)
                            {
                                // Attribute not found; look for attribute xsi:schemaLocation
                                strValue = GetAttribValue(mzXMLRootAttrbuteNames.xsi_schemaLocation, string.Empty);
                            }

                            ValidateMZXmlFileVersion(strValue);
                        }

                        break;
                    }

                case HeaderSectionNames.msInstrument:
                    {
                        if ((GetParentElement() ?? "") == XMLSectionNames.msRun)
                        {
                            mCurrentXMLDataFileSection = eCurrentMZXMLDataFileSectionConstants.msInstrument;
                        }

                        break;
                    }

                case XMLSectionNames.msRun:
                    {
                        mCurrentXMLDataFileSection = eCurrentMZXMLDataFileSectionConstants.msRun;
                        if (mXMLReader.HasAttributes)
                        {
                            mInputFileStats.ScanCount = GetAttribValue(MSRunAttributeNames.scanCount, 0);
                            {
                                ref var withBlock2 = ref mInputFileStatsAddnl;
                                withBlock2.StartTimeMin = GetAttribTimeValueMinutes(MSRunAttributeNames.startTime);
                                withBlock2.EndTimeMin = GetAttribTimeValueMinutes(MSRunAttributeNames.endTime);

                                // Note: A bug in the ReAdW software we use to create mzXML files records the .StartTime and .EndTime values in minutes but labels them as seconds
                                // Check for this by computing the average seconds/scan
                                // If too low, multiply the start and end times by 60
                                if (mInputFileStats.ScanCount > 0)
                                {
                                    if ((withBlock2.EndTimeMin - withBlock2.StartTimeMin) / mInputFileStats.ScanCount * 60f < 0.1d)
                                    {
                                        // Less than 0.1 sec/scan; this is unlikely
                                        withBlock2.StartTimeMin = withBlock2.StartTimeMin * 60f;
                                        withBlock2.EndTimeMin = withBlock2.EndTimeMin * 60f;
                                    }
                                }
                            }
                        }
                        else
                        {
                            mInputFileStats.ScanCount = 0;
                        }

                        break;
                    }

                case HeaderSectionNames.dataProcessing:
                    {
                        if ((GetParentElement() ?? "") == XMLSectionNames.msRun)
                        {
                            mCurrentXMLDataFileSection = eCurrentMZXMLDataFileSectionConstants.dataProcessing;
                            mInputFileStatsAddnl.IsCentroid = GetAttribValue(DataProcessingAttributeNames.centroided, false);
                        }

                        break;
                    }
            }

            mSkippedStartElementAdvance = false;
        }

        /// <summary>
    /// Updates the current XMLReader object with a new reader positioned at the XML for a new mass spectrum
    /// </summary>
    /// <param name="newReader"></param>
    /// <returns></returns>
    /// <remarks></remarks>
        public bool SetXMLReaderForSpectrum(XmlReader newReader)
        {
            try
            {
                mInputFilePath = "TextStream";
                mXMLReader = newReader;
                mErrorMessage = string.Empty;
                InitializeLocalVariables();
                return true;
            }
            catch (Exception ex)
            {
                mErrorMessage = "Error updating mXMLReader";
                return false;
            }
        }

        public static bool ExtractMzXmlFileVersion(string xmlWithFileVersion, out string xmlFileVersion)
        {

            // Currently, the supported versions are mzXML_2.x and mzXML_3.x
            var objFileVersionRegEx = new System.Text.RegularExpressions.Regex(@"mzXML_[^\s""/]+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Validate the mzXML file version
            if (!string.IsNullOrWhiteSpace(xmlWithFileVersion))
            {
                // Parse out the version number
                var objMatch = objFileVersionRegEx.Match(xmlWithFileVersion);
                if (objMatch.Success)
                {
                    // Record the version
                    xmlFileVersion = objMatch.Value;
                    return true;
                }
            }

            xmlFileVersion = string.Empty;
            return false;
        }

        private void ValidateMZXmlFileVersion(string xmlWithFileVersion)
        {
            // This sub should be called from ParseStartElement

            string strMessage;
            try
            {
                mFileVersion = string.Empty;
                if (!ExtractMzXmlFileVersion(xmlWithFileVersion, out mFileVersion))
                {
                    strMessage = "Unknown mzXML file version; expected text not found in xmlWithFileVersion";
                    if (mParseFilesWithUnknownVersion)
                    {
                        strMessage += "; attempting to parse since ParseFilesWithUnknownVersion = True";
                    }
                    else
                    {
                        mAbortProcessing = true;
                        strMessage += "; aborting read";
                    }

                    OnErrorEvent(strMessage);
                    return;
                }

                if (mFileVersion.Length > 0)
                {
                    if (!(mFileVersion.IndexOf("mzxml_2", StringComparison.InvariantCultureIgnoreCase) >= 0 || mFileVersion.IndexOf("mzxml_3", StringComparison.InvariantCultureIgnoreCase) >= 0))
                    {
                        // strFileVersion contains mzXML_ but not mxXML_2 or mxXML_3
                        // Thus, assume unknown version
                        // Log error and abort if mParseFilesWithUnknownVersion = False
                        strMessage = "Unknown mzXML file version: " + mFileVersion;
                        if (mParseFilesWithUnknownVersion)
                        {
                            strMessage += "; attempting to parse since ParseFilesWithUnknownVersion = True";
                        }
                        else
                        {
                            mAbortProcessing = true;
                            strMessage += "; aborting read";
                        }

                        OnErrorEvent(strMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ValidateMZXmlFileVersion", ex);
                mFileVersion = string.Empty;
            }
        }
    }
}