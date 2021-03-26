Option Strict On

Imports System.Runtime.InteropServices
' This class holds the values associated with each spectrum in an MS Data file
'
' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2006, Battelle Memorial Institute.  All Rights Reserved.
' Started March 23, 2006
'
' E-mail: matthew.monroe@pnl.gov or proteomics@pnnl.gov
' Website: http://omics.pnl.gov/ or http://panomics.pnnl.gov/
' -------------------------------------------------------------------------------

<Serializable()>
Public Class clsSpectrumInfo
    Implements ICloneable

    Public Sub New()
        mAutoShrinkDataLists = True
        Me.Clear()
    End Sub

#Region "Constants and Enums"

    Public Class SpectrumTypeNames
        Public Const discrete As String = "discrete"
        Public Const continuous As String = "continuous"
    End Class

    Public Enum eSpectrumStatusConstants
        Initialized = 0                     ' This is set when .Clear() is called
        DataDefined = 1                     ' This is set when any of the values are set via a property
        Validated = 2                       ' This is set when .Validate() is called
    End Enum

#End Region

#Region "Spectrum Variables"

    Private mSpectrumID As Integer                ' Spectrum ID number; often the same as ScanNumber
    Private mScanNumber As Integer                ' First scan number if ScanCount is > 1
    Private mScanCount As Integer                 ' Number of spectra combined together to get the given spectrum
    Private mScanNumberEnd As Integer             ' Last scan if more than one scan was combined to make this spectrum

    Private mSpectrumType As String               ' See Class SpectrumTypeNames for typical names (discrete or continuous)
    Private mSpectrumCombinationMethod As String

    Private mMSLevel As Integer                   ' 1 for MS, 2 for MS/MS, 3 for MS^3, etc.
    Private mCentroided As Boolean                ' True if the data is centroided (supported by mzXML v3.x)
    Private mPolarity As String
    Private mRetentionTimeMin As Single

    Private mmzRangeStart As Single
    Private mmzRangeEnd As Single
    Private mBasePeakMZ As Double
    Private mBasePeakIntensity As Single

    Private mTotalIonCurrent As Double
    Private mParentIonMZ As Double
    Private mParentIonIntensity As Single

    ' Number of m/z and intensity pairs in this spectrum; see note concerning mAutoShrinkDataLists below
    Public DataCount As Integer

    Public MZList() As Double
    Public IntensityList() As Single

#End Region

#Region "Classwide Variables"
    ' When mAutoShrinkDataLists is True, then MZList().Length and IntensityList().Length will equal DataCount;
    ' When mAutoShrinkDataLists is False, then the memory will not be freed when DataCount shrinks or .Clear() is called
    ' Setting mAutoShrinkDataLists to False helps reduce slow, increased memory usage due to inefficient garbage collection
    Private mAutoShrinkDataLists As Boolean
    Protected mErrorMessage As String

    Protected mSpectrumStatus As eSpectrumStatusConstants

#End Region

