Option Strict On

Imports System.Runtime.InteropServices
' This class holds the values associated with each spectrum in an mzData file
'
' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2006, Battelle Memorial Institute.  All Rights Reserved.
' Started March 24, 2006
'
' E-mail: matthew.monroe@pnl.gov or matt@alchemistmatt.com
' Website: http://ncrr.pnl.gov/ or http://www.sysbio.org/resources/staff/
' -------------------------------------------------------------------------------
'
' Last modified April 4, 2006

<Serializable()> _
Public Class clsSpectrumInfoMzData
    Inherits clsSpectrumInfo

    Public Sub New()
        Me.Clear()
    End Sub

#Region "Constants and Enums"
    Public Class EndianModes
        Public Const littleEndian As String = "little"
        Public Const bigEndian As String = "big"
    End Class
#End Region

#Region "Spectrum Variables"
    Protected mCollisionEnergy As Single
    Protected mCollisionEnergyUnits As String
    Protected mCollisionMethod As String

    Protected mScanMode As String
    Protected mParentIonCharge As Integer
    Protected mParentIonSpectrumMSLevel As Integer
    Protected mParentIonSpectrumID As Integer

    Protected mNumericPrecisionOfDataMZ As Integer          ' Typically 32 or 64
    Protected mPeaksEndianModeMZ As String                  ' See class EndianModes for values; typically EndianModes.littleEndian

    Protected mNumericPrecisionOfDataIntensity As Integer   ' Typically 32 or 64
    Protected mPeaksEndianModeIntensity As String           ' See class EndianModes for values; typically EndianModes.littleEndian
#End Region

#Region "Classwide Variables"
#End Region

#Region "Spectrum Variable Interface Functions"
    Public Property CollisionEnergy() As Single
        Get
            Return mCollisionEnergy
        End Get
        Set(ByVal Value As Single)
            MyBase.mSpectrumStatus = clsSpectrumInfo.eSpectrumStatusConstants.DataDefined
            mCollisionEnergy = Value
        End Set
    End Property
    Public Property CollisionEnergyUnits() As String
        Get
            Return mCollisionEnergyUnits
        End Get
        Set(ByVal Value As String)
            MyBase.mSpectrumStatus = clsSpectrumInfo.eSpectrumStatusConstants.DataDefined
            mCollisionEnergyUnits = Value
        End Set
    End Property
    Public Property CollisionMethod() As String
        Get
            Return mCollisionMethod
        End Get
        Set(ByVal Value As String)
            MyBase.mSpectrumStatus = clsSpectrumInfo.eSpectrumStatusConstants.DataDefined
            mCollisionMethod = Value
        End Set
    End Property

    Public Property ParentIonCharge() As Integer
        Get
            Return mParentIonCharge
        End Get
        Set(ByVal Value As Integer)
            MyBase.mSpectrumStatus = clsSpectrumInfo.eSpectrumStatusConstants.DataDefined
            mParentIonCharge = Value
        End Set
    End Property

    Public Property ParentIonSpectrumMSLevel() As Integer
        Get
            Return mParentIonSpectrumMSLevel
        End Get
        Set(ByVal Value As Integer)
            mParentIonSpectrumMSLevel = Value
        End Set
    End Property
    Public Property ParentIonSpectrumID() As Integer
        Get
            Return mParentIonSpectrumID
        End Get
        Set(ByVal Value As Integer)
            mParentIonSpectrumID = Value
        End Set
    End Property

    Public Property ScanMode() As String
        Get
            Return mScanMode
        End Get
        Set(ByVal Value As String)
            MyBase.mSpectrumStatus = clsSpectrumInfo.eSpectrumStatusConstants.DataDefined
            mScanMode = Value
        End Set
    End Property

    Public Property NumericPrecisionOfDataMZ() As Integer
        Get
            Return mNumericPrecisionOfDataMZ
        End Get
        Set(ByVal Value As Integer)
            MyBase.mSpectrumStatus = clsSpectrumInfo.eSpectrumStatusConstants.DataDefined
            mNumericPrecisionOfDataMZ = Value
        End Set
    End Property
    Public Property PeaksEndianModeMZ() As String
        Get
            Return mPeaksEndianModeMZ
        End Get
        Set(ByVal Value As String)
            MyBase.mSpectrumStatus = clsSpectrumInfo.eSpectrumStatusConstants.DataDefined
            mPeaksEndianModeMZ = Value
        End Set
    End Property

    Public Property NumericPrecisionOfDataIntensity() As Integer
        Get
            Return mNumericPrecisionOfDataIntensity
        End Get
        Set(ByVal Value As Integer)
            MyBase.mSpectrumStatus = clsSpectrumInfo.eSpectrumStatusConstants.DataDefined
            mNumericPrecisionOfDataIntensity = Value
        End Set
    End Property
    Public Property PeaksEndianModeIntensity() As String
        Get
            Return mPeaksEndianModeIntensity
        End Get
        Set(ByVal Value As String)
            MyBase.mSpectrumStatus = clsSpectrumInfo.eSpectrumStatusConstants.DataDefined
            mPeaksEndianModeIntensity = Value
        End Set
    End Property
