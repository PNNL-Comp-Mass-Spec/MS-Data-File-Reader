using System;
using System.Collections.Generic;
using System.IO;
using MSDataFileReader;
using NUnit.Framework;

namespace MSDataFileReaderUnitTests
{
    [TestFixture]
    public class MSDataFileReaderTests
    {
        // Ignore Spelling: centroided

        internal static FileInfo FindInputFile(string dataFileName)
        {
            var localDirPath = Path.Combine("..", "..", "Docs");
            const string remoteDirPath = @"\\proto-2\UnitTest_Files\MSDataFileReaderDLL";

            var localFile = new FileInfo(Path.Combine(localDirPath, dataFileName));

            if (localFile.Exists)
            {
                return localFile;
            }

            // Look for the file on Proto-2
            var remoteFile = new FileInfo(Path.Combine(remoteDirPath, dataFileName));

            if (remoteFile.Exists)
            {
                return remoteFile;
            }

            var msg = string.Format("File not found: {0}; checked in both {1} and {2}", dataFileName, localDirPath, remoteDirPath);

            Console.WriteLine(msg);
            Assert.Fail(msg);

            return null;
        }

        [Test]
        [TestCase("Angiotensin_Excerpt_dta.txt", 93, 0, 93)]
        public void TestCDTAReader(string cdtaFileName, int expectedScanCount, int expectedMS1, int expectedMS2)
        {
            var expectedScanInfo = GetExpectedDtaOrMgfScanInfo(cdtaFileName);

            var dataFile = FindInputFile(cdtaFileName);

            var reader = new DtaTextFileReader();
            reader.OpenFile(dataFile.FullName);

            ValidateScanInfoUsingReader(reader, expectedScanCount, expectedMS1, expectedMS2, expectedScanInfo);
        }

        [Test]
        [TestCase("Angiotensin_Excerpt_dta.txt", 93, 0, 93)]
        public void TestCDTAReaderCached(string cdtaFileName, int expectedScanCount, int expectedMS1, int expectedMS2)
        {
            var expectedScanInfo = GetExpectedDtaOrMgfScanInfo(cdtaFileName);

            var dataFile = FindInputFile(cdtaFileName);

            var reader = new DtaTextFileReader();
            reader.OpenFile(dataFile.FullName);

            reader.ReadAndCacheEntireFile();

            ValidateScanInfoUsingCachedSpectra(reader, expectedScanCount, expectedMS1, expectedMS2, expectedScanInfo);
        }

        [Test]
        [TestCase("Angiotensin_Excerpt.mgf", 93, 0, 93)]
        public void TestMgfReader(string mgfFileName, int expectedScanCount, int expectedMS1, int expectedMS2)
        {
            var expectedScanInfo = GetExpectedDtaOrMgfScanInfo(mgfFileName);

            var dataFile = FindInputFile(mgfFileName);

            var reader = new MgfFileReader();
            reader.OpenFile(dataFile.FullName);

            ValidateScanInfoUsingReader(reader, expectedScanCount, expectedMS1, expectedMS2, expectedScanInfo);
        }

        [Test]
        [TestCase("Angiotensin_Excerpt.mgf", 93, 0, 93)]
        public void TestMgfReaderCached(string mgfFileName, int expectedScanCount, int expectedMS1, int expectedMS2)
        {
            var expectedScanInfo = GetExpectedDtaOrMgfScanInfo(mgfFileName);

            var dataFile = FindInputFile(mgfFileName);

            var reader = new MgfFileReader();
            reader.OpenFile(dataFile.FullName);

            reader.ReadAndCacheEntireFile();

            ValidateScanInfoUsingCachedSpectra(reader, expectedScanCount, expectedMS1, expectedMS2, expectedScanInfo);
        }

        [Test]
        [TestCase("SmallTest.mzData", 6, 0, 6)]
        [TestCase("SmallTest_Unix.mzData", 6, 0, 6)]
        [TestCase("SampleData_myo_excerpt_1.05cv.mzdata", 220, 147, 73)]
        public void TestMzDataReader(string mzDataFileName, int expectedScanCount, int expectedMS1, int expectedMS2)
        {
            var expectedScanInfo = GetExpectedMzDataScanInfo(mzDataFileName);

            var dataFile = FindInputFile(mzDataFileName);

            var reader = new MzDataFileReader();
            reader.OpenFile(dataFile.FullName);

            ValidateScanInfoUsingReader(reader, expectedScanCount, expectedMS1, expectedMS2, expectedScanInfo);
        }

        [Test]
        [TestCase("SmallTest.mzData", false, 6, 345, 345, 0, 1)]
        [TestCase("SmallTest.mzData", true, 6, 345, 345, 0, 1)]
        [TestCase("SmallTest_Unix.mzData", true, 6, 345, 345, 0, 1)]
        [TestCase("SampleData_myo_excerpt_1.05cv.mzdata", false, 0, 100, 200, 68, 33)]
        [TestCase("SampleData_myo_excerpt_1.05cv.mzdata", true, 220, 100, 200, 68, 33)]
        public void TestMzDataAccessor(string mzDataFileName, bool cacheFileInMemory, int expectedScanCount, int scanStart, int scanEnd, int expectedMS1, int expectedMS2)
        {
            var expectedScanInfo = GetExpectedMzDataScanInfo(mzDataFileName);

            var dataFile = FindInputFile(mzDataFileName);

            var reader = new MzDataFileAccessor();
            reader.OpenFile(dataFile.FullName);

            if (!cacheFileInMemory)
            {
                var validScan = reader.GetSpectrumByScanNumber(scanStart, out _);

                Assert.IsFalse(validScan, "GetSpectrumByScanNumber returned true for scan {0}, but it should have returned false since ReadAndCacheEntireFile has not yet been called", scanStart);

                Console.WriteLine(
                    "Since ReadAndCacheEntireFile() has not yet been called, GetSpectrumByScanNumber returned false for scan {0}, as expected",
                    scanStart);

                return;
            }

            reader.ReadAndCacheEntireFile();

            ValidateScanInfo(reader, expectedScanCount, scanStart, scanEnd, expectedMS1, expectedMS2, expectedScanInfo);
        }

