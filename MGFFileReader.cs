using System;

// This class can be used to open a Mascot Generic File (.MGF) and return each spectrum present
// 
// -------------------------------------------------------------------------------
// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
// Copyright 2005, Battelle Memorial Institute.  All Rights Reserved.
// Started November 15, 2003
// 
// E-mail: matthew.monroe@pnl.gov or proteomics@pnnl.gov
// Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://panomics.pnnl.gov/ or https://www.pnnl.gov/integrative-omics
// -------------------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;

namespace MSDataFileReader
{
    public class clsMGFFileReader : clsMSTextFileReaderBaseClass
    {
        public clsMGFFileReader()
        {
            InitializeLocalVariables();
        }

        #region Constants and Enums
        // Note: The extension must be in all caps
        public const string MGF_FILE_EXTENSION = ".MGF";
        private const char COMMENT_LINE_START_CHAR = '#';        // The comment character is an Equals sign
        private const string LINE_START_BEGIN_IONS = "BEGIN IONS";
        private const string LINE_START_END_IONS = "END IONS";
        private const string LINE_START_MSMS = "MSMS:";
        private const string LINE_START_PEPMASS = "PEPMASS=";
        private const string LINE_START_CHARGE = "CHARGE=";
        private const string LINE_START_TITLE = "TITLE=";
        private const string LINE_START_RT = "RTINSECONDS=";
        private const string LINE_START_SCANS = "SCANS=";

        #endregion

        #region Classwide Variables
        // mScanNumberStartSaved is used to create fake scan numbers when reading .MGF files that do not have
        // scan numbers defined using   ###MSMS: #1234   or   TITLE=Filename.1234.1234.2.dta  or  TITLE=Filename.1234.1234.2
        private int mScanNumberStartSaved;

        #endregion

        protected override void InitializeLocalVariables()
        {
            base.InitializeLocalVariables();
            mCommentLineStartChar = COMMENT_LINE_START_CHAR;
            mScanNumberStartSaved = 0;
        }

        protected override void LogErrors(string strCallingFunction, string strErrorDescription)
        {
            base.LogErrors("clsMGFFileReader." + strCallingFunction, strErrorDescription);
        }

        /// <summary>
    /// Parse out a scan number or scan number range from strData
    /// </summary>
    /// <param name="strData">Single integer or two integers separated by a dash</param>
    /// <param name="spectrumInfo"></param>
    /// <returns></returns>
        private bool ExtractScanRange(string strData, clsSpectrumInfo spectrumInfo)
        {
            bool scanNumberFound = false;
            int charIndex = strData.IndexOf('-');
            if (charIndex > 0)
            {
                // strData contains a dash, and thus a range of scans
                string strRemaining = strData.Substring(charIndex + 1).Trim();
                strData = strData.Substring(0, charIndex).Trim();
                if (IsNumber(strData))
                {
                    spectrumInfo.ScanNumber = Conversions.ToInteger(strData);
                    if (IsNumber(strRemaining))
                    {
                        if (spectrumInfo.ScanNumberEnd == 0)
                        {
                            spectrumInfo.ScanNumberEnd = Conversions.ToInteger(strRemaining);
                        }
                    }
                    else
                    {
                        spectrumInfo.ScanNumberEnd = spectrumInfo.ScanNumber;
                    }

                    scanNumberFound = true;
                }
            }
            else if (IsNumber(strData))
            {
                spectrumInfo.ScanNumber = Conversions.ToInteger(strData);
                if (spectrumInfo.ScanNumberEnd == 0)
                {
                    spectrumInfo.ScanNumberEnd = spectrumInfo.ScanNumber;
                }

                scanNumberFound = true;
            }

            if (!scanNumberFound)
            {
                return false;
            }

            mCurrentSpectrum.SpectrumID = mCurrentSpectrum.ScanNumber;
            if (spectrumInfo.ScanNumber == spectrumInfo.ScanNumberEnd || spectrumInfo.ScanNumber > spectrumInfo.ScanNumberEnd)
            {
                mCurrentSpectrum.ScanCount = 1;
            }
            else
            {
                mCurrentSpectrum.ScanCount = spectrumInfo.ScanNumberEnd - spectrumInfo.ScanNumber + 1;
            }

            return true;
        }