#Region "Spectrum Variable Interface Functions"

    Public Property SpectrumID() As Integer
        Get
            Return mSpectrumID
        End Get
        Set(Value As Integer)
            mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mSpectrumID = Value
        End Set
    End Property

    Public Property ScanNumber() As Integer
        Get
            Return mScanNumber
        End Get
        Set(Value As Integer)
            mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mScanNumber = Value
        End Set
    End Property

    Public Property ScanCount() As Integer
        Get
            Return mScanCount
        End Get
        Set(Value As Integer)
            mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mScanCount = Value
        End Set
    End Property

    Public Property ScanNumberEnd() As Integer
        Get
            Return mScanNumberEnd
        End Get
        Set(Value As Integer)
            mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mScanNumberEnd = Value
        End Set
    End Property

    Public Property SpectrumType() As String
        Get
            Return mSpectrumType
        End Get
        Set(Value As String)
            mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mSpectrumType = Value
        End Set
    End Property

    Public Property SpectrumCombinationMethod() As String
        Get
            Return mSpectrumCombinationMethod
        End Get
        Set(Value As String)
            mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mSpectrumCombinationMethod = Value
        End Set
    End Property

    Public Property MSLevel() As Integer
        Get
            Return mMSLevel
        End Get
        Set(Value As Integer)
            mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mMSLevel = Value
        End Set
    End Property

    Public Property Centroided() As Boolean
        Get
            Return mCentroided
        End Get
        Set(value As Boolean)
            mCentroided = value
        End Set
    End Property

    Public Property Polarity() As String
        Get
            Return mPolarity
        End Get
        Set(Value As String)
            mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mPolarity = Value
        End Set
    End Property

    Public Property RetentionTimeMin() As Single
        Get
            Return mRetentionTimeMin
        End Get
        Set(Value As Single)
            mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mRetentionTimeMin = Value
        End Set
    End Property

    Public Property mzRangeStart() As Single
        Get
            Return mmzRangeStart
        End Get
        Set(Value As Single)
            mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mmzRangeStart = Value
        End Set
    End Property

    Public Property mzRangeEnd() As Single
        Get
            Return mmzRangeEnd
        End Get
        Set(Value As Single)
            mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mmzRangeEnd = Value
        End Set
    End Property

    Public Property BasePeakMZ() As Double
        Get
            Return mBasePeakMZ
        End Get
        Set(Value As Double)
            mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mBasePeakMZ = Value
        End Set
    End Property

    Public Property BasePeakIntensity() As Single
        Get
            Return mBasePeakIntensity
        End Get
        Set(Value As Single)
            mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mBasePeakIntensity = Value
        End Set
    End Property

    Public Property TotalIonCurrent() As Double
        Get
            Return mTotalIonCurrent
        End Get
        Set(Value As Double)
            mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mTotalIonCurrent = Value
        End Set
    End Property

    Public Property ParentIonMZ() As Double
        Get
            Return mParentIonMZ
        End Get
        Set(Value As Double)
            mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mParentIonMZ = Value
        End Set
    End Property

    Public Property ParentIonIntensity() As Single
        Get
            Return mParentIonIntensity
        End Get
        Set(Value As Single)
            mSpectrumStatus = eSpectrumStatusConstants.DataDefined
            mParentIonIntensity = Value
        End Set
    End Property

    Public ReadOnly Property SpectrumStatus() As eSpectrumStatusConstants
        Get
            Return mSpectrumStatus
        End Get
    End Property

#End Region

#Region "Processing Options"

    Public Property AutoShrinkDataLists() As Boolean
        Get
            Return mAutoShrinkDataLists
        End Get
        Set(Value As Boolean)
            mAutoShrinkDataLists = Value
        End Set
    End Property

    Public ReadOnly Property ErrorMessage() As String
        Get
            If mErrorMessage Is Nothing Then mErrorMessage = String.Empty
            Return mErrorMessage
        End Get
    End Property

