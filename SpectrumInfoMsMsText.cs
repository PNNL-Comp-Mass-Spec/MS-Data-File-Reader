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
    /// This class holds the values associated with each spectrum in an DTA or MGF file
    /// </summary>
    [Serializable]
    public class SpectrumInfoMsMsText : SpectrumInfo
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public SpectrumInfoMsMsText()
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
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mSpectrumTitleWithCommentChars = value;
            }
        }

        public string SpectrumTitle
        {
            get => mSpectrumTitle;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mSpectrumTitle = value;
            }
        }

        public string ParentIonLineText
        {
            get => mParentIonLineText;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mParentIonLineText = value;
            }
        }

        public double ParentIonMH
        {
            get => mParentIonMH;

            set
            {
                mSpectrumStatus = SpectrumStatusMode.DataDefined;
                mParentIonMH = value;
            }
        }

        /// <summary>
        /// Reset values to defaults
        /// </summary>
        public sealed override void Clear()
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
        /// If addToExistingChargeList is True, adds newCharge to the ParentIonCharges array
        /// Otherwise, clears ParentIonCharges and sets ParentIonCharges[0] to newCharge
        /// </summary>
        /// <param name="newCharge"></param>
        /// <param name="addToExistingChargeList"></param>
        public void AddOrUpdateChargeList(int newCharge, bool addToExistingChargeList)
        {
            try
            {
                if (addToExistingChargeList)
                {
                    if (ParentIonChargeCount < 0)
                        ParentIonChargeCount = 0;

                    if (ParentIonChargeCount < MAX_CHARGE_COUNT)
                    {
                        // Insert newCharge into ParentIonCharges() in the appropriate slot
                        var chargeAdded = false;
                        var indexEnd = ParentIonChargeCount - 1;

                        for (var index = 0; index <= indexEnd; index++)
                        {
                            if (ParentIonCharges[index] == newCharge)
                            {
                                // Charge already exists
                                chargeAdded = true;
                                break;
                            }

                            if (ParentIonCharges[index] > newCharge)
                            {
                                // Need to shift each of the existing charges up one
                                var copyIndexEnd = index + 1;

                                for (var copyIndex = ParentIonChargeCount; copyIndex >= copyIndexEnd; copyIndex--)
                                {
                                    ParentIonCharges[copyIndex] = ParentIonCharges[copyIndex - 1];
                                }

                                ParentIonCharges[index] = newCharge;
                                chargeAdded = true;
                                break;
                            }
                        }

                        if (!chargeAdded)
                        {
                            ParentIonCharges[ParentIonChargeCount] = newCharge;
                            ParentIonChargeCount++;
                        }
                    }
                }
                else
                {
                    ParentIonChargeCount = 1;
                    Array.Clear(ParentIonCharges, 0, ParentIonCharges.Length);
                    ParentIonCharges[0] = newCharge;
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
        public new SpectrumInfoMsMsText Clone()
        {
            // First create a shallow copy of this object
            var target = (SpectrumInfoMsMsText)MemberwiseClone();

            // Next, manually copy the array objects and any other objects
            // Duplicate code from the base class

            target.MzList.AddRange(MzList);
            target.IntensityList.AddRange(IntensityList);

            // Code specific to clsSpectrumInfoMsMsText
            if (ParentIonCharges is null)
            {
                target.ParentIonCharges = null;
            }
            else
            {
                target.ParentIonCharges = new int[ParentIonCharges.Length];
                ParentIonCharges.CopyTo(target.ParentIonCharges, 0);
            }

            return target;
        }

        public void CopyTo(out SpectrumInfoMsMsText target)
        {
            target = Clone();
        }

        public override void Validate(bool computeBasePeakAndTIC, bool updateMZRange)
        {
            base.Validate(computeBasePeakAndTIC, updateMZRange);

            if (Math.Abs(ParentIonMZ) > float.Epsilon && Math.Abs(ParentIonMH) < float.Epsilon)
            {
                if (ParentIonChargeCount > 0)
                {
                    ParentIonMH = MsDataFileReaderBaseClass.ConvoluteMass(ParentIonMZ, ParentIonCharges[0], 1, MsDataFileReaderBaseClass.CHARGE_CARRIER_MASS_MONOISOTOPIC);
                }
            }
            else if (Math.Abs(ParentIonMZ) < float.Epsilon && Math.Abs(ParentIonMH) > float.Epsilon)
            {
                if (ParentIonChargeCount > 0)
                {
                    ParentIonMZ = MsDataFileReaderBaseClass.ConvoluteMass(ParentIonMH, 1, ParentIonCharges[0], MsDataFileReaderBaseClass.CHARGE_CARRIER_MASS_MONOISOTOPIC);
                }
            }
        }
    }
}