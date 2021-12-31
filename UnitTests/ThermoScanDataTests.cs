using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSDataFileReader;
using NUnit.Framework;
using PRISM;
using ThermoRawFileReader;

namespace MSDataFileReaderUnitTests
{
    [TestFixture]
    public class ThermoScanDataTests
    {
        // Ignore Spelling: cid, etd, hcd, xxx

        [Test]
        [TestCase("Shew_246a_LCQa_15Oct04_Andro_0904-2_4-20.mzXML")]
        [TestCase("HCC-38_ETciD_EThcD_4xdil_20uL_3hr_3_08Jan16_Pippin_15-08-53.mzXML")]
        [TestCase("HCC-38_ETciD_EThcD_07Jan16_Pippin_15-08-53.mzXML")]
        [TestCase("MZ0210MnxEF889ETD.mzXML")]
        [TestCase("QC_Mam_16_01_125ng_2pt0-IT22_Run-A_16Oct17_Pippin_AQ_17-10-01.mzXML")]
        public void TestGetCollisionEnergy(string mzXmlFileName)
        {
            // Keys in this Dictionary are filename, values are Collision Energies by scan
            var expectedData = new Dictionary<string, Dictionary<int, List<double>>>();

            var ce30 = new List<double> { 30.00 };
            var ce45 = new List<double> { 45.00 };
            var ce20_120 = new List<double> { 20.00, 120.550003 };
            var ce120 = new List<double> { 120.550003 };
            var ms1Scan = new List<double>();

            // Keys in this dictionary are scan number and values are collision energies
            var file1Data = new Dictionary<int, List<double>>
            {
                {2250, ce45},
                {2251, ce45},
                {2252, ce45},
                {2253, ms1Scan},
                {2254, ce45},
                {2255, ce45},
                {2256, ce45},
                {2257, ms1Scan},
                {2258, ce45},
                {2259, ce45},
                {2260, ce45}
            };
            expectedData.Add("Shew_246a_LCQa_15Oct04_Andro_0904-2_4-20", file1Data);

            var file2Data = new Dictionary<int, List<double>>
            {
                {39000, ce30},
                {39001, ce30},
                {39002, ms1Scan},
                {39003, ce30},
                {39004, ce30},
                {39005, ce30},
                {39006, ce120},
                {39007, ce20_120},
                {39008, ce20_120},
                {39009, ce30},
                {39010, ce30}
            };
            expectedData.Add("HCC-38_ETciD_EThcD_4xdil_20uL_3hr_3_08Jan16_Pippin_15-08-53", file2Data);

            var file3Data = new Dictionary<int, List<double>>
            {
                {19000, ce120},
                {19001, ce20_120},
                {19002, ce20_120},
                {19003, ms1Scan},
                {19004, ce30},
                {19005, ce30},
                {19006, ce30},
                {19007, ce120},
                {19008, ce20_120},
                {19009, ce20_120},
                {19010, ce30}
            };
            expectedData.Add("HCC-38_ETciD_EThcD_07Jan16_Pippin_15-08-53", file3Data);

            var file4Data = new Dictionary<int, List<double>>
            {
                {1, ce30},
                {2, ce30}
            };
            expectedData.Add("MZ0210MnxEF889ETD", file4Data);

            var file5Data = new Dictionary<int, List<double>>
            {
                {27799, ms1Scan},
                {27800, ce30},
                {27801, ce30},
                {27802, ce30},
                {27803, ce30},
                {27804, ce30},
                {27805, ms1Scan},
                {27806, ce30},
                {27807, ce30},
            };
            expectedData.Add("QC_Mam_16_01_125ng_2pt0-IT22_Run-A_16Oct17_Pippin_AQ_17-10-01", file5Data);

            var dataFile = FindInputFile(mzXmlFileName);

            if (!expectedData.TryGetValue(Path.GetFileNameWithoutExtension(dataFile.Name), out var collisionEnergiesThisFile))
            {
                Assert.Fail("Dataset {0} not found in dictionary expectedData", dataFile.Name);
            }

            // Keys are scan number, values are the collision energy
            var collisionEnergiesActual = new Dictionary<int, float>();

            // Keys are scan number, values are msLevel
            var msLevelsActual = new Dictionary<int, int>();

            // Keys are scan number, values are the ActivationType, for example cid, etd, hcd
            var activationTypesActual = new Dictionary<int, string>();

            var reader = new MzXMLFileAccessor();
            reader.OpenFile(dataFile.FullName);

            foreach (var scanNumber in collisionEnergiesThisFile.Keys)
            {
                var validScan = reader.GetSpectrumByScanNumber(scanNumber, out var spectrumInfo);

                Assert.IsTrue(validScan, "GetSpectrumByScanNumber returned false for scan {0}", scanNumber);

                if (spectrumInfo is not SpectrumInfoMzXML mzXmlSpectrum)
                {
                    Assert.Fail("Input file is not a .mzXML file; cannot validate collision energy");
                    return;
                }

                collisionEnergiesActual.Add(scanNumber, mzXmlSpectrum.CollisionEnergy);

                msLevelsActual.Add(scanNumber, mzXmlSpectrum.MSLevel);

                activationTypesActual.Add(scanNumber, mzXmlSpectrum.ActivationMethod);
            }

            Console.WriteLine("{0,-5} {1,-5} {2}", "Valid", "Scan", "Collision Energy");

            foreach (var actualEnergiesOneScan in (from item in collisionEnergiesActual orderby item.Key select item))
            {
                var scanNumber = actualEnergiesOneScan.Key;

                var expectedEnergies = collisionEnergiesThisFile[scanNumber];

                var activationTypes = string.Join(", ", activationTypesActual[scanNumber]);

                if (Math.Abs(actualEnergiesOneScan.Value) < float.Epsilon)
                {
                    var msLevel = msLevelsActual[scanNumber];

                    if (msLevel != 1)
                    {
                        var msg = string.Format(
                            "Scan {0} has no collision energies, which should only be true for spectra with msLevel=1. This scan has msLevel={1} and activationType={2}",
                            scanNumber, msLevel, activationTypes);
                        Console.WriteLine(msg);

                        Assert.Fail(msg);
                    }
                    else
                    {
                        Console.WriteLine("{0,-5} {1,-5} {2}", true, scanNumber, "MS1 scan");
                    }
                }
                else
                {
                    var actualEnergy = actualEnergiesOneScan.Value;
                    var isValid = expectedEnergies.Any(expectedEnergy => Math.Abs(actualEnergy - expectedEnergy) < 0.00001);

                    Console.WriteLine("{0,-5} {1,-5} {2:F2}", isValid, scanNumber, actualEnergy);

                    Assert.IsTrue(isValid, "Unexpected collision energy {0:F2} for scan {1}", actualEnergy, scanNumber);
                }
            }
        }

