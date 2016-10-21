Option Strict On

Imports System.Runtime.InteropServices
' This class holds the values associated with each spectrum in an DTA or MGF file
'
' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2006, Battelle Memorial Institute.  All Rights Reserved.
' Started March 24, 2006
'
' E-mail: matthew.monroe@pnl.gov or matt@alchemistmatt.com
' Website: http://omics.pnl.gov/ or http://www.sysbio.org/resources/staff/
' -------------------------------------------------------------------------------

<Serializable()>
Public Class clsSpectrumInfoMsMsText
    Inherits clsSpectrumInfo

    Public Sub New()
        Me.Clear()
    End Sub

    Public Const MAX_CHARGE_COUNT As Integer = 5

#Region "Spectrum Variables"

    Private mSpectrumTitleWithCommentChars As String
    Private mSpectrumTitle As String
    Private mParentIonLineText As String

    ' DTA files include this value, but not the MZ value
    Private mParentIonMH As Double

    Public ParentIonChargeCount As Integer

    ' 0 if unknown, otherwise typically 1, 2, or 3; Max index is MAX_CHARGE_COUNT-1
    Public ParentIonCharges() As Integer

    Private mChargeIs2And3Plus As Boolean

#End Region

#Region "Spectrum Variable Interface Functions"

    Public Property SpectrumTitleWithCommentChars() As String
        Get
            Return mSpectrumTitleWithCommentChars
        End Get
        Set(Value As String)
            MyBase.mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mSpectrumTitleWithCommentChars = Value
        End Set
    End Property

    Public Property SpectrumTitle() As String
        Get
            Return mSpectrumTitle
        End Get
        Set(Value As String)
            MyBase.mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mSpectrumTitle = Value
        End Set
    End Property

    Public Property ParentIonLineText() As String
        Get
            Return mParentIonLineText
        End Get
        Set(Value As String)
            MyBase.mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mParentIonLineText = Value
        End Set
    End Property

    Public Property ParentIonMH() As Double
        Get
            Return mParentIonMH
        End Get
        Set(Value As Double)
            MyBase.mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mParentIonMH = Value
        End Set
    End Property

    Public Property ChargeIs2And3Plus() As Boolean
        Get
            Return mChargeIs2And3Plus
        End Get
        Set(Value As Boolean)
            mChargeIs2And3Plus = Value
        End Set
    End Property

