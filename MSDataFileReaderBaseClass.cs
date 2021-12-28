using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using PRISM;

namespace MSDataFileReader
{

    // This is the base class for the various MS data file readers
    // 
    // -------------------------------------------------------------------------------
    // Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    // Copyright 2006, Battelle Memorial Institute.  All Rights Reserved.
    // Started March 24, 2006
    // 
    // E-mail: matthew.monroe@pnl.gov or proteomics@pnnl.gov
    // Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://panomics.pnnl.gov/ or https://www.pnnl.gov/integrative-omics
    // -------------------------------------------------------------------------------
    // 

    public abstract class clsMSDataFileReaderBaseClass : EventNotifier
    {
        public event ProgressResetEventHandler ProgressReset;

        public delegate void ProgressResetEventHandler();

        public event ProgressChangedEventHandler ProgressChanged;

        public delegate void ProgressChangedEventHandler(string taskDescription, float percentComplete);

        // PercentComplete ranges from 0 to 100, but can contain decimal percentage values
        public event ProgressCompleteEventHandler ProgressComplete;

        public delegate void ProgressCompleteEventHandler();

        protected string mProgressStepDescription;

        // Ranges from 0 to 100, but can contain decimal percentage values
        protected float mProgressPercentComplete;

        public clsMSDataFileReaderBaseClass()
        {
            InitializeLocalVariables();
        }

        #region Constants and Enums

        public const string PROGRAM_DATE = "July 8, 2016";
        public const double CHARGE_CARRIER_MASS_AVG = 1.00739d;
        public const double CHARGE_CARRIER_MASS_MONOISO = 1.00727649d;
        public const double MASS_HYDROGEN = 1.0078246d;
        protected const int DEFAULT_MAX_CACHE_MEMORY_USAGE_MB = 128;

        public enum drmDataReaderModeConstants
        {
            Sequential = 0,
            Cached = 1,
            Indexed = 2
        }

        public enum dftDataFileTypeConstants
        {
            Unknown = -1,
            mzData = 0,
            mzXML = 1,
            DtaText = 2,
            MGF = 3
        }

        #endregion

        #region Structures

        protected struct udtFileStatsType
        {
            // Actual scan count if mDataReaderMode = Cached or mDataReaderMode = Indexed, or scan count as reported by the XML file if mDataReaderMode = Sequential
            public int ScanCount;
            public int ScanNumberMinimum;
            public int ScanNumberMaximum;
        }

        #endregion

        #region Classwide Variables

        protected double mChargeCarrierMass;
        protected string mErrorMessage;
        protected string mFileVersion;
        protected drmDataReaderModeConstants mDataReaderMode;
        protected bool mReadingAndStoringSpectra;
        protected bool mAbortProcessing;
        protected bool mParseFilesWithUnknownVersion = false;
        protected string mInputFilePath = string.Empty;
        protected udtFileStatsType mInputFileStats;

        // These variables are used when mDataReaderMode = Cached
        protected int mCachedSpectrumCount;
        protected clsSpectrumInfo[] mCachedSpectra;

        // This dictionary maps scan number to index in mCachedSpectra()
        // If more than one spectrum comes from the same scan, then tracks the first one read
        protected readonly Dictionary<int, int> mCachedSpectraScanToIndex = new();

        protected bool mAutoShrinkDataLists;

        #endregion

        #region Processing Options and Interface Functions

        /// <summary>
    /// When mAutoShrinkDataLists is True, clsSpectrumInfo.MZList().Length and clsSpectrumInfo.IntensityList().Length will equal DataCount;
    /// When mAutoShrinkDataLists is False, the memory will not be freed when DataCount shrinks or clsSpectrumInfo.Clear() is called
    /// </summary>
    /// <value></value>
    /// <returns></returns>
    /// <remarks>
    /// Setting mAutoShrinkDataLists to False helps reduce slow, increased memory usage due to inefficient garbage collection
    /// (this is not much of an issue in 2016, and thus this parameter defaults to True)
    /// </remarks>
        public bool AutoShrinkDataLists
        {
            get
            {
                return mAutoShrinkDataLists;
            }

            set
            {
                mAutoShrinkDataLists = value;
            }
        }

