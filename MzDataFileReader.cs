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
    public class clsMzDataFileReader : clsMSXMLFileReaderBaseClass
    {
        // Ignore Spelling: deisotoping, endian, xmlns, xsi

        public clsMzDataFileReader()
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

        private enum eCurrentMZDataFileSectionConstants
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

        private struct udtFileStatsAddnlType
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

        private eCurrentMZDataFileSectionConstants mCurrentXMLDataFileSection;

        private clsSpectrumInfoMzData mCurrentSpectrum;

        private int mAcquisitionElementCount;

        private Queue mMostRecentSurveyScanSpectra;

        private udtFileStatsAddnlType mInputFileStatsAddnl;

        // ReSharper disable UnusedMember.Global

        public string PeakProcessing => mInputFileStatsAddnl.PeakProcessing;

        public bool FileInfoIsCentroid => mInputFileStatsAddnl.IsCentroid;

        public bool IsDeisotoped => mInputFileStatsAddnl.IsDeisotoped;

        public bool HasChargeDeconvolution => mInputFileStatsAddnl.HasChargeDeconvolution;

        // ReSharper restore UnusedMember.Global

        private float FindIonIntensityInRecentSpectra(int intSpectrumIDToFind, double dblMZToFind)
        {
            var sngIntensityMatch = 0f;

            if (mMostRecentSurveyScanSpectra != null)
            {
                var objEnumerator = mMostRecentSurveyScanSpectra.GetEnumerator();

                while (objEnumerator.MoveNext())
                {
                    var objSpectrum = (clsSpectrumInfoMzData)objEnumerator.Current;

                    if (objSpectrum.SpectrumID == intSpectrumIDToFind)
                    {
                        sngIntensityMatch = objSpectrum.LookupIonIntensityByMZ(dblMZToFind, 0f);
                        break;
                    }
                }
            }

            return sngIntensityMatch;
        }

        protected override clsSpectrumInfo GetCurrentSpectrum()
        {
            return mCurrentSpectrum;
        }

        private bool GetCVNameAndValue(out string strName, out string strValue)
        {
            try
            {
                if (mXMLReader.HasAttributes)
                {
                    strName = mXMLReader.GetAttribute("name");
                    strValue = mXMLReader.GetAttribute("value");
                    return true;
                }
            }
            catch (Exception ex)
            {
                // Ignore errors here
            }

            strName = string.Empty;
            strValue = string.Empty;
            return false;
        }

        protected override void InitializeCurrentSpectrum(bool blnAutoShrinkDataLists)
        {
            if (mCurrentSpectrum != null)
            {
                if (mCurrentSpectrum.MSLevel == 1)
                {
                    if (mMostRecentSurveyScanSpectra.Count >= MOST_RECENT_SURVEY_SCANS_TO_CACHE)
                    {
                        mMostRecentSurveyScanSpectra.Dequeue();
                    }

                    // Add mCurrentSpectrum to mMostRecentSurveyScanSpectra
                    mCurrentSpectrum.CopyTo(out var objSpectrumCopy);
                    mMostRecentSurveyScanSpectra.Enqueue(objSpectrumCopy);
                }
            }

            if (ReadingAndStoringSpectra || mCurrentSpectrum is null)
            {
                mCurrentSpectrum = new clsSpectrumInfoMzData();
            }
            else
            {
                mCurrentSpectrum.Clear();
            }

            mCurrentSpectrum.AutoShrinkDataLists = blnAutoShrinkDataLists;
        }

        protected sealed override void InitializeLocalVariables()
        {
            base.InitializeLocalVariables();
            mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.UnknownFile;
            mAcquisitionElementCount = 0;

            mInputFileStatsAddnl.PeakProcessing = string.Empty;
            mInputFileStatsAddnl.IsCentroid = false;
            mInputFileStatsAddnl.IsDeisotoped = false;
            mInputFileStatsAddnl.HasChargeDeconvolution = false;

            mMostRecentSurveyScanSpectra = new Queue();
        }

        public override bool OpenFile(string strInputFilePath)
        {
            InitializeLocalVariables();
            return base.OpenFile(strInputFilePath);
        }

        /// <summary>
        /// Parse strMSMSDataBase64Encoded and store the data in sngValues
        /// </summary>
        /// <param name="strMSMSDataBase64Encoded"></param>
        /// <param name="sngValues"></param>
        /// <param name="NumericPrecisionOfData"></param>
        /// <param name="PeaksEndianMode"></param>
        /// <param name="blnUpdatePeaksCountIfInconsistent"></param>
        /// <returns>True if successful, false if an error</returns>
        private bool ParseBinaryData(string strMSMSDataBase64Encoded, ref float[] sngValues, int NumericPrecisionOfData, string PeaksEndianMode, bool blnUpdatePeaksCountIfInconsistent)
        {
            var zLibCompressed = false;

            if (strMSMSDataBase64Encoded is null || strMSMSDataBase64Encoded.Length == 0)
            {
                sngValues = Array.Empty<float>();
                return false;
            }

            try
            {
                var eEndianMode = mCurrentSpectrum.GetEndianModeValue(PeaksEndianMode);
                var success = false;

                switch (NumericPrecisionOfData)
                {
                    case 32:
                        if (clsBase64EncodeDecode.DecodeNumericArray(strMSMSDataBase64Encoded, out float[] sngDataArray, zLibCompressed, eEndianMode))
                        {
                            sngValues = new float[sngDataArray.Length];
                            sngDataArray.CopyTo(sngValues, 0);
                            success = true;
                        }

                        break;

                    case 64:
                        if (clsBase64EncodeDecode.DecodeNumericArray(strMSMSDataBase64Encoded, out double[] dblDataArray, zLibCompressed, eEndianMode))
                        {
                            sngValues = new float[dblDataArray.Length];
                            var intIndexEnd = dblDataArray.Length - 1;

                            for (var intIndex = 0; intIndex <= intIndexEnd; intIndex++)
                            {
                                sngValues[intIndex] = (float)dblDataArray[intIndex];
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

                if (sngValues.Length == mCurrentSpectrum.DataCount)
                    return true;

                if (mCurrentSpectrum.DataCount == 0 && sngValues.Length > 0 && Math.Abs(sngValues[0]) < float.Epsilon)
                {
                    // Leave .PeaksCount at 0
                }
                else if (blnUpdatePeaksCountIfInconsistent)
                {
                    // This shouldn't normally be necessary
                    OnErrorEvent("Unexpected condition in ParseBinaryData: sngValues.Length <> .DataCount and .DataCount > 0");
                    mCurrentSpectrum.DataCount = sngValues.Length;
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
        /// Parse strMSMSDataBase64Encoded and store the data in dblValues
        /// </summary>
        /// <param name="strMSMSDataBase64Encoded"></param>
        /// <param name="dblValues"></param>
        /// <param name="NumericPrecisionOfData"></param>
        /// <param name="PeaksEndianMode"></param>
        /// <param name="blnUpdatePeaksCountIfInconsistent"></param>
        /// <returns>True if successful, false if an error</returns>
        private bool ParseBinaryData(string strMSMSDataBase64Encoded, ref double[] dblValues, int NumericPrecisionOfData, string PeaksEndianMode, bool blnUpdatePeaksCountIfInconsistent)
        {
            var zLibCompressed = false;

            if (strMSMSDataBase64Encoded is null || strMSMSDataBase64Encoded.Length == 0)
            {
                dblValues = Array.Empty<double>();
                return false;
            }

            try
            {
                var eEndianMode = mCurrentSpectrum.GetEndianModeValue(PeaksEndianMode);
                var success = false;

                switch (NumericPrecisionOfData)
                {
                    case 32:
                        if (clsBase64EncodeDecode.DecodeNumericArray(strMSMSDataBase64Encoded, out float[] sngDataArray, zLibCompressed, eEndianMode))
                        {
                            dblValues = new double[sngDataArray.Length];
                            sngDataArray.CopyTo(dblValues, 0);
                            success = true;
                        }

                        break;

                    case 64:
                        if (clsBase64EncodeDecode.DecodeNumericArray(strMSMSDataBase64Encoded, out double[] dblDataArray, zLibCompressed, eEndianMode))
                        {
                            dblValues = new double[dblDataArray.Length];
                            dblDataArray.CopyTo(dblValues, 0);
                            success = true;
                        }

                        break;

                    default:
                        // Invalid numeric precision
                        break;
                }

                if (!success)
                    return false;

                if (dblValues.Length == mCurrentSpectrum.DataCount)
                    return true;

                if (mCurrentSpectrum.DataCount == 0 && dblValues.Length > 0 && Math.Abs(dblValues[0]) < float.Epsilon)
                {
                    // Leave .PeaksCount at 0
                }
                else if (blnUpdatePeaksCountIfInconsistent)
                {
                    // This shouldn't normally be necessary
                    OnErrorEvent("Unexpected condition in ParseBinaryData: sngValues.Length <> .DataCount and .DataCount > 0");
                    mCurrentSpectrum.DataCount = dblValues.Length;
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
                    case eCurrentMZDataFileSectionConstants.SpectrumDataArrayMZ:
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

                    case eCurrentMZDataFileSectionConstants.SpectrumDataArrayIntensity:
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
                    string strCVName;
                    string strValue;

                    switch (mCurrentXMLDataFileSection)
                    {
                        case eCurrentMZDataFileSectionConstants.DataProcessingMethod:
                            {
                                if (GetCVNameAndValue(out strCVName, out strValue))
                                {
                                    switch (strCVName)
                                    {
                                        case ProcessingMethodCVParamNames.Deisotoping:
                                            {
                                                mInputFileStatsAddnl.IsDeisotoped = CBoolSafe(strValue, false);
                                                break;
                                            }

                                        case ProcessingMethodCVParamNames.ChargeDeconvolution:
                                            {
                                                mInputFileStatsAddnl.HasChargeDeconvolution = CBoolSafe(strValue, false);
                                                break;
                                            }

                                        case ProcessingMethodCVParamNames.PeakProcessing:
                                            {
                                                mInputFileStatsAddnl.PeakProcessing = strValue;

                                                if (strValue.ToLower().IndexOf("centroid", StringComparison.Ordinal) >= 0)
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
                                }

                                break;
                            }

                        case eCurrentMZDataFileSectionConstants.SpectrumInstrument:
                            {
                                if (GetCVNameAndValue(out strCVName, out strValue))
                                {
                                    switch (strCVName)
                                    {
                                        case SpectrumInstrumentCVParamNames.ScanMode:
                                            {
                                                mCurrentSpectrum.ScanMode = strValue;
                                                break;
                                            }

                                        case SpectrumInstrumentCVParamNames.Polarity:
                                            {
                                                mCurrentSpectrum.Polarity = strValue;
                                                break;
                                            }

                                        case SpectrumInstrumentCVParamNames.TimeInMinutes:
                                            {
                                                mCurrentSpectrum.RetentionTimeMin = CSngSafe(strValue, 0f);
                                                break;
                                            }
                                    }
                                }

                                break;
                            }

                        case eCurrentMZDataFileSectionConstants.PrecursorIonSelection:
                            {
                                if (GetCVNameAndValue(out strCVName, out strValue))
                                {
                                    switch (strCVName)
                                    {
                                        case PrecursorIonSelectionCVParamNames.MassToChargeRatio:
                                            mCurrentSpectrum.ParentIonMZ = CDblSafe(strValue, 0d);
                                            mCurrentSpectrum.ParentIonIntensity =
                                                FindIonIntensityInRecentSpectra(mCurrentSpectrum.ParentIonSpectrumID,
                                                    mCurrentSpectrum.ParentIonMZ);

                                            break;

                                        case PrecursorIonSelectionCVParamNames.ChargeState:
                                            mCurrentSpectrum.ParentIonCharge = CIntSafe(strValue, 0);
                                            break;
                                    }
                                }

                                break;
                            }

                        case eCurrentMZDataFileSectionConstants.PrecursorActivation:
                            {
                                if (GetCVNameAndValue(out strCVName, out strValue))
                                {
                                    switch (strCVName)
                                    {
                                        case PrecursorActivationCVParamNames.Method:
                                            {
                                                mCurrentSpectrum.CollisionMethod = strValue;
                                                break;
                                            }

                                        case PrecursorActivationCVParamNames.CollisionEnergy:
                                            {
                                                mCurrentSpectrum.CollisionEnergy = CSngSafe(strValue, 0f);
                                                break;
                                            }

                                        case PrecursorActivationCVParamNames.EnergyUnits:
                                            {
                                                mCurrentSpectrum.CollisionEnergyUnits = strValue;
                                                break;
                                            }
                                    }
                                }

                                break;
                            }
                    }

                    break;

                case ScanSectionNames.spectrumList:
                    if (GetParentElement().Equals(XMLSectionNames.RootName))
                    {
                        mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.SpectrumList;

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
                        mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.SpectrumList;
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
                    mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.SpectrumSettings;
                    break;

                case ScanSectionNames.acqSpecification:
                    if (GetParentElement().Equals(ScanSectionNames.spectrumSettings))
                    {
                        mCurrentSpectrum.SpectrumType = GetAttribValue(AcqSpecificationAttributeNames.spectrumType,
                            clsSpectrumInfo.SpectrumTypeNames.discrete);

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
                        mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.SpectrumInstrument;
                        mCurrentSpectrum.MSLevel = GetAttribValue(SpectrumInstrumentAttributeNames.msLevel, 1);
                        mCurrentSpectrum.mzRangeStart = GetAttribValue(SpectrumInstrumentAttributeNames.mzRangeStart, 0);
                        mCurrentSpectrum.mzRangeEnd = GetAttribValue(SpectrumInstrumentAttributeNames.mzRangeStop, 0);
                    }

                    break;

                case ScanSectionNames.precursorList:
                    mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.PrecursorList;
                    break;

                case ScanSectionNames.precursor:
                    if (GetParentElement().Equals(ScanSectionNames.precursorList))
                    {
                        mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.PrecursorEntry;
                        mCurrentSpectrum.ParentIonSpectrumMSLevel = GetAttribValue(PrecursorAttributeNames.msLevel, 0);
                        mCurrentSpectrum.ParentIonSpectrumID = GetAttribValue(PrecursorAttributeNames.spectrumRef, 0);
                    }

                    break;

                case ScanSectionNames.ionSelection:
                    if (GetParentElement().Equals(ScanSectionNames.precursor))
                    {
                        if (GetParentElement(mParentElementStack.Count - 1).Equals(ScanSectionNames.precursorList))
                        {
                            mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.PrecursorIonSelection;
                        }
                    }

                    break;

                case ScanSectionNames.activation:
                    if (GetParentElement().Equals(ScanSectionNames.precursor))
                    {
                        if (GetParentElement(mParentElementStack.Count - 1).Equals(ScanSectionNames.precursorList))
                        {
                            mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.PrecursorActivation;
                        }
                    }

                    break;

                case ScanSectionNames.mzArrayBinary:
                    mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.SpectrumDataArrayMZ;
                    break;

                case ScanSectionNames.intensityArrayBinary:
                    mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.SpectrumDataArrayIntensity;
                    break;

                case ScanSectionNames.ArrayData:
                    switch (mCurrentXMLDataFileSection)
                    {
                        case eCurrentMZDataFileSectionConstants.SpectrumDataArrayMZ:
                            {
                                mCurrentSpectrum.NumericPrecisionOfDataMZ = GetAttribValue(BinaryDataAttributeNames.precision, 32);
                                mCurrentSpectrum.PeaksEndianModeMZ = GetAttribValue(BinaryDataAttributeNames.endian, clsSpectrumInfoMzData.EndianModes.littleEndian);
                                mCurrentSpectrum.DataCount = GetAttribValue(BinaryDataAttributeNames.length, 0);

                                break;
                            }

                        case eCurrentMZDataFileSectionConstants.SpectrumDataArrayIntensity:
                            {
                                mCurrentSpectrum.NumericPrecisionOfDataIntensity = GetAttribValue(BinaryDataAttributeNames.precision, 32);
                                mCurrentSpectrum.PeaksEndianModeIntensity = GetAttribValue(BinaryDataAttributeNames.endian, clsSpectrumInfoMzData.EndianModes.littleEndian);

                                // Only update .DataCount if it is currently 0
                                if (mCurrentSpectrum.DataCount == 0)
                                {
                                    mCurrentSpectrum.DataCount = GetAttribValue(BinaryDataAttributeNames.length, 0);
                                }

                                break;
                            }
                    }

                    break;

                case XMLSectionNames.RootName:
                    mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.Start;

                    if (mXMLReader.HasAttributes)
                    {
                        ValidateMZDataFileVersion(GetAttribValue(mzDataRootAttributeNames.version, string.Empty));
                    }

                    break;

                case HeaderSectionNames.Description:
                    if (GetParentElement().Equals(XMLSectionNames.RootName))
                    {
                        mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.Headers;
                    }

                    break;

                case HeaderSectionNames.admin:
                    if (GetParentElement().Equals(HeaderSectionNames.Description))
                    {
                        mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.Admin;
                    }

                    break;

                case HeaderSectionNames.instrument:
                    if (GetParentElement().Equals(HeaderSectionNames.Description))
                    {
                        mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.Instrument;
                    }

                    break;

                case HeaderSectionNames.dataProcessing:
                    if (GetParentElement().Equals(HeaderSectionNames.Description))
                    {
                        mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.DataProcessing;
                    }

                    break;

                case HeaderSectionNames.processingMethod:
                    if (GetParentElement().Equals(HeaderSectionNames.dataProcessing))
                    {
                        mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.DataProcessingMethod;
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

        private void ValidateMZDataFileVersion(string strFileVersion)
        {
            try
            {
                mFileVersion = string.Empty;

                // Currently, the only version supported is 1.x (typically 1.05)
                var objFileVersionRegEx = new System.Text.RegularExpressions.Regex(@"1\.[0-9]+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                // Validate the mzData file version
                if (!string.IsNullOrWhiteSpace(strFileVersion))
                {
                    mFileVersion = string.Copy(strFileVersion);
                    var objMatch = objFileVersionRegEx.Match(strFileVersion);

                    if (!objMatch.Success)
                    {
                        // Unknown version
                        // Log error and abort if mParseFilesWithUnknownVersion = False
                        var strMessage = "Unknown mzData file version: " + mFileVersion;

                        if (mParseFilesWithUnknownVersion)
                        {
                            strMessage += "; attempting to parse since ParseFilesWithUnknownVersion = True";
                        }
                        else
                        {
                            mAbortProcessing = true;
                            strMessage += "; aborting read";
                        }

                        OnErrorEvent("Error in ValidateMZDataFileVersion: {0}", strMessage);
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