        [Test]
        [TestCase("Shew_246a_LCQa_15Oct04_Andro_0904-2_4-20.mzXML", 1, 5000, 23)]
        [TestCase("HCC-38_ETciD_EThcD_4xdil_20uL_3hr_3_08Jan16_Pippin_15-08-53.mzXML", 1, 75000, 127)]
        [TestCase("QC_Shew_15_02_Run-2_9Nov15_Oak_14-11-08.mzXML", 1, 8000, 29)]
        [TestCase("MeOHBlank03POS_11May16_Legolas_HSS-T3_A925.mzXML", 1, 8000, 27)]
        [TestCase("Lewy2_19Ct1_2Nov13_Samwise_13-07-28.mzXML", 1, 44000, 127)]
        public void TestDataIsSortedByMz(string mzXmlFileName, int scanStart, int scanEnd, int scanStep)
        {
            var dataFile = FindInputFile(mzXmlFileName);

            var reader = new MzXMLFileAccessor();
            reader.OpenFile(dataFile.FullName);

            for (var iteration = 1; iteration <= 4; iteration++)
            {
                int maxNumberOfPeaks;
                bool centroidData;

                switch (iteration)
                {
                    case 1:
                        maxNumberOfPeaks = 0;
                        centroidData = false;
                        break;
                    case 2:
                        maxNumberOfPeaks = 0;
                        centroidData = true;
                        break;
                    case 3:
                        maxNumberOfPeaks = 50;
                        centroidData = false;
                        break;
                    default:
                        maxNumberOfPeaks = 50;
                        centroidData = true;
                        break;
                }

                if (iteration == 1)
                {
                    Console.WriteLine("Scan data for {0}", dataFile.Name);
                    Console.WriteLine(
                        "{0,5} {1,-5} {2,-10} {3,-8} {4,-8} {5,-10} {6,-8} {7,-10} {8,-8}  {9}",
                        "Scan", "Max#", "Centroid", "MzCount", "IntCount",
                        "FirstMz", "FirstInt", "MidMz", "MidInt", "ScanFilter");
                }

                if (scanEnd > reader.CachedSpectraScanNumberMaximum)
                    scanEnd = reader.CachedSpectraScanNumberMaximum;

                if (scanStep < 1)
                    scanStep = 1;

                var statsInterval = (int)Math.Floor((scanEnd - scanStart) / (double)scanStep / 10);
                var scansProcessed = 0;

                for (var scanNumber = scanStart; scanNumber <= scanEnd; scanNumber += scanStep)
                {
                    var validScan = reader.GetSpectrumByScanNumber(scanNumber, out var spectrumInfo);

                    if (!validScan)
                    {
                        Assert.Fail("Invalid scan number: {0}", scanNumber);
                    }

                    var mzList = spectrumInfo.MzList;
                    var intensityList = spectrumInfo.IntensityList;

                    var unsortedMzValues = 0;

                    for (var i = 0; i < mzList.Count - 1; i++)
                    {
                        if (mzList[i] > mzList[i + 1])
                            unsortedMzValues++;
                    }

                    Assert.AreEqual(0, unsortedMzValues, "Scan {0} has {1} m/z values not sorted properly", scanNumber, unsortedMzValues);

                    scansProcessed++;
                    if (scansProcessed % statsInterval == 0)
                    {
                        var scanInfo = GenerateThermoScanFilter(spectrumInfo);

                        if (mzList.Count > 0)
                        {
                            var midIndex = (int)Math.Floor(mzList.Count / 2.0);

                            Console.WriteLine(
                                "{0,5} {1,-5} {2,-10} {3,-8} {4,-8} {5,-10:0.0000} {6,-8:0.0} {7,-10:0.0000} {8,-8:0.0}  {9}",
                                scanNumber, maxNumberOfPeaks, centroidData, mzList.Count, intensityList.Count,
                                mzList[0], intensityList[0], mzList[midIndex], intensityList[midIndex], scanInfo);
                        }
                        else
                        {
                            Console.WriteLine(
                                "{0,5} {1,-5} {2,-10} {3,-8} {4,-8} {5,-10} {6,-8} {7,-10} {8,-8}  {9}",
                                scanNumber, maxNumberOfPeaks, centroidData, mzList.Count, intensityList.Count,
                                "n/a", "n/a", "n/a", "n/a", scanInfo);
                        }
                    }
                }
            }
        }