#End Region

    Public Overridable Sub Clear()
        mSpectrumID = 0
        mScanNumber = 0
        mScanCount = 0
        mScanNumberEnd = 0

        mSpectrumType = SpectrumTypeNames.discrete
        mSpectrumCombinationMethod = String.Empty

        mMSLevel = 1
        mCentroided = False
        mPolarity = "Positive"
        mRetentionTimeMin = 0

        mmzRangeStart = 0
        mmzRangeEnd = 0
        mBasePeakMZ = 0
        mBasePeakIntensity = 0

        mTotalIonCurrent = 0
        mParentIonMZ = 0
        mParentIonIntensity = 0

        DataCount = 0

        If mAutoShrinkDataLists OrElse MZList Is Nothing Then
            ReDim MZList(-1)
        Else
            Array.Clear(MZList, 0, MZList.Length)
        End If

        If mAutoShrinkDataLists OrElse IntensityList Is Nothing Then
            ReDim IntensityList(-1)
        Else
            Array.Clear(IntensityList, 0, IntensityList.Length)
        End If

        mSpectrumStatus = eSpectrumStatusConstants.Initialized
        mErrorMessage = String.Empty
    End Sub

    Private Function CloneMe() As Object Implements ICloneable.Clone
        ' Use the strongly typed Clone module to do the cloning
        Return Clone()
    End Function

    Public Function Clone() As clsSpectrumInfo
        ' Note: Clone() functions in the derived SpectrumInfo classes Shadow this function and duplicate its code

        ' First create a shallow copy of this object
        Dim objTarget = CType(Me.MemberwiseClone, clsSpectrumInfo)

        ' Next, manually copy the array objects and any other objects
        ' Note: Since Clone() functions in the derived classes Shadow this function,
        '      be sure to update them too if you change any code below
        With objTarget
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

    Public Overridable Sub CopyTo(<Out()> ByRef objTarget As clsSpectrumInfo)
        objTarget = Me.Clone()
    End Sub

    Public Sub UpdateMZRange()
        Dim sngMzRangeStart As Single = 0
        Dim sngMzRangeEnd As Single = 0

        Try
            If DataCount > 0 AndAlso MZList IsNot Nothing Then
                sngMzRangeStart = CSng(MZList(0))
                sngMzRangeEnd = CSng(MZList(DataCount - 1))
            End If
        Catch ex As Exception
            mErrorMessage = "Error in UpdateMZRange: " & ex.Message
        Finally
            mzRangeStart = sngMzRangeStart
            mzRangeEnd = sngMzRangeEnd
        End Try
    End Sub

    Public Sub ComputeBasePeakAndTIC()

        Dim intIndex As Integer

        Dim dblTotalIonCurrent As Double
        Dim dblBasePeakMZ As Double
        Dim sngBasePeakIntensity As Single

        Try
            dblTotalIonCurrent = 0
            dblBasePeakMZ = 0
            sngBasePeakIntensity = 0

            If DataCount > 0 AndAlso MZList IsNot Nothing AndAlso IntensityList IsNot Nothing Then
                dblBasePeakMZ = MZList(0)
                sngBasePeakIntensity = IntensityList(0)
                dblTotalIonCurrent = IntensityList(0)

                For intIndex = 1 To DataCount - 1
                    dblTotalIonCurrent += IntensityList(intIndex)

                    If IntensityList(intIndex) >= sngBasePeakIntensity Then
                        dblBasePeakMZ = MZList(intIndex)
                        sngBasePeakIntensity = IntensityList(intIndex)
                    End If
                Next intIndex
            End If
        Catch ex As Exception
            mErrorMessage = "Error in ComputeBasePeakAndTIC: " & ex.Message
        Finally
            TotalIonCurrent = dblTotalIonCurrent
            BasePeakMZ = dblBasePeakMZ
            BasePeakIntensity = sngBasePeakIntensity
        End Try
    End Sub

    Public Function LookupIonIntensityByMZ(dblMZToFind As Double, sngIntensityIfNotFound As Single,
                                           Optional sngMatchTolerance As Single = 0.05) As Single
        ' Looks for dblMZToFind in this spectrum's data
        ' If found, returns the intensity
        ' If not found, returns an intensity of sngIntensityIfNotFound

        Dim sngIntensityMatch As Single
        Dim dblMZMinimum As Double
        Dim dblMZDifference As Double

        Dim intIndex As Integer

        Try
            ' Define the minimum MZ value to consider
            dblMZMinimum = dblMZToFind - sngMatchTolerance
            sngIntensityMatch = sngIntensityIfNotFound

            If Not (MZList Is Nothing Or IntensityList Is Nothing) Then
                For intIndex = DataCount - 1 To 0 Step -1
                    If intIndex < MZList.Length And intIndex < IntensityList.Length Then
                        If MZList(intIndex) >= dblMZMinimum Then
                            dblMZDifference = dblMZToFind - MZList(intIndex)
                            If Math.Abs(dblMZDifference) <= sngMatchTolerance Then
                                If IntensityList(intIndex) > sngIntensityMatch Then
                                    sngIntensityMatch = IntensityList(intIndex)
                                End If
                            End If
                        Else
                            ' Assuming MZList is sorted on intensity, we can exit out of the loop once we pass dblMZMinimum
                            Exit For
                        End If
                    End If
                Next intIndex
            End If
        Catch ex As Exception
            sngIntensityMatch = sngIntensityIfNotFound
        End Try

        Return sngIntensityMatch
    End Function

    Public Overridable Sub Validate(blnComputeBasePeakAndTIC As Boolean, blnUpdateMZRange As Boolean)
        If blnComputeBasePeakAndTIC Then
            Me.ComputeBasePeakAndTIC()
        End If

        If blnUpdateMZRange Then
            Me.UpdateMZRange()
        End If

        If mAutoShrinkDataLists Then
            If MZList IsNot Nothing Then
                If MZList.Length > DataCount Then
                    ReDim Preserve MZList(DataCount - 1)
                End If
            End If

            If IntensityList IsNot Nothing Then
                If IntensityList.Length > DataCount Then
                    ReDim Preserve IntensityList(DataCount - 1)
                End If
            End If
        End If
    End Sub
End Class
