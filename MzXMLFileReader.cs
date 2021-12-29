﻿// -------------------------------------------------------------------------------
// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
// Copyright 2021, Battelle Memorial Institute.  All Rights Reserved.
//
// E-mail: matthew.monroe@pnl.gov or proteomics@pnnl.gov
// Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://panomics.pnnl.gov/ or https://www.pnnl.gov/integrative-omics
// -------------------------------------------------------------------------------

using System;
using System.Xml;

namespace MSDataFileReader
{
    /// <summary>
    /// This class uses a SAX Parser to read an mzXML file
    /// </summary>
    public class MzXMLFileReader : MsXMLFileReaderBaseClass
    {
        // Ignore Spelling: centroided, num, xmlns, xsi, zlib

        public MzXMLFileReader()
        {
            InitializeLocalVariables();
        }

        /// <summary>
        /// MzXML file extension
        /// </summary>
        /// <remarks>
        /// Must be in all caps
        /// </remarks>
        public const string MZXML_FILE_EXTENSION = ".MZXML";

        /// <summary>
        /// Alternative MzXML file extension
        /// </summary>
        /// <remarks>
        /// Must be in all caps
        /// </remarks>
        public const string MZXML_FILE_EXTENSION_XML = "_MZXML.XML";

        private static class XMLSectionNames
        {
            public const string RootName = "mzXML";

            public const string msRun = "msRun";
        }

        private static class mzXMLRootAttributeNames
        {
            public const string xmlns = "xmlns";

            public const string xsi_schemaLocation = "xsi:schemaLocation";
        }

        private static class HeaderSectionNames
        {
            public const string msInstrument = "msInstrument";

            public const string dataProcessing = "dataProcessing";
        }

        private static class ScanSectionNames
        {
            public const string scan = "scan";

            public const string precursorMz = "precursorMz";

            public const string peaks = "peaks";
        }

        private static class MSRunAttributeNames
        {
            public const string scanCount = "scanCount";

            public const string startTime = "startTime";

            public const string endTime = "endTime";
        }

        private static class DataProcessingAttributeNames
        {
            public const string centroided = "centroided";
        }

        private static class ScanAttributeNames
        {
            public const string num = "num";

            public const string msLevel = "msLevel";

            /// <summary>
            /// 0 means not centroided, 1 means centroided
            /// </summary>
            public const string centroided = "centroided";

            public const string peaksCount = "peaksCount";

            public const string polarity = "polarity";

            /// <summary>
            /// Scan type: Full, zoom, SIM, SRM, MRM, CRM, Q1, or Q3
            /// </summary>
            /// <remarks>
            /// MRM and SRM and functionally equivalent; ReadW uses SRM
            /// </remarks>
            public const string scanType = "scanType";

            /// <summary>
            /// Thermo-specific filter-line text; added by ReadW
            /// </summary>
            public const string filterLine = "filterLine";

            /// <summary>
            /// Retention time
            /// </summary>
            /// <remarks>
            /// Example: PT1.0373S
            /// </remarks>
            public const string retentionTime = "retentionTime";

            /// <summary>
            /// Collision energy used to fragment the parent ion
            /// </summary>
            // ReSharper disable once UnusedMember.Local
            public const string collisionEnergy = "collisionEnergy";

            /// <summary>
            /// Low m/z boundary (this is the instrumental setting)
            /// </summary>
            /// <remarks>
            /// Not present in .mzXML files created with ReadW
            /// </remarks>
            public const string startMz = "startMz";

            /// <summary>
            /// High m/z boundary (this is the instrumental setting)
            /// </summary>
            /// <remarks>
            /// Not present in .mzXML files created with ReadW
            /// </remarks>
            public const string endMz = "endMz";

            /// <summary>
            /// Observed low m/z (this is the m/z of the first ion observed)
            /// </summary>
            public const string lowMz = "lowMz";

            /// <summary>
            /// Observed high m/z (this is the m/z of the last ion observed)
            /// </summary>
            public const string highMz = "highMz";

