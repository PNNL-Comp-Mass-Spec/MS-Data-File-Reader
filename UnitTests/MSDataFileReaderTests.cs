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

                if (spectrumInfo.MSLevel > 1)
                    scanCountMS2++;
                else
                    scanCountMS1++;
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
                    {
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

                        scanSummary =
                            string.Format(
                                "{0,3} {1} {2,5} {3:F2} {4,3:0} {5,4:0} {6:0.0E+0} {7,8:F3} {8:0.0E+0} {9,8:F2} {10,-6} {11} {12} {13} {14}",
                                spectrumInfo.ScanNumber, spectrumInfo.MSLevel,
                                spectrumInfo.PeaksCount, spectrumInfo.RetentionTimeMin,
                                spectrumInfo.MzRangeStart, spectrumInfo.MzRangeEnd,
                                spectrumInfo.TotalIonCurrent, spectrumInfo.BasePeakMZ, spectrumInfo.BasePeakIntensity, spectrumInfo.ParentIonMZ,
                                mzXmlSpectrum.ActivationMethod,
                                spectrumInfo.Polarity, spectrumInfo.Centroided,
                                mzXmlSpectrum.SpectrumType, filterLine);

                        break;
                    }

                case SpectrumInfoMzData mzDataSpectrum:
                    scanSummary =
                        string.Format(
                            "{0,3} {1} {2,5} {3:F2} {4,3:0} {5,4:0} {6:0.0E+0} {7,8:F3} {8:0.0E+0} {9,8:F2} {10} {11} {12} {13} {14} {15:F2} {16} {17}",
                            spectrumInfo.ScanNumber, spectrumInfo.MSLevel,
                            spectrumInfo.PeaksCount, spectrumInfo.RetentionTimeMin,
                            spectrumInfo.MzRangeStart, spectrumInfo.MzRangeEnd,
                            spectrumInfo.TotalIonCurrent, spectrumInfo.BasePeakMZ, spectrumInfo.BasePeakIntensity,
                            spectrumInfo.ParentIonMZ, mzDataSpectrum.CollisionMethod,
                            spectrumInfo.Polarity, spectrumInfo.Centroided,
                            mzDataSpectrum.SpectrumType, mzDataSpectrum.ScanMode,
                            mzDataSpectrum.CollisionEnergy, mzDataSpectrum.CollisionEnergyUnits,
                            mzDataSpectrum.ParentIonSpectrumID);

                    break;

                case SpectrumInfoMsMsText msmsSpectrum:
                    scanSummary =
                        string.Format(
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

        /// <summary>
        /// Get the expected scan info for the given file
        /// </summary>
        /// <param name="mzXmlFileName"></param>
        /// <returns>Dictionary where Keys are scan number and values are the expected scan info</returns>
        internal static Dictionary<int, string> GetExpectedMzXmlScanInfo(string mzXmlFileName)
        {
            switch (mzXmlFileName)
            {
                case "HCC-38_ETciD_EThcD_4xdil_20uL_3hr_3_08Jan16_Pippin_15-08-53.mzXML":
                    return new Dictionary<int, string>
                    {
                        { 16121, "1 11888 47.68 347 1566 1.9E+9  503.565 3.4E+8     0.00        + False discrete " },
                        { 16122, "2   490 47.68 116  805 1.6E+6  550.309 2.1E+5   403.22 CID    + True discrete " },
                        { 16123, "2   785 47.68 155 1374 5.5E+5  506.272 4.9E+4   538.84 CID    + True discrete " },
                        { 16124, "2   996 47.68 222 1904 7.8E+5  737.530 7.0E+4   775.94 CID    + True discrete " },
                        { 16125, "2   703 47.68 159 1626 2.1E+5  808.486 2.2E+4   538.84 ETD    + True discrete " },
                        { 16126, "2   753 47.68 231 1625 1.4E+5  536.209 9.0E+3   538.84 ETD+SA + True discrete " },
                        { 16127, "2   872 47.68 129 1626 1.3E+5  808.487 1.4E+4   538.84 ETD    + True discrete " },
                        { 16128, "2   972 47.69 234 1677 4.4E+5  805.579 2.3E+4   835.88 CID    + True discrete " },
                        { 16129, "2   937 47.69 286 1938 3.4E+5  938.679 2.9E+4   987.40 CID    + True discrete " },
                        { 16130, "2   622 47.69 120  847 2.7E+5  411.977 1.2E+4   421.26 CID    + True discrete " },
                        { 16131, "2    29 47.69 985 1981 2.1E+4  984.504 9.5E+3   987.40 ETD    + True discrete " },
                        { 16132, "2   239 47.69 131  850 1.2E+4  421.052 6.8E+2   421.26 ETD    + True discrete " },
                        { 16133, "2   280 47.70 260  853 1.5E+4  421.232 1.2E+3   421.26 ETD+SA + True discrete " },
                        { 16134, "2   343 47.70 136  849 1.4E+4  838.487 7.5E+2   421.26 ETD    + True discrete " },
                        { 16135, "2    38 47.70 882 1980 2.1E+4  984.498 9.2E+3   987.40 ETD+SA + True discrete " },
                        { 16136, "2    93 47.71 175 1979 2.3E+4  984.491 9.4E+3   987.40 ETD    + True discrete " },
                        { 16137, "2  1172 47.71 343 2000 3.5E+5 1536.038 4.7E+3  1240.76 CID    + True discrete " },
                        { 16138, "2   925 47.72 245 1739 2.9E+5  826.095 2.5E+4   874.84 CID    + True discrete " },
                        { 16139, "2    96 47.72 863 1756 1.6E+4  875.506 2.1E+3   874.84 ETD    + True discrete " },
                        { 16140, "2   174 47.72 530 1756 1.8E+4 1749.846 2.0E+3   874.84 ETD+SA + True discrete " },
                        { 16141, "2   240 47.72 304 1757 1.6E+4  874.664 1.6E+3   874.84 ETD    + True discrete " },
                        { 16142, "1 13501 47.73 347 1566 1.3E+9  503.565 1.9E+8     0.00        + False discrete " },
                        { 16143, "2   651 47.73 137  971 6.5E+5  444.288 6.4E+4   485.28 CID    + True discrete " },
                        { 16144, "2   512 47.73 110 1106 5.0E+5  591.309 4.0E+4   387.41 CID    + True discrete " },
                        { 16145, "2   817 47.73 171 1519 4.0E+5  567.912 2.8E+4   606.29 CID    + True discrete " },
                        { 16146, "2   573 47.73 109  767 1.9E+5  532.308 3.4E+4   379.72 CID    + True discrete " },
                        { 16147, "2   813 47.74 131 1830 3.8E+5  603.095 3.1E+4   606.29 ETD    + True discrete " },
                        { 16148, "2   882 47.74 278 1829 1.5E+5  603.076 1.3E+4   606.29 ETD+SA + True discrete " },
                        { 16149, "2  1121 47.74 129 1829 1.6E+5  603.027 1.1E+4   606.29 ETD    + True discrete " },
                        { 16150, "2   625 47.74 100  908 3.8E+5  418.536 1.2E+5   365.88 CID    + True discrete " },
                        { 16151, "2   679 47.75 159 1241 2.8E+5  501.523 4.3E+4   548.54 CID    + True discrete " },
                        { 16152, "2  1171 47.75 345 1999 1.8E+5  848.497 2.2E+3  1210.06 CID    + True discrete " },
                        { 16153, "2   600 47.75 130 1656 1.3E+5  548.396 1.3E+4   548.54 ETD    + True discrete " },
                        { 16154, "2   566 47.75 259 1656 4.2E+4  548.450 4.2E+3   548.54 ETD+SA + True discrete " },
                        { 16155, "2   753 47.76 129 1655 4.2E+4  550.402 3.6E+3   548.54 ETD    + True discrete " },
                        { 16156, "2  1120 47.76 352 1999 1.5E+5 1491.872 1.0E+4  1197.16 CID    + True discrete " },
                        { 16157, "2   714 47.76 136  941 2.2E+5  420.689 2.2E+4   469.71 CID    + True discrete " },
                        { 16158, "2   692 47.76 323 1998 1.3E+5 1100.042 3.5E+3  1132.02 CID    + True discrete " },
                        { 16159, "2   667 47.76 133  933 1.9E+5  445.117 2.7E+4   462.15 CID    + True discrete " },
                        { 16160, "2   694 47.77 152 1302 3.4E+5  539.065 6.0E+4   544.84 CID    + True discrete " },
                        { 16161, "2   737 47.77 167 1161 2.8E+5  541.462 6.0E+4   590.28 CID    + True discrete " },
                        { 16162, "2   288 47.77 159 1190 8.4E+4 1180.615 5.1E+3   590.28 ETD    + True discrete " },
                        { 16163, "2   305 47.77 363 1190 1.8E+4 1184.614 9.0E+2   590.28 ETD+SA + True discrete " },
                        { 16164, "2   372 47.77 131 1190 1.7E+4 1184.644 8.7E+2   590.28 ETD    + True discrete " },
                        { 16165, "1 13816 47.78 347 1566 1.2E+9  503.565 1.6E+8     0.00        + False discrete "}
                    };

                case "QC_Mam_16_01_125ng_2pt0-IT22_Run-A_16Oct17_Pippin_AQ_17-10-01.mzXML":
                    return new Dictionary<int, string>
                    {
                        { 20500, "2    46 41.90 110  828 2.6E+6  416.224 2.9E+5   352.54 HCD    + True discrete " },
                        { 20501, "1  3440 41.90 347 1818 1.3E+9  599.293 1.0E+8     0.00        + False discrete " },
                        { 20502, "2    72 41.90 120 1133 3.9E+6 1063.568 2.2E+5   624.30 HCD    + True discrete " },
                        { 20503, "2    54 41.90 110 1051 3.1E+6  637.336 3.0E+5   637.69 HCD    + True discrete " },
                        { 20504, "1  3596 41.90 347 1818 1.3E+9  554.304 9.7E+7     0.00        + False discrete " },
                        { 20505, "2    95 41.90 110 1107 8.4E+6  911.447 1.1E+6   611.29 HCD    + True discrete " },
                        { 20506, "2    60 41.90 120  916 6.9E+6  207.112 6.4E+5   550.31 HCD    + True discrete " },
                        { 20507, "1  3591 41.91 347 1818 1.2E+9  554.304 9.0E+7     0.00        + False discrete " },
                        { 20508, "2    48 41.91 129 1044 3.9E+6  887.511 4.0E+5   621.35 HCD    + True discrete " },
                        { 20509, "2    78 41.91 110  966 6.0E+6  445.242 5.1E+5   532.29 HCD    + True discrete " },
                        { 20510, "1  3608 41.91 347 1818 1.3E+9  554.304 9.4E+7     0.00        + False discrete " },
                        { 20511, "2   106 41.91 110  902 1.1E+7  120.081 8.8E+5   473.77 HCD    + True discrete " },
                        { 20512, "2    65 41.91 129 1192 8.4E+6  891.457 1.5E+6   694.86 HCD    + True discrete " },
                        { 20513, "2    99 41.91 110  878 5.1E+6  777.422 4.6E+5   457.74 HCD    + True discrete " },
                        { 20514, "1  3767 41.91 347 1818 1.4E+9  554.305 1.0E+8     0.00        + False discrete " },
                        { 20515, "2    25 41.91 175 1102 8.0E+6  859.948 3.1E+6   859.94 HCD    + True discrete " },
                        { 20516, "1  3619 41.92 347 1818 1.4E+9  554.305 1.1E+8     0.00        + False discrete " },
                        { 20517, "2    79 41.92 110  811 4.2E+6  697.397 3.0E+5   442.91 HCD    + True discrete " },
                        { 20518, "2    43 41.92 129 1350 3.7E+6  999.457 3.8E+5   737.36 HCD    + True discrete " },
                        { 20519, "2   101 41.92 120 1083 8.9E+6  742.409 5.7E+5   614.77 HCD    + True discrete " },
                        { 20520, "1  3667 41.92 347 1818 1.7E+9  554.305 1.3E+8     0.00        + False discrete " }
                    };

                case "Shew_246a_LCQa_15Oct04_Andro_0904-2_4-20.mzXML":
                    return new Dictionary<int, string>
                    {
                        { 1513, "1   851 44.57 410 2000 6.3E+8 1089.978 1.2E+7     0.00        + True discrete " },
                        { 1514, "2   109 44.60 282 1600 5.0E+6  528.128 7.2E+5   884.41 CID    + True discrete " },
                        { 1515, "2   290 44.63 336 1988 2.6E+7 1327.414 6.0E+6  1147.67 CID    + True discrete " },
                        { 1516, "2   154 44.66 462 1986 7.6E+5 1251.554 3.7E+4  1492.90 CID    + True discrete " },
                        { 1517, "1   887 44.69 420 1999 8.0E+8 1147.613 1.0E+7     0.00        + True discrete " },
                        { 1518, "2   190 44.71 408 1994 4.6E+6 1844.618 2.7E+5  1421.21 CID    + True discrete " },
                        { 1519, "2   165 44.74 450 1991 6.0E+6 1842.547 6.9E+5  1419.24 CID    + True discrete " },
                        { 1520, "2   210 44.77 302 1917 1.5E+6 1361.745 4.2E+4  1014.93 CID    + True discrete " },
                        { 1521, "1   860 44.80 410 1998 6.9E+8 1126.627 2.9E+7     0.00        + True discrete " }
                    };
            }

            throw new Exception("Filename not recognized in GetExpectedMzXmlScanInfo: " + mzXmlFileName);
        }

        private static void WriteScanInfoColumnNames(MsDataFileReaderBaseClass reader)
        {
            if (reader is DtaTextFileReader or MgfFileReader)
            {
                Console.WriteLine(
                    "{0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10}",
                    "Scan", "MSLevel",
                    "NumPeaks", "RetentionTime",
                    "LowMass", "HighMass", "TotalIonCurrent",
                    "BasePeakMZ", "BasePeakIntensity",
                    "ParentIonMZ", "ParentIonMH");
            }
            else if (reader is MzXMLFileAccessor or MzXMLFileReader)
            {
                Console.WriteLine(
                    "{0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10} {11} {12} {13} {14}",
                    "Scan", "MSLevel",
                    "NumPeaks", "RetentionTime",
                    "LowMass", "HighMass", "TotalIonCurrent",
                    "BasePeakMZ", "BasePeakIntensity",
                    "ParentIonMZ", "ActivationMethod",
                    "Polarity", "IsCentroided",
                    "SpectrumType", "FilterLine");
            }
            else if (reader is MzDataFileAccessor or MzDataFileReader)
            {
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
            }
            else
            {
                Console.WriteLine("Unrecognized reader type");
            }
        }
    }
}