        [Test]
        [TestCase("Shew_246a_LCQa_15Oct04_Andro_0904-2_4-20.mzXML", 3316)]
        [TestCase("HCC-38_ETciD_EThcD_4xdil_20uL_3hr_3_08Jan16_Pippin_15-08-53.mzXML", 71147)]
        [TestCase("Angiotensin_325-CID.mzXML", 10)]
        [TestCase("Angiotensin_325-ETciD-15.mzXML", 10)]
        [TestCase("Angiotensin_325-ETD.mzXML", 10)]
        [TestCase("Angiotensin_325-HCD.mzXML", 10)]
        [TestCase("Angiotensin_AllScans.mzXML", 1775)]
        public void TestGetNumScans(string mzXmlFileName, int expectedResult)
        {
            var dataFile = FindInputFile(mzXmlFileName);

            var reader = new MzXMLFileAccessor();
            reader.OpenFile(dataFile.FullName);

            var scanCount = reader.ScanCount;

            Console.WriteLine("Scan count for {0}: {1}", dataFile.Name, scanCount);
            if (expectedResult >= 0)
                Assert.AreEqual(expectedResult, scanCount, "Scan count mismatch");
        }

        [Test]
        [TestCase("Shew_246a_LCQa_15Oct04_Andro_0904-2_4-20.mzXML", 500, 525, 497, 0, 501, 501, 501, 0, 505, 505, 505, 0, 509, 509, 509, 0, 513, 513, 513, 0, 517, 517, 517, 0, 521, 521, 521, 0)]
        [TestCase("HCC-38_ETciD_EThcD_4xdil_20uL_3hr_3_08Jan16_Pippin_15-08-53.mzXML", 500, 525, 493, 493, 0, 0, 0, 503, 503, 0, 504, 504, 0, 507, 507, 507, 507, 0, 510, 0, 515, 515, 515, 0, 0, 521, 0, 522)]
        [TestCase("Angiotensin_325-CID.mzXML", 1, 5, 0, 0, 0, 0, 0)]
        [TestCase("Angiotensin_325-ETciD-15.mzXML", 1, 5, 0, 0, 0, 0, 0)]
        [TestCase("Angiotensin_325-ETD.mzXML", 1, 5, 0, 0, 0, 0, 0)]
        [TestCase("Angiotensin_325-HCD.mzXML", 1, 5, 0, 0, 0, 0, 0)]
        [TestCase("Angiotensin_AllScans.mzXML", 500, 550, 477, 477, 498, 498, 498, 498, 498, 498, 498, 498, 498, 477, 477, 498, 498, 498, 498, 498, 498, 0, 498, 498, 498, 519, 519, 519, 519, 519, 519, 519, 519, 519, 498, 498, 519, 519, 519, 519, 519, 519, 0, 519, 519, 519, 540, 540, 540, 540, 540, 540, 540)]
        public void TestGetParentScan(string mzXmlFileName, int startScan, int endScan, params int[] expectedParents)
        {
            var dataFile = FindInputFile(mzXmlFileName);

            var reader = new MzXMLFileAccessor();
            reader.OpenFile(dataFile.FullName);

            var i = 0;
            var validScanCount = 0;

            for (var scanNumber = startScan; scanNumber <= endScan; scanNumber++)
            {
                var validScan = reader.GetSpectrumByScanNumber(scanNumber, out var spectrumInfo);

                if (!validScan)
                {
                    ConsoleMsgUtils.ShowWarning("Invalid scan number: {0}", scanNumber);
                    i++;
                    continue;
                }

                validScanCount++;

                if (spectrumInfo is not SpectrumInfoMzXML mzXmlSpectrum)
                {
                    Assert.Fail("Input file is not a .mzXML file; cannot validate precursor scan numbers");
                    return;
                }

                Console.WriteLine("MS{0} scan {1,-4} has parent {2,-4}", spectrumInfo.MSLevel, scanNumber, mzXmlSpectrum.PrecursorScanNum);

                if (i < expectedParents.Length && expectedParents[i] != 0)
                {
                    Assert.AreEqual(
                        expectedParents[i], mzXmlSpectrum.PrecursorScanNum,
                        "Parent scan does not match expected value: {0}",
                        expectedParents[i]);
                }

                i++;
            }

            var percentValid = validScanCount / (double)(endScan - startScan + 1) * 100;
            Assert.Greater(percentValid, 90, "Over 10% of the spectra had invalid scan numbers");
        }

