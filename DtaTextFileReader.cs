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
using System.Runtime.InteropServices;

/// <summary>
/// This class can be used to open a _Dta.txt file and return each spectrum present
/// </summary>
namespace MSDataFileReader
{
    public class clsDtaTextFileReader : clsMSTextFileReaderBaseClass
    {
        public clsDtaTextFileReader() : this(true)
        {
        }

        public clsDtaTextFileReader(bool blnCombineIdenticalSpectra)
        {
            mCombineIdenticalSpectra = blnCombineIdenticalSpectra;
            InitializeLocalVariables();
        }

        #region Constants and Enums
        // Note: The extension must be in all caps
        public const string DTA_TEXT_FILE_EXTENSION = "_DTA.TXT";
        private const char COMMENT_LINE_START_CHAR = '=';        // The comment character is an Equals sign

        #endregion

        #region Classwide Variables

        private bool mCombineIdenticalSpectra;

        // mHeaderSaved is used to store the previous header title; it is needed when the next
        // header was read for comparison with the current scan, but it didn't match, and thus
        // wasn't used for grouping
        private string mHeaderSaved;

        #endregion

        #region Processing Options and Interface Functions

        public bool CombineIdenticalSpectra
        {
            get
            {
                return mCombineIdenticalSpectra;
            }

            set
            {
                mCombineIdenticalSpectra = value;
            }
        }

        #endregion

        protected override void InitializeLocalVariables()
        {
            base.InitializeLocalVariables();
            mCommentLineStartChar = COMMENT_LINE_START_CHAR;
            mHeaderSaved = string.Empty;
        }

