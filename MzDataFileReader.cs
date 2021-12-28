﻿using System;
using System.Collections;
using System.Xml;

namespace MSDataFileReader
{

    // This class uses a SAX Parser to read an mzData file
    //
    // -------------------------------------------------------------------------------
    // Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    // Copyright 2006, Battelle Memorial Institute.  All Rights Reserved.
    // Started April 1, 2006
    //
    // E-mail: matthew.monroe@pnl.gov or proteomics@pnnl.gov
    // Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://panomics.pnnl.gov/ or https://www.pnnl.gov/integrative-omics
    // -------------------------------------------------------------------------------
    //

    public class clsMzDataFileReader : clsMSXMLFileReaderBaseClass
    {
        public clsMzDataFileReader()
        {
            InitializeLocalVariables();
        }

        #region Constants and Enums
        // Note: The extensions must be in all caps
        public const string MZDATA_FILE_EXTENSION = ".MZDATA";
        public const string MZDATA_FILE_EXTENSION_XML = "_MZDATA.XML";

        // Note that I'm using classes to group the constants
        private class XMLSectionNames
        {
            public const string RootName = "mzData";
            public const string CVParam = "cvParam";
        }

        private class HeaderSectionNames
        {
            public const string Description = "description";
            public const string admin = "admin";
            public const string instrument = "instrument";
            public const string dataProcessing = "dataProcessing";
            public const string processingMethod = "processingMethod";
        }

        private class ScanSectionNames
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
            public const string intenArrayBinary = "intenArrayBinary";
            public const string ArrayData = "data";
        }

        private class mzDataRootAttrbuteNames
        {
            public const string version = "version";
            public const string accessionNumber = "accessionNumber";
            public const string xmlns_xsi = "xmlns:xsi";
        }

        private class SpectrumListAttributeNames
        {
            public const string count = "count";
        }

        private class SpectrumAttributeNames
        {
            public const string id = "id";
        }

        private class ProcessingMethodCVParamNames
        {
            public const string Deisotoping = "Deisotoping";
            public const string ChargeDeconvolution = "ChargeDeconvolution";
            public const string PeakProcessing = "PeakProcessing";
        }

        private class AcqSpecificationAttributeNames
        {
            public const string spectrumType = "spectrumType";
            public const string methodOfCombination = "methodOfCombination";
            public const string count = "count";
        }

        private class AcquisitionAttributeNames
        {
            public const string acqNumber = "acqNumber";
        }

        private class SpectrumInstrumentAttributeNames
        {
            public const string msLevel = "msLevel";
            public const string mzRangeStart = "mzRangeStart";
            public const string mzRangeStop = "mzRangeStop";
        }

        private class SpectrumInstrumentCVParamNames
        {
            public const string ScanMode = "ScanMode";
            public const string Polarity = "Polarity";
            public const string TimeInMinutes = "TimeInMinutes";
        }

        private class PrecursorAttributeNames
        {
            public const string msLevel = "msLevel";
            public const string spectrumRef = "spectrumRef";
        }

        private class PrecursorIonSelectionCVParamNames
        {
            public const string MassToChargeRatio = "MassToChargeRatio";
            public const string ChargeState = "ChargeState";
        }

        private class PrecursorActivationCVParamNames
        {
            public const string Method = "Method";
            public const string CollisionEnergy = "CollisionEnergy";
            public const string EnergyUnits = "EnergyUnits";
        }

        private class BinaryDataAttributeNames
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

        #endregion

        #region Structures

        private struct udtFileStatsAddnlType
        {
            public string PeakProcessing;
            public bool IsCentroid;      // True if centroid (aka stick) data; False if profile (aka continuum) data
            public bool IsDeisotoped;
            public bool HasChargeDeconvolution;
        }

        #endregion

        #region Classwide Variables

        private eCurrentMZDataFileSectionConstants mCurrentXMLDataFileSection;
        private clsSpectrumInfoMzData mCurrentSpectrum;
        private int mAcquisitionElementCount;
        private Queue mMostRecentSurveyScanSpectra;
        private udtFileStatsAddnlType mInputFileStatsAddnl;

        #endregion

        #region Processing Options and Interface Functions

        public string PeakProcessing
        {
            get
            {
                return mInputFileStatsAddnl.PeakProcessing;
            }
        }

        public bool FileInfoIsCentroid
        {
            get
            {
                return mInputFileStatsAddnl.IsCentroid;
            }
        }

