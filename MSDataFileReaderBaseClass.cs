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

// ReSharper disable UnusedMember.Global

namespace MSDataFileReader
{
    /// <summary>
    /// This is the base class for the various MS data file readers
    /// </summary>
    public abstract class MsDataFileReaderBaseClass : EventNotifier
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

        /// <summary>
        /// Constructor
        /// </summary>
        protected MsDataFileReaderBaseClass()
        {
            mCachedSpectra = new List<SpectrumInfo>();
            InitializeLocalVariables();
        }

        public const string PROGRAM_DATE = "December 28, 2021";

        /// <summary>
        /// Charge carrier for average mass mode
        /// </summary>
        public const double CHARGE_CARRIER_MASS_AVG = 1.00739d;

        /// <summary>
        /// Charge carrier for monoisotopic mass mode
        /// </summary>
        public const double CHARGE_CARRIER_MASS_MONOISOTOPIC = 1.00727649d;

        public const double MASS_HYDROGEN = 1.0078246d;

        protected const int DEFAULT_MAX_CACHE_MEMORY_USAGE_MB = 128;

        public enum DataReaderMode
        {
            Sequential = 0,
            Cached = 1,
            Indexed = 2
        }

        public enum DataFileType
        {
            Unknown = -1,
            mzData = 0,
            mzXML = 1,
            DtaText = 2,
            MGF = 3
        }

        protected struct FileStatsType
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

        protected double mChargeCarrierMass;

        protected string mErrorMessage;

        protected string mFileVersion;

        protected DataReaderMode mDataReaderMode;

        protected bool mReadingAndStoringSpectra;

        protected bool mAbortProcessing;

        protected bool mParseFilesWithUnknownVersion;

        protected string mInputFilePath = string.Empty;

        protected FileStatsType mInputFileStats;

        /// <summary>
        /// Cached spectra
        /// </summary>
        /// <remarks>
        /// Used when mDataReaderMode is Cached
        /// </remarks>
        protected readonly List<SpectrumInfo> mCachedSpectra;

        // This dictionary maps scan number to index in mCachedSpectra()
        // If more than one spectrum comes from the same scan, tracks the first one read
        protected readonly Dictionary<int, int> mCachedSpectraScanToIndex = new();

        /// <summary>
        /// When mAutoShrinkDataLists is True, clsSpectrumInfo.MZList().Length and clsSpectrumInfo.IntensityList().Length will equal DataCount;
        /// When mAutoShrinkDataLists is False, the memory will not be freed when DataCount shrinks or clsSpectrumInfo.Clear() is called
        /// </summary>
        /// <remarks>
        /// Setting mAutoShrinkDataLists to False helps reduce slow, increased memory usage due to inefficient garbage collection
        /// (this is not much of an issue in 2016, and thus this parameter defaults to True)
        /// </remarks>
        [Obsolete("No longer applicable since MzList and IntensityList are now lists instead of arrays")]
        public bool AutoShrinkDataLists { get; set; }

        public virtual int CachedSpectrumCount => mDataReaderMode == DataReaderMode.Cached ? mCachedSpectra.Count : 0;

        /// <summary>
        /// First scan number
        /// </summary>
        public int CachedSpectraScanNumberMinimum => mInputFileStats.ScanNumberMinimum;

        /// <summary>
        /// Last scan number
        /// </summary>
        public int CachedSpectraScanNumberMaximum => mInputFileStats.ScanNumberMaximum;

        public double ChargeCarrierMass
        {
            get => mChargeCarrierMass;

            set => mChargeCarrierMass = value;
        }

        public string ErrorMessage => mErrorMessage ?? string.Empty;

        public string InputFilePath => mInputFilePath;

        public string FileVersion => mFileVersion;

        public virtual bool ParseFilesWithUnknownVersion
        {
            get => mParseFilesWithUnknownVersion;

            set => mParseFilesWithUnknownVersion = value;
        }

        public virtual string ProgressStepDescription => mProgressStepDescription;

        // ProgressPercentComplete ranges from 0 to 100, but can contain decimal percentage values
        public float ProgressPercentComplete => (float)Math.Round(mProgressPercentComplete, 2);

        protected bool ReadingAndStoringSpectra => mReadingAndStoringSpectra;

