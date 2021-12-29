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
    /// This class holds the values associated with each spectrum in an mzData file
    /// </summary>
    [Serializable]
    public class SpectrumInfoMzData : SpectrumInfo
    {
        public SpectrumInfoMzData()
        {
            Clear();
        }

        public static class EndianModes
        {
            public const string littleEndian = "little";

            public const string bigEndian = "big";
        }

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

        public float CollisionEnergy
        {
            get => mCollisionEnergy;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mCollisionEnergy = value;
            }
        }

        public string CollisionEnergyUnits
        {
            get => mCollisionEnergyUnits;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mCollisionEnergyUnits = value;
            }
        }

        public string CollisionMethod
        {
            get => mCollisionMethod;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mCollisionMethod = value;
            }
        }

        public int ParentIonCharge
        {
            get => mParentIonCharge;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mParentIonCharge = value;
            }
        }

        public int ParentIonSpectrumMSLevel
        {
            get => mParentIonSpectrumMSLevel;

            set => mParentIonSpectrumMSLevel = value;
        }

        public int ParentIonSpectrumID
        {
            get => mParentIonSpectrumID;

            set => mParentIonSpectrumID = value;
        }

        public string ScanMode
        {
            get => mScanMode;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mScanMode = value;
            }
        }

        public int NumericPrecisionOfDataMZ
        {
            get => mNumericPrecisionOfDataMZ;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mNumericPrecisionOfDataMZ = value;
            }
        }

        public string PeaksEndianModeMZ
        {
            get => mPeaksEndianModeMZ;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mPeaksEndianModeMZ = value;
            }
        }

        public int NumericPrecisionOfDataIntensity
        {
            get => mNumericPrecisionOfDataIntensity;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mNumericPrecisionOfDataIntensity = value;
            }
        }

        public string PeaksEndianModeIntensity
        {
            get => mPeaksEndianModeIntensity;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mPeaksEndianModeIntensity = value;
            }
        }

        public sealed override void Clear()
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

        public Base64EncodeDecode.EndianType GetEndianModeValue(string strEndianModeText)
        {
            switch (strEndianModeText)
            {
                case EndianModes.bigEndian:
                    return Base64EncodeDecode.EndianType.BigEndian;

                case EndianModes.littleEndian:
                    return Base64EncodeDecode.EndianType.LittleEndian;

                default:
                    // Assume littleEndian
                    return Base64EncodeDecode.EndianType.LittleEndian;
            }
        }

        /// <summary>
        /// Clone this spectrum object
        /// </summary>
        /// <returns>Deep copy of this spectrum</returns>
        public new SpectrumInfoMzData Clone()
        {
            // First create a shallow copy of this object
            var objTarget = (SpectrumInfoMzData)MemberwiseClone();

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

        public void CopyTo(out SpectrumInfoMzData objTarget)
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

            mSpectrumStatus = SpectrumStatusMode.Validated;
        }
    }
}