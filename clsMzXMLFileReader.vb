Option Strict On

' This class uses a SAX Parser to read an mzXML file

' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2006, Battelle Memorial Institute.  All Rights Reserved.
' Started March 26, 2006
'
' E-mail: matthew.monroe@pnl.gov or matt@alchemistmatt.com
' Website: http://ncrr.pnl.gov/ or http://www.sysbio.org/resources/staff/
' -------------------------------------------------------------------------------
'
' Last modified May 22, 2006

Public Class clsMzXMLFileReader
    Inherits clsMSXMLFileReaderBaseClass

    Public Sub New()
        Me.InitializeLocalVariables()
    End Sub

#Region "Constants and Enums"
    ' Note: The extensions must be in all caps
    Public Const MZXML_FILE_EXTENSION As String = ".MZXML"
    Public Const MZXML_FILE_EXTENSION_XML As String = "_MZXML.XML"

    ' Note that I'm using classes to group the constants
    Protected Class XMLSectionNames
        Public Const RootName As String = "mzXML"
        Public Const msRun As String = "msRun"
    End Class

    Protected Class mzXMLRootAttrbuteNames
        Public Const xmlns As String = "xmlns"
        Public Const xsi_schemaLocation As String = "xsi:schemaLocation"
    End Class

    Protected Class HeaderSectionNames
        Public Const msInstrument As String = "msInstrument"
        Public Const dataProcessing As String = "dataProcessing"
    End Class

    Protected Class ScanSectionNames
        Public Const scan As String = "scan"
        Public Const precursorMz As String = "precursorMz"
        Public Const peaks As String = "peaks"
    End Class

    Protected Class MSRunAttributeNames
        Public Const scanCount As String = "scanCount"
        Public Const startTime As String = "startTime"
        Public Const endTime As String = "endTime"
    End Class

    Protected Class DataProcessingAttributeNames
        Public Const centroided As String = "centroided"
    End Class

    Protected Class ScanAttributeNames
        Public Const num As String = "num"
        Public Const msLevel As String = "msLevel"
        Public Const peaksCount As String = "peaksCount"
        Public Const polarity As String = "polarity"
        Public Const retentionTime As String = "retentionTime"
        Public Const collisionEnergy As String = "collisionEnergy"
        Public Const lowMz As String = "lowMz"
        Public Const highMz As String = "highMz"
        Public Const basePeakMz As String = "basePeakMz"
        Public Const basePeakIntensity As String = "basePeakIntensity"
        Public Const totIonCurrent As String = "totIonCurrent"
    End Class

    Protected Class PrecursorAttributeNames
        Public Const precursorIntensity As String = "precursorIntensity"
    End Class

    Protected Class PeaksAttributeNames
        Public Const precision As String = "precision"
        Public Const byteOrder As String = "byteOrder"
        Public Const pairOrder As String = "pairOrder"
    End Class

    Protected Enum eCurrentMZXMLDataFileSectionConstants As Integer
        UnknownFile = 0
        Start = 1
        msRun = 2
        msInstrument = 3
        dataProcessing = 4
        ScanList = 5
    End Enum
#End Region

#Region "Structures"

    Protected Structure udtFileStatsAddnlType
        Public StartTimeMin As Single
        Public EndTimeMin As Single

        Public IsCentroid As Boolean      ' True if centroid (aka stick) data; False if profile (aka continuum) data
    End Structure

#End Region

#Region "Classwide Variables"
    Protected mCurrentXMLDataFileSection As eCurrentMZXMLDataFileSectionConstants
    Protected mScanDepth As Integer       ' > 0 if we're inside a scan element

    Protected mCurrentSpectrum As clsSpectrumInfoMzXML

    Protected mInputFileStatsAddnl As udtFileStatsAddnlType
#End Region

#Region "Processing Options and Interface Functions"

    Public ReadOnly Property FileInfoStartTimeMin() As Single
        Get
            Return mInputFileStatsAddnl.StartTimeMin
        End Get
    End Property

    Public ReadOnly Property FileInfoEndTimeMin() As Single
        Get
            Return mInputFileStatsAddnl.EndTimeMin
        End Get
    End Property

    Public ReadOnly Property FileInfoIsCentroid() As Boolean
        Get
            Return mInputFileStatsAddnl.IsCentroid
        End Get
    End Property
