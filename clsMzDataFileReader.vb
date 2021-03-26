Option Strict On

Imports System.Runtime.InteropServices
Imports System.Xml

' This class uses a SAX Parser to read an mzData file
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

Public Class clsMzDataFileReader
    Inherits clsMSXMLFileReaderBaseClass

    Public Sub New()
        Me.InitializeLocalVariables()
    End Sub

#Region "Constants and Enums"
    ' Note: The extensions must be in all caps
    Public Const MZDATA_FILE_EXTENSION As String = ".MZDATA"
    Public Const MZDATA_FILE_EXTENSION_XML As String = "_MZDATA.XML"

    ' Note that I'm using classes to group the constants
    Private Class XMLSectionNames
        Public Const RootName As String = "mzData"
        Public Const CVParam As String = "cvParam"
    End Class

    Private Class HeaderSectionNames
        Public Const Description As String = "description"
        Public Const admin As String = "admin"
        Public Const instrument As String = "instrument"
        Public Const dataProcessing As String = "dataProcessing"
        Public Const processingMethod As String = "processingMethod"
    End Class

    Private Class ScanSectionNames
        Public Const spectrumList As String = "spectrumList"
        Public Const spectrum As String = "spectrum"

        Public Const spectrumSettings As String = "spectrumSettings"
        Public Const acqSpecification As String = "acqSpecification"
        Public Const acquisition As String = "acquisition"
        Public Const spectrumInstrument As String = "spectrumInstrument"

        Public Const precursorList As String = "precursorList"
        Public Const precursor As String = "precursor"
        Public Const ionSelection As String = "ionSelection"
        Public Const activation As String = "activation"

        Public Const mzArrayBinary As String = "mzArrayBinary"
        Public Const intenArrayBinary As String = "intenArrayBinary"

        Public Const ArrayData As String = "data"
    End Class

    Private Class mzDataRootAttrbuteNames
        Public Const version As String = "version"
        Public Const accessionNumber As String = "accessionNumber"
        Public Const xmlns_xsi As String = "xmlns:xsi"
    End Class

    Private Class SpectrumListAttributeNames
        Public Const count As String = "count"
    End Class

    Private Class SpectrumAttributeNames
        Public Const id As String = "id"
    End Class

    Private Class ProcessingMethodCVParamNames
        Public Const Deisotoping As String = "Deisotoping"
        Public Const ChargeDeconvolution As String = "ChargeDeconvolution"
        Public Const PeakProcessing As String = "PeakProcessing"
    End Class

    Private Class AcqSpecificationAttributeNames
        Public Const spectrumType As String = "spectrumType"
        Public Const methodOfCombination As String = "methodOfCombination"
        Public Const count As String = "count"
    End Class

    Private Class AcquisitionAttributeNames
        Public Const acqNumber As String = "acqNumber"
    End Class

    Private Class SpectrumInstrumentAttributeNames
        Public Const msLevel As String = "msLevel"
        Public Const mzRangeStart As String = "mzRangeStart"
        Public Const mzRangeStop As String = "mzRangeStop"
    End Class

    Private Class SpectrumInstrumentCVParamNames
        Public Const ScanMode As String = "ScanMode"
        Public Const Polarity As String = "Polarity"
        Public Const TimeInMinutes As String = "TimeInMinutes"
    End Class

    Private Class PrecursorAttributeNames
        Public Const msLevel As String = "msLevel"
        Public Const spectrumRef As String = "spectrumRef"
    End Class

    Private Class PrecursorIonSelectionCVParamNames
        Public Const MassToChargeRatio As String = "MassToChargeRatio"
        Public Const ChargeState As String = "ChargeState"
    End Class

    Private Class PrecursorActivationCVParamNames
        Public Const Method As String = "Method"
        Public Const CollisionEnergy As String = "CollisionEnergy"
        Public Const EnergyUnits As String = "EnergyUnits"
    End Class

    Private Class BinaryDataAttributeNames
        Public Const precision As String = "precision"
        Public Const endian As String = "endian"
        Public Const length As String = "length"
    End Class

    Private Const MOST_RECENT_SURVEY_SCANS_TO_CACHE As Integer = 20

    Private Enum eCurrentMZDataFileSectionConstants
        UnknownFile = 0
        Start = 1
        Headers = 2
        Admin = 3
        Instrument = 4
        DataProcessing = 5
        DataProcessingMethod = 6
        SpectrumList = 7
        SpectrumSettings = 8
        SpectrumInstrument = 9
        PrecursorList = 10
        PrecursorEntry = 11
        PrecursorIonSelection = 12
        PrecursorActivation = 13
        SpectrumDataArrayMZ = 14
        SpectrumDataArrayIntensity = 15
    End Enum