            /// <summary>
            /// m/z of the base peak (most intense peak)
            /// </summary>
            public const string basePeakMz = "basePeakMz";

            /// <summary>
            /// Intensity of the base peak (most intense peak)
            /// </summary>
            public const string basePeakIntensity = "basePeakIntensity";

            /// <summary>
            /// Total ion current (total intensity in the scan)
            /// </summary>
            public const string totIonCurrent = "totIonCurrent";

            public const string msInstrumentID = "msInstrumentID";
        }

        private static class PrecursorAttributeNames
        {
            /// <summary>
            /// Scan number of the precursor
            /// </summary>
            public const string precursorScanNum = "precursorScanNum";

            /// <summary>
            /// Intensity of the precursor ion
            /// </summary>
            public const string precursorIntensity = "precursorIntensity";

            /// <summary>
            /// Charge of the precursor, typically determined at time of acquisition by the mass spectrometer
            /// </summary>
            public const string precursorCharge = "precursorCharge";

            /// <summary>
            /// Fragmentation method, e.g. CID, ETD, or HCD
            /// </summary>
            public const string activationMethod = "activationMethod";

            /// <summary>
            /// Isolation window width, e.g. 2.0
            /// </summary>
            public const string windowWideness = "windowWideness";
        }

        private static class PeaksAttributeNames
        {
            public const string precision = "precision";

            public const string byteOrder = "byteOrder";

            /// <summary>
            /// Pair order, for example: "m/z-int"
            /// </summary>
            /// <remarks>
            /// Superseded by "contentType" in mzXML 3
            /// </remarks>
            public const string pairOrder = "pairOrder";

            /// <summary>
            /// Compression type: "none" or "zlib"
            /// </summary>
            public const string compressionType = "compressionType";

            /// <summary>
            /// Integer value required when using zlib compression
            /// </summary>
            public const string compressedLen = "compressedLen";

            /// <summary>
            /// Content type: "m/z-int", "m/z", "intensity", "S/N", "charge", "m/z ruler", or "TOF"
            /// </summary>
            public const string contentType = "contentType";
        }

        private enum CurrentMzXMLDataFileSection
        {
            UnknownFile = 0,
            Start = 1,
            msRun = 2,
            msInstrument = 3,
            dataProcessing = 4,
            ScanList = 5
        }

        private struct FileStatsAddnlType
        {
            public float StartTimeMin;

            public float EndTimeMin;

            /// <summary>
            /// True if centroid (aka stick) data
            /// False if profile (aka continuum) data
            /// </summary>
            public bool IsCentroid;
        }

        private CurrentMzXMLDataFileSection mCurrentXMLDataFileSection;

        /// <summary>
        /// Scan depth
        /// </summary>
        /// <remarks>
        /// Greater than 0 if inside a scan element
        /// </remarks>
        private int mScanDepth;

        private SpectrumInfoMzXML mCurrentSpectrum;

        private FileStatsAddnlType mInputFileStatsAddnl;

        // ReSharper disable UnusedMember.Global

        public float FileInfoStartTimeMin => mInputFileStatsAddnl.StartTimeMin;

        public float FileInfoEndTimeMin => mInputFileStatsAddnl.EndTimeMin;

        public bool FileInfoIsCentroid => mInputFileStatsAddnl.IsCentroid;

        // ReSharper restore UnusedMember.Global

        protected override SpectrumInfo GetCurrentSpectrum()
        {
            return mCurrentSpectrum;
        }

        protected override void InitializeCurrentSpectrum(bool autoShrinkDataLists)
        {
            if (ReadingAndStoringSpectra || mCurrentSpectrum is null)
            {
                mCurrentSpectrum = new SpectrumInfoMzXML();
            }
            else
            {
                mCurrentSpectrum.Clear();
            }

            mCurrentSpectrum.AutoShrinkDataLists = autoShrinkDataLists;
        }

