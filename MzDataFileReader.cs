// -------------------------------------------------------------------------------
// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
// Copyright 2021, Battelle Memorial Institute.  All Rights Reserved.
//
// E-mail: matthew.monroe@pnl.gov or proteomics@pnnl.gov
// Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://panomics.pnnl.gov/ or https://www.pnnl.gov/integrative-omics
// -------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;

namespace MSDataFileReader
{
    /// <summary>
    /// This class uses a SAX Parser to read an mzData file
    /// </summary>
    public class MzDataFileReader : MsXMLFileReaderBaseClass
    {
        // Ignore Spelling: deisotoping, endian, xmlns, xsi

        /// <summary>
        /// Constructor
        /// </summary>
        public MzDataFileReader()
        {
            InitializeLocalVariables();
        }

        /// <summary>
        /// MzData file extension
        /// </summary>
        /// <remarks>
        /// Must be in all caps
        /// </remarks>
        public const string MZDATA_FILE_EXTENSION = ".MZDATA";

        /// <summary>
        /// Alternative MzData file extension
        /// </summary>
        /// <remarks>
        /// Must be in all caps
        /// </remarks>
        public const string MZDATA_FILE_EXTENSION_XML = "_MZDATA.XML";

        private static class XMLSectionNames
        {
            public const string RootName = "mzData";

            public const string CVParam = "cvParam";
        }

        private static class HeaderSectionNames
        {
            public const string Description = "description";

            public const string Admin = "admin";

            public const string Instrument = "instrument";

            public const string DataProcessing = "dataProcessing";

            public const string ProcessingMethod = "processingMethod";
        }

        private static class ScanSectionNames
        {
            public const string SpectrumList = "spectrumList";

            public const string Spectrum = "spectrum";

            public const string SpectrumSettings = "spectrumSettings";

            public const string AcqSpecification = "acqSpecification";

            public const string Acquisition = "acquisition";

            public const string SpectrumInstrument = "spectrumInstrument";

            public const string PrecursorList = "precursorList";

            public const string Precursor = "precursor";

            public const string IonSelection = "ionSelection";

            public const string Activation = "activation";

            public const string MzArrayBinary = "mzArrayBinary";

            public const string IntensityArrayBinary = "intenArrayBinary";

            public const string ArrayData = "data";
        }

        private static class MzDataRootAttributeNames
        {
            public const string Version = "version";

            // ReSharper disable UnusedMember.Local

            public const string AccessionNumber = "accessionNumber";

            public const string XmlnsXsi = "xmlns:xsi";

            // ReSharper restore UnusedMember.Local
        }

        private static class SpectrumListAttributeNames
        {
            public const string Count = "count";
        }

        private static class SpectrumAttributeNames
        {
            public const string Id = "id";
        }

        private static class ProcessingMethodCVParamNames
        {
            public const string Deisotoping = "Deisotoping";

            public const string ChargeDeconvolution = "ChargeDeconvolution";

            public const string PeakProcessing = "PeakProcessing";
        }

        private static class AcqSpecificationAttributeNames
        {
            public const string SpectrumType = "spectrumType";

            public const string MethodOfCombination = "methodOfCombination";

            public const string Count = "count";
        }

        private static class AcquisitionAttributeNames
        {
            public const string AcqNumber = "acqNumber";
        }

        private static class SpectrumInstrumentAttributeNames
        {
            public const string MsLevel = "msLevel";

            public const string MzRangeStart = "mzRangeStart";

            public const string MzRangeStop = "mzRangeStop";
        }

        private static class SpectrumInstrumentCVParamNames
        {
            public const string ScanMode = "ScanMode";

            public const string Polarity = "Polarity";

            public const string TimeInMinutes = "TimeInMinutes";
        }

        private static class PrecursorAttributeNames
        {
            public const string MsLevel = "msLevel";

            public const string SpectrumRef = "spectrumRef";
        }

        private static class PrecursorIonSelectionCVParamNames
        {
            public const string MassToChargeRatio = "MassToChargeRatio";

