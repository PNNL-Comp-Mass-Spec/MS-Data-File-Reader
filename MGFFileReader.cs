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

namespace MSDataFileReader
{
    /// <summary>
    /// This class can be used to open a Mascot Generic File (.MGF) and return each spectrum present
    /// </summary>
    public class clsMGFFileReader : clsMSTextFileReaderBaseClass
    {
        public clsMGFFileReader()
        {
            InitializeLocalVariables();
        }

        /// <summary>
        /// MGF file extension
        /// </summary>
        /// <remarks>
        /// Must be in all caps
        /// </remarks>
        public const string MGF_FILE_EXTENSION = ".MGF";

        private const char COMMENT_LINE_START_CHAR = '#';

        private const string LINE_START_BEGIN_IONS = "BEGIN IONS";

        private const string LINE_START_END_IONS = "END IONS";

        private const string LINE_START_MSMS = "MSMS:";

        private const string LINE_START_PEPMASS = "PEPMASS=";

        private const string LINE_START_CHARGE = "CHARGE=";

        private const string LINE_START_TITLE = "TITLE=";

        private const string LINE_START_RT = "RTINSECONDS=";

        private const string LINE_START_SCANS = "SCANS=";

        // mScanNumberStartSaved is used to create fake scan numbers when reading .MGF files that do not have
        // scan numbers defined using   ###MSMS: #1234   or   TITLE=Filename.1234.1234.2.dta  or  TITLE=Filename.1234.1234.2
        private int mScanNumberStartSaved;

        protected sealed override void InitializeLocalVariables()
        {
            base.InitializeLocalVariables();
            CommentLineStartChar = COMMENT_LINE_START_CHAR;
            mScanNumberStartSaved = 0;
        }