        protected sealed override void InitializeLocalVariables()
        {
            base.InitializeLocalVariables();
            mCurrentXMLDataFileSection = CurrentMzXMLDataFileSection.UnknownFile;
            mScanDepth = 0;

            mInputFileStatsAddnl.StartTimeMin = 0f;
            mInputFileStatsAddnl.EndTimeMin = 0f;
            mInputFileStatsAddnl.IsCentroid = false;
        }

        public override bool OpenFile(string inputFilePath)
        {
            InitializeLocalVariables();
            return base.OpenFile(inputFilePath);
        }

        /// <summary>
        /// Parse msmsDataBase64Encoded and store the data in mIntensityList() and mMZList()
        /// </summary>
        /// <param name="msmsDataBase64Encoded"></param>
        /// <param name="compressionType"></param>
        /// <returns>True if successful, false if an error</returns>
        private void ParseBinaryData(string msmsDataBase64Encoded, string compressionType)
        {
            var endianMode = Base64EncodeDecode.EndianType.BigEndian;

            if (mCurrentSpectrum is null)
            {
                return;
            }

            if (msmsDataBase64Encoded is null || msmsDataBase64Encoded.Length == 0)
            {
                mCurrentSpectrum.DataCount = 0;
                mCurrentSpectrum.MZList = Array.Empty<double>();
                mCurrentSpectrum.IntensityList = Array.Empty<float>();
                return;
            }

            try
            {
                var zLibCompressed = compressionType.Equals(SpectrumInfoMzXML.CompressionTypes.zlib);

                var success = false;

                switch (mCurrentSpectrum.NumericPrecisionOfData)
                {
                    case 32:
                        if (Base64EncodeDecode.DecodeNumericArray(msmsDataBase64Encoded, out float[] floatArray, zLibCompressed, endianMode))
                        {
                            // floatArray now contains pairs of singles, either m/z and intensity or intensity and m/z
                            // Need to split this apart into two arrays

                            mCurrentSpectrum.MZList = new double[(int)Math.Round(floatArray.Length / 2d)];
                            mCurrentSpectrum.IntensityList = new float[(int)Math.Round(floatArray.Length / 2d)];

                            if (mCurrentSpectrum.PeaksPairOrder.Equals(SpectrumInfoMzXML.PairOrderTypes.IntensityAndMZ))
                            {
                                var indexEnd = floatArray.Length - 1;

                                for (var index = 0; index <= indexEnd; index += 2)
                                {
                                    mCurrentSpectrum.IntensityList[(int)Math.Round(index / 2d)] = floatArray[index];
                                    mCurrentSpectrum.MZList[(int)Math.Round(index / 2d)] = floatArray[index + 1];
                                }
                            }
                            else
                            {
                                // Assume PairOrderTypes.MZandIntensity
                                var indexEnd = floatArray.Length - 1;

                                for (var index = 0; index <= indexEnd; index += 2)
                                {
                                    mCurrentSpectrum.MZList[(int)Math.Round(index / 2d)] = floatArray[index];
                                    mCurrentSpectrum.IntensityList[(int)Math.Round(index / 2d)] = floatArray[index + 1];
                                }
                            }

                            success = true;
                        }

                        break;

                    case 64:
                        if (Base64EncodeDecode.DecodeNumericArray(msmsDataBase64Encoded, out double[] doubleArray, zLibCompressed, endianMode))
                        {
                            // doubleArray now contains pairs of doubles, either m/z and intensity or intensity and m/z
                            // Need to split this apart into two arrays

                            mCurrentSpectrum.MZList = new double[(int)Math.Round(doubleArray.Length / 2d)];
                            mCurrentSpectrum.IntensityList = new float[(int)Math.Round(doubleArray.Length / 2d)];

                            if (mCurrentSpectrum.PeaksPairOrder.Equals(SpectrumInfoMzXML.PairOrderTypes.IntensityAndMZ))
                            {
                                var indexEnd = doubleArray.Length - 1;

                                for (var index = 0; index <= indexEnd; index += 2)
                                {
                                    mCurrentSpectrum.IntensityList[(int)Math.Round(index / 2d)] = (float)doubleArray[index];
                                    mCurrentSpectrum.MZList[(int)Math.Round(index / 2d)] = doubleArray[index + 1];
                                }
                            }
                            else
                            {
                                // Assume PairOrderTypes.MZandIntensity
                                var indexEnd = doubleArray.Length - 1;

                                for (var index = 0; index <= indexEnd; index += 2)
                                {
                                    mCurrentSpectrum.MZList[(int)Math.Round(index / 2d)] = doubleArray[index];
                                    mCurrentSpectrum.IntensityList[(int)Math.Round(index / 2d)] = (float)doubleArray[index + 1];
                                }
                            }

                            success = true;
                        }

                        break;

                    default:
                        // Invalid numeric precision
                        break;
                }

                if (!success)
                    return;

                if (mCurrentSpectrum.MZList.Length == mCurrentSpectrum.DataCount)
                {
                    return;
                }

                if (mCurrentSpectrum.DataCount == 0 && mCurrentSpectrum.MZList.Length > 0 &&
                    Math.Abs(mCurrentSpectrum.MZList[0] - 0d) < float.Epsilon &&
                    Math.Abs(mCurrentSpectrum.IntensityList[0] - 0f) < float.Epsilon)
                {
                    // Leave .PeaksCount at 0
                    return;
                }

                if (mCurrentSpectrum.MZList.Length > 1 && mCurrentSpectrum.IntensityList.Length > 1)
                {
                    // Check whether the last entry has a mass and intensity of 0
                    if (Math.Abs(mCurrentSpectrum.MZList[mCurrentSpectrum.MZList.Length - 1]) < float.Epsilon &&
                        Math.Abs(mCurrentSpectrum.IntensityList[mCurrentSpectrum.MZList.Length - 1]) < float.Epsilon)
                    {
                        // Remove the final entry
                        Array.Resize(ref mCurrentSpectrum.MZList, mCurrentSpectrum.MZList.Length - 2 + 1);
                        Array.Resize(ref mCurrentSpectrum.IntensityList, mCurrentSpectrum.IntensityList.Length - 2 + 1);
                    }
                }

                if (mCurrentSpectrum.MZList.Length != mCurrentSpectrum.DataCount)
                {
                    // This shouldn't normally be necessary
                    OnErrorEvent("Unexpected condition in ParseBinaryData: .MZList.Length <> .DataCount and .DataCount > 0");
                    mCurrentSpectrum.DataCount = mCurrentSpectrum.MZList.Length;
                }

                return;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ParseBinaryData", ex);
                return;
            }
        }