        [Test]
        [TestCase("Shew_246a_LCQa_15Oct04_Andro_0904-2_4-20.mzXML", 1513, 1521, 3, 6)]
        [TestCase("HCC-38_ETciD_EThcD_4xdil_20uL_3hr_3_08Jan16_Pippin_15-08-53.mzXML", 16121, 16165, 3, 42)]
        [TestCase("QC_Mam_16_01_125ng_2pt0-IT22_Run-A_16Oct17_Pippin_AQ_17-10-01.mzXML", 20500, 20520, 7, 14)]
        public void TestGetScanInfo(string mzXmlFileName, int scanStart, int scanEnd, int expectedMS1, int expectedMS2)
        {
            var expectedData = new Dictionary<string, Dictionary<int, string>>();

            // Keys in this dictionary are the scan number whose metadata is being retrieved
            var file1Data = new Dictionary<int, string>
            {
                {1513, "1   851 44.57 400 2000 6.3E+8 1089.978 1.2E+7     0.00 Positive True"},
                {1514, "2   109 44.60 230 1780 5.0E+6  528.128 7.2E+5   884.41 Positive True"},
                {1515, "2   290 44.63 305 2000 2.6E+7 1327.414 6.0E+6  1147.67 Positive True"},
                {1516, "2   154 44.66 400 2000 7.6E+5 1251.554 3.7E+4  1492.90 Positive True"},
                {1517, "1   887 44.69 400 2000 8.0E+8 1147.613 1.0E+7     0.00 Positive True"},
                {1518, "2   190 44.71 380 2000 4.6E+6 1844.618 2.7E+5  1421.21 Positive True"},
                {1519, "2   165 44.74 380 2000 6.0E+6 1842.547 6.9E+5  1419.24 Positive True"},
                {1520, "2   210 44.77 265 2000 1.5E+6 1361.745 4.2E+4  1014.93 Positive True"},
                {1521, "1   860 44.80 400 2000 6.9E+8 1126.627 2.9E+7     0.00 Positive True"}
            };
            expectedData.Add("Shew_246a_LCQa_15Oct04_Andro_0904-2_4-20", file1Data);

            // Note that for this dataset NumPeaks does not accurately reflect the number of data in each mass spectrum (it's much higher than it should be)
            // For example, scan 16121 has NumPeaks = 45876, but TestGetScanData() correctly finds 11888 data points for that scan
            var file2Data = new Dictionary<int, string>
            {
                {16121, "1 45876 47.68 350 1550 1.9E+9  503.565 3.4E+8     0.00 Positive False"},
                {16122, "2  4124 47.68 106  817 1.6E+6  550.309 2.1E+5   403.22 Positive True"},
                {16123, "2  6484 47.68 143 1627 5.5E+5  506.272 4.9E+4   538.84 Positive True"},
                {16124, "2  8172 47.68 208 2000 7.8E+5  737.530 7.0E+4   776.27 Positive True"},
                {16125, "2  5828 47.68 120 1627 2.1E+5  808.486 2.2E+4   538.84 Positive True"},
                {16126, "2  6228 47.68 120 1627 1.4E+5  536.209 9.0E+3   538.84 Positive True"},
                {16127, "2  7180 47.68 120 1627 1.3E+5  808.487 1.4E+4   538.84 Positive True"},
                {16128, "2  7980 47.69 225 1682 4.4E+5  805.579 2.3E+4   835.88 Positive True"},
                {16129, "2  7700 47.69 266 1986 3.4E+5  938.679 2.9E+4   987.89 Positive True"},
                {16130, "2  5180 47.69 110  853 2.7E+5  411.977 1.2E+4   421.26 Positive True"},
                {16131, "2   436 47.69 120 1986 2.1E+4  984.504 9.5E+3   987.89 Positive True"},
                {16132, "2  2116 47.69 120  853 1.2E+4  421.052 6.8E+2   421.26 Positive True"},
                {16133, "2  2444 47.70 120  853 1.5E+4  421.232 1.2E+3   421.26 Positive True"},
                {16134, "2  2948 47.70 120  853 1.4E+4  838.487 7.5E+2   421.26 Positive True"},
                {16135, "2   508 47.70 120 1986 2.1E+4  984.498 9.2E+3   987.89 Positive True"},
                {16136, "2   948 47.71 120 1986 2.3E+4  984.491 9.4E+3   987.89 Positive True"},
                {16137, "2  9580 47.71 336 2000 3.5E+5 1536.038 4.7E+3  1241.01 Positive True"},
                {16138, "2  7604 47.72 235 1760 2.9E+5  826.095 2.5E+4   874.84 Positive True"},
                {16139, "2   972 47.72 120 1760 1.6E+4  875.506 2.1E+3   874.84 Positive True"},
                {16140, "2  1596 47.72 120 1760 1.8E+4 1749.846 2.0E+3   874.84 Positive True"},
                {16141, "2  2124 47.72 120 1760 1.6E+4  874.664 1.6E+3   874.84 Positive True"},
                {16142, "1 51976 47.73 350 1550 1.3E+9  503.565 1.9E+8     0.00 Positive False"},
                {16143, "2  5412 47.73 128  981 6.5E+5  444.288 6.4E+4   485.28 Positive True"},
                {16144, "2  4300 47.73 101 1561 5.0E+5  591.309 4.0E+4   387.66 Positive True"},
                {16145, "2  6740 47.73 162 1830 4.0E+5  567.912 2.8E+4   606.62 Positive True"},
                {16146, "2  4788 47.73  99  770 1.9E+5  532.308 3.4E+4   379.72 Positive True"},
                {16147, "2  6708 47.74 120 1830 3.8E+5  603.095 3.1E+4   606.62 Positive True"},
                {16148, "2  7260 47.74 120 1830 1.5E+5  603.076 1.3E+4   606.62 Positive True"},
                {16149, "2  9172 47.74 120 1830 1.6E+5  603.027 1.1E+4   606.62 Positive True"},
                {16150, "2  5204 47.74  95 1108 3.8E+5  418.536 1.2E+5   365.88 Positive True"},
                {16151, "2  5636 47.75 146 1656 2.8E+5  501.523 4.3E+4   548.54 Positive True"},
                {16152, "2  9572 47.75 328 2000 1.8E+5  848.497 2.2E+3  1210.30 Positive True"},
                {16153, "2  5004 47.75 120 1656 1.3E+5  548.396 1.3E+4   548.54 Positive True"},
                {16154, "2  4732 47.75 120 1656 4.2E+4  548.450 4.2E+3   548.54 Positive True"},
                {16155, "2  6228 47.76 120 1656 4.2E+4  550.402 3.6E+3   548.54 Positive True"},
                {16156, "2  9164 47.76 324 2000 1.5E+5 1491.872 1.0E+4  1197.57 Positive True"},
                {16157, "2  5916 47.76 124  950 2.2E+5  420.689 2.2E+4   469.71 Positive True"},
                {16158, "2  5740 47.76 306 2000 1.3E+5 1100.042 3.5E+3  1132.02 Positive True"},
                {16159, "2  5540 47.76 122  935 1.9E+5  445.117 2.7E+4   462.15 Positive True"},
                {16160, "2  5756 47.77 145 1646 3.4E+5  539.065 6.0E+4   545.18 Positive True"},
                {16161, "2  6100 47.77 157 1191 2.8E+5  541.462 6.0E+4   590.28 Positive True"},
                {16162, "2  2508 47.77 120 1191 8.4E+4 1180.615 5.1E+3   590.28 Positive True"},
                {16163, "2  2644 47.77 120 1191 1.8E+4 1184.614 9.0E+2   590.28 Positive True"},
                {16164, "2  3180 47.77 120 1191 1.7E+4 1184.644 8.7E+2   590.28 Positive True"},
                {16165, "1 53252 47.78 350 1550 1.2E+9  503.565 1.6E+8     0.00 Positive False"}
            };
            expectedData.Add("HCC-38_ETciD_EThcD_4xdil_20uL_3hr_3_08Jan16_Pippin_15-08-53", file2Data);

            var file3Data = new Dictionary<int, string>
            {
                {20500, "2  1264 41.90 110 1068 2.6E+6  416.224 2.9E+5   352.54 Positive True"},
                {20501, "1 13472 41.90 350 1800 1.3E+9  599.293 1.0E+8     0.00 Positive False"},
                {20502, "2  1680 41.90 110 1883 3.9E+6 1063.568 2.2E+5   624.30 Positive True"},
                {20503, "2  1392 41.90 110 1924 3.1E+6  637.336 3.0E+5   637.69 Positive True"},
                {20504, "1 14120 41.90 350 1800 1.3E+9  554.304 9.7E+7     0.00 Positive False"},
                {20505, "2  2048 41.90 110 1233 8.4E+6  911.447 1.1E+6   611.29 Positive True"},
                {20506, "2  1488 41.90 110 1111 6.9E+6  207.112 6.4E+5   550.31 Positive True"},
                {20507, "1 14016 41.91 350 1800 1.2E+9  554.304 9.0E+7     0.00 Positive False"},
                {20508, "2  1296 41.91 110 1253 3.9E+6  887.511 4.0E+5   621.35 Positive True"},
                {20509, "2  1776 41.91 110 1075 6.0E+6  445.242 5.1E+5   532.29 Positive True"},
                {20510, "1 14184 41.91 350 1800 1.3E+9  554.304 9.4E+7     0.00 Positive False"},
                {20511, "2  2224 41.91 110  958 1.1E+7  120.081 8.8E+5   473.77 Positive True"},
                {20512, "2  1568 41.91 110 1401 8.4E+6  891.457 1.5E+6   695.36 Positive True"},
                {20513, "2  2112 41.91 110  926 5.1E+6  777.422 4.6E+5   457.74 Positive True"},
                {20514, "1 14804 41.91 350 1800 1.4E+9  554.305 1.0E+8     0.00 Positive False"},
                {20515, "2   928 41.91 110 1730 8.0E+6  859.948 3.1E+6   859.94 Positive True"},
                {20516, "1 14232 41.92 350 1800 1.4E+9  554.305 1.1E+8     0.00 Positive False"},
                {20517, "2  1792 41.92 110 1339 4.2E+6  697.397 3.0E+5   442.91 Positive True"},
                {20518, "2  1216 41.92 110 2000 3.7E+6  999.457 3.8E+5   737.69 Positive True"},
                {20519, "2  2144 41.92 110 1241 8.9E+6  742.409 5.7E+5   615.27 Positive True"},
                {20520, "1 14428 41.92 350 1800 1.7E+9  554.305 1.3E+8     0.00 Positive False"}
            };
            expectedData.Add("QC_Mam_16_01_125ng_2pt0-IT22_Run-A_16Oct17_Pippin_AQ_17-10-01", file3Data);

            var dataFile = FindInputFile(mzXmlFileName);

            var reader = new MzXMLFileAccessor();
            reader.OpenFile(dataFile.FullName);

            Console.WriteLine("Scan info for {0}", dataFile.Name);
            Console.WriteLine(
                "{0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10} {11} {12} {13} {14} {15}",
                "Scan", "MSLevel",
                "NumPeaks", "RetentionTime",
                "LowMass", "HighMass", "TotalIonCurrent",
                "BasePeakMZ", "BasePeakIntensity",
                "ParentIonMZ", "ActivationMethod", "CollisionMode",
                "Polarity", "IsCentroided", "SpectrumType",
                "FilterLine");

            var scanCountMS1 = 0;
            var scanCountMS2 = 0;

            for (var scanNumber = scanStart; scanNumber <= scanEnd; scanNumber++)
            {
                var validScan = reader.GetSpectrumByScanNumber(scanNumber, out var spectrumInfo);

                Assert.IsTrue(validScan, "GetSpectrumByScanNumber returned false for scan {0}", scanNumber);

                if (spectrumInfo is not SpectrumInfoMzXML mzXmlSpectrum)
                {
                    Assert.Fail("Input file is not a .mzXML file; cannot validate spectrum info");
                    return;
                }

                string filterLine;
                string collisionMode;

                if (string.IsNullOrWhiteSpace(mzXmlSpectrum.FilterLine))
                {
                    filterLine = "Empty filter line; cannot parse";
                    collisionMode = string.Empty;
                }
                else
                {
                    var validFilterLine = ParseFilterLine(mzXmlSpectrum.FilterLine, out _, out _, out collisionMode);
                    if (validFilterLine)
                    {
                        filterLine = mzXmlSpectrum.FilterLine;
                    }
                    else
                    {
                        filterLine = "Unrecognized filter line: " + mzXmlSpectrum.FilterLine;
                    }
                }

                var scanSummary =
                    string.Format(
                        "{0} {1} {2,5} {3:F2} {4,3:0} {5,4:0} {6:0.0E+0} {7,8:F3} {8:0.0E+0} {9,8:F2} {10} {11,5} {12} {13} {14} {15}",
                        spectrumInfo.ScanNumber, spectrumInfo.MSLevel,
                        spectrumInfo.PeaksCount, spectrumInfo.RetentionTimeMin,
                        spectrumInfo.MzRangeStart, spectrumInfo.MzRangeEnd,
                        spectrumInfo.TotalIonCurrent, spectrumInfo.BasePeakMZ, spectrumInfo.BasePeakIntensity, spectrumInfo.ParentIonMZ,
                        mzXmlSpectrum.ActivationMethod, collisionMode,
                        spectrumInfo.Polarity, spectrumInfo.Centroided, mzXmlSpectrum.SpectrumType,
                        filterLine.Substring(0, 12) + "...");

                Console.WriteLine(scanSummary);

                if (spectrumInfo.MSLevel > 1)
                    scanCountMS2++;
                else
                    scanCountMS1++;

                if (!expectedData.TryGetValue(Path.GetFileNameWithoutExtension(dataFile.Name), out var expectedDataThisFile))
                {
                    Assert.Fail("Dataset {0} not found in dictionary expectedData", dataFile.Name);
                }

                if (false && expectedDataThisFile.TryGetValue(scanNumber, out var expectedScanSummary))
                {
                    Assert.AreEqual(scanNumber + " " + expectedScanSummary, scanSummary,
                        "Scan summary mismatch, scan " + scanNumber);
                }
            }

            Console.WriteLine("scanCountMS1={0}", scanCountMS1);
            Console.WriteLine("scanCountMS2={0}", scanCountMS2);

            Assert.AreEqual(expectedMS1, scanCountMS1, "MS1 scan count mismatch");
            Assert.AreEqual(expectedMS2, scanCountMS2, "MS2 scan count mismatch");
        }