        [Test]
        [TestCase("Shew_246a_LCQa_15Oct04_Andro_0904-2_4-20.mzXML", 3316, 862, 2454)]
        [TestCase("Angiotensin_AllScans.mzXML", 1775, 87, 1688)]
        [TestCase("Angiotensin_Excerpt_NoIndex.mzXML", 120, 8, 112)]
        public void TestMzXmlReader(string mzXmlFileName, int expectedScanCount, int expectedMS1, int expectedMS2)
        {
            var expectedScanInfo = GetExpectedMzXmlScanInfo(mzXmlFileName);

            var dataFile = FindInputFile(mzXmlFileName);

            var reader = new MzXMLFileReader();
            reader.OpenFile(dataFile.FullName);

            ValidateScanInfoUsingReader(reader, expectedScanCount, expectedMS1, expectedMS2, expectedScanInfo);
        }

        [Test]
        [TestCase("Shew_246a_LCQa_15Oct04_Andro_0904-2_4-20.mzXML", 3316, 1513, 1521, 3, 6)]
        [TestCase("Angiotensin_AllScans.mzXML", 1775, 1200, 1231, 2, 30)]
        [TestCase("Angiotensin_Excerpt_NoIndex.mzXML", 120, 1, 120, 8, 112, true)]
        [TestCase("Angiotensin_AllScans_centroided.mzXML", 1775, 1200, 1231, 2, 30)]
        [TestCase("Angiotensin_AllScans_zlib_compression.mzXML", 1775, 1200, 1231, 2, 30)]
        public void TestMzXmlAccessor(string mzXmlFileName, int expectedScanCount, int scanStart, int scanEnd, int expectedMS1, int expectedMS2, bool cacheIfNoIndex = false)
        {
            var expectedScanInfo = GetExpectedMzXmlScanInfo(mzXmlFileName);

            var dataFile = FindInputFile(mzXmlFileName);

            var reader = new MzXMLFileAccessor();
            reader.OpenFile(dataFile.FullName);

            if (reader.IndexedSpectrumCount == 0 && cacheIfNoIndex)
            {
                // Cache the entire file in memory
                reader.ReadAndCacheEntireFileNonIndexed();
            }

            ValidateScanInfo(reader, expectedScanCount, scanStart, scanEnd, expectedMS1, expectedMS2, expectedScanInfo);
        }

        internal static void ValidateScanInfo(
            MsDataFileAccessorBaseClass reader,
            int expectedScanCount,
            int scanStart, int scanEnd,
            int expectedMS1, int expectedMS2,
            Dictionary<int, string> expectedScanInfo)
        {
            Assert.AreEqual(expectedScanCount, reader.ScanCount);

            Console.WriteLine("Scan info for {0}", Path.GetFileName(reader.InputFilePath));

            WriteScanInfoColumnNames(reader);

            var scanCountMS1 = 0;
            var scanCountMS2 = 0;

            for (var scanNumber = scanStart; scanNumber <= scanEnd; scanNumber++)
            {
                var validScan = reader.GetSpectrumByScanNumber(scanNumber, out var spectrumInfo);

                Assert.IsTrue(validScan, "GetSpectrumByScanNumber returned false for scan {0}", scanNumber);

                ValidateScanInfo(expectedScanInfo, spectrumInfo);

                switch (spectrumInfo.MSLevel)
                {
                    case 1:
                        scanCountMS1++;
                        break;

                    case > 1:
                        scanCountMS2++;
                        break;
                }
            }

            Console.WriteLine("scanCountMS1={0}", scanCountMS1);
            Console.WriteLine("scanCountMS2={0}", scanCountMS2);

            Assert.AreEqual(expectedMS1, scanCountMS1, "MS1 scan count mismatch");
            Assert.AreEqual(expectedMS2, scanCountMS2, "MS2 scan count mismatch");
        }

        private static void ValidateScanInfo(IReadOnlyDictionary<int, string> expectedScanInfo, SpectrumInfo spectrumInfo)
        {
            string scanSummary;

            switch (spectrumInfo)
            {
                case SpectrumInfoMzXML mzXmlSpectrum:
                    string filterLine;

                    // Only files created with ReAdW will have filter line text
                    if (string.IsNullOrWhiteSpace(mzXmlSpectrum.FilterLine))
                    {
                        filterLine = string.Empty;
                    }
                    else
                    {
                        filterLine = mzXmlSpectrum.FilterLine.Substring(0, 12) + "...";
                    }

                    scanSummary = string.Format(
                        "{0,3} {1} {2,5} {3:F2} {4,3:0} {5,4:0} {6:0.0E+0} {7,8:F3} {8:0.0E+0} {9,8:F2} {10,-6} {11} {12,-5} {13} {14}",
                        spectrumInfo.ScanNumber, spectrumInfo.MSLevel,
                        spectrumInfo.PeaksCount, spectrumInfo.RetentionTimeMin,
                        spectrumInfo.MzRangeStart, spectrumInfo.MzRangeEnd,
                        spectrumInfo.TotalIonCurrent,
                        spectrumInfo.BasePeakMZ,
                        spectrumInfo.BasePeakIntensity,
                        spectrumInfo.ParentIonMZ,
                        mzXmlSpectrum.ActivationMethod,
                        spectrumInfo.Polarity,
                        spectrumInfo.Centroided,
                        mzXmlSpectrum.SpectrumType, filterLine);

                    break;

                case SpectrumInfoMzData mzDataSpectrum:
                    var collisionEnergyUnits = mzDataSpectrum.CollisionEnergyUnits.Equals("Percent", StringComparison.OrdinalIgnoreCase)
                        ? "%"
                        : " " + mzDataSpectrum.CollisionEnergyUnits;

                    scanSummary = string.Format(
                        "{0,3} {1} {2,5} {3,5:F2} {4,3:0} {5,4:0} {6:0.0E+0} {7,8:F3} {8:0.0E+0} {9,8:F2} {10,-6} {11} {12,-6} {13,-10} {14} {15,4:F0}{16} {17,4}",
                        spectrumInfo.ScanNumber, spectrumInfo.MSLevel,
                        spectrumInfo.PeaksCount, spectrumInfo.RetentionTimeMin,
                        spectrumInfo.MzRangeStart, spectrumInfo.MzRangeEnd,
                        spectrumInfo.TotalIonCurrent, spectrumInfo.BasePeakMZ, spectrumInfo.BasePeakIntensity,
                        spectrumInfo.ParentIonMZ, mzDataSpectrum.CollisionMethod,
                        spectrumInfo.Polarity, spectrumInfo.Centroided,
                        mzDataSpectrum.SpectrumType, mzDataSpectrum.ScanMode,
                        mzDataSpectrum.CollisionEnergy,
                        collisionEnergyUnits,
                        mzDataSpectrum.ParentIonSpectrumID);

                    break;

                case SpectrumInfoMsMsText msmsSpectrum:
                    scanSummary = string.Format(
                        "{0,3} {1} {2,5} {3:F2} {4,3:0} {5,4:0} {6:0.0E+0} {7,8:F3} {8:0.0E+0} {9,8:F2} {10:F2}",
                        spectrumInfo.ScanNumber, spectrumInfo.MSLevel,
                        spectrumInfo.PeaksCount, spectrumInfo.RetentionTimeMin,
                        spectrumInfo.MzRangeStart, spectrumInfo.MzRangeEnd,
                        spectrumInfo.TotalIonCurrent, spectrumInfo.BasePeakMZ, spectrumInfo.BasePeakIntensity,
                        spectrumInfo.ParentIonMZ,
                        msmsSpectrum.ParentIonMH);
                    break;

                default:
                    scanSummary = "Unrecognized spectrum type";
                    break;
            }

            Console.WriteLine(scanSummary);

            if (expectedScanInfo.TryGetValue(spectrumInfo.ScanNumber, out var expectedScanSummary) && !string.IsNullOrWhiteSpace(expectedScanSummary))
            {
                Assert.AreEqual(string.Format("{0,3} {1}", spectrumInfo.ScanNumber, expectedScanSummary), scanSummary,
                    "Scan summary mismatch, scan " + spectrumInfo.ScanNumber);
            }
        }