            public const string ChargeState = "ChargeState";
        }

        private static class PrecursorActivationCVParamNames
        {
            public const string Method = "Method";

            public const string CollisionEnergy = "CollisionEnergy";

            public const string EnergyUnits = "EnergyUnits";
        }

        private static class BinaryDataAttributeNames
        {
            public const string Precision = "precision";

            public const string Endian = "endian";

            public const string Length = "length";
        }

        private const int MOST_RECENT_SURVEY_SCANS_TO_CACHE = 50;

        private enum CurrentMzDataFileSection
        {
            UnknownFile = 0,
            Start = 1,
            Headers = 2,
            Admin = 3,
            Instrument = 4,
            DataProcessing = 5,
            DataProcessingMethod = 6,
            SpectrumList = 7,
            SpectrumSettings = 8,
            SpectrumInstrument = 9,
            PrecursorList = 10,
            PrecursorEntry = 11,
            PrecursorIonSelection = 12,
            PrecursorActivation = 13,
            SpectrumDataArrayMZ = 14,
            SpectrumDataArrayIntensity = 15
        }

        private struct FileStatsAddnlType
        {
            public string PeakProcessing;

            /// <summary>
            /// True if centroid (aka stick) data
            /// False if profile (aka continuum) data
            /// </summary>
            public bool IsCentroid;

            public bool IsDeisotoped;

            public bool HasChargeDeconvolution;
        }

        private CurrentMzDataFileSection mCurrentXMLDataFileSection;

        private SpectrumInfoMzData mCurrentSpectrum;

        private int mAcquisitionElementCount;

        private Queue mMostRecentSurveyScanSpectra;

        private FileStatsAddnlType mInputFileStatsAddnl;

        // ReSharper disable UnusedMember.Global

        public string PeakProcessing => mInputFileStatsAddnl.PeakProcessing;

        public bool FileInfoIsCentroid => mInputFileStatsAddnl.IsCentroid;

        public bool IsDeisotoped => mInputFileStatsAddnl.IsDeisotoped;

        public bool HasChargeDeconvolution => mInputFileStatsAddnl.HasChargeDeconvolution;

        // ReSharper restore UnusedMember.Global

        private float FindIonIntensityInRecentSpectra(int spectrumIDToFind, double mzToFind)
        {
            var intensityMatch = 0f;

            if (mMostRecentSurveyScanSpectra == null)
                return intensityMatch;

            var enumerator = mMostRecentSurveyScanSpectra.GetEnumerator();

            while (enumerator.MoveNext())
            {
                var spectrum = (SpectrumInfoMzData)enumerator.Current;

                if (spectrum == null)
                    continue;

                if (spectrum.SpectrumID != spectrumIDToFind)
                    continue;

                intensityMatch = spectrum.LookupIonIntensityByMZ(mzToFind, 0f);
                break;
            }

            return intensityMatch;
        }

        protected override SpectrumInfo GetCurrentSpectrum()
        {
            return mCurrentSpectrum;
        }

        private bool GetCVNameAndValue(out string name, out string value)
        {
            try
            {
                if (mXMLReader.HasAttributes)
                {
                    name = mXMLReader.GetAttribute("name");
                    value = mXMLReader.GetAttribute("value");
                    return true;
                }
            }
            catch (Exception)
            {
                // Ignore errors here
            }

            name = string.Empty;
            value = string.Empty;
            return false;
        }

        protected override void InitializeCurrentSpectrum()
        {
            if (mCurrentSpectrum is { MSLevel: 1 })
            {
                if (mMostRecentSurveyScanSpectra.Count >= MOST_RECENT_SURVEY_SCANS_TO_CACHE)
                {
                    mMostRecentSurveyScanSpectra.Dequeue();
                }

                // Add mCurrentSpectrum to mMostRecentSurveyScanSpectra
                mMostRecentSurveyScanSpectra.Enqueue(mCurrentSpectrum);
            }

            mCurrentSpectrum = new SpectrumInfoMzData();
        }

