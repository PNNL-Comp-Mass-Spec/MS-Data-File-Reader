Option Strict On

Imports System.Runtime.InteropServices
' This class holds the values associated with each spectrum in an mzXML file
'
' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2006, Battelle Memorial Institute.  All Rights Reserved.
' Started April 1, 2006
'
' E-mail: matthew.monroe@pnl.gov or matt@alchemistmatt.com
' Website: http://omics.pnl.gov/ or http://www.sysbio.org/resources/staff/
' -------------------------------------------------------------------------------
'

<Serializable()>
Public Class clsSpectrumInfoMzXML
    Inherits clsSpectrumInfo

    Public Sub New()
        Me.Clear()
    End Sub

#Region "Constants and Enums"

    Public Class ByteOrderTypes
        Public Const network As String = "network"
    End Class

    Public Class CompressionTypes
        Public Const none As String = "none"
        Public Const zlib As String = "zlib"
    End Class

    ''' <summary>
    ''' Tracks pairOrder for mzXML v1.x and v2.x
    ''' Tracks contentType for mzXML 3.x files
    ''' </summary>
    ''' <remarks></remarks>
    Public Class PairOrderTypes
        Public Const MZandIntensity As String = "m/z-int"
        Public Const IntensityAndMZ As String = "int-m/z"
        Public Const MZ As String = "m/z"
        Public Const Intensity As String = "intensity"
        Public Const SN As String = "S/N"
        Public Const Charge As String = "charge"
        Public Const MZRuler As String = "m/z ruler"
        Public Const TOF As String = "TOF"
    End Class

    Public Class ScanTypeNames
        Public Const Full As String = "Full"
        Public Const zoom As String = "zoom"
        Public Const SIM As String = "SIM"
        Public Const SRM As String = "SRM"      ' MRM is synonymous with SRM
        Public Const CRM As String = "CRM"
        Public Const Q1 As String = "Q1"
        Public Const Q3 As String = "Q3"
        Public Const MRM As String = "MRM"
    End Class

#End Region

#Region "Spectrum Variables"

    Protected mCollisionEnergy As Single

    ' See class ScanTypeNames for typical names
    Protected mScanType As String

    ' Thermo-specific filter line text
    Protected mFilterLine As String

    ' Setted low m/z boundary (this is the instrumetal setting)
    Protected mStartMZ As Single

    ' Setted high m/z boundary (this is the instrumetal setting)
    Protected mEndMZ As Single

    ' Typically 32 or 64
    Protected mNumericPrecisionOfData As Integer

    ' See class ByteOrderTypes for values; typically ByteOrderTypes.network
    Protected mPeaksByteOrder As String

    ' See class PairOrderTypes for values; typically PairOrderTypes.MZandIntensity; stores contentType for mzXML v3.x
    Protected mPeaksPairOrder As String

    ' See class CompressionTypes for values; will be "none" or "zlib"
    Protected mCompressionType As String
    Protected mCompressedLen As Integer

    Protected mActivationMethod As String
    Protected mIsolationWindow As Single
    Protected mParentIonCharge As Integer
    Protected mPrecursorScanNum As Integer

#End Region

#Region "Classwide Variables"

#End Region

#Region "Spectrum Variable Interface Functions"

    Public Property ActivationMethod() As String
        Get
            Return mActivationMethod
        End Get
        Set(value As String)
            mActivationMethod = value
        End Set
    End Property

    Public Property CollisionEnergy() As Single
        Get
            Return mCollisionEnergy
        End Get
        Set(Value As Single)
            MyBase.mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mCollisionEnergy = Value
        End Set
    End Property

    Public Property FilterLine() As String
        Get
            Return mFilterLine
        End Get
        Set(value As String)
            MyBase.mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mFilterLine = value
        End Set
    End Property

    Public Property NumericPrecisionOfData() As Integer
        Get
            Return mNumericPrecisionOfData
        End Get
        Set(Value As Integer)
            MyBase.mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mNumericPrecisionOfData = Value
        End Set
    End Property

    Public Property PeaksByteOrder() As String
        Get
            Return mPeaksByteOrder
        End Get
        Set(Value As String)
            MyBase.mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mPeaksByteOrder = Value
        End Set
    End Property

    Public Property PeaksPairOrder() As String
        Get
            Return mPeaksPairOrder
        End Get
        Set(Value As String)
            MyBase.mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mPeaksPairOrder = Value
        End Set
    End Property

    Public Property CompressionType() As String
        Get
            Return mCompressionType
        End Get
        Set(value As String)
            mCompressionType = value
        End Set
    End Property

    Public Property CompressedLen() As Integer
        Get
            Return mCompressedLen
        End Get
        Set(value As Integer)
            mCompressedLen = value
        End Set
    End Property

    Public Property EndMZ() As Single
        Get
            Return mEndMZ
        End Get
        Set(value As Single)
            MyBase.mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mEndMZ = value
        End Set
    End Property

    Public Property IsolationWindow() As Single
        Get
            Return mIsolationWindow
        End Get
        Set(Value As Single)
            mIsolationWindow = Value
        End Set
    End Property

    Public Property ParentIonCharge() As Integer
        Get
            Return mParentIonCharge
        End Get
        Set(Value As Integer)
            mParentIonCharge = Value
        End Set
    End Property

    Public Property PrecursorScanNum() As Integer
        Get
            Return mPrecursorScanNum
        End Get
        Set(Value As Integer)
            mPrecursorScanNum = Value
        End Set
    End Property

    Public Property StartMZ() As Single
        Get
            Return mStartMZ
        End Get
        Set(value As Single)
            MyBase.mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mStartMZ = value
        End Set
    End Property

    Public Property ScanType() As String
        Get
            Return mScanType
        End Get
        Set(value As String)
            mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mScanType = value
        End Set
    End Property

#End Region

    Public Overrides Sub Clear()
        MyBase.Clear()

        mCollisionEnergy = 0

        mScanType = ScanTypeNames.Full
        mFilterLine = String.Empty
        mStartMZ = 0
        mEndMZ = 0

        mNumericPrecisionOfData = 32            ' Assume 32-bit for now
        mPeaksByteOrder = ByteOrderTypes.network
        mPeaksPairOrder = PairOrderTypes.MZandIntensity

        mCompressionType = CompressionTypes.none
        mCompressedLen = 0

        mParentIonCharge = 0
        mActivationMethod = String.Empty

        mIsolationWindow = 0
        mPrecursorScanNum = 0
    End Sub

    Public Shadows Function Clone() As clsSpectrumInfoMzXML

        ' First create a shallow copy of this object
        Dim objTarget = CType(Me.MemberwiseClone, clsSpectrumInfoMzXML)

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

    Public Overloads Sub CopyTo(<Out()> ByRef objTarget As clsSpectrumInfoMzXML)
        objTarget = Me.Clone()
    End Sub

    Public Overloads Sub Validate()
        Me.Validate(False, False)
    End Sub

    Public Overloads Overrides Sub Validate(blnComputeBasePeakAndTIC As Boolean, blnUpdateMZRange As Boolean)
        MyBase.Validate(blnComputeBasePeakAndTIC, blnUpdateMZRange)

        If SpectrumID = 0 And ScanNumber <> 0 Then
            SpectrumID = ScanNumber
        End If

        MyBase.mSpectrumStatus = eSpectrumStatusConstants.Validated
    End Sub
End Class