#End Region

#Region "Structures"

    Private Structure udtFileStatsAddnlType
        Public PeakProcessing As String
        Public IsCentroid As Boolean      ' True if centroid (aka stick) data; False if profile (aka continuum) data
        Public IsDeisotoped As Boolean
        Public HasChargeDeconvolution As Boolean
    End Structure

#End Region

#Region "Classwide Variables"

    Private mCurrentXMLDataFileSection As eCurrentMZDataFileSectionConstants

    Private mCurrentSpectrum As clsSpectrumInfoMzData
    Private mAcquisitionElementCount As Integer

    Private mMostRecentSurveyScanSpectra As Queue

    Private mInputFileStatsAddnl As udtFileStatsAddnlType

#End Region

#Region "Processing Options and Interface Functions"

    Public ReadOnly Property PeakProcessing() As String
        Get
            Return mInputFileStatsAddnl.PeakProcessing
        End Get
    End Property

    Public ReadOnly Property FileInfoIsCentroid() As Boolean
        Get
            Return mInputFileStatsAddnl.IsCentroid
        End Get
    End Property

    Public ReadOnly Property IsDeisotoped() As Boolean
        Get
            Return mInputFileStatsAddnl.IsDeisotoped
        End Get
    End Property

    Public ReadOnly Property HasChargeDeconvolution() As Boolean
        Get
            Return mInputFileStatsAddnl.HasChargeDeconvolution
        End Get
    End Property

