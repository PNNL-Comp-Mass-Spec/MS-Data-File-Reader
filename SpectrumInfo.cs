// -------------------------------------------------------------------------------
// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
// Copyright 2021, Battelle Memorial Institute.  All Rights Reserved.
//
// E-mail: matthew.monroe@pnl.gov or proteomics@pnnl.gov
// Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://panomics.pnnl.gov/ or https://www.pnnl.gov/integrative-omics
// -------------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace MSDataFileReader
{
    /// <summary>
    /// This class holds the values associated with each spectrum in an MS Data file
    /// </summary>
    [Serializable]
    public class SpectrumInfo : ICloneable
    {
        // Ignore Spelling: centroided

        public SpectrumInfo()
        {
            MzList = new List<double>();
            IntensityList = new List<float>();

            // ReSharper disable once VirtualMemberCallInConstructor
            Clear();
        }

        public static class SpectrumTypeNames
        {
            public const string Discrete = "discrete";

            // ReSharper disable once UnusedMember.Global
            public const string Continuous = "continuous";
        }

        public enum SpectrumStatusMode
        {
            /// <summary>
            /// Used when .Clear() is called
            /// </summary>
            Initialized = 0,

            /// <summary>
            /// Used when any of the values are set via a property
            /// </summary>
            DataDefined = 1,

            /// <summary>
            /// Used when .Validate() is called
            /// </summary>
            Validated = 2
        }

        /// <summary>
        /// Spectrum ID number
        /// </summary>
        /// <remarks>
        /// Often the same as ScanNumber
        /// </remarks>
        private int mSpectrumID;

        /// <summary>
        /// First scan number if ScanCount is > 1
        /// </summary>
        private int mScanNumber;

        /// <summary>
        /// Number of spectra combined together to get the given spectrum
        /// </summary>
        private int mScanCount;

        /// <summary>
        /// Last scan if more than one scan was combined to make this spectrum
        /// </summary>
        private int mScanNumberEnd;

        /// <summary>
        /// See Class SpectrumTypeNames for typical names (discrete or continuous)
        /// </summary>
        private string mSpectrumType;

        private string mSpectrumCombinationMethod;

        /// <summary>
        /// 1 for MS, 2 for MS/MS, 3 for MS^3, etc.
        /// </summary>
        private int mMsLevel;

        private string mPolarity;

        private float mRetentionTimeMin;

        private float mMzRangeStart;

        private float mMzRangeEnd;

        private double mBasePeakMZ;

        private float mBasePeakIntensity;

        private double mTotalIonCurrent;

        private double mParentIonMZ;

        private float mParentIonIntensity;

        private int mPeaksCount;

        /// <summary>
        /// Returns the number of items in MzList
        /// </summary>
        public int DataCount => MzList.Count;

        public readonly List<double> MzList;

        public readonly List<float> IntensityList;

        protected string mErrorMessage;

        protected SpectrumStatusMode mSpectrumStatus;

        public int SpectrumID
        {
            get => mSpectrumID;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mSpectrumID = value;
            }
        }

        public int ScanNumber
        {
            get => mScanNumber;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mScanNumber = value;
            }
        }

        public int ScanCount
        {
            get => mScanCount;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mScanCount = value;
            }
        }

        public int ScanNumberEnd
        {
            get => mScanNumberEnd;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mScanNumberEnd = value;
            }
        }

        public string SpectrumType
        {
            get => mSpectrumType;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mSpectrumType = value;
            }
        }

        public string SpectrumCombinationMethod
        {
            get => mSpectrumCombinationMethod;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mSpectrumCombinationMethod = value;
            }
        }

        public int MSLevel
        {
            get => mMsLevel;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mMsLevel = value;
            }
        }

        /// <summary>
        /// True if the data is centroided (supported by mzXML v3.x)
        /// </summary>
        public bool Centroided { get; set; }

        public string Polarity
        {
            get => mPolarity;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mPolarity = value;
            }
        }

        public float RetentionTimeMin
        {
            get => mRetentionTimeMin;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mRetentionTimeMin = value;
            }
        }

        public float MzRangeStart
        {
            get => mMzRangeStart;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mMzRangeStart = value;
            }
        }

        public float MzRangeEnd
        {
            get => mMzRangeEnd;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mMzRangeEnd = value;
            }
        }

        public double BasePeakMZ
        {
            get => mBasePeakMZ;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mBasePeakMZ = value;
            }
        }

        public float BasePeakIntensity
        {
            get => mBasePeakIntensity;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mBasePeakIntensity = value;
            }
        }

        public double TotalIonCurrent
        {
            get => mTotalIonCurrent;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mTotalIonCurrent = value;
            }
        }

        public double ParentIonMZ
        {
            get => mParentIonMZ;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mParentIonMZ = value;
            }
        }

        public float ParentIonIntensity
        {
            get => mParentIonIntensity;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mParentIonIntensity = value;
            }
        }

        /// <summary>
        /// This is the number of data points that should be associated with the mass spectrum
        /// </summary>
        /// <remarks>
        /// If ions are loaded, this should match MzList.Count
        /// </remarks>
        public int PeaksCount
        {
            get => mPeaksCount;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mPeaksCount = value;
            }
        }

        public SpectrumStatusMode SpectrumStatus => mSpectrumStatus;

        /// <summary>
        /// When True, MzList().Length and IntensityList().Length will equal DataCount
        /// When False, the memory will not be freed when DataCount shrinks or .Clear() is called
        /// </summary>
        /// <remarks>
        /// Set this to False helps reduce slow, increased memory usage due to inefficient garbage collection
        /// </remarks>
        [Obsolete("No longer applicable since MzList and IntensityList are now lists instead of arrays")]
        public bool AutoShrinkDataLists { get; set; }

        // ReSharper disable once UnusedMember.Global
        public string ErrorMessage => mErrorMessage ?? string.Empty;

        public virtual void Clear()
        {
            mSpectrumID = 0;
            mScanNumber = 0;
            mScanCount = 0;
            mScanNumberEnd = 0;
            mSpectrumType = SpectrumTypeNames.Discrete;
            mSpectrumCombinationMethod = string.Empty;
            mMsLevel = 1;
            Centroided = false;
            mPolarity = "Positive";
            mRetentionTimeMin = 0f;
            mMzRangeStart = 0f;
            mMzRangeEnd = 0f;
            mBasePeakMZ = 0d;
            mBasePeakIntensity = 0f;
            mTotalIonCurrent = 0d;
            mParentIonMZ = 0d;
            mParentIonIntensity = 0f;
            mPeaksCount = 0;

            ClearMzAndIntensityData();

            mSpectrumStatus = SpectrumStatusMode.Initialized;
            mErrorMessage = string.Empty;
        }

        public void ClearMzAndIntensityData()
        {
            MzList.Clear();
            IntensityList.Clear();
        }

        object ICloneable.Clone()
        {
            // Use the strongly typed Clone module to do the cloning
            return Clone();
        }

        private object CloneMe() => ((ICloneable)this).Clone();

        /// <summary>
        /// Clone this spectrum object
        /// </summary>
        /// <remarks>
        /// Clone() methods in the derived SpectrumInfo classes hide this method using new
        /// </remarks>
        /// <returns>Deep copy of this spectrum</returns>
        public SpectrumInfo Clone()
        {
            // First create a shallow copy of this object
            var target = (SpectrumInfo)MemberwiseClone();

            // Next, manually copy the array objects and any other objects
            // Note: Since Clone() methods in the derived classes hide this method,
            // be sure to update them too if you change any code below

            foreach (var item in MzList)
            {
                target.MzList.Add(item);
            }

            foreach (var item in IntensityList)
            {
                target.IntensityList.Add(item);
            }

            return target;
        }

        public virtual void CopyTo(out SpectrumInfo target)
        {
            target = Clone();
        }

        public void UpdateMZRange()
        {
            try
            {
                if (MzList.Count == 0)
                    return;

                MzRangeStart = (float)MzList[0];
                MzRangeEnd = (float)MzList[MzList.Count - 1];
            }
            catch (Exception ex)
            {
                mErrorMessage = "Error in UpdateMZRange: " + ex.Message;
                MzRangeStart = 0;
                MzRangeEnd = 0;
            }
        }

        public void ComputeBasePeakAndTIC()
        {
            var totalIonCurrent = default(double);
            var basePeakMZ = default(double);
            var basePeakIntensity = default(float);

            try
            {
                totalIonCurrent = 0d;
                basePeakMZ = 0d;
                basePeakIntensity = 0f;

                if (MzList.Count > 0)
                {
                    basePeakMZ = MzList[0];
                    basePeakIntensity = IntensityList[0];
                    totalIonCurrent = IntensityList[0];
                    var indexEnd = MzList.Count - 1;

                    for (var index = 1; index <= indexEnd; index++)
                    {
                        totalIonCurrent += IntensityList[index];

                        if (IntensityList[index] >= basePeakIntensity)
                        {
                            basePeakMZ = MzList[index];
                            basePeakIntensity = IntensityList[index];
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                mErrorMessage = "Error in ComputeBasePeakAndTIC: " + ex.Message;
            }
            finally
            {
                TotalIonCurrent = totalIonCurrent;
                BasePeakMZ = basePeakMZ;
                BasePeakIntensity = basePeakIntensity;
            }
        }

        /// <summary>
        /// Look for mzToFind in this spectrum's data and return the intensity, if found
        /// </summary>
        /// <param name="mzToFind">m/z to find</param>
        /// <param name="intensityIfNotFound">Intensity to return, if not found</param>
        /// <param name="matchTolerance">Match tolerance</param>
        /// <returns>Intensity for the given ion, or intensityIfNotFound if not found</returns>
        public float LookupIonIntensityByMZ(double mzToFind, float intensityIfNotFound, float matchTolerance = 0.05f)
        {
            float intensityMatch;

            try
            {
                // Define the minimum MZ value to consider
                var mzMinimum = mzToFind - matchTolerance;
                intensityMatch = intensityIfNotFound;

                if (!(MzList is null || IntensityList is null))
                {
                    int index;
                    for (index = MzList.Count - 1; index >= 0; index--)
                    {
                        if (index >= MzList.Count)
                            continue;

                        if (MzList[index] >= mzMinimum)
                        {
                            var mzDifference = mzToFind - MzList[index];

                            if (Math.Abs(mzDifference) <= matchTolerance)
                            {
                                if (IntensityList[index] > intensityMatch)
                                {
                                    intensityMatch = IntensityList[index];
                                }
                            }
                        }
                        else
                        {
                            // Assuming MzListData is sorted on intensity, we can exit out of the loop once we pass mzMinimum
                            break;
                        }
                    }
                }
            }
            catch (Exception)
            {
                intensityMatch = intensityIfNotFound;
            }

            return intensityMatch;
        }

        public void StoreIon(double mz, float intensity)
        {
            MzList.Add(mz);
            IntensityList.Add(intensity);
        }

        public void StoreIons(List<double> mzList, List<float> intensityList)
        {
            ClearMzAndIntensityData();
            MzList.AddRange(mzList);
            IntensityList.AddRange(intensityList);
        }

        public virtual void Validate(bool computeBasePeakAndTIC, bool updateMZRange)
        {
            if (computeBasePeakAndTIC)
            {
                ComputeBasePeakAndTIC();
            }

            if (updateMZRange)
            {
                UpdateMZRange();
            }
        }
    }
}