        /// <summary>
        /// Parse out a scan number or scan number range from strData
        /// </summary>
        /// <param name="strData">Single integer or two integers separated by a dash</param>
        /// <param name="spectrumInfo"></param>
        /// <returns>True if the scan number was found, otherwise false</returns>
        private bool ExtractScanRange(string strData, clsSpectrumInfo spectrumInfo)
        {
            var scanNumberFound = false;
            var charIndex = strData.IndexOf('-');

            if (charIndex > 0)
            {
                // strData contains a dash, and thus a range of scans
                var strRemaining = strData.Substring(charIndex + 1).Trim();
                strData = strData.Substring(0, charIndex).Trim();

                if (IsNumber(strData))
                {
                    spectrumInfo.ScanNumber = int.Parse(strData);

                    if (IsNumber(strRemaining))
                    {
                        if (spectrumInfo.ScanNumberEnd == 0)
                        {
                            spectrumInfo.ScanNumberEnd = int.Parse(strRemaining);
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
                spectrumInfo.ScanNumber = int.Parse(strData);

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

        /// <summary>
        /// Read the next spectrum from a .mgf file
        /// </summary>
        /// <param name="objSpectrumInfo"></param>
        /// <returns>True if a spectrum is found, otherwise false</returns>
        public override bool ReadNextSpectrum(out clsSpectrumInfo objSpectrumInfo)
        {
            var strSepChars = new[] { ' ', '\t' };

            var blnSpectrumFound = false;

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
                var blnScanNumberFound = false;

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
                        mCurrentSpectrum.SpectrumTitleWithCommentChars = string.Empty;
                        mCurrentSpectrum.SpectrumTitle = string.Empty;
                        mCurrentSpectrum.MSLevel = 2;
                    }

                    var intLastProgressUpdateLine = mInFileLineNumber;

                    while (!blnSpectrumFound && mFileReader.Peek() > -1 && !mAbortProcessing)
                    {
                        var strLineIn = mFileReader.ReadLine();

                        if (strLineIn != null)
                            mTotalBytesRead += strLineIn.Length + 2;

                        mInFileLineNumber++;

                        if (strLineIn != null && strLineIn.Trim().Length > 0)
                        {
                            AddNewRecentFileText(strLineIn);
                            strLineIn = strLineIn.Trim();

                            // See if strLineIn starts with the comment line start character (a pound sign, #)
                            if (strLineIn.StartsWith(CommentLineStartChar.ToString()))
                            {
                                // Remove any comment characters at the start of strLineIn
                                strLineIn = strLineIn.TrimStart(CommentLineStartChar).Trim();

                                // Look for LINE_START_MSMS in strLineIn
                                // This will be present in MGF files created using Agilent's DataAnalysis software
                                if (strLineIn.StartsWith(LINE_START_MSMS, StringComparison.OrdinalIgnoreCase))
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
                                    var charIndex = strLineIn.IndexOf('/');

                                    if (charIndex > 0)
                                    {
                                        string strTemp;

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
                                                    mCurrentSpectrum.ScanCount++;

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
                                                mCurrentSpectrum.ScanNumberEnd = int.Parse(strTemp);
                                            }
                                        }
                                    }

                                    blnScanNumberFound = ExtractScanRange(strLineIn, mCurrentSpectrum);
                                }
                            }

                            // Line does not start with a comment character
                            // Look for LINE_START_BEGIN_IONS in strLineIn
                            else if (strLineIn.StartsWith(LINE_START_BEGIN_IONS, StringComparison.OrdinalIgnoreCase))
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

                                var blnParentIonFound = false;

                                // We have found an MS/MS scan
                                // Look for LINE_START_PEPMASS and LINE_START_CHARGE to determine the parent ion m/z and charge
                                while (mFileReader.Peek() > -1)
                                {
                                    strLineIn = mFileReader.ReadLine();
                                    mInFileLineNumber++;

                                    if (strLineIn != null)
                                    {
                                        mTotalBytesRead += strLineIn.Length + 2;
                                        AddNewRecentFileText(strLineIn);

                                        if (strLineIn.Trim().Length > 0)
                                        {
                                            strLineIn = strLineIn.Trim();
                                            string[] strSplitLine;

                                            if (strLineIn.StartsWith(LINE_START_PEPMASS, StringComparison.OrdinalIgnoreCase))
                                            {
                                                // This line defines the peptide mass as an m/z value
                                                // It may simply contain the m/z value, or it may also contain an intensity value
                                                // The two values will be separated by a space or a tab
                                                // We do not save the intensity value since it cannot be included in a .Dta file
                                                strLineIn = strLineIn.Substring(LINE_START_PEPMASS.Length).Trim();
                                                strSplitLine = strLineIn.Split(strSepChars);

                                                if (strSplitLine.Length > 0 && IsNumber(strSplitLine[0]))
                                                {
                                                    mCurrentSpectrum.ParentIonMZ = double.Parse(strSplitLine[0]);
                                                    blnParentIonFound = true;
                                                }
                                                else
                                                {
                                                    // Invalid LINE_START_PEPMASS Line
                                                    // Ignore this entire scan
                                                    break;
                                                }
                                            }
                                            else if (strLineIn.StartsWith(LINE_START_CHARGE, StringComparison.OrdinalIgnoreCase))
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
                                                    var intIndexEnd = strSplitLine.Length - 1;

                                                    for (var intIndex = 0; intIndex <= intIndexEnd; intIndex++)
                                                    {
                                                        // Step through the split line and add any numbers to the charge list
                                                        // Typically, strSplitLine(1) will contain "and"
                                                        if (IsNumber(strSplitLine[intIndex].Trim()))
                                                        {
                                                            if (mCurrentSpectrum.ParentIonChargeCount < clsSpectrumInfoMsMsText.MAX_CHARGE_COUNT)
                                                            {
                                                                mCurrentSpectrum.ParentIonCharges[mCurrentSpectrum.ParentIonChargeCount] =
                                                                    int.Parse(strSplitLine[intIndex].Trim());

                                                                mCurrentSpectrum.ParentIonChargeCount++;
                                                            }
                                                        }
                                                    }
                                                }
                                                else if (IsNumber(strLineIn))
                                                {
                                                    mCurrentSpectrum.ParentIonChargeCount = 1;
                                                    mCurrentSpectrum.ParentIonCharges[0] = int.Parse(strLineIn);
                                                }
                                            }
                                            else if (strLineIn.StartsWith(LINE_START_TITLE, StringComparison.OrdinalIgnoreCase))
                                            {
                                                mCurrentSpectrum.SpectrumTitle = string.Copy(strLineIn);
                                                strLineIn = strLineIn.Substring(LINE_START_TITLE.Length).Trim();
                                                mCurrentSpectrum.SpectrumTitleWithCommentChars = string.Copy(strLineIn);

                                                if (!blnScanNumberFound)
                                                {
                                                    // We didn't find a scan number in a ### MSMS: comment line
                                                    // Attempt to extract out the scan numbers from the Title
                                                    {
                                                        ExtractScanInfoFromDtaHeader(strLineIn, out var scanNumberStart, out var scanNumberEnd, out var scanCount);
                                                        mCurrentSpectrum.ScanNumber = scanNumberStart;
                                                        mCurrentSpectrum.ScanNumberEnd = scanNumberEnd;
                                                        mCurrentSpectrum.ScanCount = scanCount;
                                                    }
                                                }
                                            }
                                            else if (strLineIn.StartsWith(LINE_START_END_IONS, StringComparison.OrdinalIgnoreCase))
                                            {
                                                // Empty ion list
                                                break;
                                            }
                                            else if (strLineIn.StartsWith(LINE_START_RT, StringComparison.OrdinalIgnoreCase))
                                            {
                                                strLineIn = strLineIn.Substring(LINE_START_RT.Length).Trim();

                                                if (double.TryParse(strLineIn, out var rtSeconds))
                                                {
                                                    mCurrentSpectrum.RetentionTimeMin = (float)(rtSeconds / 60.0d);
                                                }
                                            }
                                            else if (strLineIn.StartsWith(LINE_START_SCANS, StringComparison.OrdinalIgnoreCase))
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
                                        if (mCurrentSpectrum.ParentIonChargeCount >= 1)
                                        {
                                            mCurrentSpectrum.ParentIonMH = ConvoluteMass(mCurrentSpectrum.ParentIonMZ, mCurrentSpectrum.ParentIonCharges[0], 1);
                                        }
                                        else
                                        {
                                            mCurrentSpectrum.ParentIonMH = mCurrentSpectrum.ParentIonMZ;
                                        }
                                    }

                                    // Read in the ions and populate mCurrentMsMsDataList
                                    // Read all of the MS/MS spectrum ions up to the next blank line or up to LINE_START_END_IONS
                                    while (mFileReader.Peek() > -1)
                                    {
                                        strLineIn = mFileReader.ReadLine();
                                        mInFileLineNumber++;

                                        // See if strLineIn is blank
                                        if (strLineIn != null)
                                        {
                                            mTotalBytesRead += strLineIn.Length + 2;
                                            AddNewRecentFileText(strLineIn);

                                            if (strLineIn.Trim().Length > 0)
                                            {
                                                if (strLineIn.Trim().StartsWith(LINE_START_END_IONS, StringComparison.OrdinalIgnoreCase))
                                                {
                                                    break;
                                                }

                                                // Add to MS/MS data sting list
                                                mCurrentMsMsDataList.Add(strLineIn.Trim());
                                            }
                                        }

                                        if (mInFileLineNumber - intLastProgressUpdateLine >= 250)
                                        {
                                            intLastProgressUpdateLine = mInFileLineNumber;
                                            UpdateStreamReaderProgress();
                                        }
                                    }

                                    blnSpectrumFound = true;

                                    if (ReadTextDataOnly)
                                    {
                                        // Do not parse the text data to populate .MZList and .IntensityList
                                        mCurrentSpectrum.DataCount = 0;
                                    }
                                    else
                                    {
                                        try
                                        {
                                            mCurrentSpectrum.DataCount = ParseMsMsDataList(mCurrentMsMsDataList, out mCurrentSpectrum.MZList, out mCurrentSpectrum.IntensityList, mCurrentSpectrum.AutoShrinkDataLists);
                                            mCurrentSpectrum.Validate(true, true);
                                        }
                                        catch (Exception ex)
                                        {
                                            mCurrentSpectrum.DataCount = 0;
                                            blnSpectrumFound = false;
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

                        if (mInFileLineNumber - intLastProgressUpdateLine >= 250 || blnSpectrumFound)
                        {
                            intLastProgressUpdateLine = mInFileLineNumber;

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
                objSpectrumInfo = new clsSpectrumInfoMsMsText();
            }

            return blnSpectrumFound;
        }
    }
}