        /// <summary>
        /// Actual scan count if mDataReaderMode = Cached or mDataReaderMode = Indexed
        /// Scan count as reported by the XML file if mDataReaderMode = Sequential
        /// </summary>
        /// <remarks>
        /// <para>
        /// When reading mzXML and mzData files with the FileReader classes, this value is not populated until after the first scan is read
        /// </para>
        /// <para>
        /// When using the FileAccessor classes, this value is populated after the file is indexed
        /// </para>
        /// <para>
        /// For .MGF and .DtaText files, this value will always be 0
        /// </para>
        /// </remarks>
        public int ScanCount => mInputFileStats.ScanCount;

        public void AbortProcessingNow()
        {
            mAbortProcessing = true;
        }

        protected bool CBoolSafe(string value, bool defaultValue)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return defaultValue;
                }

                if (double.TryParse(value, out var numericValue))
                {
                    return !(Math.Abs(numericValue) < float.Epsilon);
                }

                if (bool.TryParse(value, out var boolValue))
                {
                    return boolValue;
                }

                return defaultValue;
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        protected double CDblSafe(string value, double defaultValue)
        {
            try
            {
                return double.Parse(value);
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        protected int CIntSafe(string value, int defaultValue)
        {
            try
            {
                return int.Parse(value);
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        protected float CSngSafe(string value, float defaultValue)
        {
            try
            {
                return float.Parse(value);
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        public abstract void CloseFile();

        public double ConvoluteMass(double massMZ, int currentCharge, int desiredCharge)
        {
            return ConvoluteMass(massMZ, currentCharge, desiredCharge, mChargeCarrierMass);
        }

        /// <summary>
        /// Converts massMZ to the MZ that would appear at the given desiredCharge
        /// </summary>
        /// <remarks>To return the neutral mass, set desiredCharge to 0</remarks>
        /// <param name="massMZ"></param>
        /// <param name="currentCharge"></param>
        /// <param name="desiredCharge"></param>
        /// <param name="chargeCarrierMass"></param>
        /// <returns>Converted m/z</returns>
        public static double ConvoluteMass(double massMZ, int currentCharge, int desiredCharge, double chargeCarrierMass)
        {
            double newMZ;

            if (currentCharge == desiredCharge)
            {
                newMZ = massMZ;
            }
            else
            {
                if (currentCharge == 1)
                {
                    newMZ = massMZ;
                }
                else if (currentCharge > 1)
                {
                    // Convert massMZ to M+H
                    newMZ = massMZ * currentCharge - chargeCarrierMass * (currentCharge - 1);
                }
                else if (currentCharge == 0)
                {
                    // Convert massMZ (which is neutral) to M+H and store in newMZ
                    newMZ = massMZ + chargeCarrierMass;
                }
                else
                {
                    // Negative charges are not supported; return 0
                    return 0d;
                }

                if (desiredCharge > 1)
                {
                    newMZ = (newMZ + chargeCarrierMass * (desiredCharge - 1)) / desiredCharge;
                }
                else if (desiredCharge == 1)
                {
                    // Return M+H, which is currently stored in newMZ
                }
                else if (desiredCharge == 0)
                {
                    // Return the neutral mass
                    newMZ -= chargeCarrierMass;
                }
                else
                {
                    // Negative charges are not supported; return 0
                    newMZ = 0d;
                }
            }

            return newMZ;
        }

        /// <summary>
        /// Determine the file type based on its extension
        /// </summary>
        /// <param name="fileNameOrPath"></param>
        /// <param name="fileType"></param>
        /// <returns>True if a known file type, otherwise false</returns>
        public static bool DetermineFileType(string fileNameOrPath, out DataFileType fileType)
        {
            bool knownType;
            fileType = DataFileType.Unknown;

            try
            {
                if (string.IsNullOrWhiteSpace(fileNameOrPath))
                {
                    return false;
                }

                var fileName = Path.GetFileName(fileNameOrPath);

                var fileExtension = Path.GetExtension(fileName).ToUpper();

                if (string.IsNullOrWhiteSpace(fileExtension))
                {
                    return false;
                }

                if (!fileExtension.StartsWith("."))
                {
                    fileExtension = '.' + fileExtension;
                }

                // Assume known file type for now
                knownType = true;

                switch (fileExtension)
                {
                    case MzDataFileReader.MZDATA_FILE_EXTENSION:
                        fileType = DataFileType.mzData;
                        break;

                    case MzXMLFileReader.MZXML_FILE_EXTENSION:
                        fileType = DataFileType.mzXML;
                        break;

                    case MgfFileReader.MGF_FILE_EXTENSION:
                        fileType = DataFileType.MGF;
                        break;

                    default:
                        // See if the filename ends with MZDATA_FILE_EXTENSION_XML or MZXML_FILE_EXTENSION_XML
                        if (fileName.EndsWith(MzDataFileReader.MZDATA_FILE_EXTENSION_XML, StringComparison.OrdinalIgnoreCase))
                        {
                            fileType = DataFileType.mzData;
                        }
                        else if (fileName.EndsWith(MzXMLFileReader.MZXML_FILE_EXTENSION_XML, StringComparison.OrdinalIgnoreCase))
                        {
                            fileType = DataFileType.mzXML;
                        }
                        else if (fileName.EndsWith(DtaTextFileReader.DTA_TEXT_FILE_EXTENSION, StringComparison.OrdinalIgnoreCase))
                        {
                            fileType = DataFileType.DtaText;
                        }
                        else
                        {
                            // Unknown file type
                            knownType = false;
                        }

                        break;
                }
            }
            catch (Exception)
            {
                knownType = false;
            }

            return knownType;
        }

        /// <summary>
        /// Obtain a forward-only reader for the given file
        /// </summary>
        /// <param name="fileNameOrPath"></param>
        /// <returns>An MS File reader, or null if an error or unknown file extension</returns>
        public static MsDataFileReaderBaseClass GetFileReaderBasedOnFileType(string fileNameOrPath)
        {
            MsDataFileReaderBaseClass fileReader = null;

            if (DetermineFileType(fileNameOrPath, out var fileType))
            {
                switch (fileType)
                {
                    case DataFileType.DtaText:
                        fileReader = new DtaTextFileReader();
                        break;

                    case DataFileType.MGF:
                        fileReader = new MgfFileReader();
                        break;

                    case DataFileType.mzData:
                        fileReader = new MzDataFileReader();
                        break;

                    case DataFileType.mzXML:
                        fileReader = new MzXMLFileReader();
                        break;

                    default:
                        break;
                        // Unknown file type
                }
            }

            return fileReader;
        }

        /// <summary>
        /// Obtain a random-access reader for the given file
        /// </summary>
        /// <remarks>
        /// Returns null if the file type is _dta.txt or .mgf since those file types do not have file accessors
        /// </remarks>
        /// <param name="fileNameOrPath"></param>
        /// <returns>An MS file accessor, or null if an error or unknown file extension</returns>
        public static MsDataFileAccessorBaseClass GetFileAccessorBasedOnFileType(string fileNameOrPath)
        {
            MsDataFileAccessorBaseClass fileAccessor = null;

            if (DetermineFileType(fileNameOrPath, out var fileType))
            {
                switch (fileType)
                {
                    case DataFileType.mzData:
                        fileAccessor = new MzDataFileAccessor();
                        break;

                    case DataFileType.mzXML:
                        fileAccessor = new MzXMLFileAccessor();
                        break;

                    // These file types do not have file accessors
                    case DataFileType.DtaText:
                    case DataFileType.MGF:
                        break;

                    default:
                        break;
                        // Unknown file type
                }
            }

            return fileAccessor;
        }

        protected abstract string GetInputFileLocation();

        /// <summary>
        /// Obtain the list of scan numbers (aka acquisition numbers)
        /// </summary>
        /// <param name="ScanNumberList"></param>
        /// <returns>True if successful, false if an error or no cached spectra</returns>
        public virtual bool GetScanNumberList(out int[] ScanNumberList)
        {
            try
            {
                if (mDataReaderMode == DataReaderMode.Cached)
                {
                    ScanNumberList = new int[mCachedSpectra.Count];
                    var indexEnd = ScanNumberList.Length - 1;

                    for (var spectrumIndex = 0; spectrumIndex <= indexEnd; spectrumIndex++)
                    {
                        ScanNumberList[spectrumIndex] = mCachedSpectra[spectrumIndex].ScanNumber;
                    }

                    return true;
                }

                ScanNumberList = Array.Empty<int>();
                return false;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in GetScanNumberList", ex);
                ScanNumberList = Array.Empty<int>();
                return false;
            }
        }

        /// <summary>
        /// Get the spectrum at the given index
        /// </summary>
        /// <remarks>
        /// Only valid if we have Cached data in memory
        /// </remarks>
        /// <param name="spectrumIndex"></param>
        /// <param name="spectrumInfo"></param>
        /// <returns>True if successful, false if an error</returns>
        public virtual bool GetSpectrumByIndex(int spectrumIndex, out SpectrumInfo spectrumInfo)
        {
            if (mDataReaderMode == DataReaderMode.Cached && mCachedSpectra.Count > 0)
            {
                if (spectrumIndex >= 0 && spectrumIndex < mCachedSpectra.Count && mCachedSpectra != null)
                {
                    spectrumInfo = mCachedSpectra[spectrumIndex];
                    return true;
                }

                mErrorMessage = "Invalid spectrum index: " + spectrumIndex;
                spectrumInfo = null;
            }
            else
            {
                mErrorMessage = "Cached data not in memory";
                spectrumInfo = null;
            }

            return false;
        }

        /// <summary>
        /// Get the spectrum for the given scan number by
        /// looking for the first entry in mCachedSpectra with .ScanNumber = scanNumber
        /// </summary>
        /// <remarks>
        /// Only valid if we have Cached data in memory
        /// </remarks>
        /// <param name="scanNumber"></param>
        /// <param name="spectrumInfo"></param>
        /// <returns>True if successful, false if an error or invalid scan number</returns>
        public virtual bool GetSpectrumByScanNumber(int scanNumber, out SpectrumInfo spectrumInfo)
        {
            spectrumInfo = null;

            try
            {
                mErrorMessage = string.Empty;

                if (mDataReaderMode == DataReaderMode.Cached)
                {
                    if (mCachedSpectraScanToIndex.Count == 0)
                    {
                        var indexEnd = mCachedSpectra.Count - 1;

                        for (var spectrumIndex = 0; spectrumIndex <= indexEnd; spectrumIndex++)
                        {
                            if (mCachedSpectra[spectrumIndex].ScanNumber == scanNumber)
                            {
                                spectrumInfo = mCachedSpectra[spectrumIndex];
                                return true;
                            }
                        }
                    }
                    else
                    {
                        var index = mCachedSpectraScanToIndex[scanNumber];
                        spectrumInfo = mCachedSpectra[index];
                        return true;
                    }

                    if (string.IsNullOrWhiteSpace(mErrorMessage))
                    {
                        mErrorMessage = "Invalid scan number: " + scanNumber;
                    }
                }
                else
                {
                    mErrorMessage = "Cached data not in memory";
                }

                return false;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in GetSpectrumByScanNumber", ex);
                return false;
            }
        }

        protected virtual void InitializeLocalVariables()
        {
            mChargeCarrierMass = CHARGE_CARRIER_MASS_MONOISOTOPIC;
            mErrorMessage = string.Empty;
            mFileVersion = string.Empty;
            mProgressStepDescription = string.Empty;
            mProgressPercentComplete = 0f;
            mCachedSpectra.Clear();

            mInputFileStats.ScanCount = 0;
            mInputFileStats.ScanNumberMinimum = 0;
            mInputFileStats.ScanNumberMaximum = 0;

            mCachedSpectraScanToIndex.Clear();

            mAbortProcessing = false;
        }

        public static bool IsNumber(string value)
        {
            try
            {
                return double.TryParse(value, out _);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ReSharper disable once UnusedMemberInSuper.Global
        public abstract bool OpenFile(string inputFilePath);

        public abstract bool OpenTextStream(string textStream);

        /// <summary>
        /// Validates that inputFilePath exists
        /// </summary>
        /// <param name="inputFilePath"></param>
        /// <returns>True if the file exists, otherwise false</returns>
        /// <remarks>Updates mFilePath if the file is valid</remarks>
        protected bool OpenFileInit(string inputFilePath)
        {
            // Make sure any open file or text stream is closed
            CloseFile();

            if (string.IsNullOrEmpty(inputFilePath))
            {
                mErrorMessage = "Error opening file: input file path is blank";
                return false;
            }

            if (!File.Exists(inputFilePath))
            {
                mErrorMessage = "File not found: " + inputFilePath;
                return false;
            }

            mInputFilePath = inputFilePath;
            return true;
        }

        protected void OperationComplete()
        {
            ProgressComplete?.Invoke();
        }

        public abstract bool ReadNextSpectrum(out SpectrumInfo spectrumInfo);

        /// <summary>
        /// Cache the entire file in memory
        /// </summary>
        /// <returns>True if successful, false if an error</returns>
        public virtual bool ReadAndCacheEntireFile()
        {
            try
            {
                mDataReaderMode = DataReaderMode.Cached;
                mReadingAndStoringSpectra = true;
                ResetProgress();

                while (ReadNextSpectrum(out var spectrumInfo) && !mAbortProcessing)
                {
                    if (spectrumInfo == null)
                        continue;

                    mCachedSpectra.Add(spectrumInfo);

                    if (!mCachedSpectraScanToIndex.ContainsKey(spectrumInfo.ScanNumber))
                    {
                        mCachedSpectraScanToIndex.Add(spectrumInfo.ScanNumber, mCachedSpectra.Count - 1);
                    }

                    mInputFileStats.ScanCount = mCachedSpectra.Count;
                    var scanNumber = spectrumInfo.ScanNumber;

                    if (mInputFileStats.ScanCount == 1)
                    {
                        mInputFileStats.ScanNumberMaximum = scanNumber;
                        mInputFileStats.ScanNumberMinimum = scanNumber;
                    }
                    else
                    {
                        if (scanNumber < mInputFileStats.ScanNumberMinimum)
                        {
                            mInputFileStats.ScanNumberMinimum = scanNumber;
                        }

                        if (scanNumber > mInputFileStats.ScanNumberMaximum)
                        {
                            mInputFileStats.ScanNumberMaximum = scanNumber;
                        }
                    }
                }

                if (!mAbortProcessing)
                {
                    OperationComplete();
                }

                return true;
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

        protected void ResetProgress()
        {
            ProgressReset?.Invoke();
        }

        protected void ResetProgress(string progressStepDescription)
        {
            UpdateProgress(progressStepDescription, 0f);
            ProgressReset?.Invoke();
        }

        protected void UpdateFileStats(int scanNumber)
        {
            UpdateFileStats(mInputFileStats.ScanCount + 1, scanNumber);
        }

        protected void UpdateFileStats(int scanCount, int scanNumber)
        {
            mInputFileStats.ScanCount = scanCount;

            if (scanCount <= 1)
            {
                mInputFileStats.ScanNumberMinimum = scanNumber;
                mInputFileStats.ScanNumberMaximum = scanNumber;
            }
            else
            {
                if (scanNumber < mInputFileStats.ScanNumberMinimum)
                {
                    mInputFileStats.ScanNumberMinimum = scanNumber;
                }

                if (scanNumber > mInputFileStats.ScanNumberMaximum)
                {
                    mInputFileStats.ScanNumberMaximum = scanNumber;
                }
            }
        }

        public void UpdateProgressDescription(string progressStepDescription)
        {
            mProgressStepDescription = progressStepDescription;
        }

        protected void UpdateProgress(string progressStepDescription)
        {
            UpdateProgress(progressStepDescription, mProgressPercentComplete);
        }

        protected void UpdateProgress(double percentComplete)
        {
            UpdateProgress(ProgressStepDescription, (float)percentComplete);
        }

        protected void UpdateProgress(float percentComplete)
        {
            UpdateProgress(ProgressStepDescription, percentComplete);
        }

        protected void UpdateProgress(string progressStepDescription, float percentComplete)
        {
            mProgressStepDescription = progressStepDescription;

            if (percentComplete < 0f)
            {
                percentComplete = 0f;
            }
            else if (percentComplete > 100f)
            {
                percentComplete = 100f;
            }

            mProgressPercentComplete = percentComplete;
            ProgressChanged?.Invoke(ProgressStepDescription, ProgressPercentComplete);
        }
    }
}