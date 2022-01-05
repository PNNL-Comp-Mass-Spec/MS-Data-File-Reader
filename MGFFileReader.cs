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
    public class MgfFileReader : MsTextFileReaderBaseClass
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public MgfFileReader()
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
        /// Parse out a scan number or scan number range from data
        /// </summary>
        /// <param name="data">Single integer or two integers separated by a dash</param>
        /// <param name="spectrumInfo"></param>
        /// <returns>True if the scan number was found, otherwise false</returns>
        private bool ExtractScanRange(string data, SpectrumInfo spectrumInfo)
        {
            var scanNumberFound = false;
            var charIndex = data.IndexOf('-');

            if (charIndex > 0)
            {
                // data contains a dash, and thus a range of scans
                var remaining = data.Substring(charIndex + 1).Trim();
                data = data.Substring(0, charIndex).Trim();

                if (IsNumber(data))
                {
                    spectrumInfo.ScanNumber = int.Parse(data);

                    if (IsNumber(remaining))
                    {
                        if (spectrumInfo.ScanNumberEnd == 0)
                        {
                            spectrumInfo.ScanNumberEnd = int.Parse(remaining);
                        }
                    }
                    else
                    {
                        spectrumInfo.ScanNumberEnd = spectrumInfo.ScanNumber;
                    }

                    scanNumberFound = true;
                }
            }
            else if (IsNumber(data))
            {
                spectrumInfo.ScanNumber = int.Parse(data);

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
        /// <param name="spectrumInfo"></param>
        /// <returns>True if a spectrum is found, otherwise false</returns>
        public override bool ReadNextSpectrum(out SpectrumInfo spectrumInfo)
        {
            var sepChars = new[] { ' ', '\t' };

            var spectrumFound = false;

            try
            {
                if (ReadingAndStoringSpectra || mCurrentSpectrum is null)
                {
                    mCurrentSpectrum = new SpectrumInfoMsMsText();
                }
                else
                {
                    mCurrentSpectrum.Clear();
                }

                var scanNumberFound = false;

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
                    spectrumInfo = new SpectrumInfoMsMsText();
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

                    var lastProgressUpdateLine = mInFileLineNumber;

                    while (!spectrumFound && mFileReader.Peek() > -1 && !mAbortProcessing)
                    {
                        var lineIn = mFileReader.ReadLine();

                        if (lineIn != null)
                            mTotalBytesRead += lineIn.Length + 2;

                        mInFileLineNumber++;

                        if (lineIn?.Trim().Length > 0)
                        {
                            AddNewRecentFileText(lineIn);
                            lineIn = lineIn.Trim();

                            // See if lineIn starts with the comment line start character (a pound sign, #)
                            if (lineIn.StartsWith(CommentLineStartChar.ToString()))
                            {
                                // Remove any comment characters at the start of lineIn
                                lineIn = lineIn.TrimStart(CommentLineStartChar).Trim();

                                // Look for LINE_START_MSMS in lineIn
                                // This will be present in MGF files created using Agilent's DataAnalysis software
                                if (lineIn.StartsWith(LINE_START_MSMS, StringComparison.OrdinalIgnoreCase))
                                {
                                    lineIn = lineIn.Substring(LINE_START_MSMS.Length).Trim();

                                    // Initialize these values
                                    mCurrentSpectrum.ScanNumberEnd = 0;
                                    mCurrentSpectrum.ScanCount = 1;

                                    // Remove the # sign in front of the scan number
                                    lineIn = lineIn.TrimStart('#').Trim();

                                    // Look for the / sign and remove any text following it
                                    // For example,
                                    // ###MS: 4458/4486/
                                    // ###MSMS: 4459/4488/
                                    // The / sign is used to indicate that several MS/MS scans were combined to make the given spectrum; we'll just keep the first one
                                    var charIndex = lineIn.IndexOf('/');

                                    if (charIndex > 0)
                                    {
                                        string temp;

                                        if (charIndex < lineIn.Length - 1)
                                        {
                                            temp = lineIn.Substring(charIndex + 1).Trim();
                                        }
                                        else
                                        {
                                            temp = string.Empty;
                                        }

                                        lineIn = lineIn.Substring(0, charIndex).Trim();
                                        mCurrentSpectrum.ScanCount = 1;

                                        if (temp.Length > 0)
                                        {
                                            while (true)
                                            {
                                                charIndex = temp.IndexOf('/');

                                                if (charIndex > 0)
                                                {
                                                    mCurrentSpectrum.ScanCount++;

                                                    if (charIndex < temp.Length - 1)
                                                    {
                                                        temp = temp.Substring(charIndex + 1).Trim();
                                                    }
                                                    else
                                                    {
                                                        temp = temp.Substring(0, charIndex).Trim();
                                                        break;
                                                    }
                                                }
                                                else
                                                {
                                                    break;
                                                }
                                            }

                                            if (IsNumber(temp))
                                            {
                                                mCurrentSpectrum.ScanNumberEnd = int.Parse(temp);
                                            }
                                        }
                                    }

                                    scanNumberFound = ExtractScanRange(lineIn, mCurrentSpectrum);
                                }
                            }

                            // Line does not start with a comment character
                            // Look for LINE_START_BEGIN_IONS in lineIn
                            else if (lineIn.StartsWith(LINE_START_BEGIN_IONS, StringComparison.OrdinalIgnoreCase))
                            {
                                if (!scanNumberFound)
                                {
                                    // Need to update scanNumberStart
                                    // Set it to one more than mScanNumberStartSaved
                                    mCurrentSpectrum.ScanNumber = mScanNumberStartSaved + 1;
                                    mCurrentSpectrum.ScanNumberEnd = mCurrentSpectrum.ScanNumber;
                                    mCurrentSpectrum.SpectrumID = mCurrentSpectrum.ScanNumber;
                                    mCurrentSpectrum.ScanCount = 1;
                                }

                                var parentIonFound = false;

                                // We have found an MS/MS scan
                                // Look for LINE_START_PEPMASS and LINE_START_CHARGE to determine the parent ion m/z and charge
                                while (mFileReader.Peek() > -1)
                                {
                                    lineIn = mFileReader.ReadLine();
                                    mInFileLineNumber++;

                                    if (lineIn != null)
                                    {
                                        mTotalBytesRead += lineIn.Length + 2;
                                        AddNewRecentFileText(lineIn);

                                        if (lineIn.Trim().Length > 0)
                                        {
                                            lineIn = lineIn.Trim();
                                            string[] splitLine;

                                            if (lineIn.StartsWith(LINE_START_PEPMASS, StringComparison.OrdinalIgnoreCase))
                                            {
                                                // This line defines the peptide mass as an m/z value
                                                // It may simply contain the m/z value, or it may also contain an intensity value
                                                // The two values will be separated by a space or a tab
                                                // We do not save the intensity value since it cannot be included in a .Dta file
                                                lineIn = lineIn.Substring(LINE_START_PEPMASS.Length).Trim();
                                                splitLine = lineIn.Split(sepChars);

                                                if (splitLine.Length > 0 && IsNumber(splitLine[0]))
                                                {
                                                    mCurrentSpectrum.ParentIonMZ = double.Parse(splitLine[0]);
                                                    parentIonFound = true;
                                                }
                                                else
                                                {
                                                    // Invalid LINE_START_PEPMASS Line
                                                    // Ignore this entire scan
                                                    break;
                                                }
                                            }
                                            else if (lineIn.StartsWith(LINE_START_CHARGE, StringComparison.OrdinalIgnoreCase))
                                            {
                                                // This line defines the peptide charge
                                                // It may simply contain a single charge, like 1+ or 2+
                                                // It may also contain two charges, as in 2+ and 3+
                                                // Not all spectra in the MGF file will have a CHARGE= entry
                                                lineIn = lineIn.Substring(LINE_START_CHARGE.Length).Trim();

                                                // Remove any + signs in the line
                                                lineIn = lineIn.Replace("+", string.Empty);

                                                if (lineIn.IndexOf(' ') > 0)
                                                {
                                                    // Multiple charges may be present
                                                    splitLine = lineIn.Split(sepChars);
                                                    var indexEnd = splitLine.Length - 1;

                                                    for (var index = 0; index <= indexEnd; index++)
                                                    {
                                                        // Step through the split line and add any numbers to the charge list
                                                        // Typically, splitLine(1) will contain "and"
                                                        if (IsNumber(splitLine[index].Trim()))
                                                        {
                                                            if (mCurrentSpectrum.ParentIonChargeCount < SpectrumInfoMsMsText.MAX_CHARGE_COUNT)
                                                            {
                                                                mCurrentSpectrum.ParentIonCharges[mCurrentSpectrum.ParentIonChargeCount] =
                                                                    int.Parse(splitLine[index].Trim());

                                                                mCurrentSpectrum.ParentIonChargeCount++;
                                                            }
                                                        }
                                                    }
                                                }
                                                else if (IsNumber(lineIn))
                                                {
                                                    mCurrentSpectrum.ParentIonChargeCount = 1;
                                                    mCurrentSpectrum.ParentIonCharges[0] = int.Parse(lineIn);
                                                }
                                            }
                                            else if (lineIn.StartsWith(LINE_START_TITLE, StringComparison.OrdinalIgnoreCase))
                                            {
                                                mCurrentSpectrum.SpectrumTitle = lineIn;
                                                lineIn = lineIn.Substring(LINE_START_TITLE.Length).Trim();
                                                mCurrentSpectrum.SpectrumTitleWithCommentChars = lineIn;

                                                if (!scanNumberFound)
                                                {
                                                    // We didn't find a scan number in a ### MSMS: comment line
                                                    // Attempt to extract out the scan numbers from the Title
                                                    {
                                                        ExtractScanInfoFromDtaHeader(lineIn, out var scanNumberStart, out var scanNumberEnd, out var scanCount);
                                                        mCurrentSpectrum.ScanNumber = scanNumberStart;
                                                        mCurrentSpectrum.ScanNumberEnd = scanNumberEnd;
                                                        mCurrentSpectrum.ScanCount = scanCount;
                                                    }
                                                }
                                            }
                                            else if (lineIn.StartsWith(LINE_START_END_IONS, StringComparison.OrdinalIgnoreCase))
                                            {
                                                // Empty ion list
                                                break;
                                            }
                                            else if (lineIn.StartsWith(LINE_START_RT, StringComparison.OrdinalIgnoreCase))
                                            {
                                                lineIn = lineIn.Substring(LINE_START_RT.Length).Trim();

                                                if (double.TryParse(lineIn, out var rtSeconds))
                                                {
                                                    mCurrentSpectrum.RetentionTimeMin = (float)(rtSeconds / 60.0d);
                                                }
                                            }
                                            else if (lineIn.StartsWith(LINE_START_SCANS, StringComparison.OrdinalIgnoreCase))
                                            {
                                                lineIn = lineIn.Substring(LINE_START_SCANS.Length).Trim();
                                                scanNumberFound = ExtractScanRange(lineIn, mCurrentSpectrum);
                                            }
                                            else if (char.IsNumber(lineIn, 0))
                                            {
                                                // Found the start of the ion list
                                                // Add to the MsMs data list
                                                if (parentIonFound)
                                                {
                                                    mCurrentMsMsDataList.Add(lineIn);
                                                }

                                                break;
                                            }
                                        }
                                    }
                                }

                                if (parentIonFound && mCurrentMsMsDataList.Count > 0)
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
                                        lineIn = mFileReader.ReadLine();
                                        mInFileLineNumber++;

                                        // See if lineIn is blank
                                        if (lineIn != null)
                                        {
                                            mTotalBytesRead += lineIn.Length + 2;
                                            AddNewRecentFileText(lineIn);

                                            if (lineIn.Trim().Length > 0)
                                            {
                                                if (lineIn.Trim().StartsWith(LINE_START_END_IONS, StringComparison.OrdinalIgnoreCase))
                                                {
                                                    break;
                                                }

                                                // Add to MS/MS data sting list
                                                mCurrentMsMsDataList.Add(lineIn.Trim());
                                            }
                                        }

                                        if (mInFileLineNumber - lastProgressUpdateLine >= 250)
                                        {
                                            lastProgressUpdateLine = mInFileLineNumber;
                                            UpdateStreamReaderProgress();
                                        }
                                    }

                                    spectrumFound = true;

                                    mCurrentSpectrum.ClearMzAndIntensityData();

                                    if (ReadTextDataOnly)
                                    {
                                        // Do not parse the text data to populate .MZList and .IntensityList
                                        mCurrentSpectrum.PeaksCount = 0;
                                    }
                                    else
                                    {
                                        try
                                        {
                                            ParseMsMsDataList(mCurrentMsMsDataList, out var mzList, out var intensityList);

                                            mCurrentSpectrum.PeaksCount = mzList.Count;
                                            mCurrentSpectrum.StoreIons(mzList, intensityList);

                                            mCurrentSpectrum.Validate(true, true);
                                        }
                                        catch (Exception ex)
                                        {
                                            mCurrentSpectrum.PeaksCount = 0;
                                            spectrumFound = false;
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

                        if (mInFileLineNumber - lastProgressUpdateLine >= 250 || spectrumFound)
                        {
                            lastProgressUpdateLine = mInFileLineNumber;

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

                    spectrumInfo = mCurrentSpectrum;

                    if (spectrumFound)
                    {
                        mScanCountRead++;

                        if (!ReadingAndStoringSpectra)
                        {
                            if (mInputFileStats.ScanCount < mScanCountRead)
                                mInputFileStats.ScanCount = mScanCountRead;

                            UpdateFileStats(mInputFileStats.ScanCount, spectrumInfo.ScanNumber, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ReadNextSpectrum", ex);
                spectrumInfo = new SpectrumInfoMsMsText();
            }

            return spectrumFound;
        }
    }
}