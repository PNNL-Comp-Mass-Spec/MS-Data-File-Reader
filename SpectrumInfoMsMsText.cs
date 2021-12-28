﻿using System;

namespace MSDataFileReader
{
    // This class holds the values associated with each spectrum in an DTA or MGF file
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
    public class clsSpectrumInfoMsMsText : clsSpectrumInfo
    {
        public clsSpectrumInfoMsMsText()
        {
            Clear();
        }

        public const int MAX_CHARGE_COUNT = 5;

        #region Spectrum Variables

        private string mSpectrumTitleWithCommentChars;
        private string mSpectrumTitle;
        private string mParentIonLineText;

        // DTA files include this value, but not the MZ value
        private double mParentIonMH;
        public int ParentIonChargeCount;

        // 0 if unknown, otherwise typically 1, 2, or 3; Max index is MAX_CHARGE_COUNT-1
        public int[] ParentIonCharges;
        private bool mChargeIs2And3Plus;

        #endregion

        #region Spectrum Variable Interface Functions

        public string SpectrumTitleWithCommentChars
        {
            get
            {
                return mSpectrumTitleWithCommentChars;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mSpectrumTitleWithCommentChars = value;
            }
        }

        public string SpectrumTitle
        {
            get
            {
                return mSpectrumTitle;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mSpectrumTitle = value;
            }
        }

        public string ParentIonLineText
        {
            get
            {
                return mParentIonLineText;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mParentIonLineText = value;
            }
        }

        public double ParentIonMH
        {
            get
            {
                return mParentIonMH;
            }

            set
            {
                mSpectrumStatus = eSpectrumStatusConstants.DataDefined;
                mParentIonMH = value;
            }
        }

        public bool ChargeIs2And3Plus
        {
            get
            {
                return mChargeIs2And3Plus;
            }

            set
            {
                mChargeIs2And3Plus = value;
            }
        }

        #endregion

        public override void Clear()
        {
            base.Clear();
            mSpectrumTitleWithCommentChars = string.Empty;
            mSpectrumTitle = string.Empty;
            mParentIonLineText = string.Empty;
            mParentIonMH = 0d;
            ParentIonChargeCount = 0;
            ParentIonCharges = new int[5];
            mChargeIs2And3Plus = false;
        }

        public void AddOrUpdateChargeList(int intNewCharge, bool blnAddToExistingChargeList)
        {
            // If blnAddToExistingChargeList is True, then adds intNewCharge to ParentIonCharges()
            // Otherwise, clears ParentIonCharges and sets ParentIonCharges(0) to intNewCharge

            int intIndex, intCopyIndex;
            bool blnChargeAdded;
            try
            {
                if (blnAddToExistingChargeList)
                {
                    if (ParentIonChargeCount < 0)
                        ParentIonChargeCount = 0;
                    if (ParentIonChargeCount < MAX_CHARGE_COUNT)
                    {
                        // Insert intNewCharge into ParentIonCharges() in the appropriate slot
                        blnChargeAdded = false;
                        var loopTo = ParentIonChargeCount - 1;
                        for (intIndex = 0; intIndex <= loopTo; intIndex++)
                        {
                            if (ParentIonCharges[intIndex] == intNewCharge)
                            {
                                // Charge already exists
                                blnChargeAdded = true;
                                break;
                            }
                            else if (ParentIonCharges[intIndex] > intNewCharge)
                            {
                                // Need to shift each of the existing charges up one
                                var loopTo1 = intIndex + 1;
                                for (intCopyIndex = ParentIonChargeCount; intCopyIndex >= loopTo1; intCopyIndex -= 1)
                                    ParentIonCharges[intCopyIndex] = ParentIonCharges[intCopyIndex - 1];
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

        public new clsSpectrumInfoMsMsText Clone()
        {

            // First create a shallow copy of this object
            clsSpectrumInfoMsMsText objTarget = (clsSpectrumInfoMsMsText)MemberwiseClone();

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

        // 'Public Function CloneDoesntWork() As clsSpectrumInfoMsMsText
        // '    Dim objTarget As clsSpectrumInfoMsMsText
        // '    Dim objTargetBase As clsSpectrumInfo

        // '    objTarget = New clsSpectrumInfoMsMsText
        // '    objTargetBase = objTarget

        // '    ' Note: Cannot use "objTarget = MyBase.Clone()" since the Clone() function
        // '    '       in the base class returns clsSpectrumInfo
        // '    ' Could also use "objTarget = CType(MyBase.Clone(), clsSpectrumInfoMsMsText)"
        // '    '  but what I've shown here works fine
        // '    objTargetBase = MyBase.Clone()

        // '    ' Now copy the members specific to clsSpectrumInfoMsMsText
        // '    ' Unfortunately, this re-copies the base class members too and
        // '    '  creates shallow, reference-based copies of my arrays
        // '    objTarget = CType(Me.MemberwiseClone, clsSpectrumInfoMsMsText)

        // '    Return objTarget
        // 'End Function

        public void CopyTo(out clsSpectrumInfoMsMsText objTarget)
        {
            // ' Note; in classes derived from clsSpectrumInfo, call MyBase.CopyTo() but do not call objTarget.Clear()
            // 'Dim objTargetBase As clsSpectrumInfo

            // 'If objTarget Is Nothing Then
            // '    objTarget = New clsSpectrumInfoMsMsText
            // 'Else
            // '    objTarget.Clear()
            // 'End If

            // 'objTargetBase = objTarget
            // 'MyBase.CopyTo(objTargetBase)

            // ' Perform a deep copy of this class's members to objTarget
            // 'With objTarget
            // '    .mSpectrumTitleWithCommentChars = Me.mSpectrumTitleWithCommentChars
            // '    .mSpectrumTitle = Me.mSpectrumTitle
            // '    .mParentIonLineText = Me.mParentIonLineText
            // '    .mParentIonMH = Me.mParentIonMH

            // '    .ParentIonChargeCount = Me.ParentIonChargeCount
            // '    If Me.ParentIonCharges Is Nothing Then
            // '        .ParentIonCharges = Nothing
            // '    Else
            // '        ReDim .ParentIonCharges(Me.ParentIonCharges.Length - 1)
            // '        Me.ParentIonCharges.CopyTo(.ParentIonCharges, 0)
            // '    End If

            // '    .mChargeIs2And3Plus = Me.mChargeIs2And3Plus
            // 'End With

            objTarget = Clone();
        }

        public override void Validate(bool blnComputeBasePeakAndTIC, bool blnUpdateMZRange)
        {
            base.Validate(blnComputeBasePeakAndTIC, blnUpdateMZRange);
            if (Math.Abs(ParentIonMZ) > float.Epsilon & Math.Abs(ParentIonMH) < float.Epsilon)
            {
                if (ParentIonChargeCount > 0)
                {
                    ParentIonMH = clsMSDataFileReaderBaseClass.ConvoluteMass(ParentIonMZ, ParentIonCharges[0], 1, clsMSDataFileReaderBaseClass.CHARGE_CARRIER_MASS_MONOISO);
                }
            }
            else if (Math.Abs(ParentIonMZ) < float.Epsilon & Math.Abs(ParentIonMH) > float.Epsilon)
            {
                if (ParentIonChargeCount > 0)
                {
                    ParentIonMZ = clsMSDataFileReaderBaseClass.ConvoluteMass(ParentIonMH, 1, ParentIonCharges[0], clsMSDataFileReaderBaseClass.CHARGE_CARRIER_MASS_MONOISO);
                }
            }
        }
    }
}