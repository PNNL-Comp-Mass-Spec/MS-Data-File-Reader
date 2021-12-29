// -------------------------------------------------------------------------------
// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
// Copyright 2021, Battelle Memorial Institute.  All Rights Reserved.
//
// E-mail: matthew.monroe@pnl.gov or proteomics@pnnl.gov
// Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://panomics.pnnl.gov/ or https://www.pnnl.gov/integrative-omics
// -------------------------------------------------------------------------------

using System;

namespace MSDataFileReader
{
    /// <summary>
    /// This class holds the values associated with each spectrum in an MS Data file
    /// </summary>
    [Serializable]
    public class clsSpectrumInfo : ICloneable
    {
        // Ignore Spelling: centroided

        public clsSpectrumInfo()
        {
            AutoShrinkDataLists = true;
            Clear();
        }

        public static class SpectrumTypeNames
        {
            public const string discrete = "discrete";

            public const string continuous = "continuous";
        }

        public enum eSpectrumStatusConstants
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
        private int mMSLevel;

        private string mPolarity;

        private float mRetentionTimeMin;

        private float mmzRangeStart;

        private float mmzRangeEnd;

        private double mBasePeakMZ;

        private float mBasePeakIntensity;

        private double mTotalIonCurrent;

        private double mParentIonMZ;

        private float mParentIonIntensity;

        // Number of m/z and intensity pairs in this spectrum; see note concerning mAutoShrinkDataLists below
        public int DataCount;

        public double[] MZList;

        public float[] IntensityList;

        protected string mErrorMessage;

        protected eSpectrumStatusConstants mSpectrumStatus;

        public int SpectrumID
        {
            get => mSpectrumID;

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mSpectrumID = value;
            }
        }