#End Region

    Protected Overrides Function GetCurrentSpectrum() As clsSpectrumInfo
        Return mCurrentSpectrum
    End Function

    Protected Overrides Sub InitializeCurrentSpectrum(ByRef objTemplateSpectrum As clsSpectrumInfo)
        If MyBase.ReadingAndStoringSpectra OrElse mCurrentSpectrum Is Nothing Then
            mCurrentSpectrum = New clsSpectrumInfoMzXML
        Else
            mCurrentSpectrum.Clear()
        End If

        If Not objTemplateSpectrum Is Nothing Then
            mCurrentSpectrum.AutoShrinkDataLists = objTemplateSpectrum.AutoShrinkDataLists
        End If
    End Sub

    Protected Overrides Sub InitializeLocalVariables()
        MyBase.InitializeLocalVariables()

        mCurrentXMLDataFileSection = eCurrentMZXMLDataFileSectionConstants.UnknownFile
        mScanDepth = 0

        With mInputFileStatsAddnl
            .StartTimeMin = 0
            .EndTimeMin = 0
            .IsCentroid = False
        End With
    End Sub

    Protected Overrides Sub LogErrors(ByVal strCallingFunction As String, ByVal strErrorDescription As String)
        MyBase.LogErrors("clsMzXMLFileReader." & strCallingFunction, strErrorDescription)
    End Sub

    Public Overrides Function OpenFile(ByVal strInputFilePath As String) As Boolean
        Dim blnSuccess As Boolean

        Me.InitializeLocalVariables()

        blnSuccess = MyBase.OpenFile(strInputFilePath)

        Return blnSuccess
    End Function

    Protected Function ParseBinaryData(ByRef strMSMSDataBase64Encoded As String) As Boolean
        ' Parses strMSMSDataBase64Encoded and stores the data in mIntensityList() and mMZList()

        Dim sngDataArray() As Single
        Dim dblDataArray() As Double

        Dim eEndianMode As clsBase64EncodeDecode.eEndianTypeConstants = clsBase64EncodeDecode.eEndianTypeConstants.BigEndian
        Dim intIndex As Integer
        Dim blnSuccess As Boolean

        If mCurrentSpectrum Is Nothing Then
            Return False
        End If

        blnSuccess = False
        If strMSMSDataBase64Encoded Is Nothing OrElse strMSMSDataBase64Encoded.Length = 0 Then
            With mCurrentSpectrum
                .DataCount = 0
                ReDim .MZList(-1)
                ReDim .IntensityList(-1)
            End With
        Else
            Try

                Select Case mCurrentSpectrum.NumericPrecisionOfData
                    Case 32
                        If mBase64Decoder.DecodeNumericArray(strMSMSDataBase64Encoded, sngDataArray, eEndianMode) Then
                            ' sngDataArray now contains pairs of singles, either m/z and intensity or intensity and m/z
                            ' Need to split this apart into two arrays

                            With mCurrentSpectrum
                                ReDim .MZList(CInt(sngDataArray.Length / 2) - 1)
                                ReDim .IntensityList(CInt(sngDataArray.Length / 2) - 1)

                                If mCurrentSpectrum.PeaksPairOrder = clsSpectrumInfoMzXML.PairOrderTypes.IntensityAndMZ Then
                                    For intIndex = 0 To sngDataArray.Length - 1 Step 2
                                        .IntensityList(CInt(intIndex / 2)) = sngDataArray(intIndex)
                                        .MZList(CInt(intIndex / 2)) = sngDataArray(intIndex + 1)
                                    Next intIndex
                                Else
                                    ' Assume PairOrderTypes.MZandIntensity
                                    For intIndex = 0 To sngDataArray.Length - 1 Step 2
                                        .MZList(CInt(intIndex / 2)) = sngDataArray(intIndex)
                                        .IntensityList(CInt(intIndex / 2)) = sngDataArray(intIndex + 1)
                                    Next intIndex
                                End If
                            End With

                            blnSuccess = True
                        End If
                    Case 64
                        If mBase64Decoder.DecodeNumericArray(strMSMSDataBase64Encoded, dblDataArray, eEndianMode) Then
                            ' dblDataArray now contains pairs of doubles, either m/z and intensity or intensity and m/z
                            ' Need to split this apart into two arrays

                            With mCurrentSpectrum
                                ReDim .MZList(CInt(dblDataArray.Length / 2) - 1)
                                ReDim .IntensityList(CInt(dblDataArray.Length / 2) - 1)

                                If mCurrentSpectrum.PeaksPairOrder = clsSpectrumInfoMzXML.PairOrderTypes.IntensityAndMZ Then
                                    For intIndex = 0 To dblDataArray.Length - 1 Step 2
                                        .IntensityList(CInt(intIndex / 2)) = CSng(dblDataArray(intIndex))
                                        .MZList(CInt(intIndex / 2)) = dblDataArray(intIndex + 1)
                                    Next intIndex
                                Else
                                    ' Assume PairOrderTypes.MZandIntensity
                                    For intIndex = 0 To dblDataArray.Length - 1 Step 2
                                        .MZList(CInt(intIndex / 2)) = dblDataArray(intIndex)
                                        .IntensityList(CInt(intIndex / 2)) = CSng(dblDataArray(intIndex + 1))
                                    Next intIndex
                                End If
                            End With

                            blnSuccess = True
                        End If
                    Case Else
                        ' Invalid numeric precision
                End Select

                If blnSuccess Then
                    With mCurrentSpectrum
                        If .MZList.Length <> .DataCount Then
                            If .DataCount = 0 AndAlso .MZList.Length > 0 AndAlso .MZList(0) = 0 AndAlso .IntensityList(0) = 0 Then
                                ' Leave .PeaksCount at 0
                            Else
                                ' This shouldn't normally be necessary
                                LogErrors("ParseBinaryData", "Unexpected condition: .MZList.Length <> .DataCount and .DataCount > 0")
                                .DataCount = .MZList.Length
                            End If
                        End If
                    End With
                End If

            Catch ex As Exception
                LogErrors("ParseBinaryData", ex.Message)
            End Try
        End If

        Return blnSuccess

    End Function

    Protected Overrides Sub ParseElementContent()

        Dim blnSuccess As Boolean

        If mAbortProcessing Then Exit Sub
        If mCurrentSpectrum Is Nothing Then Exit Sub

        Try
            ' Skip the element if we aren't parsing a scan (inside a scan element)
            ' This is an easy way to skip whitespace
            ' We can do this since since we only care about the data inside the 
            ' ScanSectionNames.precursorMz and ScanSectionNames.peaks elements
            If mScanDepth > 0 Then
                ' Check the last element name sent to startElement to determine
                ' what to do with the data we just received
                Select Case mCurrentElement
                    Case ScanSectionNames.precursorMz
                        Try
                            mCurrentSpectrum.ParentIonMZ = CSng(XMLTextReaderGetInnerText())
                        Catch ex As Exception
                            mCurrentSpectrum.ParentIonMZ = 0
                        End Try
                    Case ScanSectionNames.peaks
                        If Not mSkipBinaryData Then
                            blnSuccess = ParseBinaryData(XMLTextReaderGetInnerText())
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
            ' If we just moved out of a scan element, then finalize the current scan
            If mXMLReader.Name = ScanSectionNames.scan Then
                If mCurrentSpectrum.SpectrumStatus <> clsSpectrumInfo.eSpectrumStatusConstants.Initialized And _
                   mCurrentSpectrum.SpectrumStatus <> clsSpectrumInfo.eSpectrumStatusConstants.Validated Then
                    mCurrentSpectrum.Validate()
                    mSpectrumFound = True
                End If

                mScanDepth = mScanDepth - 1
                If mScanDepth < 0 Then
                    ' This shouldn't happen
                    LogErrors("ParseEndElement", "Unexpected condition: mScanDepth < 0")
                    mScanDepth = 0
                End If
            End If

            ParentElementStackRemove()

            ' Clear the current element name
            mCurrentElement = String.Empty
        Catch ex As Exception
            LogErrors("ParseEndElement", ex.Message)
        End Try

    End Sub

    Protected Overrides Sub ParseStartElement()
        Dim strValue As String
        Dim blnAttributeMissing As Boolean

        If mAbortProcessing Then Exit Sub
        If mCurrentSpectrum Is Nothing Then Exit Sub

        If Not MyBase.mSkippedStartElementAdvance Then
            ' Add mXMLReader.Name to mParentElementStack
            ParentElementStackAdd(mXMLReader)
        End If

        ' Store name of the element we just entered
        mCurrentElement = mXMLReader.Name

        Select Case mXMLReader.Name
            Case ScanSectionNames.scan
                mCurrentXMLDataFileSection = eCurrentMZXMLDataFileSectionConstants.ScanList

                If mScanDepth > 0 And Not MyBase.mSkippedStartElementAdvance Then
                    If mCurrentSpectrum.SpectrumStatus <> clsSpectrumInfo.eSpectrumStatusConstants.Initialized And _
                       mCurrentSpectrum.SpectrumStatus <> clsSpectrumInfo.eSpectrumStatusConstants.Validated Then
                        mCurrentSpectrum.Validate()
                        MyBase.mSkipNextReaderAdvance = True
                        MyBase.mSpectrumFound = True
                        Exit Sub
                    End If
                End If

                mCurrentSpectrum.Clear()
                mScanDepth += 1

                If Not mXMLReader.HasAttributes Then
                    blnAttributeMissing = True
                Else
                    mCurrentSpectrum.ScanNumber = GetAttribValue(ScanAttributeNames.num, Int32.MinValue)
                    If mCurrentSpectrum.ScanNumber = Int32.MinValue Then
                        blnAttributeMissing = True
                    Else
                        blnAttributeMissing = False
                        With mCurrentSpectrum
                            .ScanCount = 1
                            .ScanNumberEnd = .ScanNumber

                            .MSLevel = GetAttribValue(ScanAttributeNames.msLevel, 1)
                            .DataCount = GetAttribValue(ScanAttributeNames.peaksCount, 0)
                            .Polarity = GetAttribValue(ScanAttributeNames.polarity, "+")
                            .RetentionTimeMin = GetAttribTimeValueMinutes(ScanAttributeNames.retentionTime)
                            .mzRangeStart = GetAttribValue(ScanAttributeNames.lowMz, CSng(0))
                            .mzRangeEnd = GetAttribValue(ScanAttributeNames.highMz, CSng(0))
                            .BasePeakMZ = GetAttribValue(ScanAttributeNames.basePeakMz, CSng(0))
                            .BasePeakIntensity = GetAttribValue(ScanAttributeNames.basePeakIntensity, CSng(0))
                            .TotalIonCurrent = GetAttribValue(ScanAttributeNames.totIonCurrent, CDbl(0))
                        End With
                    End If
                End If

                If blnAttributeMissing Then
                    mCurrentSpectrum.ScanNumber = 0
                    LogErrors("ParseStartElement", "Unable to read the ""num"" attribute for the current scan since it is missing")
                End If

            Case ScanSectionNames.precursorMz
                If mXMLReader.HasAttributes Then
                    mCurrentSpectrum.ParentIonIntensity = GetAttribValue(PrecursorAttributeNames.precursorIntensity, CSng(0))
                End If

            Case ScanSectionNames.peaks
                If mXMLReader.HasAttributes Then
                    With mCurrentSpectrum
                        .NumericPrecisionOfData = GetAttribValue(PeaksAttributeNames.precision, 32)
                        .PeaksByteOrder = GetAttribValue(PeaksAttributeNames.byteOrder, clsSpectrumInfoMzXML.ByteOrderTypes.network)
                        .PeaksPairOrder = GetAttribValue(PeaksAttributeNames.pairOrder, clsSpectrumInfoMzXML.PairOrderTypes.MZandIntensity)
                    End With
                End If

            Case XMLSectionNames.RootName
                mCurrentXMLDataFileSection = eCurrentMZXMLDataFileSectionConstants.Start
                If mXMLReader.HasAttributes Then
                    ' First look for attribute xlmns
                    strValue = GetAttribValue(mzXMLRootAttrbuteNames.xmlns, String.Empty)
                    If strValue Is Nothing OrElse strValue.Length = 0 Then
                        ' Attribute not found; look for attribute xsi:schemaLocation
                        strValue = GetAttribValue(mzXMLRootAttrbuteNames.xsi_schemaLocation, String.Empty)
                    End If

                    ValidateMZXmlFileVersion(strValue)
                End If

            Case HeaderSectionNames.msInstrument
                If GetParentElement() = XMLSectionNames.msRun Then
                    mCurrentXMLDataFileSection = eCurrentMZXMLDataFileSectionConstants.msInstrument
                End If

            Case XMLSectionNames.msRun
                mCurrentXMLDataFileSection = eCurrentMZXMLDataFileSectionConstants.msRun

                If mXMLReader.HasAttributes Then
                    mInputFileStats.ScanCount = GetAttribValue(MSRunAttributeNames.scanCount, 0)

                    With mInputFileStatsAddnl
                        .StartTimeMin = GetAttribTimeValueMinutes(MSRunAttributeNames.startTime)
                        .EndTimeMin = GetAttribTimeValueMinutes(MSRunAttributeNames.endTime)

                        ' Note: A bug in the ReAdW software we use to create mzXML files records the .StartTime and .EndTime values in minutes but labels them as seconds
                        ' Check for this by computing the average seconds/scan
                        ' If too low, multiply the start and end times by 60
                        If mInputFileStats.ScanCount > 0 Then
                            If (.EndTimeMin - .StartTimeMin) / mInputFileStats.ScanCount * 60 < 0.1 Then
                                ' Less than 0.1 sec/scan; this is unlikely
                                .StartTimeMin = .StartTimeMin * 60
                                .EndTimeMin = .EndTimeMin * 60
                            End If
                        End If
                    End With
                Else
                    mInputFileStats.ScanCount = 0
                End If

            Case HeaderSectionNames.dataProcessing
                If GetParentElement() = XMLSectionNames.msRun Then
                    mCurrentXMLDataFileSection = eCurrentMZXMLDataFileSectionConstants.dataProcessing
                    mInputFileStatsAddnl.IsCentroid = GetAttribValue(DataProcessingAttributeNames.centroided, False)
                End If

        End Select

        MyBase.mSkippedStartElementAdvance = False
    End Sub

    Protected Sub ValidateMZXmlFileVersion(ByVal strFileVersion As String)
        ' This sub should be called from ParseStartElement

        Dim objFileVersionRegEx As System.Text.RegularExpressions.Regex
        Dim objMatch As System.Text.RegularExpressions.Match
        Dim strMessage As String

        Try
            mFileVersion = String.Empty

            ' Currently, the only version supported is mzXML_2.x
            objFileVersionRegEx = New System.Text.RegularExpressions.Regex("mzXML_[^\s""/]+", Text.RegularExpressions.RegexOptions.IgnoreCase)

            ' Validate the mzXML file version
            If Not strFileVersion Is Nothing AndAlso strFileVersion.Length > 0 Then
                ' Parse out the version number
                objMatch = objFileVersionRegEx.Match(strFileVersion)
                If objMatch.Success AndAlso Not objMatch.Value Is Nothing Then
                    ' Record the version
                    mFileVersion = objMatch.Value
                End If
            End If

            If mFileVersion.Length > 0 Then
                If mFileVersion.IndexOf("mzXML_2") < 0 Then
                    ' strFileVersion contains mzXML_ but not mxXML_2
                    ' Thus, assume unknown version
                    ' Log error and abort if mParseFilesWithUnknownVersion = False
                    strMessage = "Unknown mzXML file version: " & mFileVersion
                    If mParseFilesWithUnknownVersion Then
                        strMessage &= "; attempting to parse since ParseFilesWithUnknownVersion = True"
                    Else
                        mAbortProcessing = True
                        strMessage &= "; aborting read"
                    End If
                    LogErrors("ValidateMZXmlFileVersion", strMessage)
                End If
            End If
        Catch ex As Exception
            LogErrors("ValidateMZXmlFileVersion", ex.Message)
            mFileVersion = String.Empty
        End Try

    End Sub

End Class
