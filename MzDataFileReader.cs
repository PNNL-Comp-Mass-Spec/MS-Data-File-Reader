// -------------------------------------------------------------------------------
// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
// Copyright 2021, Battelle Memorial Institute.  All Rights Reserved.
//
// E-mail: matthew.monroe@pnl.gov or proteomics@pnnl.gov
// Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://panomics.pnnl.gov/ or https://www.pnnl.gov/integrative-omics
// -------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Xml;

namespace MSDataFileReader
{
    /// <summary>
    /// This class uses a SAX Parser to read an mzData file
    /// </summary>
    public class MzDataFileReader : MsXMLFileReaderBaseClass
    {
        // Ignore Spelling: deisotoping, endian, xmlns, xsi

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

            public const string admin = "admin";

            public const string instrument = "instrument";

            public const string dataProcessing = "dataProcessing";

            public const string processingMethod = "processingMethod";
        }

        private static class ScanSectionNames
        {
            public const string spectrumList = "spectrumList";

            public const string spectrum = "spectrum";

            public const string spectrumSettings = "spectrumSettings";

            public const string acqSpecification = "acqSpecification";

            public const string acquisition = "acquisition";

            public const string spectrumInstrument = "spectrumInstrument";

            public const string precursorList = "precursorList";

            public const string precursor = "precursor";

            public const string ionSelection = "ionSelection";

            public const string activation = "activation";

            public const string mzArrayBinary = "mzArrayBinary";

            public const string intensityArrayBinary = "intenArrayBinary";

            public const string ArrayData = "data";
        }

        private static class mzDataRootAttributeNames
        {
            public const string version = "version";

            // ReSharper disable UnusedMember.Local

            public const string accessionNumber = "accessionNumber";

            public const string xmlns_xsi = "xmlns:xsi";

            // ReSharper restore UnusedMember.Local
        }

        private static class SpectrumListAttributeNames
        {
            public const string count = "count";
        }

        private static class SpectrumAttributeNames
        {
            public const string id = "id";
        }

        private static class ProcessingMethodCVParamNames
        {
            public const string Deisotoping = "Deisotoping";

            public const string ChargeDeconvolution = "ChargeDeconvolution";

            public const string PeakProcessing = "PeakProcessing";
        }

        private static class AcqSpecificationAttributeNames
        {
            public const string spectrumType = "spectrumType";

            public const string methodOfCombination = "methodOfCombination";

            public const string count = "count";
        }

        private static class AcquisitionAttributeNames
        {
            public const string acqNumber = "acqNumber";
        }

        private static class SpectrumInstrumentAttributeNames
        {
            public const string msLevel = "msLevel";

            public const string mzRangeStart = "mzRangeStart";

            public const string mzRangeStop = "mzRangeStop";
        }

        private static class SpectrumInstrumentCVParamNames
        {
            public const string ScanMode = "ScanMode";

            public const string Polarity = "Polarity";

            public const string TimeInMinutes = "TimeInMinutes";
        }

        private static class PrecursorAttributeNames
        {
            public const string msLevel = "msLevel";

            public const string spectrumRef = "spectrumRef";
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
            public const string precision = "precision";

            public const string endian = "endian";

            public const string length = "length";
        }

        private const int MOST_RECENT_SURVEY_SCANS_TO_CACHE = 20;

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