        /// <summary>
        /// Read the next spectrum from a _dta.txt file
        /// </summary>
        /// <remarks>
        /// If mCombineIdenticalSpectra is true, combines spectra that have the same scan number but different charge state
        /// </remarks>
        /// <param name="objSpectrumInfo"></param>
        /// <returns>True if a spectrum is found, otherwise false</returns>
        public override bool ReadNextSpectrum(out clsSpectrumInfo objSpectrumInfo)
        {
            var strMostRecentLineIn = string.Empty;
            var blnSpectrumFound = default(bool);

            try
            {
                if (ReadingAndStoringSpectra || mCurrentSpectrum is null)
                {
                    mCurrentSpectrum = new clsSpectrumInfoMsMsText();
                }
                else
                {
                    mCurrentSpectrum.Clear();
                }

                mCurrentSpectrum.AutoShrinkDataLists = AutoShrinkDataLists;
                blnSpectrumFound = false;
                if (mFileReader is null)
                {
                    objSpectrumInfo = new clsSpectrumInfo();
                    mErrorMessage = "Data file not currently open";
                }
                else
                {
                    AddNewRecentFileText(string.Empty, true, false);
                    var intLastProgressUpdateLine = mInFileLineNumber;

                    while (!blnSpectrumFound && mFileReader.Peek() > -1 && !mAbortProcessing)
                    {
                        string strLineIn;
                        if (mHeaderSaved.Length > 0)
                        {
                            strLineIn = string.Copy(mHeaderSaved);
                            mHeaderSaved = string.Empty;
                        }
                        else
                        {
                            strLineIn = mFileReader.ReadLine();
                            if (strLineIn != null)
                                mTotalBytesRead += strLineIn.Length + 2;
                            mInFileLineNumber += 1;
                        }

                        // See if strLineIn is nothing or starts with the comment line character (equals sign)
                        if (strLineIn != null && strLineIn.Trim().StartsWith(mCommentLineStartChar.ToString()))
                        {
                            AddNewRecentFileText(strLineIn);
                            {
                                ref var withBlock = ref mCurrentSpectrum;
                                withBlock.SpectrumTitleWithCommentChars = strLineIn;
                                withBlock.SpectrumTitle = CleanupComment(strLineIn, mCommentLineStartChar, true);
                                var argintScanNumberStart = withBlock.ScanNumber;
                                var argintScanNumberEnd = withBlock.ScanNumberEnd;
                                var argintScanCount = withBlock.ScanCount;
                                ExtractScanInfoFromDtaHeader(withBlock.SpectrumTitle, out argintScanNumberStart, out argintScanNumberEnd, out argintScanCount);
                                withBlock.ScanNumber = argintScanNumberStart;
                                withBlock.ScanNumberEnd = argintScanNumberEnd;
                                withBlock.ScanCount = argintScanCount;
                                withBlock.MSLevel = 2;
                                withBlock.SpectrumID = withBlock.ScanNumber;
                            }

                            // Read the next line, which should have the parent ion MH value and charge
                            if (mFileReader.Peek() > -1)
                            {
                                strLineIn = mFileReader.ReadLine();
                            }
                            else
                            {
                                strLineIn = string.Empty;
                            }

                            if (strLineIn != null)
                                mTotalBytesRead += strLineIn.Length + 2;

                            mInFileLineNumber += 1;
                            if (string.IsNullOrWhiteSpace(strLineIn))
                            {
                            }
                            // Spectrum header is not followed by a parent ion value and charge; ignore the line
                            else
                            {
                                AddNewRecentFileText(strLineIn);

                                // Parse the parent ion info and read the MsMs Data
                                blnSpectrumFound = ReadSingleSpectrum(mFileReader, strLineIn, out mCurrentMsMsDataList, mCurrentSpectrum, ref mInFileLineNumber, ref intLastProgressUpdateLine, ref strMostRecentLineIn);
                                if (blnSpectrumFound)
                                {
                                    if (mReadTextDataOnly)
                                    {
                                        // Do not parse the text data to populate .MZList and .IntensityList
                                        mCurrentSpectrum.DataCount = 0;
                                    }
                                    else
                                    {
                                        {
                                            ref var withBlock1 = ref mCurrentSpectrum;

                                            try
                                            {
                                                withBlock1.DataCount = ParseMsMsDataList(mCurrentMsMsDataList, out withBlock1.MZList, out withBlock1.IntensityList, withBlock1.AutoShrinkDataLists);
                                                withBlock1.Validate(blnComputeBasePeakAndTIC: true, blnUpdateMZRange: true);
                                            }
                                            catch (Exception ex)
                                            {
                                                withBlock1.DataCount = 0;
                                                blnSpectrumFound = false;
                                            }
                                        }
                                    }
                                }

                                if (blnSpectrumFound && mCombineIdenticalSpectra && mCurrentSpectrum.ParentIonCharges[0] == 2)
                                {
                                    // See if the next spectrum is the identical data, but the charge is 3 (this is a common situation with .dta files prepared by Lcq_Dta)

                                    strLineIn = string.Copy(strMostRecentLineIn);
                                    if (string.IsNullOrWhiteSpace(strLineIn) && mFileReader.Peek() > -1)
                                    {
                                        // Read the next line
                                        strLineIn = mFileReader.ReadLine();
                                        if (strLineIn != null)
                                            mTotalBytesRead += strLineIn.Length + 2;

                                        mInFileLineNumber += 1;
                                    }

                                    if (strLineIn != null && strLineIn.StartsWith(mCommentLineStartChar.ToString()))
                                    {
                                        mHeaderSaved = string.Copy(strLineIn);
                                        var strCompareTitle = CleanupComment(mHeaderSaved, mCommentLineStartChar, true);
                                        if (strCompareTitle.ToLower().EndsWith("3.dta"))
                                        {
                                            if (string.Equals(mCurrentSpectrum.SpectrumTitle.Substring(0, mCurrentSpectrum.SpectrumTitle.Length - 5), strCompareTitle.Substring(0, strCompareTitle.Length - 5), StringComparison.InvariantCultureIgnoreCase))
                                            {
                                                // Yes, the spectra match

                                                {
                                                    ref var withBlock2 = ref mCurrentSpectrum;
                                                    withBlock2.ParentIonChargeCount = 2;
                                                    withBlock2.ParentIonCharges[1] = 3;
                                                    withBlock2.ChargeIs2And3Plus = true;
                                                }

                                                mHeaderSaved = string.Empty;

                                                // Read the next set of lines until the next blank line or comment line is found
                                                while (mFileReader.Peek() > -1)
                                                {
                                                    strLineIn = mFileReader.ReadLine();
                                                    mInFileLineNumber += 1;

                                                    // See if strLineIn is blank or starts with an equals sign
                                                    if (strLineIn != null)
                                                    {
                                                        mTotalBytesRead += strLineIn.Length + 2;
                                                        if (strLineIn.Trim().Length == 0)
                                                        {
                                                            break;
                                                        }
                                                        else if (strLineIn.Trim().StartsWith(mCommentLineStartChar.ToString()))
                                                        {
                                                            mHeaderSaved = string.Copy(strLineIn);
                                                            break;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                else if (strMostRecentLineIn.StartsWith(mCommentLineStartChar.ToString()))
                                {
                                    mHeaderSaved = string.Copy(strMostRecentLineIn);
                                }
                            }  // EndIf for blnSpectrumFound = True
                        }  // EndIf for strLineIn.Trim.StartsWith(mCommentLineStartChar)

                        if (mInFileLineNumber - intLastProgressUpdateLine >= 250 || blnSpectrumFound)
                        {
                            intLastProgressUpdateLine = mInFileLineNumber;
                            UpdateStreamReaderProgress();
                        }
                    }

                    objSpectrumInfo = mCurrentSpectrum;
                    if (blnSpectrumFound && !ReadingAndStoringSpectra)
                    {
                        UpdateFileStats(objSpectrumInfo.ScanNumber);
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ReadNextSpectrum", ex);
                objSpectrumInfo = new clsSpectrumInfo();
            }

            return blnSpectrumFound;
        }

        /// <summary>
        /// Read a single .dta file
        /// </summary>
        /// <param name="strInputFilePath"></param>
        /// <param name="strMsMsDataList"></param>
        /// <param name="intMsMsDataCount"></param>
        /// <param name="objSpectrumInfoMsMsText"></param>
        /// <returns>True if the file was successfully opened and a spectrum was read</returns>
        public bool ReadSingleDtaFile(string strInputFilePath, out string[] strMsMsDataList, out int intMsMsDataCount, out clsSpectrumInfoMsMsText objSpectrumInfoMsMsText)
        {
            var blnSpectrumFound = default(bool);
            var lstMsMsDataList = new List<string>();
            intMsMsDataCount = 0;
            objSpectrumInfoMsMsText = new clsSpectrumInfoMsMsText();

            try
            {
                using (var fileReader = new StreamReader(strInputFilePath))
                {
                    mTotalBytesRead = 0L;
                    ResetProgress("Parsing " + Path.GetFileName(strInputFilePath));
                    mInFileLineNumber = 0;
                    var intLastProgressUpdateLine = mInFileLineNumber;

                    while (!fileReader.EndOfStream && !mAbortProcessing)
                    {
                        var strLineIn = fileReader.ReadLine();
                        mInFileLineNumber += 1;
                        if (strLineIn != null)
                            mTotalBytesRead += strLineIn.Length + 2;
                        if (!string.IsNullOrWhiteSpace(strLineIn))
                        {
                            if (char.IsDigit(strLineIn.Trim(), 0))
                            {
                                var argstrMostRecentLineIn = "";
                                blnSpectrumFound = ReadSingleSpectrum(fileReader, strLineIn, out lstMsMsDataList, objSpectrumInfoMsMsText, ref mInFileLineNumber, ref intLastProgressUpdateLine, strMostRecentLineIn: ref argstrMostRecentLineIn);
                                break;
                            }
                        }

                        if (mInFileLineNumber - intLastProgressUpdateLine >= 100)
                        {
                            intLastProgressUpdateLine = mInFileLineNumber;
                            // MyBase.UpdateProgress(srInFile.BaseStream.Position / srInFile.BaseStream.Length * 100.0)
                            UpdateProgress(mTotalBytesRead / (double)fileReader.BaseStream.Length * 100.0d);
                        }
                    }

                    if (blnSpectrumFound)
                    {
                        // Try to determine the scan numbers by parsing strInputFilePath
                        {
                            ref var withBlock = ref objSpectrumInfoMsMsText;
                            var argintScanNumberStart = withBlock.ScanNumber;
                            var argintScanNumberEnd = withBlock.ScanNumberEnd;
                            var argintScanCount = withBlock.ScanCount;
                            ExtractScanInfoFromDtaHeader(Path.GetFileName(strInputFilePath), out argintScanNumberStart, out argintScanNumberEnd, out argintScanCount);
                            withBlock.ScanNumber = argintScanNumberStart;
                            withBlock.ScanNumberEnd = argintScanNumberEnd;
                            withBlock.ScanCount = argintScanCount;
                        }

                        strMsMsDataList = new string[lstMsMsDataList.Count];
                        lstMsMsDataList.CopyTo(strMsMsDataList);
                    }
                    else
                    {
                        strMsMsDataList = new string[1];
                    }

                    if (!mAbortProcessing)
                    {
                        OperationComplete();
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ReadSingleDtaFile", ex);
                objSpectrumInfoMsMsText = new clsSpectrumInfoMsMsText();
                strMsMsDataList = new string[1];
            }

            return blnSpectrumFound;
        }

        /// <summary>
        /// Read a single mass spectrum
        /// </summary>
        /// <param name="srReader"></param>
        /// <param name="strParentIonLineText"></param>
        /// <param name="lstMsMsDataList"></param>
        /// <param name="objSpectrumInfoMsMsText"></param>
        /// <param name="intLinesRead"></param>
        /// <param name="intLastProgressUpdateLine"></param>
        /// <param name="strMostRecentLineIn"></param>
        /// <returns>if a valid spectrum is found, otherwise, false</returns>
        private bool ReadSingleSpectrum(TextReader srReader, string strParentIonLineText, out List<string> lstMsMsDataList, clsSpectrumInfoMsMsText objSpectrumInfoMsMsText, ref int intLinesRead, ref int intLastProgressUpdateLine, [Optional, DefaultParameterValue("")] ref string strMostRecentLineIn)
        {
            var blnSpectrumFound = default(bool);
            objSpectrumInfoMsMsText.ParentIonLineText = string.Copy(strParentIonLineText);
            strParentIonLineText = strParentIonLineText.Trim();

            // Look for the first space
            var intCharIndex = strParentIonLineText.IndexOf(' ');
            if (intCharIndex >= 1)
            {
                var strValue = strParentIonLineText.Substring(0, intCharIndex);

                if (double.TryParse(strValue, out var dblValue))
                {
                    objSpectrumInfoMsMsText.ParentIonMH = dblValue;
                    strValue = strParentIonLineText.Substring(intCharIndex + 1);

                    // See if strValue contains another space
                    intCharIndex = strValue.IndexOf(' ');
                    if (intCharIndex > 0)
                    {
                        strValue = strValue.Substring(0, intCharIndex);
                    }

                    if (int.TryParse(strValue, out var intCharge))
                    {
                        objSpectrumInfoMsMsText.ParentIonChargeCount = 1;
                        objSpectrumInfoMsMsText.ParentIonCharges[0] = intCharge;

                        // Note: Dta files have Parent Ion MH defined but not Parent Ion m/z
                        // Thus, compute .ParentIonMZ using .ParentIonMH
                        if (objSpectrumInfoMsMsText.ParentIonCharges[0] <= 1)
                        {
                            objSpectrumInfoMsMsText.ParentIonMZ = objSpectrumInfoMsMsText.ParentIonMH;
                            objSpectrumInfoMsMsText.ParentIonCharges[0] = 1;
                        }
                        else
                        {
                            objSpectrumInfoMsMsText.ParentIonMZ = ConvoluteMass(objSpectrumInfoMsMsText.ParentIonMH, 1, objSpectrumInfoMsMsText.ParentIonCharges[0]);
                        }

                        blnSpectrumFound = true;
                    }
                }
            }

            strMostRecentLineIn = string.Empty;
            lstMsMsDataList = new List<string>();
            if (blnSpectrumFound)
            {
                // Read all of the MS/MS spectrum ions up to the next blank line or up to the next line starting with COMMENT_LINE_START_CHAR

                while (srReader.Peek() > -1)
                {
                    var strLineIn = srReader.ReadLine();
                    intLinesRead += 1;

                    // See if strLineIn is blank
                    if (strLineIn != null)
                    {
                        mTotalBytesRead += strLineIn.Length + 2;
                        strMostRecentLineIn = string.Copy(strLineIn);
                        if (strLineIn.Trim().Length == 0 || strLineIn.StartsWith(COMMENT_LINE_START_CHAR.ToString()))
                        {
                            break;
                        }
                        else
                        {
                            // Add to MS/MS data string list
                            lstMsMsDataList.Add(strLineIn.Trim());
                            AddNewRecentFileText(strLineIn);
                        }
                    }

                    if (intLinesRead - intLastProgressUpdateLine >= 250)
                    {
                        intLastProgressUpdateLine = intLinesRead;
                        UpdateStreamReaderProgress();
                    }
                }
            }

            return blnSpectrumFound;
        }
    }
}