        public int ScanNumber
        {
            get => mScanNumber;

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mScanNumber = value;
            }
        }

        public int ScanCount
        {
            get => mScanCount;

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mScanCount = value;
            }
        }

        public int ScanNumberEnd
        {
            get => mScanNumberEnd;

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mScanNumberEnd = value;
            }
        }

        public string SpectrumType
        {
            get => mSpectrumType;

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mSpectrumType = value;
            }
        }

        public string SpectrumCombinationMethod
        {
            get => mSpectrumCombinationMethod;

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mSpectrumCombinationMethod = value;
            }
        }

        public int MSLevel
        {
            get => mMSLevel;

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mMSLevel = value;
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
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mPolarity = value;
            }
        }

        public float RetentionTimeMin
        {
            get => mRetentionTimeMin;

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mRetentionTimeMin = value;
            }
        }

        public float mzRangeStart
        {
            get => mmzRangeStart;

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mmzRangeStart = value;
            }
        }

        public float mzRangeEnd
        {
            get => mmzRangeEnd;

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mmzRangeEnd = value;
            }
        }

        public double BasePeakMZ
        {
            get => mBasePeakMZ;

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mBasePeakMZ = value;
            }
        }

        public float BasePeakIntensity
        {
            get => mBasePeakIntensity;

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mBasePeakIntensity = value;
            }
        }

        public double TotalIonCurrent
        {
            get => mTotalIonCurrent;

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mTotalIonCurrent = value;
            }
        }

        public double ParentIonMZ
        {
            get => mParentIonMZ;

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mParentIonMZ = value;
            }
        }

        public float ParentIonIntensity
        {
            get => mParentIonIntensity;

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mParentIonIntensity = value;
            }
        }

        public eSpectrumStatusConstants SpectrumStatus => mSpectrumStatus;

        /// <summary>
        /// When True, MZList().Length and IntensityList().Length will equal DataCount
        /// When False, the memory will not be freed when DataCount shrinks or .Clear() is called
        /// </summary>
        /// <remarks>
        /// Set this to False helps reduce slow, increased memory usage due to inefficient garbage collection
        /// </remarks>
        public bool AutoShrinkDataLists { get; set; }

        public string ErrorMessage => mErrorMessage ?? string.Empty;

        public virtual void Clear()
        {
            mSpectrumID = 0;
            mScanNumber = 0;
            mScanCount = 0;
            mScanNumberEnd = 0;
            mSpectrumType = SpectrumTypeNames.discrete;
            mSpectrumCombinationMethod = string.Empty;
            mMSLevel = 1;
            Centroided = false;
            mPolarity = "Positive";
            mRetentionTimeMin = 0f;
            mmzRangeStart = 0f;
            mmzRangeEnd = 0f;
            mBasePeakMZ = 0d;
            mBasePeakIntensity = 0f;
            mTotalIonCurrent = 0d;
            mParentIonMZ = 0d;
            mParentIonIntensity = 0f;
            DataCount = 0;

            if (AutoShrinkDataLists || MZList is null)
            {
                MZList = Array.Empty<double>();
            }
            else
            {
                Array.Clear(MZList, 0, MZList.Length);
            }

            if (AutoShrinkDataLists || IntensityList is null)
            {
                IntensityList = Array.Empty<float>();
            }
            else
            {
                Array.Clear(IntensityList, 0, IntensityList.Length);
            }

            mSpectrumStatus = eSpectrumStatusConstants.Initialized;
            mErrorMessage = string.Empty;
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
        public clsSpectrumInfo Clone()
        {
            // First create a shallow copy of this object
            var objTarget = (clsSpectrumInfo)MemberwiseClone();

            // Next, manually copy the array objects and any other objects
            // Note: Since Clone() methods in the derived classes hide this method,
            // be sure to update them too if you change any code below
            if (MZList is null)
            {
                objTarget.MZList = null;
            }
            else
            {
                objTarget.MZList = new double[MZList.Length];
                MZList.CopyTo(objTarget.MZList, 0);
            }

            if (IntensityList is null)
            {
                objTarget.IntensityList = null;
            }
            else
            {
                objTarget.IntensityList = new float[IntensityList.Length];
                IntensityList.CopyTo(objTarget.IntensityList, 0);
            }

            return objTarget;
        }

        public virtual void CopyTo(out clsSpectrumInfo objTarget)
        {
            objTarget = Clone();
        }

        public void UpdateMZRange()
        {
            var sngMzRangeStart = 0f;
            var sngMzRangeEnd = 0f;

            try
            {
                if (DataCount > 0 && MZList != null)
                {
                    sngMzRangeStart = (float)MZList[0];
                    sngMzRangeEnd = (float)MZList[DataCount - 1];
                }
            }
            catch (Exception ex)
            {
                mErrorMessage = "Error in UpdateMZRange: " + ex.Message;
            }
            finally
            {
                mzRangeStart = sngMzRangeStart;
                mzRangeEnd = sngMzRangeEnd;
            }
        }

        public void ComputeBasePeakAndTIC()
        {
            var dblTotalIonCurrent = default(double);
            var dblBasePeakMZ = default(double);
            var sngBasePeakIntensity = default(float);

            try
            {
                dblTotalIonCurrent = 0d;
                dblBasePeakMZ = 0d;
                sngBasePeakIntensity = 0f;

                if (DataCount > 0 && MZList != null && IntensityList != null)
                {
                    dblBasePeakMZ = MZList[0];
                    sngBasePeakIntensity = IntensityList[0];
                    dblTotalIonCurrent = IntensityList[0];
                    var intIndexEnd = DataCount - 1;

                    for (var intIndex = 1; intIndex <= intIndexEnd; intIndex++)
                    {
                        dblTotalIonCurrent += IntensityList[intIndex];

                        if (IntensityList[intIndex] >= sngBasePeakIntensity)
                        {
                            dblBasePeakMZ = MZList[intIndex];
                            sngBasePeakIntensity = IntensityList[intIndex];
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
                TotalIonCurrent = dblTotalIonCurrent;
                BasePeakMZ = dblBasePeakMZ;
                BasePeakIntensity = sngBasePeakIntensity;
            }
        }

        /// <summary>
        /// Look for dblMZToFind in this spectrum's data and return the intensity, if found
        /// </summary>
        /// <param name="dblMZToFind">m/z to find</param>
        /// <param name="sngIntensityIfNotFound">Intensity to return, if not found</param>
        /// <param name="sngMatchTolerance">Match tolerance</param>
        /// <returns>Intensity for the given ion, or sngIntensityIfNotFound if not found</returns>
        public float LookupIonIntensityByMZ(double dblMZToFind, float sngIntensityIfNotFound, float sngMatchTolerance = 0.05f)
        {
            float sngIntensityMatch;

            try
            {
                // Define the minimum MZ value to consider
                var dblMZMinimum = dblMZToFind - sngMatchTolerance;
                sngIntensityMatch = sngIntensityIfNotFound;

                if (!(MZList is null || IntensityList is null))
                {
                    int intIndex;
                    for (intIndex = DataCount - 1; intIndex >= 0; intIndex--)
                    {
                        if (intIndex >= MZList.Length || intIndex >= IntensityList.Length)
                            continue;

                        if (MZList[intIndex] >= dblMZMinimum)
                        {
                            var dblMZDifference = dblMZToFind - MZList[intIndex];

                            if (Math.Abs(dblMZDifference) <= sngMatchTolerance)
                            {
                                if (IntensityList[intIndex] > sngIntensityMatch)
                                {
                                    sngIntensityMatch = IntensityList[intIndex];
                                }
                            }
                        }
                        else
                        {
                            // Assuming MZList is sorted on intensity, we can exit out of the loop once we pass dblMZMinimum
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                sngIntensityMatch = sngIntensityIfNotFound;
            }

            return sngIntensityMatch;
        }

        public virtual void Validate(bool blnComputeBasePeakAndTIC, bool blnUpdateMZRange)
        {
            if (blnComputeBasePeakAndTIC)
            {
                ComputeBasePeakAndTIC();
            }

            if (blnUpdateMZRange)
            {
                UpdateMZRange();
            }

            if (AutoShrinkDataLists)
            {
                if (MZList != null)
                {
                    if (MZList.Length > DataCount)
                    {
                        Array.Resize(ref MZList, DataCount);
                    }
                }

                if (IntensityList != null)
                {
                    if (IntensityList.Length > DataCount)
                    {
                        Array.Resize(ref IntensityList, DataCount);
                    }
                }
            }
        }
    }
}