        private void ValidateScanInfoUsingCachedSpectra(
            MsDataFileReaderBaseClass reader,
            int expectedScanCount,
            int expectedMS1,
            int expectedMS2,
            IReadOnlyDictionary<int, string> expectedScanInfo)
        {
            Console.WriteLine("Scan info for {0}", Path.GetFileName(reader.InputFilePath));

            WriteScanInfoColumnNames(reader);

            var scanCountMS1 = 0;
            var scanCountMS2 = 0;
            var scanCount = 0;

            for (var spectrumIndex = 0; spectrumIndex < reader.CachedSpectrumCount; spectrumIndex++)
            {
                var validScan = reader.GetSpectrumByIndex(spectrumIndex, out var spectrumInfo);

                Assert.IsTrue(validScan, "GetSpectrumByScanNumber returned false for index {0}", spectrumIndex);

                if (expectedScanInfo.TryGetValue(spectrumInfo.ScanNumber, out _))
                {
                    ValidateScanInfo(expectedScanInfo, spectrumInfo);
                }

                switch (spectrumInfo.MSLevel)
                {
                    case 1:
                        scanCountMS1++;
                        break;

                    case > 1:
                        scanCountMS2++;
                        break;
                }

                scanCount++;
            }

            Console.WriteLine("scanCountMS1={0}", scanCountMS1);
            Console.WriteLine("scanCountMS2={0}", scanCountMS2);

            Assert.AreEqual(expectedScanCount, reader.ScanCount, "reader.ScanCount mismatch");

            Assert.AreEqual(expectedScanCount, scanCount, "Scan count read mismatch");

            Assert.AreEqual(expectedMS1, scanCountMS1, "MS1 scan count mismatch");
            Assert.AreEqual(expectedMS2, scanCountMS2, "MS2 scan count mismatch");
        }

        private void ValidateScanInfoUsingReader(
            MsDataFileReaderBaseClass reader,
            int expectedScanCount,
            int expectedMS1,
            int expectedMS2,
            IReadOnlyDictionary<int, string> expectedScanInfo)
        {
            Console.WriteLine("Scan info for {0}", Path.GetFileName(reader.InputFilePath));

            WriteScanInfoColumnNames(reader);

            var scanCountMS1 = 0;
            var scanCountMS2 = 0;
            var scanCount = 0;

            while (reader.ReadNextSpectrum(out var spectrumInfo))
            {
                if (expectedScanInfo.TryGetValue(spectrumInfo.ScanNumber, out _))
                {
                    ValidateScanInfo(expectedScanInfo, spectrumInfo);
                }

                switch (spectrumInfo.MSLevel)
                {
                    case 1:
                        scanCountMS1++;
                        break;

                    case > 1:
                        scanCountMS2++;
                        break;
                }

                scanCount++;
            }

            Console.WriteLine("scanCountMS1={0}", scanCountMS1);
            Console.WriteLine("scanCountMS2={0}", scanCountMS2);

            Assert.AreEqual(expectedScanCount, reader.ScanCount, "reader.ScanCount mismatch");

            Assert.AreEqual(expectedScanCount, scanCount, "Scan count read mismatch");

            Assert.AreEqual(expectedMS1, scanCountMS1, "MS1 scan count mismatch");
            Assert.AreEqual(expectedMS2, scanCountMS2, "MS2 scan count mismatch");
        }

