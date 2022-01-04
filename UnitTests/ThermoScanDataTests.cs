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
        [TestCase("Shew_246a_LCQa_15Oct04_Andro_0904-2_4-20.mzXML", 3316, 1513, 1521, 3, 6)]
        [TestCase("HCC-38_ETciD_EThcD_4xdil_20uL_3hr_3_08Jan16_Pippin_15-08-53.mzXML", 71147, 16121, 16165, 3, 42)]
        [TestCase("QC_Mam_16_01_125ng_2pt0-IT22_Run-A_16Oct17_Pippin_AQ_17-10-01.mzXML", 55005, 20500, 20520, 7, 14)]
        public void TestGetScanInfo(string mzXmlFileName, int expectedScanCount, int scanStart, int scanEnd, int expectedMS1, int expectedMS2)
        {
            // Keys in this dictionary are the scan number whose metadata is being retrieved
            var expectedScanInfo = MSDataFileReaderTests.GetExpectedMzXmlScanInfo(mzXmlFileName);

            var dataFile = FindInputFile(mzXmlFileName);

            var reader = new MzXMLFileAccessor();
            reader.OpenFile(dataFile.FullName);

            MSDataFileReaderTests.ValidateScanInfo(reader, expectedScanCount, scanStart, scanEnd, expectedMS1, expectedMS2, expectedScanInfo);
        }

        private FileInfo FindInputFile(string mzXmlFileName)
        {
            return MSDataFileReaderTests.FindInputFile(mzXmlFileName);
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
    }
}
