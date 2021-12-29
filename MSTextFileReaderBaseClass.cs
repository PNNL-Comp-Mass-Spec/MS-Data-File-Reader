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
using System.Text.RegularExpressions;

// ReSharper disable UnusedMember.Global

namespace MSDataFileReader
{
    /// <summary>
    /// This is the base class for the DTA and MGF file readers
    /// </summary>
    public abstract class MsTextFileReaderBaseClass : MsDataFileReaderBaseClass
    {
        // Ignore Spelling: Da, deconvoluted

        protected MsTextFileReaderBaseClass()
        {
            // ReSharper disable once VirtualMemberCallInConstructor
            InitializeLocalVariables();
        }

        ~MsTextFileReaderBaseClass()
        {
            try
            {
                mFileReader?.Close();
            }
            catch (Exception)
            {
                // Ignore errors here
            }
        }

        private readonly Regex mDtaHeaderScanAndCharge = new(@".+\.(\d+)\.(\d+)\.(\d*)$", RegexOptions.Compiled);

        /// <summary>
        /// Threshold ion percent to assume a 1+ spectrum (number between 0 and 100)
        /// </summary>
        /// <remarks>
        /// If the percentage of ions greater than the parent ion m/z is less than this number, the charge is definitely 1+
        /// </remarks>
        protected float mThresholdIonPctForSingleCharge;

        /// <summary>
        /// Threshold ion percent to assume a 2+ spectrum (number between 0 and 100)
        /// </summary>
        /// <remarks>
        /// If the percentage of ions greater than the parent ion m/z is greater than this number, the charge is definitely 2+ or higher
        /// </remarks>
        protected float mThresholdIonPctForDoubleCharge;

        protected TextReader mFileReader;

        private string mSecondMostRecentSpectrumFileText;

        private System.Text.StringBuilder mMostRecentSpectrumFileText;

        protected int mInFileLineNumber;

        protected SpectrumInfoMsMsText mCurrentSpectrum;

        protected List<string> mCurrentMsMsDataList;

        // When true, read the data and populate mCurrentMsMsDataList but do not populate mCurrentSpectrum.MZList() or mCurrentSpectrum.IntensityList()

        protected long mTotalBytesRead;

        protected long mInFileStreamLength;

        public char CommentLineStartChar { get; set; } = '=';

        public SpectrumInfoMsMsText CurrentSpectrum => mCurrentSpectrum;

        public bool ReadTextDataOnly { get; set; }

        public float ThresholdIonPctForSingleCharge
        {
            get => mThresholdIonPctForSingleCharge;

            set
            {
                if (value < 0f || value > 100f)
                    value = 10f;
                mThresholdIonPctForSingleCharge = value;
            }
        }

        public float ThresholdIonPctForDoubleCharge
        {
            get => mThresholdIonPctForDoubleCharge;

            set
            {
                if (value is < 0f or > 100f)
                    value = 25f;

                mThresholdIonPctForDoubleCharge = value;
            }
        }

        /// <summary>
        /// Remove any instance of commentChar from the beginning and end of commentIn
        /// </summary>
        /// <param name="commentIn"></param>
        /// <param name="commentChar"></param>
        /// <param name="removeQuoteMarks">When True, also look for double quotation marks at the beginning and end</param>
        /// <returns>Updated comment</returns>
        protected string CleanupComment(string commentIn, char commentChar, bool removeQuoteMarks)
        {
            if (string.IsNullOrWhiteSpace(commentIn))
            {
                return string.Empty;
            }

            commentIn = commentIn.TrimStart(commentChar).Trim();
            commentIn = commentIn.TrimEnd(commentChar).Trim();

            if (removeQuoteMarks)
            {
                commentIn = commentIn.TrimStart('"');
                commentIn = commentIn.TrimEnd('"');
            }

            return commentIn.Trim();
        }