        /// <summary>
        /// Get the expected scan info for the given file
        /// </summary>
        /// <param name="dataFileName"></param>
        /// <returns>Dictionary where Keys are scan number and values are the expected scan info</returns>
        internal static Dictionary<int, string> GetExpectedDtaOrMgfScanInfo(string dataFileName)
        {
            return dataFileName switch
            {
                "Angiotensin_Excerpt_dta.txt" => new Dictionary<int, string>
                {
                    { 3, "2   337 0.00 109 1290 1.6E+9  110.071 2.3E+8   432.90 1296.69" },
                    { 4, "2   280 0.00 108 1046 1.0E+9  110.071 1.6E+8   433.90 1299.69" },
                    { 5, "2   295 0.00 101 1185 8.8E+7  784.410 5.6E+6   648.85 1296.69" },
                    { 6, "2   149 0.00 101 1301 1.1E+9  649.349 1.8E+8   432.90 1296.69" },
                    { 7, "2   350 0.00 100 1302 1.2E+9  649.349 1.7E+8   432.90 1296.69" },
                    { 8, "2   275 0.00 100 1302 1.1E+9  432.900 1.5E+8   432.90 1296.69" },
                    { 9, "2   148 0.00 103 1303 7.6E+8  649.851 1.5E+8   433.90 1299.69" },
                    { 10, "2   319 0.00 102 1303 8.8E+8  649.851 1.1E+8   433.90 1299.69" },
                    { 11, "2   230 0.00 101 1303 7.9E+8  433.234 1.2E+8   433.90 1299.69" },
                    { 12, "2    82 0.00 107 1300 4.4E+7  648.846 9.5E+6   648.85 1296.69" },
                    { 13, "2   189 0.00 105 1300 4.6E+7 1297.691 5.4E+6   648.85 1296.69" },
                    { 14, "2   114 0.00 100 1300 5.1E+7  648.846 1.0E+7   648.85 1296.69" },
                    { 15, "2   181 0.00 105  948 2.4E+7  269.161 5.7E+6   513.28 1025.56" },
                    { 16, "2   266 0.00 102 1185 6.0E+7  269.161 4.0E+6   649.85 1298.69" },
                    { 17, "2    64 0.00 104 1029 1.0E+7  513.282 8.2E+6   513.28 1025.56" },
                    { 18, "2   125 0.00 104 1029 1.2E+7  513.281 3.3E+6   513.28 1025.56" },
                    { 19, "2    65 0.00 107 1029 1.2E+7  513.282 8.8E+6   513.28 1025.56" },
                    { 20, "2    80 0.00 115 1302 2.7E+7  649.348 6.7E+6   649.85 1298.69" },
                    { 21, "2   197 0.00 102 1302 3.5E+7 1298.694 3.9E+6   649.85 1298.69" },
                    { 22, "2   155 0.00 100 1302 3.6E+7  649.348 8.4E+6   649.85 1298.69" },
                    { 24, "2   333 0.00 104 1031 1.6E+9  110.071 2.4E+8   432.90 1296.68" },
                    { 25, "2   297 0.00 110 1161 1.1E+9  110.071 1.6E+8   433.90 1299.69" },
                    { 96, "2    94 0.00 107 1302 2.6E+7  649.348 6.7E+6   649.85 1298.69" },
                    { 97, "2   242 0.00 103 1302 3.1E+7 1298.694 3.5E+6   649.85 1298.69" },
                    { 98, "2   168 0.00 103 1302 3.2E+7  649.348 7.5E+6   649.85 1298.69" },
                    { 100, "2   323 0.00 107 1031 1.6E+9  110.071 2.5E+8   432.90 1296.69" }
                },
                "Angiotensin_Excerpt.mgf" => new Dictionary<int, string>
                {
                    { 3, "2   337 0.01 109 1290 1.6E+9  110.071 2.3E+8   432.90 1296.69" },
                    { 4, "2   280 0.01 108 1046 1.0E+9  110.071 1.6E+8   433.90 1299.69" },
                    { 5, "2   295 0.01 101 1185 8.8E+7  784.410 5.6E+6   648.85 1296.69" },
                    { 6, "2   149 0.02 101 1301 1.1E+9  649.349 1.8E+8   432.90 1296.69" },
                    { 7, "2   350 0.02 100 1302 1.2E+9  649.349 1.7E+8   432.90 1296.69" },
                    { 8, "2   275 0.02 100 1302 1.1E+9  432.900 1.5E+8   432.90 1296.69" },
                    { 9, "2   148 0.02 103 1303 7.6E+8  649.851 1.5E+8   433.90 1299.69" },
                    { 10, "2   319 0.02 102 1303 8.8E+8  649.851 1.1E+8   433.90 1299.69" },
                    { 11, "2   230 0.03 101 1303 7.9E+8  433.234 1.2E+8   433.90 1299.69" },
                    { 12, "2    82 0.03 107 1300 4.4E+7  648.846 9.5E+6   648.85 1296.69" },
                    { 13, "2   189 0.03 105 1300 4.6E+7 1297.691 5.4E+6   648.85 1296.69" },
                    { 14, "2   114 0.03 100 1300 5.1E+7  648.846 1.0E+7   648.85 1296.69" },
                    { 15, "2   181 0.04 105  948 2.4E+7  269.161 5.7E+6   513.28 1025.56" },
                    { 16, "2   266 0.04 102 1185 6.0E+7  269.161 4.0E+6   649.85 1298.69" },
                    { 17, "2    64 0.04 104 1029 1.0E+7  513.282 8.2E+6   513.28 1025.56" },
                    { 18, "2   125 0.05 104 1029 1.2E+7  513.281 3.3E+6   513.28 1025.56" },
                    { 19, "2    65 0.05 107 1029 1.2E+7  513.282 8.8E+6   513.28 1025.56" },
                    { 20, "2    80 0.05 115 1302 2.7E+7  649.348 6.7E+6   649.85 1298.69" },
                    { 21, "2   197 0.06 102 1302 3.5E+7 1298.694 3.9E+6   649.85 1298.69" },
                    { 22, "2   155 0.06 100 1302 3.6E+7  649.348 8.4E+6   649.85 1298.69" },
                    { 24, "2   333 0.06 104 1031 1.6E+9  110.071 2.4E+8   432.90 1296.68" },
                    { 25, "2   297 0.07 110 1161 1.1E+9  110.071 1.6E+8   433.90 1299.69" },
                    { 96, "2    94 0.27 107 1302 2.6E+7  649.348 6.7E+6   649.85 1298.69" },
                    { 97, "2   242 0.27 103 1302 3.1E+7 1298.694 3.5E+6   649.85 1298.69" },
                    { 98, "2   168 0.27 103 1302 3.2E+7  649.348 7.5E+6   649.85 1298.69" },
                    { 100, "2   323 0.28 107 1031 1.6E+9  110.071 2.5E+8   432.90 1296.69" }
                },
                _ => throw new Exception("Filename not recognized in GetExpectedDtaOrMgfScanInfo: " + dataFileName)
            };
        }

