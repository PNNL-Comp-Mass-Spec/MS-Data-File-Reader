using System;

namespace MSDataFileReader
{
    // This class holds the values associated with each spectrum in an MS Data file
    //
    // -------------------------------------------------------------------------------
    // Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    // Copyright 2006, Battelle Memorial Institute.  All Rights Reserved.
    // Started March 23, 2006
    //
    // E-mail: matthew.monroe@pnl.gov or proteomics@pnnl.gov
    // Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://panomics.pnnl.gov/ or https://www.pnnl.gov/integrative-omics
    // -------------------------------------------------------------------------------

    [Serializable()]
    public class clsSpectrumInfo : ICloneable
    {
        public clsSpectrumInfo()
        {
            mAutoShrinkDataLists = true;
            Clear();
        }

        #region Constants and Enums

        public class SpectrumTypeNames
        {
            public const string discrete = "discrete";
            public const string continuous = "continuous";
        }

        public enum eSpectrumStatusConstants
        {
            Initialized = 0,                     // This is set when .Clear() is called
            DataDefined = 1,                     // This is set when any of the values are set via a property
            Validated = 2                       // This is set when .Validate() is called
        }

        #endregion

        #region Spectrum Variables

        private int mSpectrumID;                // Spectrum ID number; often the same as ScanNumber
        private int mScanNumber;                // First scan number if ScanCount is > 1
        private int mScanCount;                 // Number of spectra combined together to get the given spectrum
        private int mScanNumberEnd;             // Last scan if more than one scan was combined to make this spectrum
        private string mSpectrumType;               // See Class SpectrumTypeNames for typical names (discrete or continuous)
        private string mSpectrumCombinationMethod;
        private int mMSLevel;                   // 1 for MS, 2 for MS/MS, 3 for MS^3, etc.
        private bool mCentroided;                // True if the data is centroided (supported by mzXML v3.x)
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

        #endregion

        #region Classwide Variables
        // When mAutoShrinkDataLists is True, then MZList().Length and IntensityList().Length will equal DataCount;
        // When mAutoShrinkDataLists is False, then the memory will not be freed when DataCount shrinks or .Clear() is called
        // Setting mAutoShrinkDataLists to False helps reduce slow, increased memory usage due to inefficient garbage collection
        private bool mAutoShrinkDataLists;
        protected string mErrorMessage;
        protected eSpectrumStatusConstants mSpectrumStatus;

        #endregion

        #region Spectrum Variable Interface Functions

        public int SpectrumID
        {
            get
            {
                return mSpectrumID;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mSpectrumID = value;
            }
        }

        public int ScanNumber
        {
            get
            {
                return mScanNumber;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mScanNumber = value;
            }
        }

        public int ScanCount
        {
            get
            {
                return mScanCount;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mScanCount = value;
            }
        }

        public int ScanNumberEnd
        {
            get
            {
                return mScanNumberEnd;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mScanNumberEnd = value;
            }
        }

        public string SpectrumType
        {
            get
            {
                return mSpectrumType;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mSpectrumType = value;
            }
        }

        public string SpectrumCombinationMethod
        {
            get
            {
                return mSpectrumCombinationMethod;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mSpectrumCombinationMethod = value;
            }
        }

        public int MSLevel
        {
            get
            {
                return mMSLevel;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mMSLevel = value;
            }
        }

        public bool Centroided
        {
            get
            {
                return mCentroided;
            }

            set
            {
                mCentroided = value;
            }
        }

        public string Polarity
        {
            get
            {
                return mPolarity;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mPolarity = value;
            }
        }

        public float RetentionTimeMin
        {
            get
            {
                return mRetentionTimeMin;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mRetentionTimeMin = value;
            }
        }

        public float mzRangeStart
        {
            get
            {
                return mmzRangeStart;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mmzRangeStart = value;
            }
        }

        public float mzRangeEnd
        {
            get
            {
                return mmzRangeEnd;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mmzRangeEnd = value;
            }
        }

        public double BasePeakMZ
        {
            get
            {
                return mBasePeakMZ;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mBasePeakMZ = value;
            }
        }

        public float BasePeakIntensity
        {
            get
            {
                return mBasePeakIntensity;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mBasePeakIntensity = value;
            }
        }

        public double TotalIonCurrent
        {
            get
            {
                return mTotalIonCurrent;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mTotalIonCurrent = value;
            }
        }

        public double ParentIonMZ
        {
            get
            {
                return mParentIonMZ;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mParentIonMZ = value;
            }
        }

        public float ParentIonIntensity
        {
            get
            {
                return mParentIonIntensity;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mParentIonIntensity = value;
            }
        }

        public eSpectrumStatusConstants SpectrumStatus
        {
            get
            {
                return mSpectrumStatus;
            }
        }

        #endregion

        #region Processing Options

        public bool AutoShrinkDataLists
        {
            get
            {
                return mAutoShrinkDataLists;
            }

            set
            {
                mAutoShrinkDataLists = value;
            }
        }

        public string ErrorMessage
        {
            get
            {
                if (mErrorMessage is null)
                    mErrorMessage = string.Empty;
                return mErrorMessage;
            }
        }

        #endregion

        public virtual void Clear()
        {
            mSpectrumID = 0;
            mScanNumber = 0;
            mScanCount = 0;
            mScanNumberEnd = 0;
            mSpectrumType = SpectrumTypeNames.discrete;
            mSpectrumCombinationMethod = string.Empty;
            mMSLevel = 1;
            mCentroided = false;
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
            if (mAutoShrinkDataLists || MZList is null)
            {
                MZList = new double[0];
            }
            else
            {
                Array.Clear(MZList, 0, MZList.Length);
            }

            if (mAutoShrinkDataLists || IntensityList is null)
            {
                IntensityList = new float[0];
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

        public clsSpectrumInfo Clone()
        {
            // Note: Clone() functions in the derived SpectrumInfo classes Shadow this function and duplicate its code

            // First create a shallow copy of this object
            clsSpectrumInfo objTarget = (clsSpectrumInfo)MemberwiseClone();

            // Next, manually copy the array objects and any other objects
            // Note: Since Clone() functions in the derived classes Shadow this function,
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
            float sngMzRangeStart = 0f;
            float sngMzRangeEnd = 0f;
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
            int intIndex;
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
                    var loopTo = DataCount - 1;
                    for (intIndex = 1; intIndex <= loopTo; intIndex++)
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

        public float LookupIonIntensityByMZ(double dblMZToFind, float sngIntensityIfNotFound, float sngMatchTolerance = 0.05f)
        {
            // Looks for dblMZToFind in this spectrum's data
            // If found, returns the intensity
            // If not found, returns an intensity of sngIntensityIfNotFound

            float sngIntensityMatch;
            double dblMZMinimum;
            double dblMZDifference;
            int intIndex;
            try
            {
                // Define the minimum MZ value to consider
                dblMZMinimum = dblMZToFind - sngMatchTolerance;
                sngIntensityMatch = sngIntensityIfNotFound;
                if (!(MZList is null | IntensityList is null))
                {
                    for (intIndex = DataCount - 1; intIndex >= 0; intIndex -= 1)
                    {
                        if (intIndex < MZList.Length & intIndex < IntensityList.Length)
                        {
                            if (MZList[intIndex] >= dblMZMinimum)
                            {
                                dblMZDifference = dblMZToFind - MZList[intIndex];
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

            if (mAutoShrinkDataLists)
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