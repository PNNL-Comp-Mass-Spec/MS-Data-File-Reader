using System;

// This is the base class for the DTA and MGF file readers
//
// -------------------------------------------------------------------------------
// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
// Copyright 2006, Battelle Memorial Institute.  All Rights Reserved.
// Started March 26, 2006
//
// E-mail: matthew.monroe@pnl.gov or proteomics@pnnl.gov
// Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://panomics.pnnl.gov/ or https://www.pnnl.gov/integrative-omics
// -------------------------------------------------------------------------------
//

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MSDataFileReader
{
    public abstract class clsMSTextFileReaderBaseClass : clsMSDataFileReaderBaseClass
    {
        public clsMSTextFileReaderBaseClass()
        {
            InitializeLocalVariables();
        }

        ~clsMSTextFileReaderBaseClass()
        {
            try
            {
                if (mFileReader is object)
                {
                    mFileReader.Close();
                }
            }
            catch (Exception ex)
            {
            }
        }

        #region Constants and Enums

        #endregion

        #region Structures

        #endregion

        #region Classwide Variables

        // Number between 0 and 100; if the percentage of ions greater than the parent ion m/z is less than this number, then the charge is definitely 1+
        protected float mThresholdIonPctForSingleCharge;

        // Number between 0 and 100; if the percentage of ions greater than the parent ion m/z is greater than this number, then the charge is definitely 2+ or higher
        protected float mThresholdIonPctForDoubleCharge;
        protected System.IO.TextReader mFileReader;
        protected char mCommentLineStartChar = '=';
        private string mSecondMostRecentSpectrumFileText;
        private System.Text.StringBuilder mMostRecentSpectrumFileText;
        protected int mInFileLineNumber;
        protected clsSpectrumInfoMsMsText mCurrentSpectrum;
        protected List<string> mCurrentMsMsDataList;

        // When true, read the data and populate mCurrentMsMsDataList but do not populate mCurrentSpectrum.MZList() or mCurrentSpectrum.IntensityList()
        protected bool mReadTextDataOnly;
        protected long mTotalBytesRead;
        protected long mInFileStreamLength;

        #endregion

        #region Processing Options and Interface Functions

        public char CommentLineStartChar
        {
            get
            {
                return mCommentLineStartChar;
            }

            set
            {
                mCommentLineStartChar = value;
            }
        }

        public clsSpectrumInfoMsMsText CurrentSpectrum
        {
            get
            {
                return mCurrentSpectrum;
            }
        }

        public bool ReadTextDataOnly
        {
            get
            {
                return mReadTextDataOnly;
            }

            set
            {
                mReadTextDataOnly = value;
            }
        }

        public float ThresholdIonPctForSingleCharge
        {
            get
            {
                return mThresholdIonPctForSingleCharge;
            }

            set
            {
                if (value < 0f | value > 100f)
                    value = 10f;
                mThresholdIonPctForSingleCharge = value;
            }
        }

        public float ThresholdIonPctForDoubleCharge
        {
            get
            {
                return mThresholdIonPctForDoubleCharge;
            }

            set
            {
                if (value < 0f | value > 100f)
                    value = 25f;
                mThresholdIonPctForDoubleCharge = value;
            }
        }

        #endregion

        /// <summary>
    /// Remove any instance of strCommentChar from the beginning and end of strCommentIn
    /// </summary>
    /// <param name="strCommentIn"></param>
    /// <param name="strCommentChar"></param>
    /// <param name="blnRemoveQuoteMarks">When True, also look for double quotation marks at the beginning and end</param>
    /// <returns></returns>
        protected string CleanupComment(string strCommentIn, char strCommentChar, bool blnRemoveQuoteMarks)
        {

            // Extract out the comment
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
                    strCommentIn = strCommentIn.TrimStart(ControlChars.Quote);
                    strCommentIn = strCommentIn.TrimEnd(ControlChars.Quote);
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
                if (!(strNewText.EndsWith(Conversions.ToString(ControlChars.Cr)) | strNewText.EndsWith(Conversions.ToString(ControlChars.Lf))))
                {
                    strNewText += ControlChars.NewLine;
                }
            }

            mMostRecentSpectrumFileText.Append(strNewText);
        }

        public override void CloseFile()
        {
            try
            {
                if (mFileReader is object)
                {
                    mFileReader.Close();
                }

                mInFileLineNumber = 0;
                mInputFilePath = string.Empty;
            }
            catch (Exception ex)
            {
            }
        }

        private void ComputePercentageDataAboveThreshold(clsSpectrumInfoMsMsText objSpectrumInfo, out float sngPctByCount, out float sngPctByIntensity)
        {
            ComputePercentageDataAboveThreshold(objSpectrumInfo.DataCount, objSpectrumInfo.MZList, objSpectrumInfo.IntensityList, objSpectrumInfo.ParentIonMZ, out sngPctByCount, out sngPctByIntensity);
        }

        protected void ComputePercentageDataAboveThreshold(int intDataCount, double[] dblMZList, float[] sngIntensityList, double dblThresholdMZ, out float sngPctByCount, out float sngPctByIntensity)
        {
            int intIndex;
            int intCountAboveThreshold = 0;
            double dblIntensitySumAboveThreshold = 0d;
            double dblTotalIntensitySum = 0d;
            var loopTo = intDataCount - 1;
            for (intIndex = 0; intIndex <= loopTo; intIndex++)
            {
                dblTotalIntensitySum += sngIntensityList[intIndex];
                if (dblMZList[intIndex] > dblThresholdMZ)
                {
                    intCountAboveThreshold += 1;
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
            int argintCharge = 0;
            return ExtractScanInfoFromDtaHeader(strSpectrumHeader, out intScanNumberStart, out intScanNumberEnd, out intScanCount, out argintCharge);
        }

        // The header should be similar to one of the following
        // FileName.1234.1234.2.dta
        // FileName.1234.1234.2      (StartScan.EndScan.Charge)
        // FileName.1234.1234.       (Proteowizard uses this format to indicate unknown charge)
        // Returns True if the scan numbers are found in the header

        // ReSharper disable once UseImplicitlyTypedVariableEvident
        public bool ExtractScanInfoFromDtaHeader(string strSpectrumHeader, out int intScanNumberStart, out int intScanNumberEnd, out int intScanCount, out int intCharge)
        {
            ;
#error Cannot convert LocalDeclarationStatementSyntax - see comment for details
            /* Cannot convert LocalDeclarationStatementSyntax, System.NotSupportedException: StaticKeyword not supported!
               at ICSharpCode.CodeConverter.CSharp.SyntaxKindExtensions.ConvertToken(SyntaxKind t, TokenContext context)
               at ICSharpCode.CodeConverter.CSharp.CommonConversions.ConvertModifier(SyntaxToken m, TokenContext context)
               at ICSharpCode.CodeConverter.CSharp.CommonConversions.<ConvertModifiersCore>d__49.MoveNext()
               at System.Linq.Enumerable.<ConcatIterator>d__59`1.MoveNext()
               at System.Linq.Enumerable.WhereEnumerableIterator`1.MoveNext()
               at System.Linq.Buffer`1..ctor(IEnumerable`1 source)
               at System.Linq.OrderedEnumerable`1.<GetEnumerator>d__1.MoveNext()
               at Microsoft.CodeAnalysis.SyntaxTokenList.CreateNode(IEnumerable`1 tokens)
               at ICSharpCode.CodeConverter.CSharp.CommonConversions.ConvertModifiers(SyntaxNode node, IReadOnlyCollection`1 modifiers, TokenContext context, Boolean isVariableOrConst, SyntaxKind[] extraCsModifierKinds)
               at ICSharpCode.CodeConverter.CSharp.MethodBodyExecutableStatementVisitor.<VisitLocalDeclarationStatement>d__31.MoveNext()
            --- End of stack trace from previous location where exception was thrown ---
               at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
               at ICSharpCode.CodeConverter.CSharp.HoistedNodeStateVisitor.<AddLocalVariablesAsync>d__6.MoveNext()
            --- End of stack trace from previous location where exception was thrown ---
               at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
               at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.<DefaultVisitInnerAsync>d__3.MoveNext()

            Input:

                    ' The header should be similar to one of the following
                    '   FileName.1234.1234.2.dta
                    '   FileName.1234.1234.2      (StartScan.EndScan.Charge)
                    '   FileName.1234.1234.       (Proteowizard uses this format to indicate unknown charge)
                    ' Returns True if the scan numbers are found in the header

                    ' ReSharper disable once UseImplicitlyTypedVariableEvident
                    Static reDtaHeaderScanAndCharge As Global.System.Text.RegularExpressions.Regex = New Global.System.Text.RegularExpressions.Regex(".+\.(\d+)\.(\d+)\.(\d*)$", Global.System.Text.RegularExpressions.RegexOptions.Compiled)

             */
            var blnScanNumberFound = default(bool);
            Match reMatch;
            intScanNumberStart = 0;
            intScanNumberEnd = 0;
            intScanCount = 0;
            intCharge = 0;
            try
            {
                blnScanNumberFound = false;
                if (strSpectrumHeader is object)
                {
                    strSpectrumHeader = strSpectrumHeader.Trim();
                    if (strSpectrumHeader.ToLower().EndsWith(".dta"))
                    {
                        // Remove the trailing .dta
                        strSpectrumHeader = strSpectrumHeader.Substring(0, strSpectrumHeader.Length - 4);
                    }

                    // Extract the scans and charge using a RegEx
                    reMatch = reDtaHeaderScanAndCharge.Match(strSpectrumHeader);
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
            if (mCurrentMsMsDataList is null)
            {
                return new List<string>();
            }
            else
            {
                return mCurrentMsMsDataList;
            }
        }

        public string GetMostRecentSpectrumFileText()
        {
            if (mMostRecentSpectrumFileText is null)
            {
                return string.Empty;
            }
            else
            {
                return mMostRecentSpectrumFileText.ToString();
            }
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

        public void GuesstimateCharge(clsSpectrumInfoMsMsText objSpectrumInfo, bool blnAddToExistingChargeList = false, bool blnForceChargeAddnFor2and3Plus = false)
        {

            // Guesstimate the parent ion charge based on its m/z and the ions in the fragmentation spectrum
            // 
            // Strategy:
            // 1) If all frag peaks have m/z values less than the parent ion m/z, then definitely assume a
            // 1+ parent ion
            // 
            // 2) If less than mThresholdIonPctForSingleCharge percent of the data's m/z values are greater
            // than the parent ion, then definitely assume 1+ parent ion
            // When determining percentage, use both # of data points and the sum of the ion intensities.
            // Both values must be less than mThresholdIonPctForSingleCharge percent to declare 1+
            // 
            // 3) If mThresholdIonPctForSingleCharge percent to mThresholdIonPctForDoubleCharge percent
            // of the data's m/z values are greater than the parent ion, then declare 1+, 2+, 3+ ...
            // up to the charge that gives a deconvoluted parent ion that matches the above test (#2)
            // At a minimum, include 2+ and 3+
            // Allow up to 5+
            // Allow a 3 Da mass tolerance when comparing deconvoluted mass to maximum ion mass
            // E.g. if parent ion m/z is 476, but frag data ranges from 624 to 1922, then guess 2+ to 5+
            // Math involved: 476*2-1 = 951:  this is less than 1922
            // 476*3-2 = 1426: this is less than 1922
            // 476*4-3 = 1902: this is less than 1922
            // 476*5-4 = 2376: this is greater than 1922
            // Thus, assign charges 2+ to 5+
            // 
            // 4) Otherwise, if both test 2 and test 3 fail, then assume 2+, 3+, ... up to the charge that
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

            float sngPctByCount;
            float sngPctByIntensity;
            int intChargeStart;
            int intChargeEnd;
            int intChargeIndex;
            double dblParentIonMH;
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
                ComputePercentageDataAboveThreshold(objSpectrumInfo, out sngPctByCount, out sngPctByIntensity);
                if (sngPctByCount < mThresholdIonPctForSingleCharge & sngPctByIntensity < mThresholdIonPctForSingleCharge)
                {
                    // Both percentages are less than the threshold for definitively single charge
                    objSpectrumInfo.AddOrUpdateChargeList(1, blnAddToExistingChargeList);
                }
                else
                {
                    if (sngPctByCount >= mThresholdIonPctForDoubleCharge & sngPctByIntensity >= mThresholdIonPctForDoubleCharge)
                    {
                        // Both percentages are above the threshold for definitively double charge (or higher)
                        intChargeStart = 2;
                    }
                    else
                    {
                        intChargeStart = 1;
                    }

                    intChargeEnd = 3;

                    // Determine whether intChargeEnd should be higher than 3+
                    do
                    {
                        dblParentIonMH = ConvoluteMass(objSpectrumInfo.ParentIonMZ, intChargeEnd, 1);
                        if (dblParentIonMH < objSpectrumInfo.MZList[objSpectrumInfo.DataCount - 1] + 3d)
                        {
                            intChargeEnd += 1;
                        }
                        else
                        {
                            break;
                        }
                    }
                    while (intChargeEnd < clsSpectrumInfoMsMsText.MAX_CHARGE_COUNT);
                    if (blnAddToExistingChargeList)
                    {
                        if (!blnForceChargeAddnFor2and3Plus & intChargeStart == 2 & intChargeEnd == 3)
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

                    var loopTo = intChargeEnd - intChargeStart;
                    for (intChargeIndex = 0; intChargeIndex <= loopTo; intChargeIndex++)
                        objSpectrumInfo.AddOrUpdateChargeList(intChargeStart + intChargeIndex, true);
                }
            }
        }

        protected override void InitializeLocalVariables()
        {
            base.InitializeLocalVariables();
            mTotalBytesRead = 0L;
            mThresholdIonPctForSingleCharge = 10f;    // Percentage
            mThresholdIonPctForDoubleCharge = 25f;    // Percentage
            mMostRecentSpectrumFileText = new System.Text.StringBuilder() { Length = 0 };
            mSecondMostRecentSpectrumFileText = string.Empty;
            mInFileLineNumber = 0;
            mCurrentMsMsDataList = new List<string>();
        }

        public override bool OpenFile(string strInputFilePath)
        {
            // Returns true if the file is successfully opened

            bool blnSuccess;
            System.IO.StreamReader objStreamReader;
            try
            {
                blnSuccess = OpenFileInit(strInputFilePath);
                if (!blnSuccess)
                    return false;
                objStreamReader = new System.IO.StreamReader(new System.IO.FileStream(strInputFilePath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite));
                mInFileStreamLength = objStreamReader.BaseStream.Length;
                mFileReader = objStreamReader;
                InitializeLocalVariables();
                ResetProgress("Parsing " + System.IO.Path.GetFileName(strInputFilePath));
                blnSuccess = true;
            }
            catch (Exception ex)
            {
                mErrorMessage = "Error opening file: " + strInputFilePath + "; " + ex.Message;
                blnSuccess = false;
            }

            return blnSuccess;
        }

        public override bool OpenTextStream(string strTextStream)
        {
            // Returns true if the text stream is successfully opened

            bool blnSuccess;

            // Make sure any open file or text stream is closed
            CloseFile();
            try
            {
                mInputFilePath = "TextStream";
                mFileReader = new System.IO.StringReader(strTextStream);
                mInFileStreamLength = strTextStream.Length;
                InitializeLocalVariables();
                ResetProgress("Parsing text stream");
                blnSuccess = true;
            }
            catch (Exception ex)
            {
                mErrorMessage = "Error opening text stream";
                blnSuccess = false;
            }

            return blnSuccess;
        }

        public int ParseMsMsDataList(string[] strMSMSData, int intMsMsDataCount, out double[] dblMasses, out float[] sngIntensities, bool blnShrinkDataArrays)
        {
            var lstMSMSData = new List<string>();
            for (int intIndex = 0, loopTo = intMsMsDataCount - 1; intIndex <= loopTo; intIndex++)
                lstMSMSData.Add(strMSMSData[intIndex]);
            return ParseMsMsDataList(lstMSMSData, out dblMasses, out sngIntensities, blnShrinkDataArrays);
        }

        public int ParseMsMsDataList(List<string> lstMSMSData, out double[] dblMasses, out float[] sngIntensities, bool blnShrinkDataArrays)
        {

            // Returns the number of data points in dblMasses() and sngIntensities()
            // If blnShrinkDataArrays = False, then will not shrink dblMasses or sngIntensities

            string[] strSplitLine;
            int intDataCount;
            var strSepChars = new char[] { ' ', ControlChars.Tab };
            if (lstMSMSData is object && lstMSMSData.Count > 0)
            {
                dblMasses = new double[lstMSMSData.Count];
                sngIntensities = new float[lstMSMSData.Count];
                intDataCount = 0;
                foreach (string strItem in lstMSMSData)
                {

                    // Each line in strMSMSData should contain a mass and intensity pair, separated by a space or Tab
                    // MGF files sometimes contain a third number, the charge of the ion
                    // Use the .Split function to parse the numbers in the line to extract the mass and intensity, and ignore the charge (if present)
                    strSplitLine = strItem.Split(strSepChars);
                    if (strSplitLine.Length >= 2)
                    {
                        if (IsNumber(strSplitLine[0]) & IsNumber(strSplitLine[1]))
                        {
                            dblMasses[intDataCount] = Conversions.ToDouble(strSplitLine[0]);
                            sngIntensities[intDataCount] = Conversions.ToSingle(strSplitLine[1]);
                            intDataCount += 1;
                        }
                    }
                }

                if (intDataCount <= 0)
                {
                    dblMasses = new double[1];
                    sngIntensities = new float[1];
                }
                else if (intDataCount != lstMSMSData.Count & blnShrinkDataArrays)
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
            System.IO.StreamReader objStreamReader = mFileReader as System.IO.StreamReader;
            if (objStreamReader is object)
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