#End Region

    Public Overrides Sub Clear()
        MyBase.Clear()

        mSpectrumTitleWithCommentChars = String.Empty
        mSpectrumTitle = String.Empty
        mParentIonLineText = String.Empty
        mParentIonMH = 0

        ParentIonChargeCount = 0
        ReDim ParentIonCharges(MAX_CHARGE_COUNT - 1)

        mChargeIs2And3Plus = False
    End Sub

    Public Sub AddOrUpdateChargeList(intNewCharge As Integer, blnAddToExistingChargeList As Boolean)
        ' If blnAddToExistingChargeList is True, then adds intNewCharge to ParentIonCharges()
        ' Otherwise, clears ParentIonCharges and sets ParentIonCharges(0) to intNewCharge

        Dim intIndex, intCopyIndex As Integer
        Dim blnChargeAdded As Boolean

        Try
            If blnAddToExistingChargeList Then

                If ParentIonChargeCount < 0 Then ParentIonChargeCount = 0
                If ParentIonChargeCount < MAX_CHARGE_COUNT Then
                    ' Insert intNewCharge into ParentIonCharges() in the appropriate slot
                    blnChargeAdded = False
                    For intIndex = 0 To ParentIonChargeCount - 1
                        If ParentIonCharges(intIndex) = intNewCharge Then
                            ' Charge already exists
                            blnChargeAdded = True
                            Exit For
                        ElseIf ParentIonCharges(intIndex) > intNewCharge Then
                            ' Need to shift each of the existing charges up one
                            For intCopyIndex = ParentIonChargeCount To intIndex + 1 Step -1
                                ParentIonCharges(intCopyIndex) = ParentIonCharges(intCopyIndex - 1)
                            Next intCopyIndex
                            ParentIonCharges(intIndex) = intNewCharge
                            blnChargeAdded = True
                            Exit For
                        End If
                    Next

                    If Not blnChargeAdded Then
                        ParentIonCharges(ParentIonChargeCount) = intNewCharge
                        ParentIonChargeCount += 1
                    End If
                End If

            Else
                ParentIonChargeCount = 1
                Array.Clear(ParentIonCharges, 0, ParentIonCharges.Length)
                ParentIonCharges(0) = intNewCharge
            End If
        Catch ex As Exception
            ' Probably too many elements in ParentIonCharges() or memory not reserved for the array
            mErrorMessage = "Error in AddOrUpdateChargeList: " & ex.Message
        End Try
    End Sub

    Public Shadows Function Clone() As clsSpectrumInfoMsMsText

        ' First create a shallow copy of this object
        Dim objTarget = CType(Me.MemberwiseClone, clsSpectrumInfoMsMsText)

        ' Next, manually copy the array objects and any other objects
        With objTarget
            ' Duplicate code from the base class
            If Me.MZList Is Nothing Then
                .MZList = Nothing
            Else
                ReDim .MZList(Me.MZList.Length - 1)
                Me.MZList.CopyTo(.MZList, 0)
            End If

            If Me.IntensityList Is Nothing Then
                .IntensityList = Nothing
            Else
                ReDim .IntensityList(Me.IntensityList.Length - 1)
                Me.IntensityList.CopyTo(.IntensityList, 0)
            End If

            ' Code specific to clsSpectrumInfoMsMsText
            If Me.ParentIonCharges Is Nothing Then
                .ParentIonCharges = Nothing
            Else
                ReDim .ParentIonCharges(Me.ParentIonCharges.Length - 1)
                Me.ParentIonCharges.CopyTo(.ParentIonCharges, 0)
            End If
        End With

        Return objTarget
    End Function

    ''Public Function CloneDoesntWork() As clsSpectrumInfoMsMsText
    ''    Dim objTarget As clsSpectrumInfoMsMsText
    ''    Dim objTargetBase As clsSpectrumInfo

    ''    objTarget = New clsSpectrumInfoMsMsText
    ''    objTargetBase = objTarget

    ''    ' Note: Cannot use "objTarget = MyBase.Clone()" since the Clone() function 
    ''    '       in the base class returns clsSpectrumInfo
    ''    ' Could also use "objTarget = CType(MyBase.Clone(), clsSpectrumInfoMsMsText)" 
    ''    '  but what I've shown here works fine
    ''    objTargetBase = MyBase.Clone()

    ''    ' Now copy the members specific to clsSpectrumInfoMsMsText
    ''    ' Unfortunately, this re-copies the base class members too and 
    ''    '  creates shallow, reference-based copies of my arrays
    ''    objTarget = CType(Me.MemberwiseClone, clsSpectrumInfoMsMsText)

    ''    Return objTarget
    ''End Function

    Public Overloads Sub CopyTo(<Out()> ByRef objTarget As clsSpectrumInfoMsMsText)
        '' Note; in classes derived from clsSpectrumInfo, call MyBase.CopyTo() but do not call objTarget.Clear()
        ''Dim objTargetBase As clsSpectrumInfo

        ''If objTarget Is Nothing Then
        ''    objTarget = New clsSpectrumInfoMsMsText
        ''Else
        ''    objTarget.Clear()
        ''End If

        ''objTargetBase = objTarget
        ''MyBase.CopyTo(objTargetBase)

        '' Perform a deep copy of this class's members to objTarget
        ''With objTarget
        ''    .mSpectrumTitleWithCommentChars = Me.mSpectrumTitleWithCommentChars
        ''    .mSpectrumTitle = Me.mSpectrumTitle
        ''    .mParentIonLineText = Me.mParentIonLineText
        ''    .mParentIonMH = Me.mParentIonMH

        ''    .ParentIonChargeCount = Me.ParentIonChargeCount
        ''    If Me.ParentIonCharges Is Nothing Then
        ''        .ParentIonCharges = Nothing
        ''    Else
        ''        ReDim .ParentIonCharges(Me.ParentIonCharges.Length - 1)
        ''        Me.ParentIonCharges.CopyTo(.ParentIonCharges, 0)
        ''    End If

        ''    .mChargeIs2And3Plus = Me.mChargeIs2And3Plus
        ''End With

        objTarget = Me.Clone
    End Sub

    Public Overrides Sub Validate(blnComputeBasePeakAndTIC As Boolean, blnUpdateMZRange As Boolean)
        MyBase.Validate(blnComputeBasePeakAndTIC, blnUpdateMZRange)

        If Math.Abs(ParentIonMZ) > Single.Epsilon And Math.Abs(ParentIonMH) < Single.Epsilon Then
            If ParentIonChargeCount > 0 Then
                ParentIonMH = clsMSDataFileReaderBaseClass.ConvoluteMass(ParentIonMZ, ParentIonCharges(0), 1,
                                                                         clsMSDataFileReaderBaseClass.
                                                                            CHARGE_CARRIER_MASS_MONOISO)
            End If
        ElseIf Math.Abs(ParentIonMZ) < Single.Epsilon And Math.Abs(ParentIonMH) > Single.Epsilon Then
            If ParentIonChargeCount > 0 Then
                ParentIonMZ = clsMSDataFileReaderBaseClass.ConvoluteMass(ParentIonMH, 1, ParentIonCharges(0),
                                                                         clsMSDataFileReaderBaseClass.
                                                                            CHARGE_CARRIER_MASS_MONOISO)
            End If
        End If
    End Sub
End Class
