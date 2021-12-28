using System;

namespace MSDataFileReader
{
    // This class holds the values associated with each spectrum in an mzData file
    // 
    // -------------------------------------------------------------------------------
    // Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    // Copyright 2006, Battelle Memorial Institute.  All Rights Reserved.
    // Started March 24, 2006
    // 
    // E-mail: matthew.monroe@pnl.gov or proteomics@pnnl.gov
    // Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://panomics.pnnl.gov/ or https://www.pnnl.gov/integrative-omics
    // -------------------------------------------------------------------------------

    [Serializable()]
    public class clsSpectrumInfoMzData : clsSpectrumInfo
    {
        public clsSpectrumInfoMzData()
        {
            Clear();
        }

        #region Constants and Enums

        public class EndianModes
        {
            public const string littleEndian = "little";
            public const string bigEndian = "big";
        }

        #endregion

        #region Spectrum Variables

        protected float mCollisionEnergy;
        protected string mCollisionEnergyUnits;
        protected string mCollisionMethod;
        protected string mScanMode;
        protected int mParentIonCharge;
        protected int mParentIonSpectrumMSLevel;
        protected int mParentIonSpectrumID;

        // Typically 32 or 64
        protected int mNumericPrecisionOfDataMZ;

        // See class EndianModes for values; typically EndianModes.littleEndian
        protected string mPeaksEndianModeMZ;

        // Typically 32 or 64
        protected int mNumericPrecisionOfDataIntensity;

        // See class EndianModes for values; typically EndianModes.littleEndian
        protected string mPeaksEndianModeIntensity;

        #endregion

        #region Classwide Variables

        #endregion

        #region Spectrum Variable Interface Functions

        public float CollisionEnergy
        {
            get
            {
                return mCollisionEnergy;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mCollisionEnergy = value;
            }
        }

        public string CollisionEnergyUnits
        {
            get
            {
                return mCollisionEnergyUnits;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mCollisionEnergyUnits = value;
            }
        }

        public string CollisionMethod
        {
            get
            {
                return mCollisionMethod;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mCollisionMethod = value;
            }
        }

        public int ParentIonCharge
        {
            get
            {
                return mParentIonCharge;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mParentIonCharge = value;
            }
        }

        public int ParentIonSpectrumMSLevel
        {
            get
            {
                return mParentIonSpectrumMSLevel;
            }

            set
            {
                mParentIonSpectrumMSLevel = value;
            }
        }

        public int ParentIonSpectrumID
        {
            get
            {
                return mParentIonSpectrumID;
            }

            set
            {
                mParentIonSpectrumID = value;
            }
        }

        public string ScanMode
        {
            get
            {
                return mScanMode;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mScanMode = value;
            }
        }

        public int NumericPrecisionOfDataMZ
        {
            get
            {
                return mNumericPrecisionOfDataMZ;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mNumericPrecisionOfDataMZ = value;
            }
        }

        public string PeaksEndianModeMZ
        {
            get
            {
                return mPeaksEndianModeMZ;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mPeaksEndianModeMZ = value;
            }
        }

        public int NumericPrecisionOfDataIntensity
        {
            get
            {
                return mNumericPrecisionOfDataIntensity;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mNumericPrecisionOfDataIntensity = value;
            }
        }

        public string PeaksEndianModeIntensity
        {
            get
            {
                return mPeaksEndianModeIntensity;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mPeaksEndianModeIntensity = value;
            }
        }

        #endregion

        public override void Clear()
        {
            base.Clear();
            mCollisionEnergy = 0f;
            mCollisionEnergyUnits = "Percent";
            mCollisionMethod = string.Empty;                  // Typically CID
            mScanMode = string.Empty;                         // Typically "MassScan"
            mParentIonCharge = 0;
            mParentIonSpectrumMSLevel = 1;
            mParentIonSpectrumID = 0;
            mNumericPrecisionOfDataMZ = 32;                   // Assume 32-bit for now
            mPeaksEndianModeMZ = EndianModes.littleEndian;
            mNumericPrecisionOfDataIntensity = 32;            // Assume 32-bit for now
            mPeaksEndianModeIntensity = EndianModes.littleEndian;
        }

        public clsBase64EncodeDecode.eEndianTypeConstants GetEndianModeValue(string strEndianModeText)
        {
            switch (strEndianModeText ?? "")
            {
                case EndianModes.bigEndian:
                    {
                        return clsBase64EncodeDecode.eEndianTypeConstants.BigEndian;
                    }

                case EndianModes.littleEndian:
                    {
                        return clsBase64EncodeDecode.eEndianTypeConstants.LittleEndian;
                    }

                default:
                    {
                        // Assume littleEndian
                        return clsBase64EncodeDecode.eEndianTypeConstants.LittleEndian;
                    }
            }
        }

        public new clsSpectrumInfoMzData Clone()
        {

            // First create a shallow copy of this object
            clsSpectrumInfoMzData objTarget = (clsSpectrumInfoMzData)MemberwiseClone();

            // Next, manually copy the array objects and any other objects
            // Duplicate code from the base class
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

        public void CopyTo(out clsSpectrumInfoMzData objTarget)
        {
            objTarget = Clone();
        }

        public void Validate()
        {
            Validate(true, false);
        }

        public override void Validate(bool blnComputeBasePeakAndTIC, bool blnUpdateMZRange)
        {
            base.Validate(blnComputeBasePeakAndTIC, blnUpdateMZRange);
            if (ScanNumber == 0 && SpectrumID != 0)
            {
                ScanNumber = SpectrumID;
                ScanNumberEnd = ScanNumber;
                ScanCount = 1;
            }

            mSpectrumStatus = eSpectrumStatusConstants.Validated;
        }
    }
}