        public virtual int CachedSpectrumCount
        {
            get
            {
                if (mDataReaderMode == drmDataReaderModeConstants.Cached)
                {
                    return mCachedSpectrumCount;
                }
                else
                {
                    return 0;
                }
            }
        }

        public int CachedSpectraScanNumberMinimum
        {
            get
            {
                return mInputFileStats.ScanNumberMinimum;
            }
        }

        public int CachedSpectraScanNumberMaximum
        {
            get
            {
                return mInputFileStats.ScanNumberMaximum;
            }
        }

        public double ChargeCarrierMass
        {
            get
            {
                return mChargeCarrierMass;
            }

            set
            {
                mChargeCarrierMass = value;
            }
        }

        public string ErrorMessage
        {
            get
            {
                if (mErrorMessage is null)
                    mErrorMessage = string.Empty;
                return mErrorMessage;
            }
        }

        public string InputFilePath
        {
            get
            {
                return mInputFilePath;
            }
        }

        public string FileVersion
        {
            get
            {
                return mFileVersion;
            }
        }

        public virtual bool ParseFilesWithUnknownVersion
        {
            get
            {
                return mParseFilesWithUnknownVersion;
            }

            set
            {
                mParseFilesWithUnknownVersion = value;
            }
        }

        public virtual string ProgressStepDescription
        {
            get
            {
                return mProgressStepDescription;
            }
        }

        // ProgressPercentComplete ranges from 0 to 100, but can contain decimal percentage values
        public float ProgressPercentComplete
        {
            get
            {
                return (float)Math.Round(mProgressPercentComplete, 2);
            }
        }

        protected bool ReadingAndStoringSpectra
        {
            get
            {
                return mReadingAndStoringSpectra;
            }
        }

        // Note: When reading mzXML and mzData files the the FileReader classes, this value is not populated until after the first scan is read
        // When using the FileAccessor classes, this value is populated after the file is indexed
        // For .MGF and .DtaText files, this value will always be 0
        public int ScanCount
        {
            get
            {
                return mInputFileStats.ScanCount;
            }
        }

        #endregion

        public void AbortProcessingNow()
        {
            mAbortProcessing = true;
        }

        protected bool CBoolSafe(string strValue, bool defaultValue)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(strValue))
                {
                    return defaultValue;
                }

                double dblValue;
                if (double.TryParse(strValue, out dblValue))
                {
                    if (Math.Abs(dblValue) < float.Epsilon)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }

                bool blnValue;
                if (bool.TryParse(strValue, out blnValue))
                {
                    return blnValue;
                }