        protected sealed override void InitializeLocalVariables()
        {
            base.InitializeLocalVariables();
            mCurrentXMLDataFileSection = CurrentMzDataFileSection.UnknownFile;
            mAcquisitionElementCount = 0;

            mInputFileStatsAddnl.PeakProcessing = string.Empty;
            mInputFileStatsAddnl.IsCentroid = false;
            mInputFileStatsAddnl.IsDeisotoped = false;
            mInputFileStatsAddnl.HasChargeDeconvolution = false;

            mMostRecentSurveyScanSpectra = new Queue();
        }

        public override bool OpenFile(string inputFilePath)
        {
            InitializeLocalVariables();
            return base.OpenFile(inputFilePath);
        }

        /// <summary>
        /// Parse msmsDataBase64Encoded and store the data in values
        /// </summary>
        /// <param name="msmsDataBase64Encoded"></param>
        /// <param name="values"></param>
        /// <param name="numericPrecisionOfData"></param>
        /// <param name="peaksEndianMode"></param>
        /// <param name="updatePeaksCountIfInconsistent"></param>
        /// <returns>True if successful, false if an error</returns>
        private bool ParseBinaryData(string msmsDataBase64Encoded, out List<float> values, int numericPrecisionOfData, string peaksEndianMode, bool updatePeaksCountIfInconsistent)
        {
            const bool zLibCompressed = false;

            values = new List<float>();

            if (string.IsNullOrEmpty(msmsDataBase64Encoded))
            {
                return false;
            }

            try
            {
                var endianMode = mCurrentSpectrum.GetEndianModeValue(peaksEndianMode);
                var success = false;

                switch (numericPrecisionOfData)
                {
                    case 32:
                        if (Base64EncodeDecode.DecodeNumericArray(msmsDataBase64Encoded, out float[] floatDataArray, zLibCompressed, endianMode))
                        {
                            values.AddRange(floatDataArray);
                            success = true;
                        }

                        break;

                    case 64:
                        if (Base64EncodeDecode.DecodeNumericArray(msmsDataBase64Encoded, out double[] doubleDataArray, zLibCompressed, endianMode))
                        {
                            foreach (var item in doubleDataArray)
                            {
                                values.Add((float)item);
                            }

                            success = true;
                        }

                        break;

                    default:
                        // Invalid numeric precision
                        break;
                }

                if (!success)
                    return false;

                if (values.Count == mCurrentSpectrum.PeaksCount)
                    return true;

                if (mCurrentSpectrum.PeaksCount == 0 && values.Count > 0 && Math.Abs(values[0]) < float.Epsilon)
                {
                    // Leave .PeaksCount at 0
                }
                else if (updatePeaksCountIfInconsistent)
                {
                    // This shouldn't normally be necessary
                    if (mCurrentSpectrum.PeaksCount > 0)
                    {
                        OnErrorEvent("Unexpected condition in ParseBinaryData: values.Count <> .PeaksCount and .PeaksCount > 0");
                    }

                    mCurrentSpectrum.PeaksCount = values.Count;
                }

                return true;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ParseBinaryData", ex);
                return false;
            }
        }

