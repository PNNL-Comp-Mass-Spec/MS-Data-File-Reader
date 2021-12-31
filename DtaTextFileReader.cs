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

/// <summary>
/// This class can be used to open a _Dta.txt file and return each spectrum present
/// </summary>
namespace MSDataFileReader
{
    public class DtaTextFileReader : MsTextFileReaderBaseClass
    {
        public DtaTextFileReader() : this(true)
        {
        }

        public DtaTextFileReader(bool combineIdenticalSpectra)
        {
            CombineIdenticalSpectra = combineIdenticalSpectra;
            InitializeLocalVariables();
        }

        /// <summary>
        /// CDTA file extension
        /// </summary>
        /// <remarks>
        /// Must be in all caps
        /// </remarks>
        public const string DTA_TEXT_FILE_EXTENSION = "_DTA.TXT";

        private const char COMMENT_LINE_START_CHAR = '=';

        // mHeaderSaved is used to store the previous header title; it is needed when the next
        // header was read for comparison with the current scan, but it didn't match, and thus
        // wasn't used for grouping
        private string mHeaderSaved;

        public bool CombineIdenticalSpectra { get; set; }

        protected sealed override void InitializeLocalVariables()
        {
            base.InitializeLocalVariables();
            CommentLineStartChar = COMMENT_LINE_START_CHAR;
            mHeaderSaved = string.Empty;
        }

