// -------------------------------------------------------------------------------
// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
// Copyright 2021, Battelle Memorial Institute.  All Rights Reserved.
//
// E-mail: matthew.monroe@pnl.gov or proteomics@pnnl.gov
// Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://www.pnnl.gov/integrative-omics
// -------------------------------------------------------------------------------

using System;

namespace MSDataFileReader
{
    /// <summary>
    /// This class holds the values associated with each spectrum in an mzXML file
    /// </summary>
    [Serializable]
    public class SpectrumInfoMzXML : SpectrumInfo
    {
        // Ignore Spelling: zlib

        /// <summary>
        /// Constructor
        /// </summary>
        public SpectrumInfoMzXML()
        {
            Clear();
        }

        /// <summary>
        /// Byte order types
        /// </summary>
        public static class ByteOrderTypes
        {
            public const string Network = "network";
        }

        /// <summary>
        /// Compression types
        /// </summary>
        public static class CompressionTypes
        {
            public const string None = "none";

            public const string ZLib = "zlib";
        }

        // ReSharper disable UnusedMember.Global

        /// <summary>
        /// Tracks pairOrder for mzXML v1.x and v2.x
        /// Tracks contentType for mzXML 3.x files
        /// </summary>
        public static class PairOrderTypes
        {
            public const string MzAndIntensity = "m/z-int";

            public const string IntensityAndMz = "int-m/z";

            public const string MZ = "m/z";

            public const string Intensity = "intensity";

            public const string SN = "S/N";

            public const string Charge = "charge";

            public const string MzRuler = "m/z ruler";

            public const string TOF = "TOF";
        }

        public static class ScanTypeNames
        {
            public const string Full = "Full";

            public const string Zoom = "zoom";

            public const string SIM = "SIM";

            // MRM is synonymous with SRM
            public const string SRM = "SRM";

            public const string CRM = "CRM";

            public const string Q1 = "Q1";

            public const string Q3 = "Q3";

            public const string MRM = "MRM";
        }

        // ReSharper restore UnusedMember.Global

        protected float mCollisionEnergy;

        protected string mScanType;

        protected string mFilterLine;

        protected float mStartMZ;

        protected float mEndMZ;

        protected int mNumericPrecisionOfData;

        protected string mPeaksByteOrder;

        protected string mPeaksPairOrder;

        protected string mCompressionType;

        protected int mCompressedLen;

        protected string mActivationMethod;

        protected float mIsolationWindow;

        protected int mParentIonCharge;

        protected int mPrecursorScanNum;

        /// <summary>
        /// Activation method
        /// </summary>
        public string ActivationMethod
        {
            get => mActivationMethod;

            set => mActivationMethod = value;
        }

        /// <summary>
        /// Collision energy
        /// </summary>
        public float CollisionEnergy
        {
            get => mCollisionEnergy;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mCollisionEnergy = value;
            }
        }

        /// <summary>
        /// Thermo-specific filter line text
        /// </summary>
        /// <remarks>
        /// Only present if the file was created with ReAdW
        /// </remarks>
        public string FilterLine
        {
            get => mFilterLine;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mFilterLine = value;
            }
        }

        /// <summary>
        /// Numeric precision
        /// </summary>
        /// <remarks>
        /// Typically 32 or 64
        /// </remarks>
        public int NumericPrecisionOfData
        {
            get => mNumericPrecisionOfData;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mNumericPrecisionOfData = value;
            }
        }

        /// <summary>
        /// Byte order for peak data
        /// </summary>
        /// <remarks>
        /// See class ByteOrderTypes for values; typically ByteOrderTypes.network
        /// </remarks>
        public string PeaksByteOrder
        {
            get => mPeaksByteOrder;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mPeaksByteOrder = value;
            }
        }

        /// <summary>
        /// Pair order for peak data
        /// </summary>
        /// <remarks>
        /// See class PairOrderTypes for values; typically PairOrderTypes.MzAndIntensity; stores contentType for mzXML v3.x
        /// </remarks>
        public string PeaksPairOrder
        {
            get => mPeaksPairOrder;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mPeaksPairOrder = value;
            }
        }

        /// <summary>
        /// Compression type
        /// </summary>
        /// <remarks>
        /// See class CompressionTypes for values; will be "none" or "zlib"
        /// </remarks>
        public string CompressionType
        {
            get => mCompressionType;

            set => mCompressionType = value;
        }

        /// <summary>
        /// Compressed length
        /// </summary>
        public int CompressedLen
        {
            get => mCompressedLen;

            set => mCompressedLen = value;
        }

        /// <summary>
        /// High m/z boundary (instrument setting, not the highest observed value)
        /// </summary>
        /// <remarks>
        /// Not present in .mzXML files created with ReAdW
        /// </remarks>
        public float EndMZ
        {
            get => mEndMZ;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mEndMZ = value;
            }
        }

        /// <summary>
        /// Precursor ion isolation window
        /// </summary>
        public float IsolationWindow
        {
            get => mIsolationWindow;

            set => mIsolationWindow = value;
        }

        /// <summary>
        /// Precursor ion (parent ion) charge
        /// </summary>
        public int ParentIonCharge
        {
            get => mParentIonCharge;

            set => mParentIonCharge = value;
        }

        /// <summary>
        /// Precursor (parent() scan number
        /// </summary>
        /// <remarks>
        /// 0 if no precursor scan
        /// </remarks>
        public int PrecursorScanNum
        {
            get => mPrecursorScanNum;

            set => mPrecursorScanNum = value;
        }

        /// <summary>
        /// Low m/z boundary (instrument setting, not the lowest observed value)
        /// </summary>
        /// <remarks>
        /// Not present in .mzXML files created with ReAdW
        /// </remarks>
        public float StartMZ
        {
            get => mStartMZ;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mStartMZ = value;
            }
        }

        /// <summary>
        /// Scan type
        /// </summary>
        /// <remarks>
        /// See class ScanTypeNames for typical names
        /// </remarks>
        public string ScanType
        {
            get => mScanType;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mScanType = value;
            }
        }

        /// <summary>
        /// Reset values to defaults
        /// </summary>
        public sealed override void Clear()
        {
            base.Clear();
            mCollisionEnergy = 0f;
            mScanType = ScanTypeNames.Full;
            mFilterLine = string.Empty;
            mStartMZ = 0f;
            mEndMZ = 0f;
            mNumericPrecisionOfData = 32;            // Assume 32-bit for now
            mPeaksByteOrder = ByteOrderTypes.Network;
            mPeaksPairOrder = PairOrderTypes.MzAndIntensity;
            mCompressionType = CompressionTypes.None;
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
        public new SpectrumInfoMzXML Clone()
        {
            // First create a shallow copy of this object
            var target = (SpectrumInfoMzXML)MemberwiseClone();

            // Next, manually copy the array objects and any other objects
            // Duplicate code from the base class

            target.MzList.AddRange(MzList);
            target.IntensityList.AddRange(IntensityList);

            return target;
        }

        public void CopyTo(out SpectrumInfoMzXML target)
        {
            target = Clone();
        }

        public void Validate()
        {
            Validate(false, false);
        }

        public override void Validate(bool computeBasePeakAndTIC, bool updateMZRange)
        {
            base.Validate(computeBasePeakAndTIC, updateMZRange);

            if (SpectrumID == 0 && ScanNumber != 0)
            {
                SpectrumID = ScanNumber;
            }

            mSpectrumStatus = SpectrumStatusMode.Validated;
        }
    }
}