                return defaultValue;
            }
            catch (Exception ex)
            {
                return defaultValue;
            }
        }

        protected double CDblSafe(string strValue, double defaultValue)
        {
            try
            {
                return double.Parse(strValue);
            }
            catch (Exception ex)
            {
                return defaultValue;
            }
        }

        protected int CIntSafe(string strValue, int defaultValue)
        {
            try
            {
                return int.Parse(strValue);
            }
            catch (Exception ex)
            {
                return defaultValue;
            }
        }

        protected float CSngSafe(string strValue, float defaultValue)
        {
            try
            {
                return float.Parse(strValue);
            }
            catch (Exception ex)
            {
                return defaultValue;
            }
        }

        public abstract void CloseFile();

        public double ConvoluteMass(double dblMassMZ, int intCurrentCharge, int intDesiredCharge)
        {
            return ConvoluteMass(dblMassMZ, intCurrentCharge, intDesiredCharge, mChargeCarrierMass);
        }

        /// <summary>
    /// Converts dblMassMZ to the MZ that would appear at the given intDesiredCharge
    /// </summary>
    /// <param name="dblMassMZ"></param>
    /// <param name="intCurrentCharge"></param>
    /// <param name="intDesiredCharge"></param>
    /// <param name="dblChargeCarrierMass"></param>
    /// <returns></returns>
    /// <remarks>To return the neutral mass, set intDesiredCharge to 0</remarks>
        public static double ConvoluteMass(double dblMassMZ, int intCurrentCharge, int intDesiredCharge, double dblChargeCarrierMass)
        {
            double dblNewMZ;
            if (intCurrentCharge == intDesiredCharge)
            {
                dblNewMZ = dblMassMZ;
            }
            else
            {
                if (intCurrentCharge == 1)
                {
                    dblNewMZ = dblMassMZ;
                }
                else if (intCurrentCharge > 1)
                {
                    // Convert dblMassMZ to M+H
                    dblNewMZ = dblMassMZ * intCurrentCharge - dblChargeCarrierMass * (intCurrentCharge - 1);
                }
                else if (intCurrentCharge == 0)
                {
                    // Convert dblMassMZ (which is neutral) to M+H and store in dblNewMZ
                    dblNewMZ = dblMassMZ + dblChargeCarrierMass;
                }
                else
                {
                    // Negative charges are not supported; return 0
                    return 0d;
                }

                if (intDesiredCharge > 1)
                {
                    dblNewMZ = (dblNewMZ + dblChargeCarrierMass * (intDesiredCharge - 1)) / intDesiredCharge;
                }
                else if (intDesiredCharge == 1)
                {
                }
                // Return M+H, which is currently stored in dblNewMZ
                else if (intDesiredCharge == 0)
                {
                    // Return the neutral mass
                    dblNewMZ -= dblChargeCarrierMass;
                }
                else
                {
                    // Negative charges are not supported; return 0
                    dblNewMZ = 0d;
                }
            }

            return dblNewMZ;
        }

        public static bool DetermineFileType(string strFileNameOrPath, out dftDataFileTypeConstants eFileType)
        {

            // Returns true if the file type is known
            // Returns false if unknown or an error

            string strFileExtension;
            string strFileName;
            bool blnKnownType;
            eFileType = dftDataFileTypeConstants.Unknown;
            try
            {
                if (string.IsNullOrWhiteSpace(strFileNameOrPath))
                {
                    return false;
                }

                strFileName = Path.GetFileName(strFileNameOrPath.ToUpper());
                strFileExtension = Path.GetExtension(strFileName);
                if (string.IsNullOrWhiteSpace(strFileExtension))
                {
                    return false;
                }

                if (!strFileExtension.StartsWith("."))
                {
                    strFileExtension = '.' + strFileExtension;
                }

                // Assume known file type for now
                blnKnownType = true;
                switch (strFileExtension ?? "")
                {
                    case clsMzDataFileReader.MZDATA_FILE_EXTENSION:
                        {
                            eFileType = dftDataFileTypeConstants.mzData;
                            break;
                        }

                    case clsMzXMLFileReader.MZXML_FILE_EXTENSION:
                        {
                            eFileType = dftDataFileTypeConstants.mzXML;
                            break;
                        }

                    case clsMGFFileReader.MGF_FILE_EXTENSION:
                        {
                            eFileType = dftDataFileTypeConstants.MGF;
                            break;
                        }

                    default:
                        {
                            // See if the filename ends with MZDATA_FILE_EXTENSION_XML or MZXML_FILE_EXTENSION_XML
                            if (strFileName.EndsWith(clsMzDataFileReader.MZDATA_FILE_EXTENSION_XML))
                            {
                                eFileType = dftDataFileTypeConstants.mzData;
                            }
                            else if (strFileName.EndsWith(clsMzXMLFileReader.MZXML_FILE_EXTENSION_XML))
                            {
                                eFileType = dftDataFileTypeConstants.mzXML;
                            }
                            else if (strFileName.EndsWith(clsDtaTextFileReader.DTA_TEXT_FILE_EXTENSION))
                            {
                                eFileType = dftDataFileTypeConstants.DtaText;
                            }
                            else
                            {
                                // Unknown file type
                                blnKnownType = false;
                            }

                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                blnKnownType = false;
            }

            return blnKnownType;
        }

        public static clsMSDataFileReaderBaseClass GetFileReaderBasedOnFileType(string strFileNameOrPath)
        {
            // Returns a file reader based on strFileNameOrPath
            // If the file type cannot be determined, then returns Nothing

            dftDataFileTypeConstants eFileType;
            clsMSDataFileReaderBaseClass objFileReader = null;
            if (DetermineFileType(strFileNameOrPath, out eFileType))
            {
                switch (eFileType)
                {
                    case dftDataFileTypeConstants.DtaText:
                        {
                            objFileReader = new clsDtaTextFileReader();
                            break;
                        }

                    case dftDataFileTypeConstants.MGF:
                        {
                            objFileReader = new clsMGFFileReader();
                            break;
                        }

                    case dftDataFileTypeConstants.mzData:
                        {
                            objFileReader = new clsMzDataFileReader();
                            break;
                        }

                    case dftDataFileTypeConstants.mzXML:
                        {
                            objFileReader = new clsMzXMLFileReader();
                            break;
                        }

                    default:
                        {
                            break;
                        }
                        // Unknown file type
                }
            }

            return objFileReader;
        }

        public static clsMSDataFileAccessorBaseClass GetFileAccessorBasedOnFileType(string strFileNameOrPath)
        {
            // Returns a file accessor based on strFileNameOrPath
            // If the file type cannot be determined, then returns Nothing
            // If the file type is _Dta.txt or .MGF then returns Nothing since those file types do not have file accessors

            dftDataFileTypeConstants eFileType;
            clsMSDataFileAccessorBaseClass objFileAccessor = null;
            if (DetermineFileType(strFileNameOrPath, out eFileType))
            {
                switch (eFileType)
                {
                    case dftDataFileTypeConstants.mzData:
                        {
                            objFileAccessor = new clsMzDataFileAccessor();
                            break;
                        }

                    case dftDataFileTypeConstants.mzXML:
                        {
                            objFileAccessor = new clsMzXMLFileAccessor();
                            break;
                        }
                    // These file types do not have file accessors
                    case dftDataFileTypeConstants.DtaText:
                    case dftDataFileTypeConstants.MGF:
                        {
                            break;
                        }

                    default:
                        {
                            break;
                        }
                        // Unknown file type
                }
            }

            return objFileAccessor;
        }

        protected abstract string GetInputFileLocation();

        public virtual bool GetScanNumberList(out int[] ScanNumberList)
        {
            // Return the list of cached scan numbers (aka acquisition numbers)

            var blnSuccess = default(bool);
            int intSpectrumIndex;
            try
            {
                blnSuccess = false;
                if (mDataReaderMode == drmDataReaderModeConstants.Cached && mCachedSpectra != null)
                {
                    ScanNumberList = new int[mCachedSpectrumCount];
                    var loopTo = ScanNumberList.Length - 1;
                    for (intSpectrumIndex = 0; intSpectrumIndex <= loopTo; intSpectrumIndex++)
                        ScanNumberList[intSpectrumIndex] = mCachedSpectra[intSpectrumIndex].ScanNumber;
                    blnSuccess = true;
                }
                else
                {
                    ScanNumberList = new int[0];
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in GetScanNumberList", ex);
                ScanNumberList = new int[0];
            }

            return blnSuccess;
        }

        public virtual bool GetSpectrumByIndex(int intSpectrumIndex, out clsSpectrumInfo objSpectrumInfo)
        {
            // Returns True if success, False if failure
            // Only valid if we have Cached data in memory

            bool blnSuccess;
            blnSuccess = false;
            if (mDataReaderMode == drmDataReaderModeConstants.Cached && mCachedSpectrumCount > 0)
            {
                if (intSpectrumIndex >= 0 & intSpectrumIndex < mCachedSpectrumCount & mCachedSpectra != null)
                {
                    objSpectrumInfo = mCachedSpectra[intSpectrumIndex];
                    blnSuccess = true;
                }
                else
                {
                    mErrorMessage = "Invalid spectrum index: " + intSpectrumIndex.ToString();
                    objSpectrumInfo = null;
                }
            }
            else
            {
                mErrorMessage = "Cached data not in memory";
                objSpectrumInfo = null;
            }

            return blnSuccess;
        }

        public virtual bool GetSpectrumByScanNumber(int intScanNumber, out clsSpectrumInfo objSpectrumInfo)
        {
            // Looks for the first entry in mCachedSpectra with .ScanNumber = intScanNumber
            // Returns True if success, False if failure
            // Only valid if we have Cached data in memory

            var blnSuccess = default(bool);
            objSpectrumInfo = null;
            try
            {
                blnSuccess = false;
                mErrorMessage = string.Empty;
                if (mDataReaderMode == drmDataReaderModeConstants.Cached)
                {
                    if (mCachedSpectraScanToIndex.Count == 0)
                    {
                        for (int intSpectrumIndex = 0, loopTo = mCachedSpectrumCount - 1; intSpectrumIndex <= loopTo; intSpectrumIndex++)
                        {
                            if (mCachedSpectra[intSpectrumIndex].ScanNumber == intScanNumber)
                            {
                                objSpectrumInfo = mCachedSpectra[intSpectrumIndex];
                                blnSuccess = true;
                                break;
                            }
                        }
                    }
                    else
                    {
                        var index = mCachedSpectraScanToIndex[intScanNumber];
                        objSpectrumInfo = mCachedSpectra[index];
                        blnSuccess = true;
                    }

                    if (!blnSuccess && mErrorMessage.Length == 0)
                    {
                        mErrorMessage = "Invalid scan number: " + intScanNumber.ToString();
                    }
                }
                else
                {
                    mErrorMessage = "Cached data not in memory";
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in GetSpectrumByScanNumber", ex);
            }

            return blnSuccess;
        }

        protected virtual void InitializeLocalVariables()
        {
            mChargeCarrierMass = CHARGE_CARRIER_MASS_MONOISO;
            mErrorMessage = string.Empty;
            mFileVersion = string.Empty;
            mProgressStepDescription = string.Empty;
            mProgressPercentComplete = 0f;
            mCachedSpectrumCount = 0;
            mCachedSpectra = new clsSpectrumInfo[500];
            {
                ref var withBlock = ref mInputFileStats;
                withBlock.ScanCount = 0;
                withBlock.ScanNumberMinimum = 0;
                withBlock.ScanNumberMaximum = 0;
            }

            mCachedSpectraScanToIndex.Clear();

            mAbortProcessing = false;
            mAutoShrinkDataLists = true;
        }

        public static bool IsNumber(string strValue)
        {
            try
            {
                double argresult = 0d;
                return double.TryParse(strValue, out argresult);
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public abstract bool OpenFile(string strInputFilePath);
        public abstract bool OpenTextStream(string strTextStream);

        /// <summary>
    /// Validates that strInputFilePath exists
    /// </summary>
    /// <param name="strInputFilePath"></param>
    /// <returns>True if the file exists, otherwise false</returns>
    /// <remarks>Updates mFilePath if the file is valid</remarks>
        protected bool OpenFileInit(string strInputFilePath)
        {

            // Make sure any open file or text stream is closed
            CloseFile();
            if (string.IsNullOrEmpty(strInputFilePath))
            {
                mErrorMessage = "Error opening file: input file path is blank";
                return false;
            }

            if (!File.Exists(strInputFilePath))
            {
                mErrorMessage = "File not found: " + strInputFilePath;
                return false;
            }
            else
            {
                mInputFilePath = strInputFilePath;
                return true;
            }
        }

        protected void OperationComplete()
        {
            ProgressComplete?.Invoke();
        }

        public abstract bool ReadNextSpectrum(out clsSpectrumInfo objSpectrumInfo);

        public virtual bool ReadAndCacheEntireFile()
        {
            bool blnSuccess;
            try
            {
                mDataReaderMode = drmDataReaderModeConstants.Cached;
                clsSpectrumInfo objSpectrumInfo = null;
                AutoShrinkDataLists = false;
                mReadingAndStoringSpectra = true;
                ResetProgress();
                while (ReadNextSpectrum(out objSpectrumInfo) & !mAbortProcessing)
                {
                    if (mCachedSpectrumCount >= mCachedSpectra.Length)
                    {
                        Array.Resize(ref mCachedSpectra, mCachedSpectra.Length * 2);
                    }

                    if (objSpectrumInfo != null)
                    {
                        mCachedSpectra[mCachedSpectrumCount] = objSpectrumInfo;
                        if (!mCachedSpectraScanToIndex.ContainsKey(objSpectrumInfo.ScanNumber))
                        {
                            mCachedSpectraScanToIndex.Add(objSpectrumInfo.ScanNumber, mCachedSpectrumCount);
                        }

                        mCachedSpectrumCount += 1;
                        {
                            ref var withBlock = ref mInputFileStats;
                            withBlock.ScanCount = mCachedSpectrumCount;
                            int intScanNumber = objSpectrumInfo.ScanNumber;
                            if (withBlock.ScanCount == 1)
                            {
                                withBlock.ScanNumberMaximum = intScanNumber;
                                withBlock.ScanNumberMinimum = intScanNumber;
                            }
                            else
                            {
                                if (intScanNumber < withBlock.ScanNumberMinimum)
                                {
                                    withBlock.ScanNumberMinimum = intScanNumber;
                                }

                                if (intScanNumber > withBlock.ScanNumberMaximum)
                                {
                                    withBlock.ScanNumberMaximum = intScanNumber;
                                }
                            }
                        }
                    }
                }

                if (!mAbortProcessing)
                {
                    OperationComplete();
                }

                blnSuccess = true;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ReadAndCacheEntireFile", ex);
                blnSuccess = false;
            }
            finally
            {
                mReadingAndStoringSpectra = false;
            }

            return blnSuccess;
        }

        protected void ResetProgress()
        {
            ProgressReset?.Invoke();
        }

        protected void ResetProgress(string strProgressStepDescription)
        {
            UpdateProgress(strProgressStepDescription, 0f);
            ProgressReset?.Invoke();
        }

        protected void UpdateFileStats(int intScanNumber)
        {
            UpdateFileStats(mInputFileStats.ScanCount + 1, intScanNumber);
        }

        protected void UpdateFileStats(int intScanCount, int intScanNumber)
        {
            {
                ref var withBlock = ref mInputFileStats;
                withBlock.ScanCount = intScanCount;
                if (intScanCount <= 1)
                {
                    withBlock.ScanNumberMinimum = intScanNumber;
                    withBlock.ScanNumberMaximum = intScanNumber;
                }
                else
                {
                    if (intScanNumber < withBlock.ScanNumberMinimum)
                    {
                        withBlock.ScanNumberMinimum = intScanNumber;
                    }

                    if (intScanNumber > withBlock.ScanNumberMaximum)
                    {
                        withBlock.ScanNumberMaximum = intScanNumber;
                    }
                }
            }
        }

        public void UpdateProgressDescription(string strProgressStepDescription)
        {
            mProgressStepDescription = strProgressStepDescription;
        }

        protected void UpdateProgress(string strProgressStepDescription)
        {
            UpdateProgress(strProgressStepDescription, mProgressPercentComplete);
        }

        protected void UpdateProgress(double dblPercentComplete)
        {
            UpdateProgress(ProgressStepDescription, (float)dblPercentComplete);
        }

        protected void UpdateProgress(float sngPercentComplete)
        {
            UpdateProgress(ProgressStepDescription, sngPercentComplete);
        }

        protected void UpdateProgress(string strProgressStepDescription, float sngPercentComplete)
        {
            mProgressStepDescription = strProgressStepDescription;
            if (sngPercentComplete < 0f)
            {
                sngPercentComplete = 0f;
            }
            else if (sngPercentComplete > 100f)
            {
                sngPercentComplete = 100f;
            }

            mProgressPercentComplete = sngPercentComplete;
            ProgressChanged?.Invoke(ProgressStepDescription, ProgressPercentComplete);
        }
    }
}