        public override bool ReadNextSpectrum(out clsSpectrumInfo objSpectrumInfo)
        {
            // Reads the next spectrum from a .MGF file
            // Returns True if a spectrum is found, otherwise, returns False

            string strLineIn;
            string strTemp;
            string[] strSplitLine;
            var strSepChars = new char[] { ' ', ControlChars.Tab };
            int intIndex;
            int charIndex;
            int intLastProgressUpdateLine;
            bool blnScanNumberFound;
            bool blnParentIonFound;
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
                blnScanNumberFound = false;

                // Initialize mCurrentMsMsDataList
                if (mCurrentMsMsDataList is null)
                {
                    mCurrentMsMsDataList = new List<string>();
                }
                else
                {
                    mCurrentMsMsDataList.Clear();
                }

                if (mFileReader is null)
                {
                    objSpectrumInfo = new clsSpectrumInfoMsMsText();
                    mErrorMessage = "Data file not currently open";
                }
                else
                {
                    AddNewRecentFileText(string.Empty, true, false);
                    {
                        ref var withBlock = ref mCurrentSpectrum;
                        withBlock.SpectrumTitleWithCommentChars = string.Empty;
                        withBlock.SpectrumTitle = string.Empty;
                        withBlock.MSLevel = 2;
                    }

                    intLastProgressUpdateLine = mInFileLineNumber;
                    while (!blnSpectrumFound && mFileReader.Peek() > -1 && !mAbortProcessing)
                    {
                        strLineIn = mFileReader.ReadLine();
                        if (strLineIn is object)
                            mTotalBytesRead += strLineIn.Length + 2;
                        mInFileLineNumber += 1;
                        if (strLineIn is object && strLineIn.Trim().Length > 0)
                        {
                            AddNewRecentFileText(strLineIn);
                            strLineIn = strLineIn.Trim();

                            // See if strLineIn starts with the comment line start character (a pound sign, #)
                            if (strLineIn.StartsWith(Conversions.ToString(mCommentLineStartChar)))
                            {
                                // Remove any comment characters at the start of strLineIn
                                strLineIn = strLineIn.TrimStart(mCommentLineStartChar).Trim();

                                // Look for LINE_START_MSMS in strLineIn
                                // This will be present in MGF files created using Agilent's DataAnalysis software
                                if (strLineIn.ToUpper().StartsWith(LINE_START_MSMS))
                                {
                                    strLineIn = strLineIn.Substring(LINE_START_MSMS.Length).Trim();

                                    // Initialize these values
                                    mCurrentSpectrum.ScanNumberEnd = 0;
                                    mCurrentSpectrum.ScanCount = 1;

                                    // Remove the # sign in front of the scan number
                                    strLineIn = strLineIn.TrimStart('#').Trim();

                                    // Look for the / sign and remove any text following it
                                    // For example,
                                    // ###MS: 4458/4486/
                                    // ###MSMS: 4459/4488/
                                    // The / sign is used to indicate that several MS/MS scans were combined to make the given spectrum; we'll just keep the first one
                                    charIndex = strLineIn.IndexOf('/');
                                    if (charIndex > 0)
                                    {
                                        if (charIndex < strLineIn.Length - 1)
                                        {
                                            strTemp = strLineIn.Substring(charIndex + 1).Trim();
                                        }
                                        else
                                        {
                                            strTemp = string.Empty;
                                        }

                                        strLineIn = strLineIn.Substring(0, charIndex).Trim();
                                        mCurrentSpectrum.ScanCount = 1;
                                        if (strTemp.Length > 0)
                                        {
                                            do
                                            {
                                                charIndex = strTemp.IndexOf('/');
                                                if (charIndex > 0)
                                                {
                                                    mCurrentSpectrum.ScanCount += 1;
                                                    if (charIndex < strTemp.Length - 1)
                                                    {
                                                        strTemp = strTemp.Substring(charIndex + 1).Trim();
                                                    }
                                                    else
                                                    {
                                                        strTemp = strTemp.Substring(0, charIndex).Trim();
                                                        break;
                                                    }
                                                }
                                                else
                                                {
                                                    break;
                                                }
                                            }
                                            while (true);
                                            if (IsNumber(strTemp))
                                            {
                                                mCurrentSpectrum.ScanNumberEnd = Conversions.ToInteger(strTemp);
                                            }
                                        }
                                    }

                                    blnScanNumberFound = ExtractScanRange(strLineIn, mCurrentSpectrum);
                                }
                            }
                            // Line does not start with a comment character
                            // Look for LINE_START_BEGIN_IONS in strLineIn
                            else if (strLineIn.ToUpper().StartsWith(LINE_START_BEGIN_IONS))
                            {
                                if (!blnScanNumberFound)
                                {
                                    // Need to update intScanNumberStart
                                    // Set it to one more than mScanNumberStartSaved
                                    mCurrentSpectrum.ScanNumber = mScanNumberStartSaved + 1;
                                    mCurrentSpectrum.ScanNumberEnd = mCurrentSpectrum.ScanNumber;
                                    mCurrentSpectrum.SpectrumID = mCurrentSpectrum.ScanNumber;
                                    mCurrentSpectrum.ScanCount = 1;
                                }

                                blnParentIonFound = false;

                                // We have found an MS/MS scan
                                // Look for LINE_START_PEPMASS and LINE_START_CHARGE to determine the parent ion m/z and charge
                                while (mFileReader.Peek() > -1)
                                {
                                    strLineIn = mFileReader.ReadLine();
                                    mInFileLineNumber += 1;
                                    if (strLineIn is object)
                                    {
                                        mTotalBytesRead += strLineIn.Length + 2;
                                        AddNewRecentFileText(strLineIn);
                                        if (strLineIn.Trim().Length > 0)
                                        {
                                            strLineIn = strLineIn.Trim();
                                            if (strLineIn.ToUpper().StartsWith(LINE_START_PEPMASS))
                                            {
                                                // This line defines the peptide mass as an m/z value
                                                // It may simply contain the m/z value, or it may also contain an intensity value
                                                // The two values will be separated by a space or a tab
                                                // We do not save the intensity value since it cannot be included in a .Dta file
                                                strLineIn = strLineIn.Substring(LINE_START_PEPMASS.Length).Trim();
                                                strSplitLine = strLineIn.Split(strSepChars);
                                                if (strSplitLine.Length > 0 && IsNumber(strSplitLine[0]))
                                                {
                                                    mCurrentSpectrum.ParentIonMZ = Conversions.ToDouble(strSplitLine[0]);
                                                    blnParentIonFound = true;
                                                }
                                                else
                                                {
                                                    // Invalid LINE_START_PEPMASS Line
                                                    // Ignore this entire scan
                                                    break;
                                                }
                                            }
                                            else if (strLineIn.ToUpper().StartsWith(LINE_START_CHARGE))
                                            {
                                                // This line defines the peptide charge
                                                // It may simply contain a single charge, like 1+ or 2+
                                                // It may also contain two charges, as in 2+ and 3+
                                                // Not all spectra in the MGF file will have a CHARGE= entry
                                                strLineIn = strLineIn.Substring(LINE_START_CHARGE.Length).Trim();

                                                // Remove any + signs in the line
                                                strLineIn = strLineIn.Replace("+", string.Empty);
                                                if (strLineIn.IndexOf(' ') > 0)
                                                {
                                                    // Multiple charges may be present
                                                    strSplitLine = strLineIn.Split(strSepChars);
                                                    var loopTo = strSplitLine.Length - 1;
                                                    for (intIndex = 0; intIndex <= loopTo; intIndex++)
                                                    {
                                                        // Step through the split line and add any numbers to the charge list
                                                        // Typically, strSplitLine(1) will contain "and"
                                                        if (IsNumber(strSplitLine[intIndex].Trim()))
                                                        {
                                                            {
                                                                ref var withBlock1 = ref mCurrentSpectrum;
                                                                if (withBlock1.ParentIonChargeCount < clsSpectrumInfoMsMsText.MAX_CHARGE_COUNT)
                                                                {
                                                                    withBlock1.ParentIonCharges[withBlock1.ParentIonChargeCount] = Conversions.ToInteger(strSplitLine[intIndex].Trim());
                                                                    withBlock1.ParentIonChargeCount += 1;
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                                else if (IsNumber(strLineIn))
                                                {
                                                    {
                                                        ref var withBlock2 = ref mCurrentSpectrum;
                                                        withBlock2.ParentIonChargeCount = 1;
                                                        withBlock2.ParentIonCharges[0] = Conversions.ToInteger(strLineIn);
                                                    }
                                                }
                                            }
                                            else if (strLineIn.ToUpper().StartsWith(LINE_START_TITLE))
                                            {
                                                mCurrentSpectrum.SpectrumTitle = string.Copy(strLineIn);
                                                strLineIn = strLineIn.Substring(LINE_START_TITLE.Length).Trim();
                                                mCurrentSpectrum.SpectrumTitleWithCommentChars = string.Copy(strLineIn);
                                                if (!blnScanNumberFound)
                                                {
                                                    // We didn't find a scan number in a ### MSMS: comment line
                                                    // Attempt to extract out the scan numbers from the Title
                                                    {
                                                        ref var withBlock3 = ref mCurrentSpectrum;
                                                        int argintScanNumberStart = withBlock3.ScanNumber;
                                                        int argintScanNumberEnd = withBlock3.ScanNumberEnd;
                                                        int argintScanCount = withBlock3.ScanCount;
                                                        ExtractScanInfoFromDtaHeader(strLineIn, out argintScanNumberStart, out argintScanNumberEnd, out argintScanCount);
                                                        withBlock3.ScanNumber = argintScanNumberStart;
                                                        withBlock3.ScanNumberEnd = argintScanNumberEnd;
                                                        withBlock3.ScanCount = argintScanCount;
                                                    }
                                                }
                                            }
                                            else if (strLineIn.ToUpper().StartsWith(LINE_START_END_IONS))
                                            {
                                                // Empty ion list
                                                break;
                                            }
                                            else if (strLineIn.ToUpper().StartsWith(LINE_START_RT))
                                            {
                                                strLineIn = strLineIn.Substring(LINE_START_RT.Length).Trim();
                                                double rtSeconds;
                                                if (double.TryParse(strLineIn, out rtSeconds))
                                                {
                                                    mCurrentSpectrum.RetentionTimeMin = (float)(rtSeconds / 60.0d);
                                                }
                                            }
                                            else if (strLineIn.ToUpper().StartsWith(LINE_START_SCANS))
                                            {
                                                strLineIn = strLineIn.Substring(LINE_START_SCANS.Length).Trim();
                                                blnScanNumberFound = ExtractScanRange(strLineIn, mCurrentSpectrum);
                                            }
                                            else if (char.IsNumber(strLineIn, 0))
                                            {
                                                // Found the start of the ion list
                                                // Add to the MsMs data list
                                                if (blnParentIonFound)
                                                {
                                                    mCurrentMsMsDataList.Add(strLineIn);
                                                }

                                                break;
                                            }
                                        }
                                    }
                                }

                                if (blnParentIonFound && mCurrentMsMsDataList.Count > 0)
                                {
                                    // We have determined the parent ion

                                    // Note: MGF files have Parent Ion MZ defined but not Parent Ion MH
                                    // Thus, compute .ParentIonMH using .ParentIonMZ
                                    {
                                        ref var withBlock4 = ref mCurrentSpectrum;
                                        if (withBlock4.ParentIonChargeCount >= 1)
                                        {
                                            withBlock4.ParentIonMH = ConvoluteMass(withBlock4.ParentIonMZ, withBlock4.ParentIonCharges[0], 1);
                                        }
                                        else
                                        {
                                            withBlock4.ParentIonMH = withBlock4.ParentIonMZ;
                                        }
                                    }

                                    // Read in the ions and populate mCurrentMsMsDataList
                                    // Read all of the MS/MS spectrum ions up to the next blank line or up to LINE_START_END_IONS
                                    while (mFileReader.Peek() > -1)
                                    {
                                        strLineIn = mFileReader.ReadLine();
                                        mInFileLineNumber += 1;

                                        // See if strLineIn is blank
                                        if (strLineIn is object)
                                        {
                                            mTotalBytesRead += strLineIn.Length + 2;
                                            AddNewRecentFileText(strLineIn);
                                            if (strLineIn.Trim().Length > 0)
                                            {
                                                if (strLineIn.Trim().ToUpper().StartsWith(LINE_START_END_IONS))
                                                {
                                                    break;
                                                }
                                                else
                                                {
                                                    // Add to MS/MS data sting list
                                                    mCurrentMsMsDataList.Add(strLineIn.Trim());
                                                }
                                            }
                                        }

                                        if (mInFileLineNumber - intLastProgressUpdateLine >= 250)
                                        {
                                            intLastProgressUpdateLine = mInFileLineNumber;
                                            UpdateStreamReaderProgress();
                                        }
                                    }

                                    blnSpectrumFound = true;
                                    if (mReadTextDataOnly)
                                    {
                                        // Do not parse the text data to populate .MZList and .IntensityList
                                        mCurrentSpectrum.DataCount = 0;
                                    }
                                    else
                                    {
                                        {
                                            ref var withBlock5 = ref mCurrentSpectrum;
                                            try
                                            {
                                                withBlock5.DataCount = ParseMsMsDataList(mCurrentMsMsDataList, out withBlock5.MZList, out withBlock5.IntensityList, withBlock5.AutoShrinkDataLists);
                                                withBlock5.Validate(blnComputeBasePeakAndTIC: true, blnUpdateMZRange: true);
                                            }
                                            catch (Exception ex)
                                            {
                                                withBlock5.DataCount = 0;
                                                blnSpectrumFound = false;
                                            }
                                        }
                                    }
                                }

                                // Copy the scan number to mScanNumberStartSaved
                                if (mCurrentSpectrum.ScanNumber > 0)
                                {
                                    mScanNumberStartSaved = mCurrentSpectrum.ScanNumber;
                                }
                            }
                        }

                        if (mInFileLineNumber - intLastProgressUpdateLine >= 250 | blnSpectrumFound)
                        {
                            intLastProgressUpdateLine = mInFileLineNumber;
                            StreamReader objStreamReader = mFileReader as StreamReader;
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

                    objSpectrumInfo = mCurrentSpectrum;
                    if (blnSpectrumFound && !ReadingAndStoringSpectra)
                    {
                        UpdateFileStats(objSpectrumInfo.ScanNumber);
                    }
                }
            }
            catch (Exception ex)
            {
                LogErrors("ReadNextSpectrum", ex.Message);
                objSpectrumInfo = new clsSpectrumInfoMsMsText();
            }

            return blnSpectrumFound;
        }
    }
}