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
    /// This class holds the values associated with each spectrum in an mzXML file
    /// </summary>
    [Serializable()]
    public class clsSpectrumInfoMzXML : clsSpectrumInfo
    {
        public clsSpectrumInfoMzXML()
        {
            Clear();
        }

        public class ByteOrderTypes
        {
            public const string network = "network";
        }

        public class CompressionTypes
        {
            public const string none = "none";

            public const string zlib = "zlib";
        }

        /// <summary>
        /// Tracks pairOrder for mzXML v1.x and v2.x
        /// Tracks contentType for mzXML 3.x files
        /// </summary>
        /// <remarks></remarks>
        public class PairOrderTypes
        {
            public const string MZandIntensity = "m/z-int";

            public const string IntensityAndMZ = "int-m/z";

            public const string MZ = "m/z";

            public const string Intensity = "intensity";

            public const string SN = "S/N";

            public const string Charge = "charge";

            public const string MZRuler = "m/z ruler";

            public const string TOF = "TOF";
        }

        public class ScanTypeNames
        {
            public const string Full = "Full";

            public const string zoom = "zoom";

            public const string SIM = "SIM";

            public const string SRM = "SRM";      // MRM is synonymous with SRM
            public const string CRM = "CRM";

            public const string Q1 = "Q1";

            public const string Q3 = "Q3";

            public const string MRM = "MRM";
        }

        protected float mCollisionEnergy;

        // See class ScanTypeNames for typical names
        protected string mScanType;

        // Thermo-specific filter line text
        protected string mFilterLine;

        // Low m/z boundary (this is the instrumental setting)
        protected float mStartMZ;

        // High m/z boundary (this is the instrumental setting)
        protected float mEndMZ;

        // Typically 32 or 64
        protected int mNumericPrecisionOfData;

        // See class ByteOrderTypes for values; typically ByteOrderTypes.network
        protected string mPeaksByteOrder;

        // See class PairOrderTypes for values; typically PairOrderTypes.MZandIntensity; stores contentType for mzXML v3.x
        protected string mPeaksPairOrder;

        // See class CompressionTypes for values; will be "none" or "zlib"
        protected string mCompressionType;

        protected int mCompressedLen;

        protected string mActivationMethod;

        protected float mIsolationWindow;

        protected int mParentIonCharge;

        protected int mPrecursorScanNum;

        public string ActivationMethod
        {
            get
            {
                return mActivationMethod;
            }

            set
            {
                mActivationMethod = value;
            }
        }

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

        public string FilterLine
        {
            get
            {
                return mFilterLine;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mFilterLine = value;
            }
        }

        public int NumericPrecisionOfData
        {
            get
            {
                return mNumericPrecisionOfData;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mNumericPrecisionOfData = value;
            }
        }

        public string PeaksByteOrder
        {
            get
            {
                return mPeaksByteOrder;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mPeaksByteOrder = value;
            }
        }

        public string PeaksPairOrder
        {
            get
            {
                return mPeaksPairOrder;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mPeaksPairOrder = value;
            }
        }

        public string CompressionType
        {
            get
            {
                return mCompressionType;
            }

            set
            {
                mCompressionType = value;
            }
        }

        public int CompressedLen
        {
            get
            {
                return mCompressedLen;
            }

            set
            {
                mCompressedLen = value;
            }
        }

        public float EndMZ
        {
            get
            {
                return mEndMZ;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mEndMZ = value;
            }
        }

        public float IsolationWindow
        {
            get
            {
                return mIsolationWindow;
            }

            set
            {
                mIsolationWindow = value;
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
                mParentIonCharge = value;
            }
        }

        public int PrecursorScanNum
        {
            get
            {
                return mPrecursorScanNum;
            }

            set
            {
                mPrecursorScanNum = value;
            }
        }

        public float StartMZ
        {
            get
            {
                return mStartMZ;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mStartMZ = value;
            }
        }

        public string ScanType
        {
            get
            {
                return mScanType;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mScanType = value;
            }
        }

        public override void Clear()
        {
            base.Clear();
            mCollisionEnergy = 0f;
            mScanType = ScanTypeNames.Full;
            mFilterLine = string.Empty;
            mStartMZ = 0f;
            mEndMZ = 0f;
            mNumericPrecisionOfData = 32;            // Assume 32-bit for now
            mPeaksByteOrder = ByteOrderTypes.network;
            mPeaksPairOrder = PairOrderTypes.MZandIntensity;
            mCompressionType = CompressionTypes.none;
            mCompressedLen = 0;
            mParentIonCharge = 0;
            mActivationMethod = string.Empty;
            mIsolationWindow = 0f;
            mPrecursorScanNum = 0;
        }

        /// <summary>
        /// Clone this spectrum object
        /// </summary>
        /// <returns>Deep copy of this spectrum</returns>
        public new clsSpectrumInfoMzXML Clone()
        {
            // First create a shallow copy of this object
            var objTarget = (clsSpectrumInfoMzXML)MemberwiseClone();

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

        public void CopyTo(out clsSpectrumInfoMzXML objTarget)
        {
            objTarget = Clone();
        }

        public void Validate()
        {
            Validate(false, false);
        }

        public override void Validate(bool blnComputeBasePeakAndTIC, bool blnUpdateMZRange)
        {
            base.Validate(blnComputeBasePeakAndTIC, blnUpdateMZRange);

            if (SpectrumID == 0 && ScanNumber != 0)
            {
                SpectrumID = ScanNumber;
            }

            mSpectrumStatus = eSpectrumStatusConstants.Validated;
        }
    }
}