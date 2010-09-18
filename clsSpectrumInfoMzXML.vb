Option Strict On

' This class holds the values associated with each spectrum in an mzXML file
'
' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2006, Battelle Memorial Institute.  All Rights Reserved.
' Started April 1, 2006
'
' E-mail: matthew.monroe@pnl.gov or matt@alchemistmatt.com
' Website: http://ncrr.pnl.gov/ or http://www.sysbio.org/resources/staff/
' -------------------------------------------------------------------------------
'
' Last modified September 17, 2010

<Serializable()> _
Public Class clsSpectrumInfoMzXML
    Inherits clsSpectrumInfo

    Public Sub New()
        Me.Clear()
    End Sub

#Region "Constants and Enums"
    Public Class ByteOrderTypes
        Public Const network As String = "network"
    End Class

    Public Class PairOrderTypes
        Public Const MZandIntensity As String = "m/z-int"
        Public Const IntensityAndMZ As String = "int-m/z"
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

    Protected mScanType As String                   ' See class ScanTypeNames for typical names
    Protected mFilterLine As String                 ' Thermo-specific filter line text
    Protected mStartMZ As Single                    ' Setted low m/z boundary (this is the instrumetal setting)
    Protected mEndMZ As Single                      ' Setted high m/z boundary (this is the instrumetal setting)

    Protected mNumericPrecisionOfData As Integer            ' Typically 32 or 64
    Protected mPeaksByteOrder As String                     ' See class ByteOrderTypes for values; typically ByteOrderTypes.network
    Protected mPeaksPairOrder As String                     ' See class PairOrderTypes for values; typically PairOrderTypes.MZandIntensity
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
    Public Property FilterLine() As String
        Get
            Return mFilterLine
        End Get
        Set(ByVal value As String)
            MyBase.mSpectrumStatus = clsSpectrumInfo.eSpectrumStatusConstants.DataDefined
            mFilterLine = value
        End Set
    End Property
    Public Property NumericPrecisionOfData() As Integer
        Get
            Return mNumericPrecisionOfData
        End Get
        Set(ByVal Value As Integer)
            MyBase.mSpectrumStatus = clsSpectrumInfo.eSpectrumStatusConstants.DataDefined
            mNumericPrecisionOfData = Value
        End Set
    End Property
    Public Property PeaksByteOrder() As String
        Get
            Return mPeaksByteOrder
        End Get
        Set(ByVal Value As String)
            MyBase.mSpectrumStatus = clsSpectrumInfo.eSpectrumStatusConstants.DataDefined
            mPeaksByteOrder = Value
        End Set
    End Property
    Public Property PeaksPairOrder() As String
        Get
            Return mPeaksPairOrder
        End Get
        Set(ByVal Value As String)
            MyBase.mSpectrumStatus = clsSpectrumInfo.eSpectrumStatusConstants.DataDefined
            mPeaksPairOrder = Value
        End Set
    End Property
    Public Property EndMZ() As Single
        Get
            Return mEndMZ
        End Get
        Set(ByVal value As Single)
            MyBase.mSpectrumStatus = clsSpectrumInfo.eSpectrumStatusConstants.DataDefined
            mEndMZ = value
        End Set
    End Property
    Public Property StartMZ() As Single
        Get
            Return mStartMZ
        End Get
        Set(ByVal value As Single)
            MyBase.mSpectrumStatus = clsSpectrumInfo.eSpectrumStatusConstants.DataDefined
            mStartMZ = value
        End Set
    End Property
    Public Property ScanType() As String
        Get
            Return mScanType
        End Get
        Set(ByVal value As String)
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

    End Sub

    Public Shadows Function Clone() As clsSpectrumInfoMzXML
        Dim objTarget As clsSpectrumInfoMzXML

        objTarget = New clsSpectrumInfoMzXML

        ' First create a shallow copy of this object
        objTarget = CType(Me.MemberwiseClone, clsSpectrumInfoMzXML)

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

    Public Overloads Sub CopyTo(ByRef objTarget As clsSpectrumInfoMzXML)
        '' Note; in classes derived from clsSpectrumInfo, call MyBase.CopyTo() but do not call objTarget.Clear()
        ''Dim objTargetBase As clsSpectrumInfo


        ''If objTarget Is Nothing Then
        ''    objTarget = New clsSpectrumInfoMzXML
        ''Else
        ''    objTarget.Clear()
        ''End If

        ''objTargetBase = objTarget
        ''MyBase.CopyTo(objTargetBase)

        '' Perform a deep copy of this class's members to objTarget
        ''With objTarget
        ''    .mCollisionEnergy = Me.mCollisionEnergy

        ''    .mNumericPrecisionOfData = Me.mNumericPrecisionOfData
        ''    .mPeaksByteOrder = Me.mPeaksByteOrder
        ''    .mPeaksPairOrder = Me.mPeaksPairOrder
        ''End With

        objTarget = Me.Clone
    End Sub

    Public Overloads Sub Validate()
        Me.Validate(False, False)
    End Sub

    Public Overloads Overrides Sub Validate(ByVal blnComputeBasePeakAndTIC As Boolean, ByVal blnUpdateMZRange As Boolean)
        MyBase.Validate(blnComputeBasePeakAndTIC, blnUpdateMZRange)

        If SpectrumID = 0 And ScanNumber <> 0 Then
            SpectrumID = ScanNumber
        End If

        MyBase.mSpectrumStatus = clsSpectrumInfo.eSpectrumStatusConstants.Validated
    End Sub

End Class
