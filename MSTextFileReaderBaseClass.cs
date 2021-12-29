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

namespace MSDataFileReader
{
    /// <summary>
    /// This is the base class for the DTA and MGF file readers
    /// </summary>
    public abstract class clsMSTextFileReaderBaseClass : clsMSDataFileReaderBaseClass
    {
        // Ignore Spelling: Da, deconvoluted

        protected clsMSTextFileReaderBaseClass()
        {
            InitializeLocalVariables();
        }

        ~clsMSTextFileReaderBaseClass()
        {
            try
            {
                mFileReader?.Close();
            }
            catch (Exception ex)
            {
                // Ignore errors here
            }
        }

        private readonly Regex mDtaHeaderScanAndCharge = new Regex(@".+\.(\d+)\.(\d+)\.(\d*)$", RegexOptions.Compiled);

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

        protected clsSpectrumInfoMsMsText mCurrentSpectrum;

        protected List<string> mCurrentMsMsDataList;

        // When true, read the data and populate mCurrentMsMsDataList but do not populate mCurrentSpectrum.MZList() or mCurrentSpectrum.IntensityList()

        protected long mTotalBytesRead;

        protected long mInFileStreamLength;

        public char CommentLineStartChar { get; set; } = '=';

        public clsSpectrumInfoMsMsText CurrentSpectrum => mCurrentSpectrum;

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
                if (value < 0f || value > 100f)
                    value = 25f;
                mThresholdIonPctForDoubleCharge = value;
            }
        }

        /// <summary>
        /// Remove any instance of strCommentChar from the beginning and end of strCommentIn
        /// </summary>
        /// <param name="strCommentIn"></param>
        /// <param name="strCommentChar"></param>
        /// <param name="blnRemoveQuoteMarks">When True, also look for double quotation marks at the beginning and end</param>
        /// <returns>Updated comment</returns>
        protected string CleanupComment(string strCommentIn, char strCommentChar, bool blnRemoveQuoteMarks)
        {
            if (strCommentIn is null)
            {
                strCommentIn = string.Empty;
            }
            else
            {
                strCommentIn = strCommentIn.TrimStart(strCommentChar).Trim();
                strCommentIn = strCommentIn.TrimEnd(strCommentChar).Trim();

                if (blnRemoveQuoteMarks)
                {
                    strCommentIn = strCommentIn.TrimStart('"');
                    strCommentIn = strCommentIn.TrimEnd('"');
                }

                strCommentIn = strCommentIn.Trim();
            }

            return strCommentIn;
        }

        protected void AddNewRecentFileText(string strNewText, bool blnNewSpectrum = false, bool blnAddCrLfIfNeeded = true)
        {
            if (blnNewSpectrum)
            {
                mSecondMostRecentSpectrumFileText = mMostRecentSpectrumFileText.ToString();
                mMostRecentSpectrumFileText.Length = 0;
            }

            if (blnAddCrLfIfNeeded)
            {
                if (!(strNewText.EndsWith("\r") || strNewText.EndsWith("\n")))
                {
                    strNewText += Environment.NewLine;
                }
            }

            mMostRecentSpectrumFileText.Append(strNewText);
        }

        public override void CloseFile()
        {
            try
            {
                mFileReader?.Close();

                mInFileLineNumber = 0;
                mInputFilePath = string.Empty;
            }
            catch (Exception ex)
            {
                // Ignore errors here
            }
        }

        private void ComputePercentageDataAboveThreshold(clsSpectrumInfoMsMsText objSpectrumInfo, out float sngPctByCount, out float sngPctByIntensity)
        {
            ComputePercentageDataAboveThreshold(objSpectrumInfo.DataCount, objSpectrumInfo.MZList, objSpectrumInfo.IntensityList, objSpectrumInfo.ParentIonMZ, out sngPctByCount, out sngPctByIntensity);
        }

        protected void ComputePercentageDataAboveThreshold(int intDataCount, double[] dblMZList, float[] sngIntensityList, double dblThresholdMZ, out float sngPctByCount, out float sngPctByIntensity)
        {
            var intCountAboveThreshold = 0;
            var dblIntensitySumAboveThreshold = 0d;
            var dblTotalIntensitySum = 0d;
            var intIndexEnd = intDataCount - 1;

            for (var intIndex = 0; intIndex <= intIndexEnd; intIndex++)
            {
                dblTotalIntensitySum += sngIntensityList[intIndex];

                if (dblMZList[intIndex] > dblThresholdMZ)
                {
                    intCountAboveThreshold++;
                    dblIntensitySumAboveThreshold += sngIntensityList[intIndex];
                }
            }

            if (intDataCount == 0)
            {
                sngPctByCount = 0f;
                sngPctByIntensity = 0f;
            }
            else
            {
                sngPctByCount = intCountAboveThreshold / (float)intDataCount * 100.0f;
                sngPctByIntensity = (float)(dblIntensitySumAboveThreshold / dblTotalIntensitySum * 100.0d);
            }
        }

        public bool ExtractScanInfoFromDtaHeader(string strSpectrumHeader, out int intScanNumberStart, out int intScanNumberEnd, out int intScanCount)
        {
            return ExtractScanInfoFromDtaHeader(strSpectrumHeader, out intScanNumberStart, out intScanNumberEnd, out intScanCount, out _);
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
        /// <param name="strSpectrumHeader"></param>
        /// <param name="intScanNumberStart"></param>
        /// <param name="intScanNumberEnd"></param>
        /// <param name="intScanCount"></param>
        /// <param name="intCharge"></param>
        /// <returns>True if the scan numbers are found in the header</returns>
        public bool ExtractScanInfoFromDtaHeader(string strSpectrumHeader, out int intScanNumberStart, out int intScanNumberEnd, out int intScanCount, out int intCharge)
        {
            var blnScanNumberFound = false;
            intScanNumberStart = 0;
            intScanNumberEnd = 0;
            intScanCount = 0;
            intCharge = 0;

            try
            {
                if (strSpectrumHeader != null)
                {
                    strSpectrumHeader = strSpectrumHeader.Trim();

                    if (strSpectrumHeader.EndsWith(".dta", StringComparison.OrdinalIgnoreCase))
                    {
                        // Remove the trailing .dta
                        strSpectrumHeader = strSpectrumHeader.Substring(0, strSpectrumHeader.Length - 4);
                    }

                    // Extract the scans and charge using a RegEx
                    var reMatch = mDtaHeaderScanAndCharge.Match(strSpectrumHeader);

                    if (reMatch.Success)
                    {
                        if (int.TryParse(reMatch.Groups[1].Value, out intScanNumberStart))
                        {
                            if (int.TryParse(reMatch.Groups[2].Value, out intScanNumberEnd))
                            {
                                if (intScanNumberEnd > intScanNumberStart)
                                {
                                    intScanCount = intScanNumberEnd - intScanNumberStart + 1;
                                }
                                else
                                {
                                    intScanCount = 1;
                                }

                                blnScanNumberFound = true;

                                // Also try to parse out the charge
                                int.TryParse(reMatch.Groups[3].Value, out intCharge);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ExtractScanInfoFromDtaHeader", ex);
            }

            return blnScanNumberFound;
        }

        protected override string GetInputFileLocation()
        {
            return "Line " + mInFileLineNumber.ToString();
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
        /// <param name="objSpectrumInfo"></param>
        /// <param name="blnAddToExistingChargeList"></param>
        /// <param name="blnForceChargeAdditionFor2and3Plus"></param>
        public void GuesstimateCharge(clsSpectrumInfoMsMsText objSpectrumInfo, bool blnAddToExistingChargeList = false, bool blnForceChargeAdditionFor2and3Plus = false)
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

            if (objSpectrumInfo.DataCount <= 0 || objSpectrumInfo.MZList is null)
            {
                // This shouldn't happen, but we'll handle it anyway
                objSpectrumInfo.AddOrUpdateChargeList(1, false);
            }

            // Test 1: See if all m/z values are less than the parent ion m/z
            // Assume the data in .IonList() is sorted by ascending m/z

            else if (objSpectrumInfo.MZList[objSpectrumInfo.DataCount - 1] <= objSpectrumInfo.ParentIonMZ)
            {
                // Yes, all data is less than the parent ion m/z
                objSpectrumInfo.AddOrUpdateChargeList(1, blnAddToExistingChargeList);
            }
            else
            {
                // Find percentage of data with m/z values greater than the Parent Ion m/z
                // Compute this number using both raw data point counts and sum of intensity values
                ComputePercentageDataAboveThreshold(objSpectrumInfo, out var sngPctByCount, out var sngPctByIntensity);

                if (sngPctByCount < mThresholdIonPctForSingleCharge && sngPctByIntensity < mThresholdIonPctForSingleCharge)
                {
                    // Both percentages are less than the threshold for definitively single charge
                    objSpectrumInfo.AddOrUpdateChargeList(1, blnAddToExistingChargeList);
                }
                else
                {
                    int intChargeStart;

                    if (sngPctByCount >= mThresholdIonPctForDoubleCharge && sngPctByIntensity >= mThresholdIonPctForDoubleCharge)
                    {
                        // Both percentages are above the threshold for definitively double charge (or higher)
                        intChargeStart = 2;
                    }
                    else
                    {
                        intChargeStart = 1;
                    }

                    var intChargeEnd = 3;

                    // Determine whether intChargeEnd should be higher than 3+
                    do
                    {
                        var dblParentIonMH = ConvoluteMass(objSpectrumInfo.ParentIonMZ, intChargeEnd, 1);

                        if (dblParentIonMH < objSpectrumInfo.MZList[objSpectrumInfo.DataCount - 1] + 3d)
                        {
                            intChargeEnd++;
                        }
                        else
                        {
                            break;
                        }
                    }
                    while (intChargeEnd < clsSpectrumInfoMsMsText.MAX_CHARGE_COUNT);

                    if (blnAddToExistingChargeList)
                    {
                        if (!blnForceChargeAdditionFor2and3Plus && intChargeStart == 2 && intChargeEnd == 3)
                        {
                            // See if objSpectrumInfo already contains a single entry and it is 2+ or 3+
                            // If so, do not alter the charge list

                            if (objSpectrumInfo.ParentIonChargeCount == 1)
                            {
                                if (objSpectrumInfo.ParentIonCharges[0] == 2 || objSpectrumInfo.ParentIonCharges[0] == 3)
                                {
                                    // The following will guarantee that the For intChargeIndex loop doesn't run
                                    intChargeStart = 0;
                                    intChargeEnd = -1;
                                }
                            }
                        }
                    }
                    else
                    {
                        objSpectrumInfo.ParentIonChargeCount = 0;
                    }

                    var intIndexEnd = intChargeEnd - intChargeStart;

                    for (var intChargeIndex = 0; intChargeIndex <= intIndexEnd; intChargeIndex++)
                    {
                        objSpectrumInfo.AddOrUpdateChargeList(intChargeStart + intChargeIndex, true);
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
        /// <param name="strInputFilePath"></param>
        /// <returns>True if successful, false if an error</returns>
        public override bool OpenFile(string strInputFilePath)
        {
            try
            {
                var success = OpenFileInit(strInputFilePath);

                if (!success)
                    return false;

                var objStreamReader = new StreamReader(new FileStream(strInputFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                mInFileStreamLength = objStreamReader.BaseStream.Length;
                mFileReader = objStreamReader;
                InitializeLocalVariables();
                ResetProgress("Parsing " + Path.GetFileName(strInputFilePath));
                return true;
            }
            catch (Exception ex)
            {
                mErrorMessage = "Error opening file: " + strInputFilePath + "; " + ex.Message;
                OnErrorEvent(mErrorMessage, ex);
                return false;
            }
        }

        /// <summary>
        /// Open a text stream
        /// </summary>
        /// <param name="strTextStream"></param>
        /// <returns>True if successful, false if an error</returns>
        public override bool OpenTextStream(string strTextStream)
        {
            // Make sure any open file or text stream is closed
            CloseFile();

            try
            {
                mInputFilePath = "TextStream";
                mFileReader = new StringReader(strTextStream);
                mInFileStreamLength = strTextStream.Length;
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

        public int ParseMsMsDataList(string[] strMSMSData, int intMsMsDataCount, out double[] dblMasses, out float[] sngIntensities, bool blnShrinkDataArrays)
        {
            var lstMSMSData = new List<string>();
            var intIndexEnd = intMsMsDataCount - 1;

            for (var intIndex = 0; intIndex <= intIndexEnd; intIndex++)
            {
                lstMSMSData.Add(strMSMSData[intIndex]);
            }

            return ParseMsMsDataList(lstMSMSData, out dblMasses, out sngIntensities, blnShrinkDataArrays);
        }

        /// <summary>
        /// Parse a space or tab separated list of list of mass and intensity pairs
        /// </summary>
        /// <param name="lstMSMSData"></param>
        /// <param name="dblMasses"></param>
        /// <param name="sngIntensities"></param>
        /// <param name="blnShrinkDataArrays">If true and any invalid lines were encountered, shrink the arrays</param>
        /// <returns>The number of data points in the output arrays</returns>
        public int ParseMsMsDataList(List<string> lstMSMSData, out double[] dblMasses, out float[] sngIntensities, bool blnShrinkDataArrays)
        {
            int intDataCount;
            var strSepChars = new[] { ' ', '\t' };

            if (lstMSMSData != null && lstMSMSData.Count > 0)
            {
                dblMasses = new double[lstMSMSData.Count];
                sngIntensities = new float[lstMSMSData.Count];
                intDataCount = 0;

                foreach (var strItem in lstMSMSData)
                {
                    // Each line in strMSMSData should contain a mass and intensity pair, separated by a space or Tab
                    // MGF files sometimes contain a third number, the charge of the ion
                    // Use .Split() to parse the numbers in the line to extract the mass and intensity, and ignore the charge (if present)
                    var strSplitLine = strItem.Split(strSepChars);

                    if (strSplitLine.Length >= 2)
                    {
                        if (IsNumber(strSplitLine[0]) && IsNumber(strSplitLine[1]))
                        {
                            dblMasses[intDataCount] = double.Parse(strSplitLine[0]);
                            sngIntensities[intDataCount] = float.Parse(strSplitLine[1]);
                            intDataCount++;
                        }
                    }
                }

                if (intDataCount <= 0)
                {
                    dblMasses = new double[1];
                    sngIntensities = new float[1];
                }
                else if (intDataCount != lstMSMSData.Count && blnShrinkDataArrays)
                {
                    Array.Resize(ref dblMasses, intDataCount);
                    Array.Resize(ref sngIntensities, intDataCount);
                }
            }
            else
            {
                intDataCount = 0;
                dblMasses = new double[1];
                sngIntensities = new float[1];
            }

            return intDataCount;
        }

        protected void UpdateStreamReaderProgress()
        {
            if (mFileReader is StreamReader objStreamReader)
            {
                UpdateProgress(objStreamReader.BaseStream.Position / (double)objStreamReader.BaseStream.Length * 100.0d);
            }
            else if (mInFileStreamLength > 0L)
            {
                UpdateProgress(mTotalBytesRead / (double)mInFileStreamLength * 100.0d);
            }
        }
    }
}