        /// <summary>
        /// Get the expected scan info for the given file
        /// </summary>
        /// <param name="mzDataFileName"></param>
        /// <returns>Dictionary where Keys are scan number and values are the expected scan info</returns>
        internal static Dictionary<int, string> GetExpectedMzDataScanInfo(string mzDataFileName)
        {
            return mzDataFileName switch
            {
                "SmallTest.mzData" or "SmallTest_Unix.mzData" => new Dictionary<int, string>
                    {
                        { 141, "2   331  3.80   0    0 7.4E+6  655.710 6.0E+5   661.65 CID    Positive False  discrete   MassScan   28%  139" },
                        { 195, "2   249  5.17   0    0 2.4E+6  652.225 2.5E+5   561.41 CID    Positive False  discrete   MassScan   28%  193" },
                        { 210, "2   230  5.56   0    0 1.6E+7  918.667 2.2E+6   927.46 CID    Positive False  discrete   MassScan   28%  208" },
                        { 309, "2   298  8.34   0    0 4.2E+6  680.556 5.0E+5   518.76 CID    Positive False  discrete   MassScan   28%  307" },
                        { 345, "2   243  9.41   0    0 1.7E+6  656.591 1.7E+5   486.97 CID    Positive False  discrete   MassScan   28%  343" },
                        { 750, "2   337 20.58   0    0 1.3E+7  603.953 2.4E+6   669.30 CID    Positive False  discrete   MassScan   28%  748" }
                    },
                "SampleData_myo_excerpt_1.05cv.mzdata" => new Dictionary<int, string>
                    {
                        { 200, "1  2000  5.32   0    0 2.5E+7  618.925 9.4E+5     0.00        Positive False  continuous MassScan    0%    0" },
                        { 201, "2   132  5.34   0    0 1.0E+7  607.606 3.7E+6   619.02 CID    Positive False  discrete   MassScan   28%  199" },
                        { 202, "1   481  5.36   0    0 5.0E+8  464.587 7.6E+7     0.00        Positive False  discrete   MassScan    0%    0" },
                        { 203, "1  2000  5.38   0    0 1.2E+7  371.560 3.1E+5     0.00        Positive False  continuous MassScan    0%    0" },
                        { 204, "2   454  5.40   0    0 2.7E+7  368.343 1.3E+6   371.73 CID    Positive False  discrete   MassScan   28%  202" },
                        { 205, "1   495  5.44   0    0 9.2E+8  464.618 1.6E+8     0.00        Positive False  discrete   MassScan    0%    0" },
                        { 206, "1  2000  5.46   0    0 1.1E+7  631.160 3.5E+5     0.00        Positive False  continuous MassScan    0%    0" },
                        { 207, "2   264  5.47   0    0 1.2E+7  619.871 1.8E+6   631.39 CID    Positive False  discrete   MassScan   28%  205" },
                        { 208, "1   494  5.52   0    0 1.1E+9  464.552 2.2E+8     0.00        Positive False  discrete   MassScan    0%    0" },
                        { 209, "1  2000  5.54   0    0 4.3E+7  927.760 1.4E+6     0.00        Positive False  continuous MassScan    0%    0" },
                        { 210, "2   230  5.56   0    0 1.6E+7  918.667 2.2E+6   927.46 CID    Positive False  discrete   MassScan   28%  208" }
                    },
                _ => throw new Exception("Filename not recognized in GetExpectedMzDataScanInfo: " + mzDataFileName)
            };
        }