        /// <summary>
        /// Parse msmsDataBase64Encoded and store the data in values
        /// </summary>
        /// <param name="msmsDataBase64Encoded"></param>
        /// <param name="values"></param>
        /// <param name="numericPrecisionOfData"></param>
        /// <param name="peaksEndianMode"></param>
        /// <param name="updatePeaksCountIfInconsistent"></param>
        /// <returns>True if successful, false if an error</returns>
        private bool ParseBinaryData(string msmsDataBase64Encoded, out List<double> values, int numericPrecisionOfData, string peaksEndianMode, bool updatePeaksCountIfInconsistent)
        {
            const bool zLibCompressed = false;

            values = new List<double>();

            if (string.IsNullOrEmpty(msmsDataBase64Encoded))
            {
                return false;
            }

            try
            {
                var endianMode = mCurrentSpectrum.GetEndianModeValue(peaksEndianMode);
                var success = false;

                switch (numericPrecisionOfData)
                {
                    case 32:
                        if (Base64EncodeDecode.DecodeNumericArray(msmsDataBase64Encoded, out float[] floatDataArray, zLibCompressed, endianMode))
                        {
                            foreach (var item in floatDataArray)
                            {
                                values.Add(item);
                            }

                            success = true;
                        }

                        break;

                    case 64:
                        if (Base64EncodeDecode.DecodeNumericArray(msmsDataBase64Encoded, out double[] doubleDataArray, zLibCompressed, endianMode))
                        {
                            values.AddRange(doubleDataArray);
                            success = true;
                        }

                        break;

                    default:
                        // Invalid numeric precision
                        break;
                }

                if (!success)
                    return false;

                if (values.Count == mCurrentSpectrum.PeaksCount)
                    return true;

                if (mCurrentSpectrum.PeaksCount == 0 && values.Count > 0 && Math.Abs(values[0]) < float.Epsilon)
                {
                    // Leave .PeaksCount at 0
                }
                else if (updatePeaksCountIfInconsistent)
                {
                    // This shouldn't normally be necessary
                    if (mCurrentSpectrum.PeaksCount > 0)
                    {
                        OnErrorEvent("Unexpected condition in ParseBinaryData: values.Count <> .PeaksCount and .PeaksCount > 0");
                    }

                    mCurrentSpectrum.PeaksCount = values.Count;
                }

                return true;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ParseBinaryData", ex);
                return false;
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
                // Check the last element name sent to startElement to determine
                // what to do with the data we just received
                if (!mCurrentElement.Equals(ScanSectionNames.ArrayData))
                    return;

                // Note: We could use GetParentElement() to determine whether this base-64 encoded data
                // belongs to mzArrayBinary or intensityArrayBinary, but it is faster to use mCurrentXMLDataFileSection

                switch (mCurrentXMLDataFileSection)
                {
                    case CurrentMzDataFileSection.SpectrumDataArrayMZ:
                        if (!mSkipBinaryData)
                        {
                            var success = ParseBinaryData(
                                XMLTextReaderGetInnerText(),
                                out List<double> mzList,
                                mCurrentSpectrum.NumericPrecisionOfDataMZ,
                                mCurrentSpectrum.PeaksEndianModeMZ,
                                true);

                            if (success)
                            {
                                mCurrentSpectrum.MzList.AddRange(mzList);
                            }
                        }

                        break;

                    case CurrentMzDataFileSection.SpectrumDataArrayIntensity:
                        if (!mSkipBinaryData)
                        {
                            var success = ParseBinaryData(
                                XMLTextReaderGetInnerText(),
                                out List<float> intensityList,
                                mCurrentSpectrum.NumericPrecisionOfDataIntensity,
                                mCurrentSpectrum.PeaksEndianModeIntensity,
                                false);
                            // Note: Not calling .ComputeBasePeakAndTIC() here since it will be called when the spectrum is Validated

                            if (success)
                            {
                                mCurrentSpectrum.IntensityList.AddRange(intensityList);
                            }
                        }

                        break;
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
                // If we just moved out of a spectrum element, finalize the current scan
                if ((mXMLReader.Name) == ScanSectionNames.Spectrum)
                {
                    mCurrentSpectrum.Validate();
                    mSpectrumFound = true;
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
                case XMLSectionNames.CVParam:
                    string cvName;
                    string cvValue;

                    switch (mCurrentXMLDataFileSection)
                    {
                        case CurrentMzDataFileSection.DataProcessingMethod:
                            if (GetCVNameAndValue(out cvName, out cvValue))
                            {
                                switch (cvName)
                                {
                                    case ProcessingMethodCVParamNames.Deisotoping:
                                        mInputFileStatsAddnl.IsDeisotoped = CBoolSafe(cvValue, false);
                                        break;

                                    case ProcessingMethodCVParamNames.ChargeDeconvolution:
                                        mInputFileStatsAddnl.HasChargeDeconvolution = CBoolSafe(cvValue, false);
                                        break;

                                    case ProcessingMethodCVParamNames.PeakProcessing:
                                        mInputFileStatsAddnl.PeakProcessing = cvValue;

                                        mInputFileStatsAddnl.IsCentroid = cvValue.ToLower().IndexOf("centroid", StringComparison.Ordinal) >= 0;

                                        break;
                                }
                            }

                            break;

                        case CurrentMzDataFileSection.SpectrumInstrument:
                            if (GetCVNameAndValue(out cvName, out cvValue))
                            {
                                switch (cvName)
                                {
                                    case SpectrumInstrumentCVParamNames.ScanMode:
                                        mCurrentSpectrum.ScanMode = cvValue;
                                        break;

                                    case SpectrumInstrumentCVParamNames.Polarity:
                                        mCurrentSpectrum.Polarity = cvValue;
                                        break;

                                    case SpectrumInstrumentCVParamNames.TimeInMinutes:
                                        mCurrentSpectrum.RetentionTimeMin = CSngSafe(cvValue, 0f);
                                        break;
                                }
                            }

                            break;

                        case CurrentMzDataFileSection.PrecursorIonSelection:
                            if (GetCVNameAndValue(out cvName, out cvValue))
                            {
                                switch (cvName)
                                {
                                    case PrecursorIonSelectionCVParamNames.MassToChargeRatio:
                                        mCurrentSpectrum.ParentIonMZ = CDblSafe(cvValue, 0d);
                                        mCurrentSpectrum.ParentIonIntensity =
                                            FindIonIntensityInRecentSpectra(mCurrentSpectrum.ParentIonSpectrumID,
                                                mCurrentSpectrum.ParentIonMZ);

                                        break;

                                    case PrecursorIonSelectionCVParamNames.ChargeState:
                                        mCurrentSpectrum.ParentIonCharge = CIntSafe(cvValue, 0);
                                        break;
                                }
                            }

                            break;

                        case CurrentMzDataFileSection.PrecursorActivation:
                            if (GetCVNameAndValue(out cvName, out cvValue))
                            {
                                switch (cvName)
                                {
                                    case PrecursorActivationCVParamNames.Method:
                                        mCurrentSpectrum.CollisionMethod = cvValue;
                                        break;

                                    case PrecursorActivationCVParamNames.CollisionEnergy:
                                        mCurrentSpectrum.CollisionEnergy = CSngSafe(cvValue, 0f);
                                        break;

                                    case PrecursorActivationCVParamNames.EnergyUnits:
                                        mCurrentSpectrum.CollisionEnergyUnits = cvValue;
                                        break;
                                }
                            }

                            break;
                    }

                    break;

                case ScanSectionNames.SpectrumList:
                    if (GetParentElement().Equals(XMLSectionNames.RootName))
                    {
                        mCurrentXMLDataFileSection = CurrentMzDataFileSection.SpectrumList;

                        mInputFileStats.ScanCount = mXMLReader.HasAttributes ? GetAttribValue(SpectrumListAttributeNames.Count, 1) : 0;
                    }

                    break;

                case ScanSectionNames.Spectrum:
                    if (GetParentElement().Equals(ScanSectionNames.SpectrumList))
                    {
                        mCurrentXMLDataFileSection = CurrentMzDataFileSection.SpectrumList;
                        mCurrentSpectrum.Clear();

                        if (mXMLReader.HasAttributes)
                        {
                            mCurrentSpectrum.SpectrumID = GetAttribValue(SpectrumAttributeNames.Id, int.MinValue);

                            if (mCurrentSpectrum.SpectrumID == int.MinValue)
                            {
                                mCurrentSpectrum.SpectrumID = 0;
                                mErrorMessage = "Unable to read the \"id\" attribute for the current spectrum since it is missing";
                            }
                        }
                    }

                    break;

                case ScanSectionNames.SpectrumSettings:
                    mCurrentXMLDataFileSection = CurrentMzDataFileSection.SpectrumSettings;
                    break;

                case ScanSectionNames.AcqSpecification:
                    if (GetParentElement().Equals(ScanSectionNames.SpectrumSettings))
                    {
                        mCurrentSpectrum.SpectrumType = GetAttribValue(AcqSpecificationAttributeNames.SpectrumType, SpectrumInfo.SpectrumTypeNames.Discrete);

                        mCurrentSpectrum.SpectrumCombinationMethod = GetAttribValue(AcqSpecificationAttributeNames.MethodOfCombination, string.Empty);

                        mCurrentSpectrum.ScanCount = GetAttribValue(AcqSpecificationAttributeNames.Count, 1);

                        mAcquisitionElementCount = 0;
                    }

                    break;

                case ScanSectionNames.Acquisition:
                    if (GetParentElement().Equals(ScanSectionNames.AcqSpecification))
                    {
                        // Only update mCurrentSpectrum.ScanNumber if mCurrentSpectrum.ScanCount = 1 or
                        // mAcquisitionElementCount = 1
                        mAcquisitionElementCount++;

                        if (mAcquisitionElementCount == 1 || mCurrentSpectrum.ScanCount == 1)
                        {
                            mCurrentSpectrum.ScanNumber = GetAttribValue(AcquisitionAttributeNames.AcqNumber, 0);
                        }
                    }

                    break;

                case ScanSectionNames.SpectrumInstrument:
                    if (GetParentElement().Equals(ScanSectionNames.SpectrumSettings))
                    {
                        mCurrentXMLDataFileSection = CurrentMzDataFileSection.SpectrumInstrument;
                        mCurrentSpectrum.MSLevel = GetAttribValue(SpectrumInstrumentAttributeNames.MsLevel, 1);
                        mCurrentSpectrum.MzRangeStart = GetAttribValue(SpectrumInstrumentAttributeNames.MzRangeStart, 0);
                        mCurrentSpectrum.MzRangeEnd = GetAttribValue(SpectrumInstrumentAttributeNames.MzRangeStop, 0);
                    }

                    break;

                case ScanSectionNames.PrecursorList:
                    mCurrentXMLDataFileSection = CurrentMzDataFileSection.PrecursorList;
                    break;

                case ScanSectionNames.Precursor:
                    if (GetParentElement().Equals(ScanSectionNames.PrecursorList))
                    {
                        mCurrentXMLDataFileSection = CurrentMzDataFileSection.PrecursorEntry;
                        mCurrentSpectrum.ParentIonSpectrumMSLevel = GetAttribValue(PrecursorAttributeNames.MsLevel, 0);
                        mCurrentSpectrum.ParentIonSpectrumID = GetAttribValue(PrecursorAttributeNames.SpectrumRef, 0);
                    }

                    break;

                case ScanSectionNames.IonSelection:
                    if (GetParentElement().Equals(ScanSectionNames.Precursor) &&
                        GetParentElement(mParentElementStack.Count - 1).Equals(ScanSectionNames.PrecursorList))
                    {
                        mCurrentXMLDataFileSection = CurrentMzDataFileSection.PrecursorIonSelection;
                    }

                    break;

                case ScanSectionNames.Activation:
                    if (GetParentElement().Equals(ScanSectionNames.Precursor) &&
                        GetParentElement(mParentElementStack.Count - 1).Equals(ScanSectionNames.PrecursorList))
                    {
                        mCurrentXMLDataFileSection = CurrentMzDataFileSection.PrecursorActivation;
                    }

                    break;

                case ScanSectionNames.MzArrayBinary:
                    mCurrentXMLDataFileSection = CurrentMzDataFileSection.SpectrumDataArrayMZ;
                    break;

                case ScanSectionNames.IntensityArrayBinary:
                    mCurrentXMLDataFileSection = CurrentMzDataFileSection.SpectrumDataArrayIntensity;
                    break;

                case ScanSectionNames.ArrayData:
                    switch (mCurrentXMLDataFileSection)
                    {
                        case CurrentMzDataFileSection.SpectrumDataArrayMZ:
                            mCurrentSpectrum.NumericPrecisionOfDataMZ = GetAttribValue(BinaryDataAttributeNames.Precision, 32);
                            mCurrentSpectrum.PeaksEndianModeMZ = GetAttribValue(BinaryDataAttributeNames.Endian, SpectrumInfoMzData.EndianModes.LittleEndian);

                            mCurrentSpectrum.PeaksCount = GetAttribValue(BinaryDataAttributeNames.Length, 0);

                            break;

                        case CurrentMzDataFileSection.SpectrumDataArrayIntensity:
                            mCurrentSpectrum.NumericPrecisionOfDataIntensity = GetAttribValue(BinaryDataAttributeNames.Precision, 32);
                            mCurrentSpectrum.PeaksEndianModeIntensity = GetAttribValue(BinaryDataAttributeNames.Endian, SpectrumInfoMzData.EndianModes.LittleEndian);

                            // Only update .PeaksCount if it is currently 0
                            if (mCurrentSpectrum.PeaksCount == 0)
                            {
                                mCurrentSpectrum.PeaksCount = GetAttribValue(BinaryDataAttributeNames.Length, 0);
                            }

                            break;
                    }

                    break;

                case XMLSectionNames.RootName:
                    mCurrentXMLDataFileSection = CurrentMzDataFileSection.Start;

                    if (mXMLReader.HasAttributes)
                    {
                        ValidateMZDataFileVersion(GetAttribValue(MzDataRootAttributeNames.Version, string.Empty));
                    }

                    break;

                case HeaderSectionNames.Description:
                    if (GetParentElement().Equals(XMLSectionNames.RootName))
                    {
                        mCurrentXMLDataFileSection = CurrentMzDataFileSection.Headers;
                    }

                    break;

                case HeaderSectionNames.Admin:
                    if (GetParentElement().Equals(HeaderSectionNames.Description))
                    {
                        mCurrentXMLDataFileSection = CurrentMzDataFileSection.Admin;
                    }

                    break;

                case HeaderSectionNames.Instrument:
                    if (GetParentElement().Equals(HeaderSectionNames.Description))
                    {
                        mCurrentXMLDataFileSection = CurrentMzDataFileSection.Instrument;
                    }

                    break;

                case HeaderSectionNames.DataProcessing:
                    if (GetParentElement().Equals(HeaderSectionNames.Description))
                    {
                        mCurrentXMLDataFileSection = CurrentMzDataFileSection.DataProcessing;
                    }

                    break;

                case HeaderSectionNames.ProcessingMethod:
                    if (GetParentElement().Equals(HeaderSectionNames.DataProcessing))
                    {
                        mCurrentXMLDataFileSection = CurrentMzDataFileSection.DataProcessingMethod;
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

        private void ValidateMZDataFileVersion(string fileVersion)
        {
            try
            {
                mFileVersion = string.Empty;

                // Currently, the only version supported is 1.x (typically 1.05)
                var fileVersionRegEx = new System.Text.RegularExpressions.Regex(@"1\.[0-9]+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                // Validate the mzData file version
                if (string.IsNullOrWhiteSpace(fileVersion))
                    return;

                mFileVersion = fileVersion;
                var match = fileVersionRegEx.Match(fileVersion);

                if (match.Success)
                    return;

                // Unknown version
                // Log error and abort if mParseFilesWithUnknownVersion = False
                var message = "Unknown mzData file version: " + mFileVersion;

                if (mParseFilesWithUnknownVersion)
                {
                    message += "; attempting to parse since ParseFilesWithUnknownVersion = True";
                }
                else
                {
                    mAbortProcessing = true;
                    message += "; aborting read";
                }

                OnErrorEvent("Error in ValidateMZDataFileVersion: {0}", message);
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ValidateMZDataFileVersion", ex);
                mFileVersion = string.Empty;
            }
        }
    }
}