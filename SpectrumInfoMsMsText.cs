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
    /// This class holds the values associated with each spectrum in an DTA or MGF file
    /// </summary>
    [Serializable()]
    public class clsSpectrumInfoMsMsText : clsSpectrumInfo
    {
        public clsSpectrumInfoMsMsText()
        {
            Clear();
        }

        public const int MAX_CHARGE_COUNT = 5;

        private string mSpectrumTitleWithCommentChars;

        private string mSpectrumTitle;

        private string mParentIonLineText;

        // DTA files include this value, but not the MZ value
        private double mParentIonMH;

        public int ParentIonChargeCount { get; set; }

        // 0 if unknown, otherwise typically 1, 2, or 3; Max index is MAX_CHARGE_COUNT-1
        public int[] ParentIonCharges;

        public bool ChargeIs2And3Plus { get; set; }

        public string SpectrumTitleWithCommentChars
        {
            get => mSpectrumTitleWithCommentChars;

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mSpectrumTitleWithCommentChars = value;
            }
        }

        public string SpectrumTitle
        {
            get => mSpectrumTitle;

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mSpectrumTitle = value;
            }
        }

        public string ParentIonLineText
        {
            get => mParentIonLineText;

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mParentIonLineText = value;
            }
        }

        public double ParentIonMH
        {
            get => mParentIonMH;

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mParentIonMH = value;
            }
        }

        public override void Clear()
        {
            base.Clear();
            mSpectrumTitleWithCommentChars = string.Empty;
            mSpectrumTitle = string.Empty;
            mParentIonLineText = string.Empty;
            mParentIonMH = 0d;
            ParentIonChargeCount = 0;
            ParentIonCharges = new int[5];
            ChargeIs2And3Plus = false;
        }

        /// <summary>
        /// If blnAddToExistingChargeList is True, adds intNewCharge to the ParentIonCharges array
        /// Otherwise, clears ParentIonCharges and sets ParentIonCharges[0] to intNewCharge
        /// </summary>
        /// <param name="intNewCharge"></param>
        /// <param name="blnAddToExistingChargeList"></param>
        public void AddOrUpdateChargeList(int intNewCharge, bool blnAddToExistingChargeList)
        {
            try
            {
                if (blnAddToExistingChargeList)
                {
                    if (ParentIonChargeCount < 0)
                        ParentIonChargeCount = 0;

                    if (ParentIonChargeCount < MAX_CHARGE_COUNT)
                    {
                        // Insert intNewCharge into ParentIonCharges() in the appropriate slot
                        var blnChargeAdded = false;
                        var loopTo = ParentIonChargeCount - 1;
                        int intIndex;
                        for (intIndex = 0; intIndex <= loopTo; intIndex++)
                        {
                            if (ParentIonCharges[intIndex] == intNewCharge)
                            {
                                // Charge already exists
                                blnChargeAdded = true;
                                break;
                            }

                            if (ParentIonCharges[intIndex] > intNewCharge)
                            {
                                // Need to shift each of the existing charges up one
                                var loopTo1 = intIndex + 1;
                                int intCopyIndex;
                                for (intCopyIndex = ParentIonChargeCount; intCopyIndex >= loopTo1; intCopyIndex -= 1)
                                {
                                    ParentIonCharges[intCopyIndex] = ParentIonCharges[intCopyIndex - 1];
                                }

                                ParentIonCharges[intIndex] = intNewCharge;
                                blnChargeAdded = true;
                                break;
                            }
                        }

                        if (!blnChargeAdded)
                        {
                            ParentIonCharges[ParentIonChargeCount] = intNewCharge;
                            ParentIonChargeCount += 1;
                        }
                    }
                }
                else
                {
                    ParentIonChargeCount = 1;
                    Array.Clear(ParentIonCharges, 0, ParentIonCharges.Length);
                    ParentIonCharges[0] = intNewCharge;
                }
            }
            catch (Exception ex)
            {
                // Probably too many elements in ParentIonCharges() or memory not reserved for the array
                mErrorMessage = "Error in AddOrUpdateChargeList: " + ex.Message;
            }
        }

        /// <summary>
        /// Clone this spectrum object
        /// </summary>
        /// <returns>Deep copy of this spectrum</returns>
        public new clsSpectrumInfoMsMsText Clone()
        {
            // First create a shallow copy of this object
            var objTarget = (clsSpectrumInfoMsMsText)MemberwiseClone();

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

            // Code specific to clsSpectrumInfoMsMsText
            if (ParentIonCharges is null)
            {
                objTarget.ParentIonCharges = null;
            }
            else
            {
                objTarget.ParentIonCharges = new int[ParentIonCharges.Length];
                ParentIonCharges.CopyTo(objTarget.ParentIonCharges, 0);
            }

            return objTarget;
        }

        public void CopyTo(out clsSpectrumInfoMsMsText objTarget)
        {
            objTarget = Clone();
        }

        public override void Validate(bool blnComputeBasePeakAndTIC, bool blnUpdateMZRange)
        {
            base.Validate(blnComputeBasePeakAndTIC, blnUpdateMZRange);

            if (Math.Abs(ParentIonMZ) > float.Epsilon && Math.Abs(ParentIonMH) < float.Epsilon)
            {
                if (ParentIonChargeCount > 0)
                {
                    ParentIonMH = clsMSDataFileReaderBaseClass.ConvoluteMass(ParentIonMZ, ParentIonCharges[0], 1, clsMSDataFileReaderBaseClass.CHARGE_CARRIER_MASS_MONOISO);
                }
            }
            else if (Math.Abs(ParentIonMZ) < float.Epsilon && Math.Abs(ParentIonMH) > float.Epsilon)
            {
                if (ParentIonChargeCount > 0)
                {
                    ParentIonMZ = clsMSDataFileReaderBaseClass.ConvoluteMass(ParentIonMH, 1, ParentIonCharges[0], clsMSDataFileReaderBaseClass.CHARGE_CARRIER_MASS_MONOISO);
                }
            }
        }
    }
}