        /// <summary>
        /// Get the expected scan info for the given file
        /// </summary>
        /// <param name="mzXmlFileName"></param>
        /// <returns>Dictionary where Keys are scan number and values are the expected scan info</returns>
        internal static Dictionary<int, string> GetExpectedMzXmlScanInfo(string mzXmlFileName)
        {
            var angiotensinAllScans = new Dictionary<int, string>
            {
                { 1200, "2   111 3.40 102 1306 5.2E+7  648.846 1.0E+7   648.85 ETD+SA + True  discrete " },
                { 1201, "2   321 3.41 108 1033 5.7E+7  269.161 3.7E+6   649.85 HCD    + True  discrete " },
                { 1202, "2   226 3.41 110 1031 2.1E+7  110.071 2.3E+6   583.30 HCD    + True  discrete " },
                { 1203, "2    86 3.41 101 1302 2.7E+7  649.348 6.8E+6   649.85 ETD    + True  discrete " },
                { 1204, "2   205 3.42 102 1302 3.1E+7 1298.694 3.6E+6   649.85 ETD    + True  discrete " },
                { 1205, "2   142 3.42 111 1302 3.0E+7  649.348 6.9E+6   649.85 ETD+SA + True  discrete " },
                { 1206, "2    86 3.42 101 1169 1.0E+7  583.299 2.1E+6   583.30 ETD    + True  discrete " },
                { 1207, "2   148 3.43 108 1169 1.1E+7 1138.603 2.0E+6   583.30 ETD    + True  discrete " },
                { 1208, "2   114 3.43 119 1169 1.2E+7  583.299 2.5E+6   583.30 ETD+SA + True  discrete " },
                { 1209, "1  3711 3.43 347 2020 2.0E+9  432.900 6.8E+8     0.00        + False discrete " },
                { 1210, "2   298 3.43 110 1045 1.9E+9  110.071 2.9E+8   432.90 HCD    + True  discrete " },
                { 1211, "2   249 3.44 110 1304 1.2E+9  110.071 1.9E+8   433.90 HCD    + True  discrete " },
                { 1212, "2   271 3.44 103 1166 8.8E+7  784.410 5.5E+6   648.85 HCD    + True  discrete " },
                { 1213, "2   152 3.44 101 1302 1.3E+9  649.349 2.2E+8   432.90 ETD    + True  discrete " },
                { 1214, "2   285 3.45 101 1302 1.2E+9  649.349 1.7E+8   432.90 ETD    + True  discrete " },
                { 1215, "2   238 3.45 101 1302 1.1E+9  432.900 1.5E+8   432.90 ETD+SA + True  discrete " },
                { 1216, "2   135 3.45 108 1303 7.0E+8  649.851 1.3E+8   433.90 ETD    + True  discrete " },
                { 1217, "2   287 3.45 100 1303 9.5E+8  649.850 1.2E+8   433.90 ETD    + True  discrete " },
                { 1218, "2   215 3.45 113 1303 8.5E+8  433.234 1.2E+8   433.90 ETD+SA + True  discrete " },
                { 1219, "2    85 3.46 113 1300 4.5E+7  648.846 8.8E+6   648.85 ETD    + True  discrete " },
                { 1220, "2   199 3.46 103 1300 4.7E+7 1297.691 5.6E+6   648.85 ETD    + True  discrete " },
                { 1221, "2   106 3.46 102 1300 4.5E+7  648.846 8.7E+6   648.85 ETD+SA + True  discrete " },
                { 1222, "2   228 3.47 105 1031 1.9E+7  110.071 2.2E+6   583.30 HCD    + True  discrete " },
                { 1223, "2   298 3.47 105 1185 5.6E+7  269.161 3.5E+6   649.85 HCD    + True  discrete " },
                { 1224, "2    68 3.47 104 1169 1.1E+7  583.299 2.2E+6   583.30 ETD    + True  discrete " },
                { 1225, "2   158 3.47 110 1169 1.3E+7 1138.603 2.2E+6   583.30 ETD    + True  discrete " },
                { 1226, "2    98 3.48 101 1169 1.2E+7  583.298 2.5E+6   583.30 ETD+SA + True  discrete " },
                { 1227, "2    83 3.48 109 1302 2.6E+7  649.348 6.4E+6   649.85 ETD    + True  discrete " },
                { 1228, "2   212 3.48 102 1302 3.1E+7 1298.694 3.5E+6   649.85 ETD    + True  discrete " },
                { 1229, "2   145 3.49 116 1302 3.0E+7  649.348 6.6E+6   649.85 ETD+SA + True  discrete " },
                { 1230, "1  3775 3.49 347 2020 1.9E+9  432.900 6.2E+8     0.00        + False discrete " },
                { 1231, "2   300 3.49 106 1031 1.7E+9  110.071 2.6E+8   432.90 HCD    + True  discrete " }
            };

            var angiotensinAllScansCentroided = new Dictionary<int, string>();

            foreach (var item in angiotensinAllScans)
            {
                angiotensinAllScansCentroided.Add(item.Key, item.Value);
            }

            // Override the values for centroided MS1 scans 1209 and 1230
            angiotensinAllScansCentroided[1209] = "1   250 3.43 355 1822 2.0E+9  432.900 6.8E+8     0.00        + True  discrete ";
            angiotensinAllScansCentroided[1230] = "1   259 3.49 353 1964 1.9E+9  432.900 6.2E+8     0.00        + True  discrete ";

            return mzXmlFileName switch
            {
                "HCC-38_ETciD_EThcD_4xdil_20uL_3hr_3_08Jan16_Pippin_15-08-53.mzXML" => new Dictionary<int, string>
                {
                    { 16121, "1 11888 47.68 347 1566 1.9E+9  503.565 3.4E+8     0.00        + False discrete " },
                    { 16122, "2   490 47.68 116  805 1.6E+6  550.309 2.1E+5   403.22 CID    + True  discrete " },
                    { 16123, "2   785 47.68 155 1374 5.5E+5  506.272 4.9E+4   538.84 CID    + True  discrete " },
                    { 16124, "2   996 47.68 222 1904 7.8E+5  737.530 7.0E+4   775.94 CID    + True  discrete " },
                    { 16125, "2   703 47.68 159 1626 2.1E+5  808.486 2.2E+4   538.84 ETD    + True  discrete " },
                    { 16126, "2   753 47.68 231 1625 1.4E+5  536.209 9.0E+3   538.84 ETD+SA + True  discrete " },
                    { 16127, "2   872 47.68 129 1626 1.3E+5  808.487 1.4E+4   538.84 ETD    + True  discrete " },
                    { 16128, "2   972 47.69 234 1677 4.4E+5  805.579 2.3E+4   835.88 CID    + True  discrete " },
                    { 16129, "2   937 47.69 286 1938 3.4E+5  938.679 2.9E+4   987.40 CID    + True  discrete " },
                    { 16130, "2   622 47.69 120  847 2.7E+5  411.977 1.2E+4   421.26 CID    + True  discrete " },
                    { 16131, "2    29 47.69 985 1981 2.1E+4  984.504 9.5E+3   987.40 ETD    + True  discrete " },
                    { 16132, "2   239 47.69 131  850 1.2E+4  421.052 6.8E+2   421.26 ETD    + True  discrete " },
                    { 16133, "2   280 47.70 260  853 1.5E+4  421.232 1.2E+3   421.26 ETD+SA + True  discrete " },
                    { 16134, "2   343 47.70 136  849 1.4E+4  838.487 7.5E+2   421.26 ETD    + True  discrete " },
                    { 16135, "2    38 47.70 882 1980 2.1E+4  984.498 9.2E+3   987.40 ETD+SA + True  discrete " },
                    { 16136, "2    93 47.71 175 1979 2.3E+4  984.491 9.4E+3   987.40 ETD    + True  discrete " },
                    { 16137, "2  1172 47.71 343 2000 3.5E+5 1536.038 4.7E+3  1240.76 CID    + True  discrete " },
                    { 16138, "2   925 47.72 245 1739 2.9E+5  826.095 2.5E+4   874.84 CID    + True  discrete " },
                    { 16139, "2    96 47.72 863 1756 1.6E+4  875.506 2.1E+3   874.84 ETD    + True  discrete " },
                    { 16140, "2   174 47.72 530 1756 1.8E+4 1749.846 2.0E+3   874.84 ETD+SA + True  discrete " },
                    { 16141, "2   240 47.72 304 1757 1.6E+4  874.664 1.6E+3   874.84 ETD    + True  discrete " },
                    { 16142, "1 13501 47.73 347 1566 1.3E+9  503.565 1.9E+8     0.00        + False discrete " },
                    { 16143, "2   651 47.73 137  971 6.5E+5  444.288 6.4E+4   485.28 CID    + True  discrete " },
                    { 16144, "2   512 47.73 110 1106 5.0E+5  591.309 4.0E+4   387.41 CID    + True  discrete " },
                    { 16145, "2   817 47.73 171 1519 4.0E+5  567.912 2.8E+4   606.29 CID    + True  discrete " },
                    { 16146, "2   573 47.73 109  767 1.9E+5  532.308 3.4E+4   379.72 CID    + True  discrete " },
                    { 16147, "2   813 47.74 131 1830 3.8E+5  603.095 3.1E+4   606.29 ETD    + True  discrete " },
                    { 16148, "2   882 47.74 278 1829 1.5E+5  603.076 1.3E+4   606.29 ETD+SA + True  discrete " },
                    { 16149, "2  1121 47.74 129 1829 1.6E+5  603.027 1.1E+4   606.29 ETD    + True  discrete " },
                    { 16150, "2   625 47.74 100  908 3.8E+5  418.536 1.2E+5   365.88 CID    + True  discrete " },
                    { 16151, "2   679 47.75 159 1241 2.8E+5  501.523 4.3E+4   548.54 CID    + True  discrete " },
                    { 16152, "2  1171 47.75 345 1999 1.8E+5  848.497 2.2E+3  1210.06 CID    + True  discrete " },
                    { 16153, "2   600 47.75 130 1656 1.3E+5  548.396 1.3E+4   548.54 ETD    + True  discrete " },
                    { 16154, "2   566 47.75 259 1656 4.2E+4  548.450 4.2E+3   548.54 ETD+SA + True  discrete " },
                    { 16155, "2   753 47.76 129 1655 4.2E+4  550.402 3.6E+3   548.54 ETD    + True  discrete " },
                    { 16156, "2  1120 47.76 352 1999 1.5E+5 1491.872 1.0E+4  1197.16 CID    + True  discrete " },
                    { 16157, "2   714 47.76 136  941 2.2E+5  420.689 2.2E+4   469.71 CID    + True  discrete " },
                    { 16158, "2   692 47.76 323 1998 1.3E+5 1100.042 3.5E+3  1132.02 CID    + True  discrete " },
                    { 16159, "2   667 47.76 133  933 1.9E+5  445.117 2.7E+4   462.15 CID    + True  discrete " },
                    { 16160, "2   694 47.77 152 1302 3.4E+5  539.065 6.0E+4   544.84 CID    + True  discrete " },
                    { 16161, "2   737 47.77 167 1161 2.8E+5  541.462 6.0E+4   590.28 CID    + True  discrete " },
                    { 16162, "2   288 47.77 159 1190 8.4E+4 1180.615 5.1E+3   590.28 ETD    + True  discrete " },
                    { 16163, "2   305 47.77 363 1190 1.8E+4 1184.614 9.0E+2   590.28 ETD+SA + True  discrete " },
                    { 16164, "2   372 47.77 131 1190 1.7E+4 1184.644 8.7E+2   590.28 ETD    + True  discrete " },
                    { 16165, "1 13816 47.78 347 1566 1.2E+9  503.565 1.6E+8     0.00        + False discrete " }
                },
                "QC_Mam_16_01_125ng_2pt0-IT22_Run-A_16Oct17_Pippin_AQ_17-10-01.mzXML" => new Dictionary<int, string>
                {
                    { 20500, "2    46 41.90 110  828 2.6E+6  416.224 2.9E+5   352.54 HCD    + True  discrete " },
                    { 20501, "1  3440 41.90 347 1818 1.3E+9  599.293 1.0E+8     0.00        + False discrete " },
                    { 20502, "2    72 41.90 120 1133 3.9E+6 1063.568 2.2E+5   624.30 HCD    + True  discrete " },
                    { 20503, "2    54 41.90 110 1051 3.1E+6  637.336 3.0E+5   637.69 HCD    + True  discrete " },
                    { 20504, "1  3596 41.90 347 1818 1.3E+9  554.304 9.7E+7     0.00        + False discrete " },
                    { 20505, "2    95 41.90 110 1107 8.4E+6  911.447 1.1E+6   611.29 HCD    + True  discrete " },
                    { 20506, "2    60 41.90 120  916 6.9E+6  207.112 6.4E+5   550.31 HCD    + True  discrete " },
                    { 20507, "1  3591 41.91 347 1818 1.2E+9  554.304 9.0E+7     0.00        + False discrete " },
                    { 20508, "2    48 41.91 129 1044 3.9E+6  887.511 4.0E+5   621.35 HCD    + True  discrete " },
                    { 20509, "2    78 41.91 110  966 6.0E+6  445.242 5.1E+5   532.29 HCD    + True  discrete " },
                    { 20510, "1  3608 41.91 347 1818 1.3E+9  554.304 9.4E+7     0.00        + False discrete " },
                    { 20511, "2   106 41.91 110  902 1.1E+7  120.081 8.8E+5   473.77 HCD    + True  discrete " },
                    { 20512, "2    65 41.91 129 1192 8.4E+6  891.457 1.5E+6   694.86 HCD    + True  discrete " },
                    { 20513, "2    99 41.91 110  878 5.1E+6  777.422 4.6E+5   457.74 HCD    + True  discrete " },
                    { 20514, "1  3767 41.91 347 1818 1.4E+9  554.305 1.0E+8     0.00        + False discrete " },
                    { 20515, "2    25 41.91 175 1102 8.0E+6  859.948 3.1E+6   859.94 HCD    + True  discrete " },
                    { 20516, "1  3619 41.92 347 1818 1.4E+9  554.305 1.1E+8     0.00        + False discrete " },
                    { 20517, "2    79 41.92 110  811 4.2E+6  697.397 3.0E+5   442.91 HCD    + True  discrete " },
                    { 20518, "2    43 41.92 129 1350 3.7E+6  999.457 3.8E+5   737.36 HCD    + True  discrete " },
                    { 20519, "2   101 41.92 120 1083 8.9E+6  742.409 5.7E+5   614.77 HCD    + True  discrete " },
                    { 20520, "1  3667 41.92 347 1818 1.7E+9  554.305 1.3E+8     0.00        + False discrete " }
                },
                "Shew_246a_LCQa_15Oct04_Andro_0904-2_4-20.mzXML" => new Dictionary<int, string>
                {
                    { 1513, "1   851 44.57 410 2000 6.3E+8 1089.978 1.2E+7     0.00        + True  discrete " },
                    { 1514, "2   109 44.60 282 1600 5.0E+6  528.128 7.2E+5   884.41 CID    + True  discrete " },
                    { 1515, "2   290 44.63 336 1988 2.6E+7 1327.414 6.0E+6  1147.67 CID    + True  discrete " },
                    { 1516, "2   154 44.66 462 1986 7.6E+5 1251.554 3.7E+4  1492.90 CID    + True  discrete " },
                    { 1517, "1   887 44.69 420 1999 8.0E+8 1147.613 1.0E+7     0.00        + True  discrete " },
                    { 1518, "2   190 44.71 408 1994 4.6E+6 1844.618 2.7E+5  1421.21 CID    + True  discrete " },
                    { 1519, "2   165 44.74 450 1991 6.0E+6 1842.547 6.9E+5  1419.24 CID    + True  discrete " },
                    { 1520, "2   210 44.77 302 1917 1.5E+6 1361.745 4.2E+4  1014.93 CID    + True  discrete " },
                    { 1521, "1   860 44.80 410 1998 6.9E+8 1126.627 2.9E+7     0.00        + True  discrete " }
                },
                "Angiotensin_AllScans.mzXML" => angiotensinAllScans,
                "Angiotensin_AllScans_zlib_compression.mzXML" => angiotensinAllScans,
                "Angiotensin_AllScans_centroided.mzXML" => angiotensinAllScansCentroided,
                "Angiotensin_Excerpt_NoIndex.mzXML" => new Dictionary<int, string>
                {
                    {  1, "1  7997 0.00 347 2020 1.4E+9  432.900 4.5E+8     0.00        + False discrete " },
                    {  2, "1   595 0.01 347 2020 2.5E+9  432.900 9.6E+8     0.00        + False discrete " },
                    {  3, "2   337 0.01 109 1290 1.6E+9  110.071 2.3E+8   432.90 HCD    + True  discrete " },
                    {  4, "2   280 0.01 108 1046 1.0E+9  110.071 1.6E+8   433.90 HCD    + True  discrete " },
                    {  5, "2   295 0.01 101 1185 8.8E+7  784.410 5.6E+6   648.85 HCD    + True  discrete " },
                    {  6, "2   149 0.02 101 1301 1.1E+9  649.349 1.8E+8   432.90 ETD    + True  discrete " },
                    {  7, "2   350 0.02 100 1302 1.2E+9  649.349 1.7E+8   432.90 ETD    + True  discrete " },
                    {  8, "2   275 0.02 100 1302 1.1E+9  432.900 1.5E+8   432.90 ETD+SA + True  discrete " },
                    {  9, "2   148 0.02 103 1303 7.6E+8  649.851 1.5E+8   433.90 ETD    + True  discrete " },
                    { 10, "2   319 0.02 102 1303 8.8E+8  649.851 1.1E+8   433.90 ETD    + True  discrete " },
                    { 11, "2   230 0.03 101 1303 7.9E+8  433.234 1.2E+8   433.90 ETD+SA + True  discrete " },
                    { 12, "2    82 0.03 107 1300 4.4E+7  648.846 9.5E+6   648.85 ETD    + True  discrete " },
                    { 13, "2   189 0.03 105 1300 4.6E+7 1297.691 5.4E+6   648.85 ETD    + True  discrete " },
                    { 14, "2   114 0.03 100 1300 5.1E+7  648.846 1.0E+7   648.85 ETD+SA + True  discrete " },
                    { 15, "2   181 0.04 105  948 2.4E+7  269.161 5.7E+6   513.28 HCD    + True  discrete " },
                    { 16, "2   266 0.04 102 1185 6.0E+7  269.161 4.0E+6   649.85 HCD    + True  discrete " },
                    { 17, "2    64 0.04 104 1029 1.0E+7  513.282 8.2E+6   513.28 ETD    + True  discrete " },
                    { 18, "2   125 0.05 104 1029 1.2E+7  513.281 3.3E+6   513.28 ETD    + True  discrete " },
                    { 19, "2    65 0.05 107 1029 1.2E+7  513.282 8.8E+6   513.28 ETD+SA + True  discrete " },
                    { 20, "2    80 0.05 115 1302 2.7E+7  649.348 6.7E+6   649.85 ETD    + True  discrete " },
                    { 21, "2   197 0.06 102 1302 3.5E+7 1298.694 3.9E+6   649.85 ETD    + True  discrete " },
                    { 22, "2   155 0.06 100 1302 3.6E+7  649.348 8.4E+6   649.85 ETD+SA + True  discrete " },
                    { 23, "1  3785 0.06 347 2020 1.9E+9  432.900 6.4E+8     0.00        + False discrete " },
                    { 24, "2   333 0.06 104 1031 1.6E+9  110.071 2.4E+8   432.90 HCD    + True  discrete " },
                    { 25, "2   297 0.07 110 1161 1.1E+9  110.071 1.6E+8   433.90 HCD    + True  discrete " },
                    { 26, "2   351 0.07 103 1299 1.7E+7  659.837 3.4E+6   659.84 HCD    + True  discrete " },
                    { 27, "2   147 0.07 101 1301 1.4E+9  649.349 2.2E+8   432.90 ETD    + True  discrete " },
                    { 28, "2   270 0.08 103 1302 1.0E+9  649.349 1.5E+8   432.90 ETD    + True  discrete " },
                    { 29, "2   253 0.08 101 1302 1.2E+9  432.900 1.8E+8   432.90 ETD+SA + True  discrete " },
                    { 30, "2   139 0.08 101 1303 7.5E+8  649.851 1.4E+8   433.90 ETD    + True  discrete " },
                    { 31, "2   293 0.08 100 1303 9.1E+8  649.851 1.2E+8   433.90 ETD    + True  discrete " },
                    { 32, "2   215 0.09 112 1303 8.6E+8  433.234 1.3E+8   433.90 ETD+SA + True  discrete " },
                    { 33, "2    77 0.09 103 1322 8.4E+6  659.837 1.8E+6   659.84 ETD    + True  discrete " },
                    { 34, "2   180 0.09 100 1322 9.7E+6  659.837 1.4E+6   659.84 ETD    + True  discrete " },
                    { 35, "2   149 0.10 102 1322 9.5E+6  659.837 1.9E+6   659.84 ETD+SA + True  discrete " }
                },
                _ => throw new Exception("Filename not recognized in GetExpectedMzXmlScanInfo: " + mzXmlFileName)
            };
        }