#End Region

    Public Overrides Sub Clear()
        MyBase.Clear()

        mCollisionEnergy = 0
        mCollisionEnergyUnits = "Percent"
        mCollisionMethod = String.Empty                  ' Typically CID

        mScanMode = String.Empty                         ' Typically "MassScan"
        mParentIonCharge = 0
        mParentIonSpectrumMSLevel = 1
        mParentIonSpectrumID = 0

        mNumericPrecisionOfDataMZ = 32                   ' Assume 32-bit for now
        mPeaksEndianModeMZ = EndianModes.littleEndian

        mNumericPrecisionOfDataIntensity = 32            ' Assume 32-bit for now
        mPeaksEndianModeIntensity = EndianModes.littleEndian

    End Sub

    Public Function GetEndianModeValue(ByVal strEndianModeText As String) As clsBase64EncodeDecode.eEndianTypeConstants
        Select Case strEndianModeText
            Case EndianModes.bigEndian
                Return clsBase64EncodeDecode.eEndianTypeConstants.BigEndian
            Case EndianModes.littleEndian
                Return clsBase64EncodeDecode.eEndianTypeConstants.LittleEndian
            Case Else
                ' Assume littleEndian
                Return clsBase64EncodeDecode.eEndianTypeConstants.LittleEndian
        End Select
    End Function

    Public Shadows Function Clone() As clsSpectrumInfoMzData
        Dim objTarget As clsSpectrumInfoMzData

        objTarget = New clsSpectrumInfoMzData

        ' First create a shallow copy of this object
        objTarget = CType(Me.MemberwiseClone, clsSpectrumInfoMzData)

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
        End With

        Return objTarget
    End Function

    Public Overloads Sub CopyTo(<Out()> ByRef objTarget As clsSpectrumInfoMzData)
        '' Note; in classes derived from clsSpectrumInfo, call MyBase.CopyTo() but do not call objTarget.Clear()
        ''Dim objTargetBase As clsSpectrumInfo

        ''If objTarget Is Nothing Then
        ''    objTarget = New clsSpectrumInfoMzData
        ''Else
        ''    objTarget.Clear()
        ''End If

        ''objTargetBase = objTarget
        ''MyBase.CopyTo(objTargetBase)

        '' Perform a deep copy of this class's members to objTarget
        ''With objTarget
        ''    .mCollisionEnergy = Me.mCollisionEnergy
        ''    .mCollisionEnergyUnits = Me.mCollisionEnergyUnits
        ''    .mCollisionMethod = Me.mCollisionMethod

        ''    .mScanMode = Me.mScanMode
        ''    .mNumericPrecisionOfDataMZ = Me.mNumericPrecisionOfDataMZ
        ''    .mPeaksEndianModeMZ = Me.mPeaksEndianModeMZ

        ''    .mParentIonCharge = Me.mParentIonCharge
        ''    .mParentIonSpectrumMSLevel = Me.mParentIonSpectrumMSLevel
        ''    .mParentIonSpectrumID = Me.mParentIonSpectrumID

        ''    .mNumericPrecisionOfDataIntensity = Me.mNumericPrecisionOfDataIntensity
        ''    .mPeaksEndianModeIntensity = Me.mPeaksEndianModeIntensity
        ''End With

        objTarget = Me.Clone
    End Sub

    Public Overloads Sub Validate()
        Me.Validate(True, False)
    End Sub

    Public Overloads Overrides Sub Validate(ByVal blnComputeBasePeakAndTIC As Boolean, ByVal blnUpdateMZRange As Boolean)
        MyBase.Validate(blnComputeBasePeakAndTIC, blnUpdateMZRange)

        If ScanNumber = 0 And SpectrumID <> 0 Then
            ScanNumber = SpectrumID
            ScanNumberEnd = ScanNumber
            ScanCount = 1
        End If

        MyBase.mSpectrumStatus = clsSpectrumInfo.eSpectrumStatusConstants.Validated
    End Sub

End Class