            if (mMostRecentSurveyScanSpectra != null)
            {
                var enumerator = mMostRecentSurveyScanSpectra.GetEnumerator();

                while (enumerator.MoveNext())
                {
                    var spectrum = (SpectrumInfoMzData)enumerator.Current;

                    if (spectrum == null)
                        continue;

                    if (spectrum.SpectrumID == spectrumIDToFind)
                    {
                        intensityMatch = spectrum.LookupIonIntensityByMZ(mzToFind, 0f);
                        break;
                    }
                }
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

        protected override void InitializeCurrentSpectrum(bool autoShrinkDataLists)
        {
            if (mCurrentSpectrum is { MSLevel: 1 })
            {
                if (mMostRecentSurveyScanSpectra.Count >= MOST_RECENT_SURVEY_SCANS_TO_CACHE)
                {
                    mMostRecentSurveyScanSpectra.Dequeue();
                }

                // Add mCurrentSpectrum to mMostRecentSurveyScanSpectra
                mCurrentSpectrum.CopyTo(out var spectrumCopy);
                mMostRecentSurveyScanSpectra.Enqueue(spectrumCopy);
            }

            if (ReadingAndStoringSpectra || mCurrentSpectrum is null)
            {
                mCurrentSpectrum = new SpectrumInfoMzData();
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
        private bool ParseBinaryData(string msmsDataBase64Encoded, ref float[] values, int numericPrecisionOfData, string peaksEndianMode, bool updatePeaksCountIfInconsistent)
        {
            var zLibCompressed = false;

            if (msmsDataBase64Encoded is null || msmsDataBase64Encoded.Length == 0)
            {
                values = Array.Empty<float>();
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
                            values = new float[floatDataArray.Length];
                            floatDataArray.CopyTo(values, 0);
                            success = true;
                        }

                        break;

                    case 64:
                        if (Base64EncodeDecode.DecodeNumericArray(msmsDataBase64Encoded, out double[] doubleDataArray, zLibCompressed, endianMode))
                        {
                            values = new float[doubleDataArray.Length];
                            var indexEnd = doubleDataArray.Length - 1;

                            for (var index = 0; index <= indexEnd; index++)
                            {
                                values[index] = (float)doubleDataArray[index];
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

                if (values.Length == mCurrentSpectrum.DataCount)
                    return true;

                if (mCurrentSpectrum.DataCount == 0 && values.Length > 0 && Math.Abs(values[0]) < float.Epsilon)
                {
                    // Leave .PeaksCount at 0
                }
                else if (updatePeaksCountIfInconsistent)
                {
                    // This shouldn't normally be necessary
                    OnErrorEvent("Unexpected condition in ParseBinaryData: values.Length <> .DataCount and .DataCount > 0");
                    mCurrentSpectrum.DataCount = values.Length;
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
        private bool ParseBinaryData(string msmsDataBase64Encoded, ref double[] values, int numericPrecisionOfData, string peaksEndianMode, bool updatePeaksCountIfInconsistent)
        {
            var zLibCompressed = false;

            if (msmsDataBase64Encoded is null || msmsDataBase64Encoded.Length == 0)
            {
                values = Array.Empty<double>();
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
                            values = new double[floatDataArray.Length];
                            floatDataArray.CopyTo(values, 0);
                            success = true;
                        }

                        break;

                    case 64:
                        if (Base64EncodeDecode.DecodeNumericArray(msmsDataBase64Encoded, out double[] doubleDataArray, zLibCompressed, endianMode))
                        {
                            values = new double[doubleDataArray.Length];
                            doubleDataArray.CopyTo(values, 0);
                            success = true;
                        }

                        break;

                    default:
                        // Invalid numeric precision
                        break;
                }

                if (!success)
                    return false;

                if (values.Length == mCurrentSpectrum.DataCount)
                    return true;

                if (mCurrentSpectrum.DataCount == 0 && values.Length > 0 && Math.Abs(values[0]) < float.Epsilon)
                {
                    // Leave .PeaksCount at 0
                }
                else if (updatePeaksCountIfInconsistent)
                {
                    // This shouldn't normally be necessary
                    OnErrorEvent("Unexpected condition in ParseBinaryData: values.Length <> .DataCount and .DataCount > 0");
                    mCurrentSpectrum.DataCount = values.Length;
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
                            var success = ParseBinaryData(XMLTextReaderGetInnerText(), ref mCurrentSpectrum.MZList,
                                mCurrentSpectrum.NumericPrecisionOfDataMZ, mCurrentSpectrum.PeaksEndianModeMZ, true);

                            if (!success)
                            {
                                mCurrentSpectrum.DataCount = 0;
                            }
                        }

                        break;

                    case CurrentMzDataFileSection.SpectrumDataArrayIntensity:
                        if (!mSkipBinaryData)
                        {
                            ParseBinaryData(
                                XMLTextReaderGetInnerText(),
                                ref mCurrentSpectrum.IntensityList,
                                mCurrentSpectrum.NumericPrecisionOfDataIntensity,
                                mCurrentSpectrum.PeaksEndianModeIntensity,
                                false);
                            // Note: Not calling .ComputeBasePeakAndTIC() here since it will be called when the spectrum is Validated
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
                if ((mXMLReader.Name) == ScanSectionNames.spectrum)
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

                                        if (cvValue.ToLower().IndexOf("centroid", StringComparison.Ordinal) >= 0)
                                        {
                                            mInputFileStatsAddnl.IsCentroid = true;
                                        }
                                        else
                                        {
                                            mInputFileStatsAddnl.IsCentroid = false;
                                        }

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

                case ScanSectionNames.spectrumList:
                    if (GetParentElement().Equals(XMLSectionNames.RootName))
                    {
                        mCurrentXMLDataFileSection = CurrentMzDataFileSection.SpectrumList;

                        if (mXMLReader.HasAttributes)
                        {
                            mInputFileStats.ScanCount = GetAttribValue(SpectrumListAttributeNames.count, 1);
                        }
                        else
                        {
                            mInputFileStats.ScanCount = 0;
                        }
                    }

                    break;

                case ScanSectionNames.spectrum:
                    if (GetParentElement().Equals(ScanSectionNames.spectrumList))
                    {
                        mCurrentXMLDataFileSection = CurrentMzDataFileSection.SpectrumList;
                        mCurrentSpectrum.Clear();

                        if (mXMLReader.HasAttributes)
                        {
                            mCurrentSpectrum.SpectrumID = GetAttribValue(SpectrumAttributeNames.id, int.MinValue);

                            if (mCurrentSpectrum.SpectrumID == int.MinValue)
                            {
                                mCurrentSpectrum.SpectrumID = 0;
                                mErrorMessage = "Unable to read the \"id\" attribute for the current spectrum since it is missing";
                            }
                        }
                    }

                    break;

                case ScanSectionNames.spectrumSettings:
                    mCurrentXMLDataFileSection = CurrentMzDataFileSection.SpectrumSettings;
                    break;

                case ScanSectionNames.acqSpecification:
                    if (GetParentElement().Equals(ScanSectionNames.spectrumSettings))
                    {
                        mCurrentSpectrum.SpectrumType = GetAttribValue(AcqSpecificationAttributeNames.spectrumType,
                            SpectrumInfo.SpectrumTypeNames.discrete);

                        mCurrentSpectrum.SpectrumCombinationMethod =
                            GetAttribValue(AcqSpecificationAttributeNames.methodOfCombination, string.Empty);

                        mCurrentSpectrum.ScanCount = GetAttribValue(AcqSpecificationAttributeNames.count, 1);

                        mAcquisitionElementCount = 0;
                    }

                    break;

                case ScanSectionNames.acquisition:
                    if (GetParentElement().Equals(ScanSectionNames.acqSpecification))
                    {
                        // Only update mCurrentSpectrum.ScanNumber if mCurrentSpectrum.ScanCount = 1 or
                        // mAcquisitionElementCount = 1
                        mAcquisitionElementCount++;

                        if (mAcquisitionElementCount == 1 || mCurrentSpectrum.ScanCount == 1)
                        {
                            mCurrentSpectrum.ScanNumber = GetAttribValue(AcquisitionAttributeNames.acqNumber, 0);
                        }
                    }

                    break;

                case ScanSectionNames.spectrumInstrument:
                    if (GetParentElement().Equals(ScanSectionNames.spectrumSettings))
                    {
                        mCurrentXMLDataFileSection = CurrentMzDataFileSection.SpectrumInstrument;
                        mCurrentSpectrum.MSLevel = GetAttribValue(SpectrumInstrumentAttributeNames.msLevel, 1);
                        mCurrentSpectrum.mzRangeStart = GetAttribValue(SpectrumInstrumentAttributeNames.mzRangeStart, 0);
                        mCurrentSpectrum.mzRangeEnd = GetAttribValue(SpectrumInstrumentAttributeNames.mzRangeStop, 0);
                    }

                    break;

                case ScanSectionNames.precursorList:
                    mCurrentXMLDataFileSection = CurrentMzDataFileSection.PrecursorList;
                    break;

                case ScanSectionNames.precursor:
                    if (GetParentElement().Equals(ScanSectionNames.precursorList))
                    {
                        mCurrentXMLDataFileSection = CurrentMzDataFileSection.PrecursorEntry;
                        mCurrentSpectrum.ParentIonSpectrumMSLevel = GetAttribValue(PrecursorAttributeNames.msLevel, 0);
                        mCurrentSpectrum.ParentIonSpectrumID = GetAttribValue(PrecursorAttributeNames.spectrumRef, 0);
                    }

                    break;

                case ScanSectionNames.ionSelection:
                    if (GetParentElement().Equals(ScanSectionNames.precursor))
                    {
                        if (GetParentElement(mParentElementStack.Count - 1).Equals(ScanSectionNames.precursorList))
                        {
                            mCurrentXMLDataFileSection = CurrentMzDataFileSection.PrecursorIonSelection;
                        }
                    }

                    break;

                case ScanSectionNames.activation:
                    if (GetParentElement().Equals(ScanSectionNames.precursor))
                    {
                        if (GetParentElement(mParentElementStack.Count - 1).Equals(ScanSectionNames.precursorList))
                        {
                            mCurrentXMLDataFileSection = CurrentMzDataFileSection.PrecursorActivation;
                        }
                    }

                    break;

                case ScanSectionNames.mzArrayBinary:
                    mCurrentXMLDataFileSection = CurrentMzDataFileSection.SpectrumDataArrayMZ;
                    break;

                case ScanSectionNames.intensityArrayBinary:
                    mCurrentXMLDataFileSection = CurrentMzDataFileSection.SpectrumDataArrayIntensity;
                    break;

                case ScanSectionNames.ArrayData:
                    switch (mCurrentXMLDataFileSection)
                    {
                        case CurrentMzDataFileSection.SpectrumDataArrayMZ:
                            mCurrentSpectrum.NumericPrecisionOfDataMZ = GetAttribValue(BinaryDataAttributeNames.precision, 32);
                            mCurrentSpectrum.PeaksEndianModeMZ = GetAttribValue(BinaryDataAttributeNames.endian, SpectrumInfoMzData.EndianModes.littleEndian);

                            mCurrentSpectrum.DataCount = GetAttribValue(BinaryDataAttributeNames.length, 0);

                            break;

                        case CurrentMzDataFileSection.SpectrumDataArrayIntensity:
                            mCurrentSpectrum.NumericPrecisionOfDataIntensity = GetAttribValue(BinaryDataAttributeNames.precision, 32);
                            mCurrentSpectrum.PeaksEndianModeIntensity = GetAttribValue(BinaryDataAttributeNames.endian, SpectrumInfoMzData.EndianModes.littleEndian);

                            // Only update .DataCount if it is currently 0
                            if (mCurrentSpectrum.DataCount == 0)
                            {
                                mCurrentSpectrum.DataCount = GetAttribValue(BinaryDataAttributeNames.length, 0);
                            }

                            break;
                    }

                    break;

                case XMLSectionNames.RootName:
                    mCurrentXMLDataFileSection = CurrentMzDataFileSection.Start;

                    if (mXMLReader.HasAttributes)
                    {
                        ValidateMZDataFileVersion(GetAttribValue(mzDataRootAttributeNames.version, string.Empty));
                    }

                    break;

                case HeaderSectionNames.Description:
                    if (GetParentElement().Equals(XMLSectionNames.RootName))
                    {
                        mCurrentXMLDataFileSection = CurrentMzDataFileSection.Headers;
                    }

                    break;

                case HeaderSectionNames.admin:
                    if (GetParentElement().Equals(HeaderSectionNames.Description))
                    {
                        mCurrentXMLDataFileSection = CurrentMzDataFileSection.Admin;
                    }

                    break;

                case HeaderSectionNames.instrument:
                    if (GetParentElement().Equals(HeaderSectionNames.Description))
                    {
                        mCurrentXMLDataFileSection = CurrentMzDataFileSection.Instrument;
                    }

                    break;

                case HeaderSectionNames.dataProcessing:
                    if (GetParentElement().Equals(HeaderSectionNames.Description))
                    {
                        mCurrentXMLDataFileSection = CurrentMzDataFileSection.DataProcessing;
                    }

                    break;

                case HeaderSectionNames.processingMethod:
                    if (GetParentElement().Equals(HeaderSectionNames.dataProcessing))
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
                if (!string.IsNullOrWhiteSpace(fileVersion))
                {
                    mFileVersion = string.Copy(fileVersion);
                    var match = fileVersionRegEx.Match(fileVersion);

                    if (!match.Success)
                    {
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
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ValidateMZDataFileVersion", ex);
                mFileVersion = string.Empty;
            }
        }
    }
}