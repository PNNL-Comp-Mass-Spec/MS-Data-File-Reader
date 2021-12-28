Option Strict On

Imports System.Runtime.InteropServices
' This class holds the values associated with each spectrum in an mzData file
'
' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2006, Battelle Memorial Institute.  All Rights Reserved.
' Started March 24, 2006
'
' E-mail: matthew.monroe@pnl.gov or proteomics@pnnl.gov
' Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://panomics.pnnl.gov/ or https://www.pnnl.gov/integrative-omics
' -------------------------------------------------------------------------------

<Serializable()>
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

    ' Typically 32 or 64
    Protected mNumericPrecisionOfDataMZ As Integer

    ' See class EndianModes for values; typically EndianModes.littleEndian
    Protected mPeaksEndianModeMZ As String

    ' Typically 32 or 64
    Protected mNumericPrecisionOfDataIntensity As Integer

    ' See class EndianModes for values; typically EndianModes.littleEndian
    Protected mPeaksEndianModeIntensity As String

#End Region

#Region "Classwide Variables"

#End Region

#Region "Spectrum Variable Interface Functions"

    Public Property CollisionEnergy() As Single
        Get
            Return mCollisionEnergy
        End Get
        Set(Value As Single)
            MyBase.mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mCollisionEnergy = Value
        End Set
    End Property

    Public Property CollisionEnergyUnits() As String
        Get
            Return mCollisionEnergyUnits
        End Get
        Set(Value As String)
            MyBase.mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mCollisionEnergyUnits = Value
        End Set
    End Property

    Public Property CollisionMethod() As String
        Get
            Return mCollisionMethod
        End Get
        Set(Value As String)
            MyBase.mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mCollisionMethod = Value
        End Set
    End Property

    Public Property ParentIonCharge() As Integer
        Get
            Return mParentIonCharge
        End Get
        Set(Value As Integer)
            MyBase.mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mParentIonCharge = Value
        End Set
    End Property

    Public Property ParentIonSpectrumMSLevel() As Integer
        Get
            Return mParentIonSpectrumMSLevel
        End Get
        Set(Value As Integer)
            mParentIonSpectrumMSLevel = Value
        End Set
    End Property

    Public Property ParentIonSpectrumID() As Integer
        Get
            Return mParentIonSpectrumID
        End Get
        Set(Value As Integer)
            mParentIonSpectrumID = Value
        End Set
    End Property

    Public Property ScanMode() As String
        Get
            Return mScanMode
        End Get
        Set(Value As String)
            MyBase.mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mScanMode = Value
        End Set
    End Property

    Public Property NumericPrecisionOfDataMZ() As Integer
        Get
            Return mNumericPrecisionOfDataMZ
        End Get
        Set(Value As Integer)
            MyBase.mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mNumericPrecisionOfDataMZ = Value
        End Set
    End Property

    Public Property PeaksEndianModeMZ() As String
        Get
            Return mPeaksEndianModeMZ
        End Get
        Set(Value As String)
            MyBase.mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mPeaksEndianModeMZ = Value
        End Set
    End Property

    Public Property NumericPrecisionOfDataIntensity() As Integer
        Get
            Return mNumericPrecisionOfDataIntensity
        End Get
        Set(Value As Integer)
            MyBase.mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mNumericPrecisionOfDataIntensity = Value
        End Set
    End Property

    Public Property PeaksEndianModeIntensity() As String
        Get
            Return mPeaksEndianModeIntensity
        End Get
        Set(Value As String)
            MyBase.mSpectrumStatus = eSpectrumStatusConstants.DataDefined
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

    Public Function GetEndianModeValue(strEndianModeText As String) As clsBase64EncodeDecode.eEndianTypeConstants
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

        ' First create a shallow copy of this object
        Dim objTarget = CType(Me.MemberwiseClone, clsSpectrumInfoMzData)

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
        objTarget = Me.Clone()
    End Sub

    Public Overloads Sub Validate()
        Me.Validate(True, False)
    End Sub

    Public Overloads Overrides Sub Validate(blnComputeBasePeakAndTIC As Boolean, blnUpdateMZRange As Boolean)
        MyBase.Validate(blnComputeBasePeakAndTIC, blnUpdateMZRange)

        If ScanNumber = 0 And SpectrumID <> 0 Then
            ScanNumber = SpectrumID
            ScanNumberEnd = ScanNumber
            ScanCount = 1
        End If

        MyBase.mSpectrumStatus = eSpectrumStatusConstants.Validated
    End Sub
End Class