        protected void AddNewRecentFileText(string newText, bool newSpectrum = false, bool addCrLfIfNeeded = true)
        {
            if (newSpectrum)
            {
                mSecondMostRecentSpectrumFileText = mMostRecentSpectrumFileText.ToString();
                mMostRecentSpectrumFileText.Length = 0;
            }

            if (addCrLfIfNeeded)
            {
                if (!(newText.EndsWith("\r") || newText.EndsWith("\n")))
                {
                    newText += Environment.NewLine;
                }
            }

            mMostRecentSpectrumFileText.Append(newText);
        }

        public override void CloseFile()
        {
            try
            {
                mFileReader?.Close();

                mInFileLineNumber = 0;
                mInputFilePath = string.Empty;
            }
            catch (Exception)
            {
                // Ignore errors here
            }
        }

        private void ComputePercentageDataAboveThreshold(SpectrumInfo spectrumInfo, out float pctByCount, out float pctByIntensity)
        {
            ComputePercentageDataAboveThreshold(spectrumInfo.DataCount, spectrumInfo.MZList, spectrumInfo.IntensityList, spectrumInfo.ParentIonMZ, out pctByCount, out pctByIntensity);
        }

        protected void ComputePercentageDataAboveThreshold(int dataCount, double[] mzList, float[] intensityList, double thresholdMZ, out float pctByCount, out float pctByIntensity)
        {
            var countAboveThreshold = 0;
            var intensitySumAboveThreshold = 0d;
            var totalIntensitySum = 0d;
            var indexEnd = dataCount - 1;

            for (var index = 0; index <= indexEnd; index++)
            {
                totalIntensitySum += intensityList[index];

                if (mzList[index] > thresholdMZ)
                {
                    countAboveThreshold++;
                    intensitySumAboveThreshold += intensityList[index];
                }
            }

            if (dataCount == 0)
            {
                pctByCount = 0f;
                pctByIntensity = 0f;
            }
            else
            {
                pctByCount = countAboveThreshold / (float)dataCount * 100.0f;
                pctByIntensity = (float)(intensitySumAboveThreshold / totalIntensitySum * 100.0d);
            }
        }

        public bool ExtractScanInfoFromDtaHeader(string spectrumHeader, out int scanNumberStart, out int scanNumberEnd, out int scanCount)
        {
            return ExtractScanInfoFromDtaHeader(spectrumHeader, out scanNumberStart, out scanNumberEnd, out scanCount, out _);
        }