        private FileInfo FindInputFile(string mzXmlFileName)
        {
            var localDirPath = Path.Combine("..", "..", "Docs");
            const string remoteDirPath = @"\\proto-2\UnitTest_Files\MSDataFileReaderDLL";

            var localFile = new FileInfo(Path.Combine(localDirPath, mzXmlFileName));

            if (localFile.Exists)
            {
                return localFile;
            }

            // Look for the file on Proto-2
            var remoteFile = new FileInfo(Path.Combine(remoteDirPath, mzXmlFileName));
            if (remoteFile.Exists)
            {
                return remoteFile;
            }

            var msg = string.Format("File not found: {0}; checked in both {1} and {2}", mzXmlFileName, localDirPath, remoteDirPath);

            Console.WriteLine(msg);
            Assert.Fail(msg);

            return null;
        }

        private static string GenerateThermoScanFilter(SpectrumInfo spectrumInfo)
        {
            string activationMethod;
            float collisionEnergy;

            if (spectrumInfo is SpectrumInfoMzXML mzXmlSpectrum)
            {
                activationMethod = mzXmlSpectrum.ActivationMethod;
                collisionEnergy = mzXmlSpectrum.CollisionEnergy;

                if (!string.IsNullOrWhiteSpace(mzXmlSpectrum.FilterLine))
                    return mzXmlSpectrum.FilterLine;
            }
            else
            {
                activationMethod = "xxx";
                collisionEnergy = 0;
            }

            var centroidOrProfile = spectrumInfo.Centroided ? "c" : "p";

            // ReSharper disable once ConvertIfStatementToSwitchExpression
            // ReSharper disable once ConvertIfStatementToSwitchStatement
            if (spectrumInfo.MSLevel == 1)
            {
                return string.Format("{0} {1} Full ms [{2}-{3}]",
                    spectrumInfo.Polarity, centroidOrProfile, spectrumInfo.MzRangeStart, spectrumInfo.MzRangeEnd);
            }

            if (spectrumInfo.MSLevel > 1)
            {
                return string.Format("{0} {1} Full ms{2} {3}@{4}{5:F2} [{6}-{7}]",
                    spectrumInfo.Polarity,
                    centroidOrProfile,
                    spectrumInfo.MSLevel,
                    spectrumInfo.ParentIonMZ,
                    activationMethod,
                    collisionEnergy,
                    spectrumInfo.MzRangeStart,
                    spectrumInfo.MzRangeEnd);
            }

            return "Invalid value for MSLevel: " + spectrumInfo.MSLevel;
        }

        private bool ParseFilterLine(string filterLine, out double parentIonMz, out int msLevelMSn, out string collisionMode)
        {
            return XRawFileIO.ExtractParentIonMZFromFilterText(
                filterLine,
                out parentIonMz,
                out msLevelMSn,
                out collisionMode,
                out _);
        }
    }
}