        protected override void ParseElementContent()
        {
            if (mAbortProcessing)
                return;

            if (mCurrentSpectrum is null)
                return;

            try
            {
                // Skip the element if we aren't parsing a scan (inside a scan element)
                // This is an easy way to skip whitespace
                // We can do this since we only care about the data inside the
                // ScanSectionNames.precursorMz and ScanSectionNames.peaks elements

                if (mScanDepth > 0)
                {
                    // Check the last element name sent to startElement to determine
                    // what to do with the data we just received
                    switch (mCurrentElement)
                    {
                        case ScanSectionNames.precursorMz:
                            try
                            {
                                mCurrentSpectrum.ParentIonMZ = double.Parse(XMLTextReaderGetInnerText());
                            }
                            catch (Exception ex)
                            {
                                mCurrentSpectrum.ParentIonMZ = 0d;
                            }

                            break;

                        case ScanSectionNames.peaks:
                            if (!mSkipBinaryData)
                            {
                                ParseBinaryData(XMLTextReaderGetInnerText(), mCurrentSpectrum.CompressionType);
                            }

                            break;
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
                // If we just moved out of a scan element, finalize the current scan
                if ((mXMLReader.Name) == ScanSectionNames.scan)
                {
                    if (mCurrentSpectrum.SpectrumStatus != SpectrumInfo.SpectrumStatusMode.Initialized && mCurrentSpectrum.SpectrumStatus != SpectrumInfo.SpectrumStatusMode.Validated)
                    {
                        mCurrentSpectrum.Validate();
                        mSpectrumFound = true;
                    }

                    mScanDepth--;

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

            switch (mXMLReader.Name)
            {
                case ScanSectionNames.scan:
                    mCurrentXMLDataFileSection = CurrentMzXMLDataFileSection.ScanList;

                    if (mScanDepth > 0 && !mSkippedStartElementAdvance)
                    {
                        if (mCurrentSpectrum.SpectrumStatus != SpectrumInfo.SpectrumStatusMode.Initialized && mCurrentSpectrum.SpectrumStatus != SpectrumInfo.SpectrumStatusMode.Validated)
                        {
                            mCurrentSpectrum.Validate();
                            mSkipNextReaderAdvance = true;
                            mSpectrumFound = true;
                            return;
                        }
                    }

                    mCurrentSpectrum.Clear();
                    mScanDepth++;
                    bool attributeMissing;

                    if (!mXMLReader.HasAttributes)
                    {
                        attributeMissing = true;
                    }
                    else
                    {
                        mCurrentSpectrum.ScanNumber = GetAttribValue(ScanAttributeNames.num, int.MinValue);

                        if (mCurrentSpectrum.ScanNumber == int.MinValue)
                        {
                            attributeMissing = true;
                        }
                        else
                        {
                            attributeMissing = false;
                            {
                                mCurrentSpectrum.ScanCount = 1;
                                mCurrentSpectrum.ScanNumberEnd = mCurrentSpectrum.ScanNumber;
                                mCurrentSpectrum.MSLevel = GetAttribValue(ScanAttributeNames.msLevel, 1);

                                if (GetAttribValue(ScanAttributeNames.centroided, 0) == 0)
                                {
                                    mCurrentSpectrum.Centroided = false;
                                }
                                else
                                {
                                    mCurrentSpectrum.Centroided = true;
                                }

                                // ReSharper disable once UnusedVariable
                                var instrumentID = GetAttribValue(ScanAttributeNames.msInstrumentID, 1);
                                mCurrentSpectrum.DataCount = GetAttribValue(ScanAttributeNames.peaksCount, 0);
                                mCurrentSpectrum.Polarity = GetAttribValue(ScanAttributeNames.polarity, "+");
                                mCurrentSpectrum.RetentionTimeMin = GetAttribTimeValueMinutes(ScanAttributeNames.retentionTime);
                                mCurrentSpectrum.ScanType = GetAttribValue(ScanAttributeNames.scanType, string.Empty);
                                mCurrentSpectrum.FilterLine = GetAttribValue(ScanAttributeNames.filterLine, string.Empty);
                                mCurrentSpectrum.StartMZ = GetAttribValue(ScanAttributeNames.startMz, 0);
                                mCurrentSpectrum.EndMZ = GetAttribValue(ScanAttributeNames.endMz, 0);
                                mCurrentSpectrum.mzRangeStart = GetAttribValue(ScanAttributeNames.lowMz, 0);
                                mCurrentSpectrum.mzRangeEnd = GetAttribValue(ScanAttributeNames.highMz, 0);
                                mCurrentSpectrum.BasePeakMZ = GetAttribValue(ScanAttributeNames.basePeakMz, 0);
                                mCurrentSpectrum.BasePeakIntensity = GetAttribValue(ScanAttributeNames.basePeakIntensity, 0);
                                mCurrentSpectrum.TotalIonCurrent = GetAttribValue(ScanAttributeNames.totIonCurrent, 0);
                            }
                        }
                    }

                    if (attributeMissing)
                    {
                        mCurrentSpectrum.ScanNumber = 0;
                        OnErrorEvent("Unable to read the 'num' attribute for the current scan since it is missing");
                    }

                    break;

                case ScanSectionNames.precursorMz:
                    if (mXMLReader.HasAttributes)
                    {
                        mCurrentSpectrum.ParentIonIntensity = GetAttribValue(PrecursorAttributeNames.precursorIntensity, 0);
                        mCurrentSpectrum.ActivationMethod = GetAttribValue(PrecursorAttributeNames.activationMethod, string.Empty);
                        mCurrentSpectrum.ParentIonCharge = GetAttribValue(PrecursorAttributeNames.precursorCharge, 0);
                        mCurrentSpectrum.PrecursorScanNum = GetAttribValue(PrecursorAttributeNames.precursorScanNum, 0);
                        mCurrentSpectrum.IsolationWindow = GetAttribValue(PrecursorAttributeNames.windowWideness, 0);
                    }

                    break;

                case ScanSectionNames.peaks:
                    if (mXMLReader.HasAttributes)
                    {
                        // mzXML 3.x files will have a contentType attribute
                        // Earlier versions will have a pairOrder attribute

                        mCurrentSpectrum.PeaksPairOrder = GetAttribValue(PeaksAttributeNames.contentType, string.Empty);

                        if (!string.IsNullOrEmpty(mCurrentSpectrum.PeaksPairOrder))
                        {
                            // mzXML v3.x
                            mCurrentSpectrum.CompressionType = GetAttribValue(PeaksAttributeNames.compressionType,
                                SpectrumInfoMzXML.CompressionTypes.none);

                            mCurrentSpectrum.CompressedLen = GetAttribValue(PeaksAttributeNames.compressedLen, 0);
                        }
                        else
                        {
                            // mzXML v1.x or v2.x
                            mCurrentSpectrum.PeaksPairOrder = GetAttribValue(PeaksAttributeNames.pairOrder,
                                SpectrumInfoMzXML.PairOrderTypes.MZandIntensity);

                            mCurrentSpectrum.CompressionType = SpectrumInfoMzXML.CompressionTypes.none;
                            mCurrentSpectrum.CompressedLen = 0;
                        }

                        mCurrentSpectrum.NumericPrecisionOfData = GetAttribValue(PeaksAttributeNames.precision, 32);
                        mCurrentSpectrum.PeaksByteOrder =
                            GetAttribValue(PeaksAttributeNames.byteOrder, SpectrumInfoMzXML.ByteOrderTypes.network);
                    }

                    break;

                case XMLSectionNames.RootName:
                    mCurrentXMLDataFileSection = CurrentMzXMLDataFileSection.Start;

                    if (mXMLReader.HasAttributes)
                    {
                        // First look for attribute xmlns
                        var value = GetAttribValue(mzXMLRootAttributeNames.xmlns, string.Empty);

                        if (value is null || value.Length == 0)
                        {
                            // Attribute not found; look for attribute xsi:schemaLocation
                            value = GetAttribValue(mzXMLRootAttributeNames.xsi_schemaLocation, string.Empty);
                        }

                        ValidateMZXmlFileVersion(value);
                    }

                    break;

                case HeaderSectionNames.msInstrument:
                    if (GetParentElement().Equals(XMLSectionNames.msRun))
                    {
                        mCurrentXMLDataFileSection = CurrentMzXMLDataFileSection.msInstrument;
                    }

                    break;

                case XMLSectionNames.msRun:
                    mCurrentXMLDataFileSection = CurrentMzXMLDataFileSection.msRun;

                    if (mXMLReader.HasAttributes)
                    {
                        mInputFileStats.ScanCount = GetAttribValue(MSRunAttributeNames.scanCount, 0);
                        mInputFileStatsAddnl.StartTimeMin = GetAttribTimeValueMinutes(MSRunAttributeNames.startTime);
                        mInputFileStatsAddnl.EndTimeMin = GetAttribTimeValueMinutes(MSRunAttributeNames.endTime);

                        // Note: The ReAdW software that creates mzXML files records the .StartTime and .EndTime values in minutes but labels them as seconds
                        // Check for this by computing the average seconds/scan
                        // If too low, multiply the start and end times by 60
                        if (mInputFileStats.ScanCount > 0)
                        {
                            if ((mInputFileStatsAddnl.EndTimeMin - mInputFileStatsAddnl.StartTimeMin) / mInputFileStats.ScanCount * 60f < 0.1d)
                            {
                                // Less than 0.1 sec/scan; this is unlikely
                                mInputFileStatsAddnl.StartTimeMin = mInputFileStatsAddnl.StartTimeMin * 60f;
                                mInputFileStatsAddnl.EndTimeMin = mInputFileStatsAddnl.EndTimeMin * 60f;
                            }
                        }
                    }
                    else
                    {
                        mInputFileStats.ScanCount = 0;
                    }

                    break;

                case HeaderSectionNames.dataProcessing:
                    if (GetParentElement().Equals(XMLSectionNames.msRun))
                    {
                        mCurrentXMLDataFileSection = CurrentMzXMLDataFileSection.dataProcessing;
                        mInputFileStatsAddnl.IsCentroid = GetAttribValue(DataProcessingAttributeNames.centroided, false);
                    }

                    break;
            }

            mSkippedStartElementAdvance = false;
        }

        /// <summary>
        /// Updates the current XMLReader object with a new reader positioned at the XML for a new mass spectrum
        /// </summary>
        /// <param name="newReader"></param>
        /// <returns>True if successful, false if an error</returns>
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
                OnErrorEvent(mErrorMessage, ex);
                return false;
            }
        }

        /// <summary>
        /// Determine the .mzXML file version
        /// </summary>
        /// <remarks>
        /// The supported versions are mzXML_2.x and mzXML_3.x
        /// </remarks>
        /// <param name="xmlWithFileVersion"></param>
        /// <param name="xmlFileVersion">Output: file version</param>
        /// <returns>True if the version could be determined, otherwise false</returns>
        public static bool ExtractMzXmlFileVersion(string xmlWithFileVersion, out string xmlFileVersion)
        {
            var fileVersionRegEx = new System.Text.RegularExpressions.Regex(@"mzXML_[^\s""/]+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Validate the mzXML file version
            if (!string.IsNullOrWhiteSpace(xmlWithFileVersion))
            {
                // Parse out the version number
                var match = fileVersionRegEx.Match(xmlWithFileVersion);

                if (match.Success)
                {
                    // Record the version
                    xmlFileVersion = match.Value;
                    return true;
                }
            }

            xmlFileVersion = string.Empty;
            return false;
        }

        private void ValidateMZXmlFileVersion(string xmlWithFileVersion)
        {
            try
            {
                mFileVersion = string.Empty;
                string message;

                if (!ExtractMzXmlFileVersion(xmlWithFileVersion, out mFileVersion))
                {
                    message = "Unknown mzXML file version; expected text not found in xmlWithFileVersion";

                    if (mParseFilesWithUnknownVersion)
                    {
                        message += "; attempting to parse since ParseFilesWithUnknownVersion = True";
                    }
                    else
                    {
                        mAbortProcessing = true;
                        message += "; aborting read";
                    }

                    OnErrorEvent(message);
                    return;
                }

                if (string.IsNullOrWhiteSpace(mFileVersion))
                    return;

                if (mFileVersion.IndexOf("mzxml_2", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    mFileVersion.IndexOf("mzxml_3", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return;
                }

                // fileVersion contains mzXML_ but not mxXML_2 or mxXML_3
                // Thus, assume unknown version
                // Log error and abort if mParseFilesWithUnknownVersion = False
                message = "Unknown mzXML file version: " + mFileVersion;

                if (mParseFilesWithUnknownVersion)
                {
                    message += "; attempting to parse since ParseFilesWithUnknownVersion = True";
                }
                else
                {
                    mAbortProcessing = true;
                    message += "; aborting read";
                }

                OnErrorEvent(message);
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ValidateMZXmlFileVersion", ex);
                mFileVersion = string.Empty;
            }
        }
    }
}