        private static void WriteScanInfoColumnNames(MsDataFileReaderBaseClass reader)
        {
            switch (reader)
            {
                case DtaTextFileReader or MgfFileReader:
                    Console.WriteLine(
                        "{0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10}",
                        "Scan", "MSLevel",
                        "NumPeaks", "RetentionTime",
                        "LowMass", "HighMass", "TotalIonCurrent",
                        "BasePeakMZ", "BasePeakIntensity",
                        "ParentIonMZ", "ParentIonMH");

                    break;

                case MzXMLFileAccessor or MzXMLFileReader:
                    Console.WriteLine(
                        "{0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10} {11} {12} {13} {14}",
                        "Scan", "MSLevel",
                        "NumPeaks", "RetentionTime",
                        "LowMass", "HighMass", "TotalIonCurrent",
                        "BasePeakMZ", "BasePeakIntensity",
                        "ParentIonMZ", "ActivationMethod",
                        "Polarity", "IsCentroided",
                        "SpectrumType", "FilterLine");

                    break;

                case MzDataFileAccessor or MzDataFileReader:
                    Console.WriteLine(
                        "{0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10} {11} {12} {13} {14} {15} {16} {17}",
                        "Scan", "MSLevel",
                        "NumPeaks", "RetentionTime",
                        "LowMass", "HighMass", "TotalIonCurrent",
                        "BasePeakMZ", "BasePeakIntensity",
                        "ParentIonMZ", "CollisionMethod",
                        "Polarity", "IsCentroided",
                        "SpectrumType", "ScanMode",
                        "CollisionEnergy", "CollisionEnergyUnits",
                        "ParentIonSpectrumID");

                    break;

                default:
                    Console.WriteLine("Unrecognized reader type");
                    break;
            }
        }
    }
}