        public bool IsDeisotoped
        {
            get
            {
                return mInputFileStatsAddnl.IsDeisotoped;
            }
        }

        public bool HasChargeDeconvolution
        {
            get
            {
                return mInputFileStatsAddnl.HasChargeDeconvolution;
            }
        }

        #endregion

        private float FindIonIntensityInRecentSpectra(int intSpectrumIDToFind, double dblMZToFind)
        {
            float sngIntensityMatch;
            IEnumerator objEnumerator;
            clsSpectrumInfoMzData objSpectrum;
            sngIntensityMatch = 0f;
            if (mMostRecentSurveyScanSpectra != null)
            {
                objEnumerator = mMostRecentSurveyScanSpectra.GetEnumerator();
                while (objEnumerator.MoveNext())
                {
                    objSpectrum = (clsSpectrumInfoMzData)objEnumerator.Current;
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
            clsSpectrumInfoMzData objSpectrumCopy = null;
            if (mCurrentSpectrum != null)
            {
                if (mCurrentSpectrum.MSLevel == 1)
                {
                    if (mMostRecentSurveyScanSpectra.Count >= MOST_RECENT_SURVEY_SCANS_TO_CACHE)
                    {
                        mMostRecentSurveyScanSpectra.Dequeue();
                    }

                    // Add mCurrentSpectrum to mMostRecentSurveyScanSpectra
                    mCurrentSpectrum.CopyTo(out objSpectrumCopy);
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

        protected override void InitializeLocalVariables()
        {
            base.InitializeLocalVariables();
            mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.UnknownFile;
            mAcquisitionElementCount = 0;
            {
                ref var withBlock = ref mInputFileStatsAddnl;
                withBlock.PeakProcessing = string.Empty;
                withBlock.IsCentroid = false;
                withBlock.IsDeisotoped = false;
                withBlock.HasChargeDeconvolution = false;
            }

            mMostRecentSurveyScanSpectra = new Queue();
        }

        public override bool OpenFile(string strInputFilePath)
        {
            bool blnSuccess;
            InitializeLocalVariables();
            blnSuccess = base.OpenFile(strInputFilePath);
            return blnSuccess;
        }

        private bool ParseBinaryData(string strMSMSDataBase64Encoded, ref float[] sngValues, int NumericPrecisionOfData, string PeaksEndianMode, bool blnUpdatePeaksCountIfInconsistent)
        {
            // Parses strMSMSDataBase64Encoded and stores the data in sngValues

            float[] sngDataArray = null;
            double[] dblDataArray = null;
            bool zLibCompressed = false;
            clsBase64EncodeDecode.eEndianTypeConstants eEndianMode;
            int intIndex;
            bool blnSuccess;
            blnSuccess = false;
            if (strMSMSDataBase64Encoded is null || strMSMSDataBase64Encoded.Length == 0)
            {
                sngValues = new float[0];
            }
            else
            {
                try
                {
                    eEndianMode = mCurrentSpectrum.GetEndianModeValue(PeaksEndianMode);
                    switch (NumericPrecisionOfData)
                    {
                        case 32:
                            {
                                if (clsBase64EncodeDecode.DecodeNumericArray(strMSMSDataBase64Encoded, out sngDataArray, zLibCompressed, eEndianMode))
                                {
                                    sngValues = new float[sngDataArray.Length];
                                    sngDataArray.CopyTo(sngValues, 0);
                                    blnSuccess = true;
                                }

                                break;
                            }

                        case 64:
                            {
                                if (clsBase64EncodeDecode.DecodeNumericArray(strMSMSDataBase64Encoded, out dblDataArray, zLibCompressed, eEndianMode))
                                {
                                    sngValues = new float[dblDataArray.Length];
                                    var loopTo = dblDataArray.Length - 1;
                                    for (intIndex = 0; intIndex <= loopTo; intIndex++)
                                        sngValues[intIndex] = (float)dblDataArray[intIndex];
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
                            ref var withBlock = ref mCurrentSpectrum;
                            if (sngValues.Length != withBlock.DataCount)
                            {
                                if (withBlock.DataCount == 0 && sngValues.Length > 0 && Math.Abs(sngValues[0]) < float.Epsilon)
                                {
                                }
                                // Leave .PeaksCount at 0
                                else if (blnUpdatePeaksCountIfInconsistent)
                                {
                                    // This shouldn't normally be necessary
                                    OnErrorEvent("Unexpected condition in ParseBinaryData: sngValues.Length <> .DataCount and .DataCount > 0");
                                    withBlock.DataCount = sngValues.Length;
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

        private bool ParseBinaryData(string strMSMSDataBase64Encoded, ref double[] dblValues, int NumericPrecisionOfData, string PeaksEndianMode, bool blnUpdatePeaksCountIfInconsistent)
        {
            // Parses strMSMSDataBase64Encoded and stores the data in dblValues

            float[] sngDataArray = null;
            double[] dblDataArray = null;
            bool zLibCompressed = false;
            clsBase64EncodeDecode.eEndianTypeConstants eEndianMode;
            bool blnSuccess;
            blnSuccess = false;
            if (strMSMSDataBase64Encoded is null || strMSMSDataBase64Encoded.Length == 0)
            {
                dblValues = new double[0];
            }
            else
            {
                try
                {
                    eEndianMode = mCurrentSpectrum.GetEndianModeValue(PeaksEndianMode);
                    switch (NumericPrecisionOfData)
                    {
                        case 32:
                            {
                                if (clsBase64EncodeDecode.DecodeNumericArray(strMSMSDataBase64Encoded, out sngDataArray, zLibCompressed, eEndianMode))
                                {
                                    dblValues = new double[sngDataArray.Length];
                                    sngDataArray.CopyTo(dblValues, 0);
                                    blnSuccess = true;
                                }

                                break;
                            }

                        case 64:
                            {
                                if (clsBase64EncodeDecode.DecodeNumericArray(strMSMSDataBase64Encoded, out dblDataArray, zLibCompressed, eEndianMode))
                                {
                                    dblValues = new double[dblDataArray.Length];
                                    dblDataArray.CopyTo(dblValues, 0);
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
                            ref var withBlock = ref mCurrentSpectrum;
                            if (dblValues.Length != withBlock.DataCount)
                            {
                                if (withBlock.DataCount == 0 && dblValues.Length > 0 && Math.Abs(dblValues[0]) < float.Epsilon)
                                {
                                }
                                // Leave .PeaksCount at 0
                                else if (blnUpdatePeaksCountIfInconsistent)
                                {
                                    // This shouldn't normally be necessary
                                    OnErrorEvent("Unexpected condition in ParseBinaryData: sngValues.Length <> .DataCount and .DataCount > 0");
                                    withBlock.DataCount = dblValues.Length;
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
                // Check the last element name sent to startElement to determine
                // what to do with the data we just received
                if ((mCurrentElement ?? "") == ScanSectionNames.ArrayData)
                {
                    // Note: We could use GetParentElement() to determine whether this base-64 encoded data
                    // belongs to mzArrayBinary or intenArrayBinary, but it is faster to use mCurrentXMLDataFileSection
                    switch (mCurrentXMLDataFileSection)
                    {
                        case eCurrentMZDataFileSectionConstants.SpectrumDataArrayMZ:
                            {
                                if (!mSkipBinaryData)
                                {
                                    {
                                        ref var withBlock = ref mCurrentSpectrum;
                                        blnSuccess = ParseBinaryData(XMLTextReaderGetInnerText(), ref withBlock.MZList, withBlock.NumericPrecisionOfDataMZ, withBlock.PeaksEndianModeMZ, true);
                                        if (!blnSuccess)
                                        {
                                            withBlock.DataCount = 0;
                                        }
                                    }
                                }
                                else
                                {
                                    blnSuccess = true;
                                }

                                break;
                            }

                        case eCurrentMZDataFileSectionConstants.SpectrumDataArrayIntensity:
                            {
                                if (!mSkipBinaryData)
                                {
                                    {
                                        ref var withBlock1 = ref mCurrentSpectrum;
                                        blnSuccess = ParseBinaryData(XMLTextReaderGetInnerText(), ref withBlock1.IntensityList, withBlock1.NumericPrecisionOfDataIntensity, withBlock1.PeaksEndianModeIntensity, false);
                                        // Note: Not calling .ComputeBasePeakAndTIC() here since it will be called when the spectrum is Validated
                                    }
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
                // If we just moved out of a spectrum element, then finalize the current scan
                if ((mXMLReader.Name ?? "") == ScanSectionNames.spectrum)
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
            string strCVName = string.Empty;
            string strValue = string.Empty;
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
                case XMLSectionNames.CVParam:
                    {
                        switch (mCurrentXMLDataFileSection)
                        {
                            case eCurrentMZDataFileSectionConstants.DataProcessingMethod:
                                {
                                    if (GetCVNameAndValue(out strCVName, out strValue))
                                    {
                                        switch (strCVName ?? "")
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
                                        switch (strCVName ?? "")
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
                                        switch (strCVName ?? "")
                                        {
                                            case PrecursorIonSelectionCVParamNames.MassToChargeRatio:
                                                {
                                                    mCurrentSpectrum.ParentIonMZ = CDblSafe(strValue, 0d);
                                                    {
                                                        ref var withBlock = ref mCurrentSpectrum;
                                                        withBlock.ParentIonIntensity = FindIonIntensityInRecentSpectra(withBlock.ParentIonSpectrumID, withBlock.ParentIonMZ);
                                                    }

                                                    break;
                                                }

                                            case PrecursorIonSelectionCVParamNames.ChargeState:
                                                {
                                                    mCurrentSpectrum.ParentIonCharge = CIntSafe(strValue, 0);
                                                    break;
                                                }
                                        }
                                    }

                                    break;
                                }

                            case eCurrentMZDataFileSectionConstants.PrecursorActivation:
                                {
                                    if (GetCVNameAndValue(out strCVName, out strValue))
                                    {
                                        switch (strCVName ?? "")
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
                    }

                case ScanSectionNames.spectrumList:
                    {
                        if ((GetParentElement() ?? "") == XMLSectionNames.RootName)
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
                    }

                case ScanSectionNames.spectrum:
                    {
                        if ((GetParentElement() ?? "") == ScanSectionNames.spectrumList)
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
                    }

                case ScanSectionNames.spectrumSettings:
                    {
                        mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.SpectrumSettings;
                        break;
                    }

                case ScanSectionNames.acqSpecification:
                    {
                        if ((GetParentElement() ?? "") == ScanSectionNames.spectrumSettings)
                        {
                            {
                                ref var withBlock1 = ref mCurrentSpectrum;
                                withBlock1.SpectrumType = GetAttribValue(AcqSpecificationAttributeNames.spectrumType, clsSpectrumInfo.SpectrumTypeNames.discrete);
                                withBlock1.SpectrumCombinationMethod = GetAttribValue(AcqSpecificationAttributeNames.methodOfCombination, string.Empty);
                                withBlock1.ScanCount = GetAttribValue(AcqSpecificationAttributeNames.count, 1);
                            }

                            mAcquisitionElementCount = 0;
                        }

                        break;
                    }

                case ScanSectionNames.acquisition:
                    {
                        if ((GetParentElement() ?? "") == ScanSectionNames.acqSpecification)
                        {
                            // Only update mCurrentSpectrum.ScanNumber if mCurrentSpectrum.ScanCount = 1 or
                            // mAcquisitionElementCount = 1
                            mAcquisitionElementCount += 1;
                            if (mAcquisitionElementCount == 1 | mCurrentSpectrum.ScanCount == 1)
                            {
                                mCurrentSpectrum.ScanNumber = GetAttribValue(AcquisitionAttributeNames.acqNumber, 0);
                            }
                        }

                        break;
                    }

                case ScanSectionNames.spectrumInstrument:
                    {
                        if ((GetParentElement() ?? "") == ScanSectionNames.spectrumSettings)
                        {
                            mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.SpectrumInstrument;
                            mCurrentSpectrum.MSLevel = GetAttribValue(SpectrumInstrumentAttributeNames.msLevel, 1);
                            mCurrentSpectrum.mzRangeStart = GetAttribValue(SpectrumInstrumentAttributeNames.mzRangeStart, 0);
                            mCurrentSpectrum.mzRangeEnd = GetAttribValue(SpectrumInstrumentAttributeNames.mzRangeStop, 0);
                        }

                        break;
                    }

                case ScanSectionNames.precursorList:
                    {
                        mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.PrecursorList;
                        break;
                    }

                case ScanSectionNames.precursor:
                    {
                        if ((GetParentElement() ?? "") == ScanSectionNames.precursorList)
                        {
                            mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.PrecursorEntry;
                            mCurrentSpectrum.ParentIonSpectrumMSLevel = GetAttribValue(PrecursorAttributeNames.msLevel, 0);
                            mCurrentSpectrum.ParentIonSpectrumID = GetAttribValue(PrecursorAttributeNames.spectrumRef, 0);
                        }

                        break;
                    }

                case ScanSectionNames.ionSelection:
                    {
                        if ((GetParentElement() ?? "") == ScanSectionNames.precursor)
                        {
                            if ((GetParentElement(mParentElementStack.Count - 1) ?? "") == ScanSectionNames.precursorList)
                            {
                                mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.PrecursorIonSelection;
                            }
                        }

                        break;
                    }

                case ScanSectionNames.activation:
                    {
                        if ((GetParentElement() ?? "") == ScanSectionNames.precursor)
                        {
                            if ((GetParentElement(mParentElementStack.Count - 1) ?? "") == ScanSectionNames.precursorList)
                            {
                                mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.PrecursorActivation;
                            }
                        }

                        break;
                    }

                case ScanSectionNames.mzArrayBinary:
                    {
                        mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.SpectrumDataArrayMZ;
                        break;
                    }

                case ScanSectionNames.intenArrayBinary:
                    {
                        mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.SpectrumDataArrayIntensity;
                        break;
                    }

                case ScanSectionNames.ArrayData:
                    {
                        switch (mCurrentXMLDataFileSection)
                        {
                            case eCurrentMZDataFileSectionConstants.SpectrumDataArrayMZ:
                                {
                                    {
                                        ref var withBlock2 = ref mCurrentSpectrum;
                                        withBlock2.NumericPrecisionOfDataMZ = GetAttribValue(BinaryDataAttributeNames.precision, 32);
                                        withBlock2.PeaksEndianModeMZ = GetAttribValue(BinaryDataAttributeNames.endian, clsSpectrumInfoMzData.EndianModes.littleEndian);
                                        withBlock2.DataCount = GetAttribValue(BinaryDataAttributeNames.length, 0);
                                    }

                                    break;
                                }

                            case eCurrentMZDataFileSectionConstants.SpectrumDataArrayIntensity:
                                {
                                    {
                                        ref var withBlock3 = ref mCurrentSpectrum;
                                        withBlock3.NumericPrecisionOfDataIntensity = GetAttribValue(BinaryDataAttributeNames.precision, 32);
                                        withBlock3.PeaksEndianModeIntensity = GetAttribValue(BinaryDataAttributeNames.endian, clsSpectrumInfoMzData.EndianModes.littleEndian);
                                        // Only update .DataCount if it is currently 0
                                        if (withBlock3.DataCount == 0)
                                        {
                                            withBlock3.DataCount = GetAttribValue(BinaryDataAttributeNames.length, 0);
                                        }
                                    }

                                    break;
                                }
                        }

                        break;
                    }

                case XMLSectionNames.RootName:
                    {
                        mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.Start;
                        if (mXMLReader.HasAttributes)
                        {
                            ValidateMZDataFileVersion(GetAttribValue(mzDataRootAttrbuteNames.version, ""));
                        }

                        break;
                    }

                case HeaderSectionNames.Description:
                    {
                        if ((GetParentElement() ?? "") == XMLSectionNames.RootName)
                        {
                            mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.Headers;
                        }

                        break;
                    }

                case HeaderSectionNames.admin:
                    {
                        if ((GetParentElement() ?? "") == HeaderSectionNames.Description)
                        {
                            mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.Admin;
                        }

                        break;
                    }

                case HeaderSectionNames.instrument:
                    {
                        if ((GetParentElement() ?? "") == HeaderSectionNames.Description)
                        {
                            mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.Instrument;
                        }

                        break;
                    }

                case HeaderSectionNames.dataProcessing:
                    {
                        if ((GetParentElement() ?? "") == HeaderSectionNames.Description)
                        {
                            mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.DataProcessing;
                        }

                        break;
                    }

                case HeaderSectionNames.processingMethod:
                    {
                        if ((GetParentElement() ?? "") == HeaderSectionNames.dataProcessing)
                        {
                            mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.DataProcessingMethod;
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

        private void ValidateMZDataFileVersion(string strFileVersion)
        {
            // This sub should be called from ParseElementContent

            System.Text.RegularExpressions.Regex objFileVersionRegEx;
            System.Text.RegularExpressions.Match objMatch;
            string strMessage;
            try
            {
                mFileVersion = string.Empty;

                // Currently, the only version supported is 1.x (typically 1.05)
                objFileVersionRegEx = new System.Text.RegularExpressions.Regex(@"1\.[0-9]+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                // Validate the mzData file version
                if (!string.IsNullOrWhiteSpace(strFileVersion))
                {
                    mFileVersion = string.Copy(strFileVersion);
                    objMatch = objFileVersionRegEx.Match(strFileVersion);
                    if (!objMatch.Success)
                    {
                        // Unknown version
                        // Log error and abort if mParseFilesWithUnknownVersion = False
                        strMessage = "Unknown mzData file version: " + mFileVersion;
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