        /// <summary>
        /// Read the next spectrum from a _dta.txt file
        /// </summary>
        /// <remarks>
        /// If mCombineIdenticalSpectra is true, combines spectra that have the same scan number but different charge state
        /// </remarks>
        /// <param name="spectrumInfo"></param>
        /// <returns>True if a spectrum is found, otherwise false</returns>
        public override bool ReadNextSpectrum(out SpectrumInfo spectrumInfo)
        {
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

                mCurrentSpectrum.AutoShrinkDataLists = AutoShrinkDataLists;

                if (mFileReader is null)
                {
                    spectrumInfo = new SpectrumInfo();
                    mErrorMessage = "Data file not currently open";
                }
                else
                {
                    AddNewRecentFileText(string.Empty, true, false);
                    var lastProgressUpdateLine = mInFileLineNumber;

                    while (!spectrumFound && mFileReader.Peek() > -1 && !mAbortProcessing)
                    {
                        string lineIn;

                        if (mHeaderSaved.Length > 0)
                        {
                            lineIn = mHeaderSaved;
                            mHeaderSaved = string.Empty;
                        }
                        else
                        {
                            lineIn = mFileReader.ReadLine();

                            if (lineIn != null)
                                mTotalBytesRead += lineIn.Length + 2;
                            mInFileLineNumber++;
                        }

                        // See if lineIn is nothing or starts with the comment line character (equals sign)
                        if (lineIn != null && lineIn.Trim().StartsWith(CommentLineStartChar.ToString()))
                        {
                            AddNewRecentFileText(lineIn);
                            {
                                mCurrentSpectrum.SpectrumTitleWithCommentChars = lineIn;
                                mCurrentSpectrum.SpectrumTitle = CleanupComment(lineIn, CommentLineStartChar, true);

                                ExtractScanInfoFromDtaHeader(mCurrentSpectrum.SpectrumTitle, out var scanNumberStart, out var scanNumberEnd, out var scanCount);
                                mCurrentSpectrum.ScanNumber = scanNumberStart;
                                mCurrentSpectrum.ScanNumberEnd = scanNumberEnd;
                                mCurrentSpectrum.ScanCount = scanCount;
                                mCurrentSpectrum.MSLevel = 2;
                                mCurrentSpectrum.SpectrumID = mCurrentSpectrum.ScanNumber;
                            }

                            // Read the next line, which should have the parent ion MH value and charge
                            if (mFileReader.Peek() > -1)
                            {
                                lineIn = mFileReader.ReadLine();
                            }
                            else
                            {
                                lineIn = string.Empty;
                            }

                            if (lineIn != null)
                                mTotalBytesRead += lineIn.Length + 2;

                            mInFileLineNumber++;

                            if (string.IsNullOrWhiteSpace(lineIn))
                            {
                                // Spectrum header is not followed by a parent ion value and charge; ignore the line
                            }
                            else
                            {
                                AddNewRecentFileText(lineIn);

                                // Parse the parent ion info and read the MsMs Data
                                spectrumFound = ReadSingleSpectrum(
                                    mFileReader, lineIn,
                                    out mCurrentMsMsDataList,
                                    mCurrentSpectrum,
                                    ref mInFileLineNumber,
                                    ref lastProgressUpdateLine,
                                    out var mostRecentLineIn);

                                if (spectrumFound)
                                {
                                    mCurrentSpectrum.ClearMzAndIntensityData();

                                    if (ReadTextDataOnly)
                                    {
                                        // Do not parse the text data to populate .MzList and .IntensityList
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

                                if (spectrumFound && CombineIdenticalSpectra && mCurrentSpectrum.ParentIonCharges[0] == 2)
                                {
                                    // See if the next spectrum is the identical data, but the charge is 3 (this is a common situation with .dta files prepared by Lcq_Dta)

                                    lineIn = mostRecentLineIn;

                                    if (string.IsNullOrWhiteSpace(lineIn) && mFileReader.Peek() > -1)
                                    {
                                        // Read the next line
                                        lineIn = mFileReader.ReadLine();

                                        if (lineIn != null)
                                            mTotalBytesRead += lineIn.Length + 2;

                                        mInFileLineNumber++;
                                    }

                                    if (lineIn != null && lineIn.StartsWith(CommentLineStartChar.ToString()))
                                    {
                                        mHeaderSaved = lineIn;
                                        var compareTitle = CleanupComment(mHeaderSaved, CommentLineStartChar, true);

                                        if (compareTitle.EndsWith("3.dta", StringComparison.OrdinalIgnoreCase))
                                        {
                                            if (string.Equals(mCurrentSpectrum.SpectrumTitle.Substring(0, mCurrentSpectrum.SpectrumTitle.Length - 5), compareTitle.Substring(0, compareTitle.Length - 5), StringComparison.OrdinalIgnoreCase))
                                            {
                                                // Yes, the spectra match
                                                mCurrentSpectrum.ParentIonChargeCount = 2;
                                                mCurrentSpectrum.ParentIonCharges[1] = 3;
                                                mCurrentSpectrum.ChargeIs2And3Plus = true;

                                                mHeaderSaved = string.Empty;

                                                // Read the next set of lines until the next blank line or comment line is found
                                                while (mFileReader.Peek() > -1)
                                                {
                                                    lineIn = mFileReader.ReadLine();
                                                    mInFileLineNumber++;

                                                    // See if lineIn is blank or starts with an equals sign
                                                    if (lineIn != null)
                                                    {
                                                        mTotalBytesRead += lineIn.Length + 2;

                                                        if (lineIn.Trim().Length == 0)
                                                        {
                                                            break;
                                                        }

                                                        if (lineIn.Trim().StartsWith(CommentLineStartChar.ToString()))
                                                        {
                                                            mHeaderSaved = lineIn;
                                                            break;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                else if (mostRecentLineIn.StartsWith(CommentLineStartChar.ToString()))
                                {
                                    mHeaderSaved = mostRecentLineIn;
                                }
                            }  // EndIf for spectrumFound = True
                        }  // EndIf for lineIn.Trim.StartsWith(mCommentLineStartChar)

                        if (mInFileLineNumber - lastProgressUpdateLine >= 250 || spectrumFound)
                        {
                            lastProgressUpdateLine = mInFileLineNumber;
                            UpdateStreamReaderProgress();
                        }
                    }

                    spectrumInfo = mCurrentSpectrum;

                    if (spectrumFound && !ReadingAndStoringSpectra)
                    {
                        UpdateFileStats(spectrumInfo.ScanNumber);
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ReadNextSpectrum", ex);
                spectrumInfo = new SpectrumInfo();
            }

            return spectrumFound;
        }

        /// <summary>
        /// Read a single .dta file
        /// </summary>
        /// <param name="inputFilePath"></param>
        /// <param name="msmsDataList"></param>
        /// <param name="msmsDataCount"></param>
        /// <param name="spectrumInfoMsMsText"></param>
        /// <returns>True if the file was successfully opened and a spectrum was read</returns>
        // ReSharper disable once UnusedMember.Global
        public bool ReadSingleDtaFile(string inputFilePath, out string[] msmsDataList, out int msmsDataCount, out SpectrumInfoMsMsText spectrumInfoMsMsText)
        {
            var spectrumFound = false;
            var msMsDataList = new List<string>();
            msmsDataCount = 0;
            spectrumInfoMsMsText = new SpectrumInfoMsMsText();

            try
            {
                using var fileReader = new StreamReader(inputFilePath);

                mTotalBytesRead = 0L;
                ResetProgress("Parsing " + Path.GetFileName(inputFilePath));
                mInFileLineNumber = 0;
                var lastProgressUpdateLine = mInFileLineNumber;

                while (!fileReader.EndOfStream && !mAbortProcessing)
                {
                    var lineIn = fileReader.ReadLine();
                    mInFileLineNumber++;

                    if (lineIn != null)
                        mTotalBytesRead += lineIn.Length + 2;

                    if (!string.IsNullOrWhiteSpace(lineIn))
                    {
                        if (char.IsDigit(lineIn.Trim(), 0))
                        {
                            spectrumFound = ReadSingleSpectrum(
                                fileReader,
                                lineIn,
                                out msMsDataList,
                                spectrumInfoMsMsText,
                                ref mInFileLineNumber,
                                ref lastProgressUpdateLine,
                                out _);
                            break;
                        }
                    }

                    if (mInFileLineNumber - lastProgressUpdateLine >= 100)
                    {
                        lastProgressUpdateLine = mInFileLineNumber;
                        // MyBase.UpdateProgress(inFile.BaseStream.Position / inFile.BaseStream.Length * 100.0)
                        UpdateProgress(mTotalBytesRead / (double)fileReader.BaseStream.Length * 100.0d);
                    }
                }

                if (spectrumFound)
                {
                    // Try to determine the scan numbers by parsing inputFilePath
                    ExtractScanInfoFromDtaHeader(Path.GetFileName(inputFilePath), out var scanNumberStart, out var scanNumberEnd, out var scanCount);
                    spectrumInfoMsMsText.ScanNumber = scanNumberStart;
                    spectrumInfoMsMsText.ScanNumberEnd = scanNumberEnd;
                    spectrumInfoMsMsText.ScanCount = scanCount;

                    msmsDataList = new string[msMsDataList.Count];
                    msMsDataList.CopyTo(msmsDataList);
                }
                else
                {
                    msmsDataList = new string[1];
                }

                if (!mAbortProcessing)
                {
                    OperationComplete();
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ReadSingleDtaFile", ex);
                spectrumInfoMsMsText = new SpectrumInfoMsMsText();
                msmsDataList = new string[1];
            }

            return spectrumFound;
        }

        /// <summary>
        /// Read a single mass spectrum
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="parentIonLineText"></param>
        /// <param name="msMsDataList"></param>
        /// <param name="spectrumInfoMsMsText"></param>
        /// <param name="linesRead"></param>
        /// <param name="lastProgressUpdateLine"></param>
        /// <param name="mostRecentLineIn"></param>
        /// <returns>if a valid spectrum is found, otherwise, false</returns>
        private bool ReadSingleSpectrum(
            TextReader reader,
            string parentIonLineText,
            out List<string> msMsDataList,
            SpectrumInfoMsMsText spectrumInfoMsMsText,
            ref int linesRead,
            ref int lastProgressUpdateLine,
            out string mostRecentLineIn)
        {
            var spectrumFound = false;
            spectrumInfoMsMsText.ParentIonLineText = parentIonLineText;
            parentIonLineText = parentIonLineText.Trim();

            // Look for the first space
            var charIndex = parentIonLineText.IndexOf(' ');

            if (charIndex >= 1)
            {
                var parentIonInfo = parentIonLineText.Substring(0, charIndex);

                if (double.TryParse(parentIonInfo, out var parentIonMH))
                {
                    spectrumInfoMsMsText.ParentIonMH = parentIonMH;
                    var chargeText = parentIonLineText.Substring(charIndex + 1);

                    // See if chargeText contains another space
                    var spaceIndex = chargeText.IndexOf(' ');

                    if (spaceIndex > 0)
                    {
                        chargeText = chargeText.Substring(0, spaceIndex);
                    }

                    if (int.TryParse(chargeText, out var charge))
                    {
                        spectrumInfoMsMsText.ParentIonChargeCount = 1;
                        spectrumInfoMsMsText.ParentIonCharges[0] = charge;

                        // Note: DTA files have Parent Ion MH defined but not Parent Ion m/z
                        // Thus, compute .ParentIonMZ using .ParentIonMH
                        if (spectrumInfoMsMsText.ParentIonCharges[0] <= 1)
                        {
                            spectrumInfoMsMsText.ParentIonMZ = spectrumInfoMsMsText.ParentIonMH;
                            spectrumInfoMsMsText.ParentIonCharges[0] = 1;
                        }
                        else
                        {
                            spectrumInfoMsMsText.ParentIonMZ = ConvoluteMass(spectrumInfoMsMsText.ParentIonMH, 1, spectrumInfoMsMsText.ParentIonCharges[0]);
                        }

                        spectrumFound = true;
                    }
                }
            }

            mostRecentLineIn = string.Empty;
            msMsDataList = new List<string>();

            if (!spectrumFound)
                return false;

            // Read all of the MS/MS spectrum ions up to the next blank line or up to the next line starting with COMMENT_LINE_START_CHAR
            while (reader.Peek() > -1)
            {
                var lineIn = reader.ReadLine();
                linesRead++;

                // See if lineIn is blank
                if (lineIn != null)
                {
                    mTotalBytesRead += lineIn.Length + 2;
                    mostRecentLineIn = lineIn;

                    if (lineIn.Trim().Length == 0 || lineIn.StartsWith(COMMENT_LINE_START_CHAR.ToString()))
                    {
                        break;
                    }

                    // Add to MS/MS data string list
                    msMsDataList.Add(lineIn.Trim());
                    AddNewRecentFileText(lineIn);
                }

                if (linesRead - lastProgressUpdateLine >= 250)
                {
                    lastProgressUpdateLine = linesRead;
                    UpdateStreamReaderProgress();
                }
            }

            return true;
        }
    }
}