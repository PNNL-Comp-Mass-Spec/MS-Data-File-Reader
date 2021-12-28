// -------------------------------------------------------------------------------
// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
// Copyright 2021, Battelle Memorial Institute.  All Rights Reserved.
//
// E-mail: matthew.monroe@pnl.gov or proteomics@pnnl.gov
// Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://panomics.pnnl.gov/ or https://www.pnnl.gov/integrative-omics
// -------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using PRISM;

namespace MSDataFileReader
{
    /// <summary>
    /// This is the base class for the various MS data file readers
    /// </summary>
    public abstract class clsMSDataFileReaderBaseClass : EventNotifier
    {
        // Ignore Spelling: accessor

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

        public const string PROGRAM_DATE = "December 28, 2021";
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
            /// <summary>
            /// Actual scan count if mDataReaderMode = Cached or mDataReaderMode = Indexed
            /// Scan count as reported by the XML file if mDataReaderMode = Sequential
            /// </summary>
            public int ScanCount;

            /// <summary>
            /// First scan number
            /// </summary>
            public int ScanNumberMinimum;

            /// <summary>
            /// Last scan number
            /// </summary>
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
        // If more than one spectrum comes from the same scan, tracks the first one read
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
                    // Return M+H, which is currently stored in dblNewMZ
                }
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

        /// <summary>
        /// Determine the file type based on its extension
        /// </summary>
        /// <param name="strFileNameOrPath"></param>
        /// <param name="eFileType"></param>
        /// <returns>True if a known file type, otherwise false</returns>
        public static bool DetermineFileType(string strFileNameOrPath, out dftDataFileTypeConstants eFileType)
        {
            bool blnKnownType;
            eFileType = dftDataFileTypeConstants.Unknown;

            try
            {
                if (string.IsNullOrWhiteSpace(strFileNameOrPath))
                {
                    return false;
                }

                var strFileName = Path.GetFileName(strFileNameOrPath.ToUpper());
                var strFileExtension = Path.GetExtension(strFileName);
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

        /// <summary>
        /// Obtain a forward-only reader for the given file
        /// </summary>
        /// <param name="strFileNameOrPath"></param>
        /// <returns>An MS File reader, or null if an error or unknown file extension</returns>
        public static clsMSDataFileReaderBaseClass GetFileReaderBasedOnFileType(string strFileNameOrPath)
        {
            clsMSDataFileReaderBaseClass objFileReader = null;

            if (DetermineFileType(strFileNameOrPath, out var eFileType))
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

        /// <summary>
        /// Obtain a random-access reader for the given file
        /// </summary>
        /// <remarks>
        /// Returns null if the file type is _dta.txt or .mgf since those file types do not have file accessors
        /// </remarks>
        /// <param name="strFileNameOrPath"></param>
        /// <returns>An MS file accessor, or null if an error or unknown file extension</returns>
        public static clsMSDataFileAccessorBaseClass GetFileAccessorBasedOnFileType(string strFileNameOrPath)
        {
            clsMSDataFileAccessorBaseClass objFileAccessor = null;

            if (DetermineFileType(strFileNameOrPath, out var eFileType))
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

        /// <summary>
        /// Obtain the list of scan numbers (aka acquisition numbers)
        /// </summary>
        /// <param name="ScanNumberList"></param>
        /// <returns>True if successful, false if an error or no cached spectra</returns>
        public virtual bool GetScanNumberList(out int[] ScanNumberList)
        {
            var blnSuccess = default(bool);

            try
            {
                blnSuccess = false;
                if (mDataReaderMode == drmDataReaderModeConstants.Cached && mCachedSpectra != null)
                {
                    ScanNumberList = new int[mCachedSpectrumCount];
                    var loopTo = ScanNumberList.Length - 1;
                    int intSpectrumIndex;
                    for (intSpectrumIndex = 0; intSpectrumIndex <= loopTo; intSpectrumIndex++)
                    {
                        ScanNumberList[intSpectrumIndex] = mCachedSpectra[intSpectrumIndex].ScanNumber;
                    }

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

        /// <summary>
        /// Get the spectrum at the given index
        /// </summary>
        /// <remarks>
        /// Only valid if we have Cached data in memory
        /// </remarks>
        /// <param name="intSpectrumIndex"></param>
        /// <param name="objSpectrumInfo"></param>
        /// <returns>True if successful, false if an error</returns>
        public virtual bool GetSpectrumByIndex(int intSpectrumIndex, out clsSpectrumInfo objSpectrumInfo)
        {
            var blnSuccess = false;

            if (mDataReaderMode == drmDataReaderModeConstants.Cached && mCachedSpectrumCount > 0)
            {
                if (intSpectrumIndex >= 0 && intSpectrumIndex < mCachedSpectrumCount && mCachedSpectra != null)
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

        /// <summary>
        /// Get the spectrum for the given scan number by
        /// looking for the first entry in mCachedSpectra with .ScanNumber = intScanNumber
        /// </summary>
        /// <remarks>
        /// Only valid if we have Cached data in memory
        /// </remarks>
        /// <param name="intScanNumber"></param>
        /// <param name="objSpectrumInfo"></param>
        /// <returns>True if successful, false if an error or invalid scan number</returns>
        public virtual bool GetSpectrumByScanNumber(int intScanNumber, out clsSpectrumInfo objSpectrumInfo)
        {
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

            mInputFileStats.ScanCount = 0;
            mInputFileStats.ScanNumberMinimum = 0;
            mInputFileStats.ScanNumberMaximum = 0;

            mCachedSpectraScanToIndex.Clear();

            mAbortProcessing = false;
            mAutoShrinkDataLists = true;
        }

        public static bool IsNumber(string strValue)
        {
            try
            {
                return double.TryParse(strValue, out _);
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

        /// <summary>
        /// Cache the entire file in memory
        /// </summary>
        /// <returns></returns>
        public virtual bool ReadAndCacheEntireFile()
        {
            bool blnSuccess;

            try
            {
                mDataReaderMode = drmDataReaderModeConstants.Cached;
                AutoShrinkDataLists = false;
                mReadingAndStoringSpectra = true;
                ResetProgress();

                while (ReadNextSpectrum(out var objSpectrumInfo) && !mAbortProcessing)
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

                        mInputFileStats.ScanCount = mCachedSpectrumCount;
                        var intScanNumber = objSpectrumInfo.ScanNumber;
                        if (mInputFileStats.ScanCount == 1)
                        {
                            mInputFileStats.ScanNumberMaximum = intScanNumber;
                            mInputFileStats.ScanNumberMinimum = intScanNumber;
                        }
                        else
                        {
                            if (intScanNumber < mInputFileStats.ScanNumberMinimum)
                            {
                                mInputFileStats.ScanNumberMinimum = intScanNumber;
                            }

                            if (intScanNumber > mInputFileStats.ScanNumberMaximum)
                            {
                                mInputFileStats.ScanNumberMaximum = intScanNumber;
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
            mInputFileStats.ScanCount = intScanCount;
            if (intScanCount <= 1)
            {
                mInputFileStats.ScanNumberMinimum = intScanNumber;
                mInputFileStats.ScanNumberMaximum = intScanNumber;
            }
            else
            {
                if (intScanNumber < mInputFileStats.ScanNumberMinimum)
                {
                    mInputFileStats.ScanNumberMinimum = intScanNumber;
                }

                if (intScanNumber > mInputFileStats.ScanNumberMaximum)
                {
                    mInputFileStats.ScanNumberMaximum = intScanNumber;
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