#End Region

    Private Function FindIonIntensityInRecentSpectra(intSpectrumIDToFind As Integer, dblMZToFind As Double) As Single
        Dim sngIntensityMatch As Single
        Dim objEnumerator As IEnumerator
        Dim objSpectrum As clsSpectrumInfoMzData

        sngIntensityMatch = 0
        If mMostRecentSurveyScanSpectra IsNot Nothing Then
            objEnumerator = mMostRecentSurveyScanSpectra.GetEnumerator
            Do While objEnumerator.MoveNext
                objSpectrum = CType(objEnumerator.Current, clsSpectrumInfoMzData)
                If objSpectrum.SpectrumID = intSpectrumIDToFind Then
                    sngIntensityMatch = objSpectrum.LookupIonIntensityByMZ(dblMZToFind, 0)
                    Exit Do
                End If
            Loop
        End If

        Return sngIntensityMatch
    End Function

    Protected Overrides Function GetCurrentSpectrum() As clsSpectrumInfo
        Return mCurrentSpectrum
    End Function

    Private Function GetCVNameAndValue(<Out()> ByRef strName As String, <Out()> ByRef strValue As String) As Boolean

        Try
            If mXMLReader.HasAttributes Then
                strName = mXMLReader.GetAttribute("name")
                strValue = mXMLReader.GetAttribute("value")
                Return True
            End If
        Catch ex As Exception
            ' Ignore errors here
        End Try

        strName = String.Empty
        strValue = String.Empty

        Return False
    End Function

    Protected Overrides Sub InitializeCurrentSpectrum(blnAutoShrinkDataLists As Boolean)
        Dim objSpectrumCopy As clsSpectrumInfoMzData = Nothing

        If mCurrentSpectrum IsNot Nothing Then
            If mCurrentSpectrum.MSLevel = 1 Then
                If mMostRecentSurveyScanSpectra.Count >= MOST_RECENT_SURVEY_SCANS_TO_CACHE Then
                    mMostRecentSurveyScanSpectra.Dequeue()
                End If

                ' Add mCurrentSpectrum to mMostRecentSurveyScanSpectra
                mCurrentSpectrum.CopyTo(objSpectrumCopy)
                mMostRecentSurveyScanSpectra.Enqueue(objSpectrumCopy)
            End If
        End If

        If MyBase.ReadingAndStoringSpectra OrElse mCurrentSpectrum Is Nothing Then
            mCurrentSpectrum = New clsSpectrumInfoMzData()
        Else
            mCurrentSpectrum.Clear()
        End If

        mCurrentSpectrum.AutoShrinkDataLists = blnAutoShrinkDataLists
    End Sub

    Protected Overrides Sub InitializeLocalVariables()
        MyBase.InitializeLocalVariables()

        mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.UnknownFile
        mAcquisitionElementCount = 0

        With mInputFileStatsAddnl
            .PeakProcessing = String.Empty
            .IsCentroid = False
            .IsDeisotoped = False
            .HasChargeDeconvolution = False
        End With

        mMostRecentSurveyScanSpectra = New Queue
    End Sub

    Protected Overrides Sub LogErrors(strCallingFunction As String, strErrorDescription As String)
        MyBase.LogErrors("clsMzDataFileReader." & strCallingFunction, strErrorDescription)
    End Sub

    Public Overrides Function OpenFile(strInputFilePath As String) As Boolean
        Dim blnSuccess As Boolean

        Me.InitializeLocalVariables()

        blnSuccess = MyBase.OpenFile(strInputFilePath)

        Return blnSuccess
    End Function

    Private Function ParseBinaryData(strMSMSDataBase64Encoded As String, ByRef sngValues() As Single,
                                     NumericPrecisionOfData As Integer, PeaksEndianMode As String,
                                     blnUpdatePeaksCountIfInconsistent As Boolean) As Boolean
        ' Parses strMSMSDataBase64Encoded and stores the data in sngValues

        Dim sngDataArray() As Single = Nothing
        Dim dblDataArray() As Double = Nothing

        Dim zLibCompressed = False

        Dim eEndianMode As clsBase64EncodeDecode.eEndianTypeConstants
        Dim intIndex As Integer
        Dim blnSuccess As Boolean

        blnSuccess = False
        If strMSMSDataBase64Encoded Is Nothing OrElse strMSMSDataBase64Encoded.Length = 0 Then
            ReDim sngValues(-1)
        Else
            Try
                eEndianMode = mCurrentSpectrum.GetEndianModeValue(PeaksEndianMode)

                Select Case NumericPrecisionOfData
                    Case 32
                        If clsBase64EncodeDecode.DecodeNumericArray(strMSMSDataBase64Encoded, sngDataArray, zLibCompressed, eEndianMode) Then
                            ReDim sngValues(sngDataArray.Length - 1)
                            sngDataArray.CopyTo(sngValues, 0)

                            blnSuccess = True
                        End If
                    Case 64
                        If clsBase64EncodeDecode.DecodeNumericArray(strMSMSDataBase64Encoded, dblDataArray, zLibCompressed, eEndianMode) Then
                            ReDim sngValues(dblDataArray.Length - 1)

                            For intIndex = 0 To dblDataArray.Length - 1
                                sngValues(intIndex) = CSng(dblDataArray(intIndex))
                            Next intIndex

                            blnSuccess = True
                        End If
                    Case Else
                        ' Invalid numeric precision
                End Select

                If blnSuccess Then
                    With mCurrentSpectrum
                        If sngValues.Length <> .DataCount Then
                            If .DataCount = 0 AndAlso sngValues.Length > 0 AndAlso
                                Math.Abs(sngValues(0)) < Single.Epsilon Then
                                ' Leave .PeaksCount at 0
                            ElseIf blnUpdatePeaksCountIfInconsistent Then
                                ' This shouldn't normally be necessary
                                LogErrors("ParseBinaryData (Single Precision)",
                                          "Unexpected condition: sngValues.Length <> .DataCount and .DataCount > 0")
                                .DataCount = sngValues.Length
                            End If
                        End If
                    End With
                End If

            Catch ex As Exception
                LogErrors("ParseBinaryData (Single Precision)", ex.Message)
            End Try
        End If

        Return blnSuccess
    End Function

    Private Function ParseBinaryData(strMSMSDataBase64Encoded As String, ByRef dblValues() As Double,
                                     NumericPrecisionOfData As Integer, PeaksEndianMode As String,
                                     blnUpdatePeaksCountIfInconsistent As Boolean) As Boolean
        ' Parses strMSMSDataBase64Encoded and stores the data in dblValues

        Dim sngDataArray() As Single = Nothing
        Dim dblDataArray() As Double = Nothing

        Dim zLibCompressed = False

        Dim eEndianMode As clsBase64EncodeDecode.eEndianTypeConstants
        Dim blnSuccess As Boolean

        blnSuccess = False
        If strMSMSDataBase64Encoded Is Nothing OrElse strMSMSDataBase64Encoded.Length = 0 Then
            ReDim dblValues(-1)
        Else
            Try
                eEndianMode = mCurrentSpectrum.GetEndianModeValue(PeaksEndianMode)

                Select Case NumericPrecisionOfData
                    Case 32
                        If clsBase64EncodeDecode.DecodeNumericArray(strMSMSDataBase64Encoded, sngDataArray,
                                                                     zLibCompressed, eEndianMode) Then
                            ReDim dblValues(sngDataArray.Length - 1)
                            sngDataArray.CopyTo(dblValues, 0)

                            blnSuccess = True
                        End If
                    Case 64
                        If clsBase64EncodeDecode.DecodeNumericArray(strMSMSDataBase64Encoded, dblDataArray,
                                                                     zLibCompressed, eEndianMode) Then
                            ReDim dblValues(dblDataArray.Length - 1)
                            dblDataArray.CopyTo(dblValues, 0)

                            blnSuccess = True
                        End If
                    Case Else
                        ' Invalid numeric precision
                End Select

                If blnSuccess Then
                    With mCurrentSpectrum
                        If dblValues.Length <> .DataCount Then
                            If .DataCount = 0 AndAlso dblValues.Length > 0 AndAlso
                                Math.Abs(dblValues(0)) < Single.Epsilon Then
                                ' Leave .PeaksCount at 0
                            ElseIf blnUpdatePeaksCountIfInconsistent Then
                                ' This shouldn't normally be necessary
                                LogErrors("ParseBinaryData (Double Precision)",
                                          "Unexpected condition: sngValues.Length <> .DataCount and .DataCount > 0")
                                .DataCount = dblValues.Length
                            End If
                        End If
                    End With
                End If

            Catch ex As Exception
                LogErrors("ParseBinaryData (Double Precision)", ex.Message)
            End Try
        End If

        Return blnSuccess
    End Function

    Protected Overrides Sub ParseElementContent()

        Dim blnSuccess As Boolean

        If mAbortProcessing Then Exit Sub
        If mCurrentSpectrum Is Nothing Then Exit Sub

        Try
            ' Check the last element name sent to startElement to determine
            ' what to do with the data we just received
            If mCurrentElement = ScanSectionNames.ArrayData Then
                ' Note: We could use GetParentElement() to determine whether this base-64 encoded data
                '  belongs to mzArrayBinary or intenArrayBinary, but it is faster to use mCurrentXMLDataFileSection
                Select Case mCurrentXMLDataFileSection
                    Case eCurrentMZDataFileSectionConstants.SpectrumDataArrayMZ
                        If Not mSkipBinaryData Then
                            With mCurrentSpectrum
                                blnSuccess = ParseBinaryData(XMLTextReaderGetInnerText(), .MZList,
                                                             .NumericPrecisionOfDataMZ, .PeaksEndianModeMZ, True)
                                If Not blnSuccess Then
                                    .DataCount = 0
                                End If
                            End With
                        Else
                            blnSuccess = True
                        End If

                    Case eCurrentMZDataFileSectionConstants.SpectrumDataArrayIntensity
                        If Not mSkipBinaryData Then
                            With mCurrentSpectrum
                                blnSuccess = ParseBinaryData(XMLTextReaderGetInnerText(), .IntensityList,
                                                             .NumericPrecisionOfDataIntensity, .PeaksEndianModeIntensity,
                                                             False)
                                ' Note: Not calling .ComputeBasePeakAndTIC() here since it will be called when the spectrum is Validated
                            End With
                        Else
                            blnSuccess = True
                        End If
                End Select
            End If
        Catch ex As Exception
            LogErrors("ParseElementContent", ex.Message)
        End Try
    End Sub

    Protected Overrides Sub ParseEndElement()

        If mAbortProcessing Then Exit Sub
        If mCurrentSpectrum Is Nothing Then Exit Sub

        Try
            ' If we just moved out of a spectrum element, then finalize the current scan
            If mXMLReader.Name = ScanSectionNames.spectrum Then
                mCurrentSpectrum.Validate()
                mSpectrumFound = True
            End If

            ParentElementStackRemove()

            ' Clear the current element name
            mCurrentElement = String.Empty
        Catch ex As Exception
            LogErrors("ParseEndElement", ex.Message)
        End Try
    End Sub

    Protected Overrides Sub ParseStartElement()

        Dim strCVName As String = String.Empty
        Dim strValue As String = String.Empty

        If mAbortProcessing Then Exit Sub
        If mCurrentSpectrum Is Nothing Then Exit Sub

        If Not MyBase.mSkippedStartElementAdvance Then
            ' Add mXMLReader.Name to mParentElementStack
            ParentElementStackAdd(mXMLReader)
        End If

        ' Store name of the element we just entered
        mCurrentElement = mXMLReader.Name

        Select Case mXMLReader.Name
            Case XMLSectionNames.CVParam
                Select Case mCurrentXMLDataFileSection
                    Case eCurrentMZDataFileSectionConstants.DataProcessingMethod
                        If GetCVNameAndValue(strCVName, strValue) Then
                            Select Case strCVName
                                Case ProcessingMethodCVParamNames.Deisotoping
                                    mInputFileStatsAddnl.IsDeisotoped = CBoolSafe(strValue, False)
                                Case ProcessingMethodCVParamNames.ChargeDeconvolution
                                    mInputFileStatsAddnl.HasChargeDeconvolution = CBoolSafe(strValue, False)
                                Case ProcessingMethodCVParamNames.PeakProcessing
                                    mInputFileStatsAddnl.PeakProcessing = strValue
                                    If strValue.ToLower.IndexOf("centroid", StringComparison.Ordinal) >= 0 Then
                                        mInputFileStatsAddnl.IsCentroid = True
                                    Else
                                        mInputFileStatsAddnl.IsCentroid = False
                                    End If
                            End Select
                        End If

                    Case eCurrentMZDataFileSectionConstants.SpectrumInstrument
                        If GetCVNameAndValue(strCVName, strValue) Then
                            Select Case strCVName
                                Case SpectrumInstrumentCVParamNames.ScanMode
                                    mCurrentSpectrum.ScanMode = strValue
                                Case SpectrumInstrumentCVParamNames.Polarity
                                    mCurrentSpectrum.Polarity = strValue
                                Case SpectrumInstrumentCVParamNames.TimeInMinutes
                                    mCurrentSpectrum.RetentionTimeMin = CSngSafe(strValue, 0)
                            End Select
                        End If

                    Case eCurrentMZDataFileSectionConstants.PrecursorIonSelection
                        If GetCVNameAndValue(strCVName, strValue) Then
                            Select Case strCVName
                                Case PrecursorIonSelectionCVParamNames.MassToChargeRatio
                                    mCurrentSpectrum.ParentIonMZ = CDblSafe(strValue, 0)
                                    With mCurrentSpectrum
                                        .ParentIonIntensity = FindIonIntensityInRecentSpectra(.ParentIonSpectrumID,
                                                                                              .ParentIonMZ)
                                    End With
                                Case PrecursorIonSelectionCVParamNames.ChargeState
                                    mCurrentSpectrum.ParentIonCharge = CIntSafe(strValue, 0)
                            End Select
                        End If

                    Case eCurrentMZDataFileSectionConstants.PrecursorActivation
                        If GetCVNameAndValue(strCVName, strValue) Then
                            Select Case strCVName
                                Case PrecursorActivationCVParamNames.Method
                                    mCurrentSpectrum.CollisionMethod = strValue
                                Case PrecursorActivationCVParamNames.CollisionEnergy
                                    mCurrentSpectrum.CollisionEnergy = CSngSafe(strValue, 0)
                                Case PrecursorActivationCVParamNames.EnergyUnits
                                    mCurrentSpectrum.CollisionEnergyUnits = strValue

                            End Select
                        End If
                End Select

            Case ScanSectionNames.spectrumList
                If GetParentElement() = XMLSectionNames.RootName Then
                    mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.SpectrumList

                    If mXMLReader.HasAttributes Then
                        mInputFileStats.ScanCount = GetAttribValue(SpectrumListAttributeNames.count, 1)
                    Else
                        mInputFileStats.ScanCount = 0
                    End If
                End If
            Case ScanSectionNames.spectrum
                If GetParentElement() = ScanSectionNames.spectrumList Then
                    mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.SpectrumList

                    mCurrentSpectrum.Clear()

                    If mXMLReader.HasAttributes Then
                        mCurrentSpectrum.SpectrumID = GetAttribValue(SpectrumAttributeNames.id, Int32.MinValue)
                        If mCurrentSpectrum.SpectrumID = Int32.MinValue Then
                            mCurrentSpectrum.SpectrumID = 0

                            mErrorMessage =
                                "Unable to read the ""id"" attribute for the current spectrum since it is missing"
                        End If

                    End If
                End If

            Case ScanSectionNames.spectrumSettings
                mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.SpectrumSettings

            Case ScanSectionNames.acqSpecification
                If GetParentElement() = ScanSectionNames.spectrumSettings Then
                    With mCurrentSpectrum
                        .SpectrumType = GetAttribValue(AcqSpecificationAttributeNames.spectrumType,
                                                       clsSpectrumInfo.SpectrumTypeNames.discrete)
                        .SpectrumCombinationMethod = GetAttribValue(AcqSpecificationAttributeNames.methodOfCombination,
                                                                    String.Empty)
                        .ScanCount = GetAttribValue(AcqSpecificationAttributeNames.count, 1)
                    End With

                    mAcquisitionElementCount = 0
                End If

            Case ScanSectionNames.acquisition
                If GetParentElement() = ScanSectionNames.acqSpecification Then
                    ' Only update mCurrentSpectrum.ScanNumber if mCurrentSpectrum.ScanCount = 1 or
                    '  mAcquisitionElementCount = 1
                    mAcquisitionElementCount += 1
                    If mAcquisitionElementCount = 1 Or mCurrentSpectrum.ScanCount = 1 Then
                        mCurrentSpectrum.ScanNumber = GetAttribValue(AcquisitionAttributeNames.acqNumber, 0)
                    End If
                End If

            Case ScanSectionNames.spectrumInstrument
                If GetParentElement() = ScanSectionNames.spectrumSettings Then
                    mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.SpectrumInstrument

                    mCurrentSpectrum.MSLevel = GetAttribValue(SpectrumInstrumentAttributeNames.msLevel, 1)
                    mCurrentSpectrum.mzRangeStart = GetAttribValue(SpectrumInstrumentAttributeNames.mzRangeStart,
                                                                   CSng(0))
                    mCurrentSpectrum.mzRangeEnd = GetAttribValue(SpectrumInstrumentAttributeNames.mzRangeStop, CSng(0))
                End If

            Case ScanSectionNames.precursorList
                mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.PrecursorList

            Case ScanSectionNames.precursor
                If GetParentElement() = ScanSectionNames.precursorList Then
                    mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.PrecursorEntry

                    mCurrentSpectrum.ParentIonSpectrumMSLevel = GetAttribValue(PrecursorAttributeNames.msLevel, CInt(0))
                    mCurrentSpectrum.ParentIonSpectrumID = GetAttribValue(PrecursorAttributeNames.spectrumRef, CInt(0))
                End If

            Case ScanSectionNames.ionSelection
                If GetParentElement() = ScanSectionNames.precursor Then
                    If GetParentElement(mParentElementStack.Count - 1) = ScanSectionNames.precursorList Then
                        mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.PrecursorIonSelection
                    End If
                End If

            Case ScanSectionNames.activation
                If GetParentElement() = ScanSectionNames.precursor Then
                    If GetParentElement(mParentElementStack.Count - 1) = ScanSectionNames.precursorList Then
                        mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.PrecursorActivation
                    End If
                End If

            Case ScanSectionNames.mzArrayBinary
                mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.SpectrumDataArrayMZ

            Case ScanSectionNames.intenArrayBinary
                mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.SpectrumDataArrayIntensity

            Case ScanSectionNames.ArrayData
                Select Case mCurrentXMLDataFileSection
                    Case eCurrentMZDataFileSectionConstants.SpectrumDataArrayMZ
                        With mCurrentSpectrum
                            .NumericPrecisionOfDataMZ = GetAttribValue(BinaryDataAttributeNames.precision, 32)
                            .PeaksEndianModeMZ = GetAttribValue(BinaryDataAttributeNames.endian,
                                                                clsSpectrumInfoMzData.EndianModes.littleEndian)
                            .DataCount = GetAttribValue(BinaryDataAttributeNames.length, 0)
                        End With

                    Case eCurrentMZDataFileSectionConstants.SpectrumDataArrayIntensity
                        With mCurrentSpectrum
                            .NumericPrecisionOfDataIntensity = GetAttribValue(BinaryDataAttributeNames.precision, 32)
                            .PeaksEndianModeIntensity = GetAttribValue(BinaryDataAttributeNames.endian,
                                                                       clsSpectrumInfoMzData.EndianModes.littleEndian)
                            ' Only update .DataCount if it is currently 0
                            If .DataCount = 0 Then
                                .DataCount = GetAttribValue(BinaryDataAttributeNames.length, 0)
                            End If
                        End With
                End Select

            Case XMLSectionNames.RootName
                mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.Start
                If mXMLReader.HasAttributes Then
                    ValidateMZDataFileVersion(GetAttribValue(mzDataRootAttrbuteNames.version, ""))
                End If
            Case HeaderSectionNames.Description
                If GetParentElement() = XMLSectionNames.RootName Then
                    mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.Headers
                End If

            Case HeaderSectionNames.admin
                If GetParentElement() = HeaderSectionNames.Description Then
                    mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.Admin
                End If

            Case HeaderSectionNames.instrument
                If GetParentElement() = HeaderSectionNames.Description Then
                    mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.Instrument
                End If

            Case HeaderSectionNames.dataProcessing
                If GetParentElement() = HeaderSectionNames.Description Then
                    mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.DataProcessing
                End If

            Case HeaderSectionNames.processingMethod
                If GetParentElement() = HeaderSectionNames.dataProcessing Then
                    mCurrentXMLDataFileSection = eCurrentMZDataFileSectionConstants.DataProcessingMethod
                End If

        End Select

        MyBase.mSkippedStartElementAdvance = False
    End Sub

    ''' <summary>
    ''' Updates the current XMLReader object with a new reader positioned at the XML for a new mass spectrum
    ''' </summary>
    ''' <param name="newReader"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function SetXMLReaderForSpectrum(newReader As XmlReader) As Boolean

        Try
            mInputFilePath = "TextStream"

            mXMLReader = newReader

            mErrorMessage = String.Empty

            InitializeLocalVariables()

            Return True

        Catch ex As Exception
            mErrorMessage = "Error updating mXMLReader"
            Return False
        End Try
    End Function

    Private Sub ValidateMZDataFileVersion(strFileVersion As String)
        ' This sub should be called from ParseElementContent

        Dim objFileVersionRegEx As Text.RegularExpressions.Regex
        Dim objMatch As Text.RegularExpressions.Match
        Dim strMessage As String

        Try
            mFileVersion = String.Empty

            ' Currently, the only version supported is 1.x (typically 1.05)
            objFileVersionRegEx = New Text.RegularExpressions.Regex("1\.[0-9]+",
                                                                    Text.RegularExpressions.RegexOptions.IgnoreCase)

            ' Validate the mzData file version
            If strFileVersion IsNot Nothing AndAlso strFileVersion.Length > 0 Then
                mFileVersion = String.Copy(strFileVersion)

                objMatch = objFileVersionRegEx.Match(strFileVersion)
                If Not objMatch.Success Then
                    ' Unknown version
                    ' Log error and abort if mParseFilesWithUnknownVersion = False
                    strMessage = "Unknown mzData file version: " & mFileVersion
                    If mParseFilesWithUnknownVersion Then
                        strMessage &= "; attempting to parse since ParseFilesWithUnknownVersion = True"
                    Else
                        mAbortProcessing = True
                        strMessage &= "; aborting read"
                    End If
                    LogErrors("ValidateMZDataFileVersion", strMessage)
                End If
            End If
        Catch ex As Exception
            LogErrors("ValidateMZDataFileVersion", ex.Message)
            mFileVersion = String.Empty
        End Try
    End Sub
End Class