        /// <summary>
        /// Extract Scan Info from DTA header
        /// </summary>
        /// <remarks>
        /// The header should be similar to one of the following
        /// FileName.1234.1234.2.dta
        /// FileName.1234.1234.2      (StartScan.EndScan.Charge)
        /// FileName.1234.1234.       (ProteoWizard uses this format to indicate unknown charge)
        /// </remarks>
        /// <param name="spectrumHeader"></param>
        /// <param name="scanNumberStart"></param>
        /// <param name="scanNumberEnd"></param>
        /// <param name="scanCount"></param>
        /// <param name="charge"></param>
        /// <returns>True if the scan numbers are found in the header</returns>
        public bool ExtractScanInfoFromDtaHeader(string spectrumHeader, out int scanNumberStart, out int scanNumberEnd, out int scanCount, out int charge)
        {
            var scanNumberFound = false;
            scanNumberStart = 0;
            scanNumberEnd = 0;
            scanCount = 0;
            charge = 0;

            try
            {
                if (spectrumHeader != null)
                {
                    spectrumHeader = spectrumHeader.Trim();

                    if (spectrumHeader.EndsWith(".dta", StringComparison.OrdinalIgnoreCase))
                    {
                        // Remove the trailing .dta
                        spectrumHeader = spectrumHeader.Substring(0, spectrumHeader.Length - 4);
                    }

                    // Extract the scans and charge using a RegEx
                    var reMatch = mDtaHeaderScanAndCharge.Match(spectrumHeader);

                    if (reMatch.Success &&
                        int.TryParse(reMatch.Groups[1].Value, out scanNumberStart) &&
                        int.TryParse(reMatch.Groups[2].Value, out scanNumberEnd))
                    {
                        if (scanNumberEnd > scanNumberStart)
                        {
                            scanCount = scanNumberEnd - scanNumberStart + 1;
                        }
                        else
                        {
                            scanCount = 1;
                        }

                        scanNumberFound = true;

                        // Also try to parse out the charge
                        int.TryParse(reMatch.Groups[3].Value, out charge);
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ExtractScanInfoFromDtaHeader", ex);
            }

            return scanNumberFound;
        }

        protected override string GetInputFileLocation()
        {
            return "Line " + mInFileLineNumber;
        }

        public List<string> GetMSMSDataAsText()
        {
            return mCurrentMsMsDataList ?? new List<string>();
        }

        public string GetMostRecentSpectrumFileText()
        {
            return mMostRecentSpectrumFileText is null ? string.Empty : mMostRecentSpectrumFileText.ToString();
        }

        public string GetSecondMostRecentSpectrumFileText()
        {
            return mSecondMostRecentSpectrumFileText;
        }

        public string GetSpectrumTitle()
        {
            return mCurrentSpectrum.SpectrumTitle;
        }

        public string GetSpectrumTitleWithCommentChars()
        {
            return mCurrentSpectrum.SpectrumTitleWithCommentChars;
        }

        /// <summary>
        /// Guesstimate the parent ion charge based on its m/z and the ions in the fragmentation spectrum
        /// </summary>
        /// <param name="spectrumInfo"></param>
        /// <param name="addToExistingChargeList"></param>
        /// <param name="forceChargeAdditionFor2and3Plus"></param>
        public void GuesstimateCharge(SpectrumInfoMsMsText spectrumInfo, bool addToExistingChargeList = false, bool forceChargeAdditionFor2and3Plus = false)
        {
            // Strategy:
            // 1) If all fragmentation ions have m/z values less than the parent ion m/z, definitely assume a 1+ parent ion
            //
            // 2) If less than mThresholdIonPctForSingleCharge percent of the data's m/z values are greater
            // than the parent ion, definitely assume 1+ parent ion
            // When determining percentage, use both # of data points and the sum of the ion intensities.
            // Both values must be less than mThresholdIonPctForSingleCharge percent to declare 1+
            //
            // 3) If mThresholdIonPctForSingleCharge percent to mThresholdIonPctForDoubleCharge percent
            // of the data's m/z values are greater than the parent ion, declare 1+, 2+, 3+ ...
            // up to the charge that gives a deconvoluted parent ion that matches the above test (#2)
            // At a minimum, include 2+ and 3+
            // Allow up to 5+
            // Allow a 3 Da mass tolerance when comparing deconvoluted mass to maximum ion mass
            // E.g. if parent ion m/z is 476, but fragmentation data ranges from 624 to 1922, guess 2+ to 5+
            // Math involved: 476*2-1 = 951:  this is less than 1922
            // 476*3-2 = 1426: this is less than 1922
            // 476*4-3 = 1902: this is less than 1922
            // 476*5-4 = 2376: this is greater than 1922
            // Thus, assign charges 2+ to 5+
            //
            // 4) Otherwise, if both test 2 and test 3 fail, assume 2+, 3+, ... up to the charge that
            // gives a deconvoluted parent ion that matches the above test (#2)
            // The same tests as outlined in step 3 will be performed to determine the maximum charge
            // to assign

            // Example, for parent ion at 700 m/z and following data, decide 1+, 2+, 3+ since percent above 700 m/z is 21%
            // m/z		Intensity
            // 300		10
            // 325		15
            // 400		40
            // 450		20
            // 470		30
            // 520		15
            // 580		50
            // 650		40
            // 720		10
            // 760		30
            // 820		5
            // 830		15
            // Sum all:	280
            // Sum below 700:	220
            // Sum above 700:	60
            // % above 700 by intensity sum:	21%
            // % above 700 by data point count:	33%

            if (spectrumInfo.DataCount <= 0 || spectrumInfo.MZList is null)
            {
                // This shouldn't happen, but we'll handle it anyway
                spectrumInfo.AddOrUpdateChargeList(1, false);
            }

            // Test 1: See if all m/z values are less than the parent ion m/z
            // Assume the data in .IonList() is sorted by ascending m/z

            else if (spectrumInfo.MZList[spectrumInfo.DataCount - 1] <= spectrumInfo.ParentIonMZ)
            {
                // Yes, all data is less than the parent ion m/z
                spectrumInfo.AddOrUpdateChargeList(1, addToExistingChargeList);
            }
            else
            {
                // Find percentage of data with m/z values greater than the Parent Ion m/z
                // Compute this number using both raw data point counts and sum of intensity values
                ComputePercentageDataAboveThreshold(spectrumInfo, out var pctByCount, out var pctByIntensity);

                if (pctByCount < mThresholdIonPctForSingleCharge && pctByIntensity < mThresholdIonPctForSingleCharge)
                {
                    // Both percentages are less than the threshold for definitively single charge
                    spectrumInfo.AddOrUpdateChargeList(1, addToExistingChargeList);
                }
                else
                {
                    int chargeStart;

                    if (pctByCount >= mThresholdIonPctForDoubleCharge && pctByIntensity >= mThresholdIonPctForDoubleCharge)
                    {
                        // Both percentages are above the threshold for definitively double charge (or higher)
                        chargeStart = 2;
                    }
                    else
                    {
                        chargeStart = 1;
                    }

                    var chargeEnd = 3;

                    // Determine whether chargeEnd should be higher than 3+
                    do
                    {
                        var parentIonMH = ConvoluteMass(spectrumInfo.ParentIonMZ, chargeEnd, 1);

                        if (parentIonMH < spectrumInfo.MZList[spectrumInfo.DataCount - 1] + 3d)
                        {
                            chargeEnd++;
                        }
                        else
                        {
                            break;
                        }
                    }
                    while (chargeEnd < SpectrumInfoMsMsText.MAX_CHARGE_COUNT);

                    if (addToExistingChargeList)
                    {
                        if (!forceChargeAdditionFor2and3Plus && chargeStart == 2 && chargeEnd == 3)
                        {
                            // See if spectrumInfo already contains a single entry and it is 2+ or 3+
                            // If so, do not alter the charge list

                            if (spectrumInfo.ParentIonChargeCount == 1)
                            {
                                if (spectrumInfo.ParentIonCharges[0] == 2 || spectrumInfo.ParentIonCharges[0] == 3)
                                {
                                    // The following will guarantee that the For chargeIndex loop doesn't run
                                    chargeStart = 0;
                                    chargeEnd = -1;
                                }
                            }
                        }
                    }
                    else
                    {
                        spectrumInfo.ParentIonChargeCount = 0;
                    }

                    var indexEnd = chargeEnd - chargeStart;

                    for (var chargeIndex = 0; chargeIndex <= indexEnd; chargeIndex++)
                    {
                        spectrumInfo.AddOrUpdateChargeList(chargeStart + chargeIndex, true);
                    }
                }
            }
        }

        protected override void InitializeLocalVariables()
        {
            base.InitializeLocalVariables();
            mTotalBytesRead = 0L;
            mThresholdIonPctForSingleCharge = 10f;    // Percentage
            mThresholdIonPctForDoubleCharge = 25f;    // Percentage
            mMostRecentSpectrumFileText = new System.Text.StringBuilder();
            mSecondMostRecentSpectrumFileText = string.Empty;
            mInFileLineNumber = 0;
            mCurrentMsMsDataList = new List<string>();
        }

        /// <summary>
        /// Open a data file
        /// </summary>
        /// <param name="inputFilePath"></param>
        /// <returns>True if successful, false if an error</returns>
        public override bool OpenFile(string inputFilePath)
        {
            try
            {
                var success = OpenFileInit(inputFilePath);

                if (!success)
                    return false;

                var streamReader = new StreamReader(new FileStream(inputFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                mInFileStreamLength = streamReader.BaseStream.Length;
                mFileReader = streamReader;
                InitializeLocalVariables();
                ResetProgress("Parsing " + Path.GetFileName(inputFilePath));
                return true;
            }
            catch (Exception ex)
            {
                mErrorMessage = "Error opening file: " + inputFilePath + "; " + ex.Message;
                OnErrorEvent(mErrorMessage, ex);
                return false;
            }
        }

        /// <summary>
        /// Open a text stream
        /// </summary>
        /// <param name="textStream"></param>
        /// <returns>True if successful, false if an error</returns>
        public override bool OpenTextStream(string textStream)
        {
            // Make sure any open file or text stream is closed
            CloseFile();

            try
            {
                mInputFilePath = "TextStream";
                mFileReader = new StringReader(textStream);
                mInFileStreamLength = textStream.Length;
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

        public int ParseMsMsDataList(string[] msmsDataArray, int msmsDataCount, out double[] masses, out float[] intensities, bool shrinkDataArrays)
        {
            var msmsData = new List<string>();
            var indexEnd = msmsDataCount - 1;

            for (var index = 0; index <= indexEnd; index++)
            {
                msmsData.Add(msmsDataArray[index]);
            }

            return ParseMsMsDataList(msmsData, out masses, out intensities, shrinkDataArrays);
        }

        /// <summary>
        /// Parse a space or tab separated list of list of mass and intensity pairs
        /// </summary>
        /// <param name="msmsData"></param>
        /// <param name="masses"></param>
        /// <param name="intensities"></param>
        /// <param name="shrinkDataArrays">If true and any invalid lines were encountered, shrink the arrays</param>
        /// <returns>The number of data points in the output arrays</returns>
        [Obsolete("Use the method that returns two lists")]
        public int ParseMsMsDataList(List<string> msmsData, out double[] masses, out float[] intensities, bool shrinkDataArrays)
        {
            var dataCount = ParseMsMsDataList(msmsData, out var massList, out var intensityList);

            if (dataCount == 0)
            {
                masses = Array.Empty<double>();
                intensities = Array.Empty<float>();
                return 0;
            }

            masses = massList.ToArray();
            intensities = intensityList.ToArray();
            return dataCount;
        }

        /// <summary>
        /// Parse a space or tab separated list of list of mass and intensity pairs
        /// </summary>
        /// <param name="msmsData"></param>
        /// <param name="masses"></param>
        /// <param name="intensities"></param>
        /// <returns>The number of data points in the output lists</returns>
        public int ParseMsMsDataList(List<string> msmsData, out List<double> masses, out List<float> intensities)
        {
            masses = new List<double>();
            intensities = new List<float>();

            // ReSharper disable once MergeIntoPattern
            if (msmsData == null || msmsData.Count == 0)
                return 0;

            var sepChars = new[] { ' ', '\t' };

            foreach (var item in msmsData)
            {
                // Each line in msmsData should contain a mass and intensity pair, separated by a space or Tab
                // MGF files sometimes contain a third number, the charge of the ion
                // Use .Split() to parse the numbers in the line to extract the mass and intensity, and ignore the charge (if present)
                var splitLine = item.Split(sepChars);

                if (splitLine.Length < 2)
                    continue;

                if (!double.TryParse(splitLine[0], out var mz) || !float.TryParse(splitLine[1], out var intensity))
                    continue;

                masses.Add(mz);
                intensities.Add(intensity);
            }

            return masses.Count;
        }

        protected void UpdateStreamReaderProgress()
        {
            if (mFileReader is StreamReader streamReader)
            {
                UpdateProgress(streamReader.BaseStream.Position / (double)streamReader.BaseStream.Length * 100.0d);
            }
            else if (mInFileStreamLength > 0L)
            {
                UpdateProgress(mTotalBytesRead / (double)mInFileStreamLength * 100.0d);
            }
        }
    }
}