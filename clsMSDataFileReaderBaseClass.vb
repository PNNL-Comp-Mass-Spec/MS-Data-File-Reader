Option Strict On

Imports System.IO
Imports System.Runtime.InteropServices

' This is the base class for the various MS data file readres
'
' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2006, Battelle Memorial Institute.  All Rights Reserved.
' Started March 24, 2006
'
' E-mail: matthew.monroe@pnl.gov or matt@alchemistmatt.com
' Website: http://omics.pnl.gov/ or http://www.sysbio.org/resources/staff/
' -------------------------------------------------------------------------------
'

Public MustInherit Class clsMSDataFileReaderBaseClass
    Public Event ProgressReset()
    Public Event ProgressChanged(taskDescription As String, percentComplete As Single)

    ' PercentComplete ranges from 0 to 100, but can contain decimal percentage values
    Public Event ProgressComplete()

    Protected mProgressStepDescription As String

    ' Ranges from 0 to 100, but can contain decimal percentage values
    Protected mProgressPercentComplete As Single


    Public Sub New()
        InitializeLocalVariables()
    End Sub

#Region "Constants and Enums"

    Public Const PROGRAM_DATE As String = "July 8, 2016"

    Public Const CHARGE_CARRIER_MASS_AVG As Double = 1.00739
    Public Const CHARGE_CARRIER_MASS_MONOISO As Double = 1.00727649
    Public Const MASS_HYDROGEN As Double = 1.0078246

    Protected Const DEFAULT_MAX_CACHE_MEMORY_USAGE_MB As Integer = 128

    Public Enum drmDataReaderModeConstants
        Sequential = 0
        Cached = 1
        Indexed = 2
    End Enum

    Public Enum dftDataFileTypeConstants
        Unknown = -1
        mzData = 0
        mzXML = 1
        DtaText = 2
        MGF = 3
    End Enum

#End Region

#Region "Structures"

    Protected Structure udtFileStatsType
        ' Actual scan count if mDataReaderMode = Cached or mDataReaderMode = Indexed, or scan count as reported by the XML file if mDataReaderMode = Sequential 
        Public ScanCount As Integer
        Public ScanNumberMinimum As Integer
        Public ScanNumberMaximum As Integer
    End Structure

#End Region

#Region "Classwide Variables"

    Protected mChargeCarrierMass As Double
    Protected mErrorMessage As String
    Protected mFileVersion As String

    Protected mDataReaderMode As drmDataReaderModeConstants

    Protected mReadingAndStoringSpectra As Boolean
    Protected mAbortProcessing As Boolean
    Protected mParseFilesWithUnknownVersion As Boolean = False

    Protected mInputFilePath As String = String.Empty
    Protected mInputFileStats As udtFileStatsType

    ' These variables are used when mDataReaderMode = Cached
    Protected mCachedSpectrumCount As Integer
    Protected mCachedSpectra() As clsSpectrumInfo

    ' This hash table maps scan number to index in mCachedSpectra()
    ' If more than one spectrum comes from the same scan, then tracks the first one read
    Protected mCachedSpectraScanToIndex As Hashtable

    Protected mAutoShrinkDataLists As Boolean

#End Region

#Region "Processing Options and Interface Functions"

    ''' <summary>
    '''  When mAutoShrinkDataLists is True, clsSpectrumInfo.MZList().Length and clsSpectrumInfo.IntensityList().Length will equal DataCount; 
    '''  When mAutoShrinkDataLists is False, the memory will not be freed when DataCount shrinks or clsSpectrumInfo.Clear() is called
    ''' </summary>
    ''' <value></value>
    ''' <returns></returns>
    ''' <remarks>
    ''' Setting mAutoShrinkDataLists to False helps reduce slow, increased memory usage due to inefficient garbage collection
    ''' (this is not much of an issue in 2016, and thus this parameter defaults to True)
    ''' </remarks>
    Public Property AutoShrinkDataLists() As Boolean
        Get
            Return mAutoShrinkDataLists
        End Get
        Set(Value As Boolean)
            mAutoShrinkDataLists = Value
        End Set
    End Property

    Public Overridable ReadOnly Property CachedSpectrumCount() As Integer
        Get
            If mDataReaderMode = drmDataReaderModeConstants.Cached Then
                Return mCachedSpectrumCount
            Else
                Return 0
            End If
        End Get
    End Property

    Public ReadOnly Property CachedSpectraScanNumberMinimum() As Integer
        Get
            Return mInputFileStats.ScanNumberMinimum
        End Get
    End Property

    Public ReadOnly Property CachedSpectraScanNumberMaximum() As Integer
        Get
            Return mInputFileStats.ScanNumberMaximum
        End Get
    End Property

    Public Property ChargeCarrierMass() As Double
        Get
            Return mChargeCarrierMass
        End Get
        Set(Value As Double)
            mChargeCarrierMass = Value
        End Set
    End Property

    Public ReadOnly Property ErrorMessage() As String
        Get
            If mErrorMessage Is Nothing Then mErrorMessage = String.Empty
            Return mErrorMessage
        End Get
    End Property

    Public ReadOnly Property InputFilePath() As String
        Get
            Return mInputFilePath
        End Get
    End Property

    Public ReadOnly Property FileVersion() As String
        Get
            Return mFileVersion
        End Get
    End Property

    Public Overridable Property ParseFilesWithUnknownVersion() As Boolean
        Get
            Return mParseFilesWithUnknownVersion
        End Get
        Set(Value As Boolean)
            mParseFilesWithUnknownVersion = Value
        End Set
    End Property

    Public Overridable ReadOnly Property ProgressStepDescription() As String
        Get
            Return mProgressStepDescription
        End Get
    End Property

    ' ProgressPercentComplete ranges from 0 to 100, but can contain decimal percentage values
    Public ReadOnly Property ProgressPercentComplete() As Single
        Get
            Return CType(Math.Round(mProgressPercentComplete, 2), Single)
        End Get
    End Property

    Protected ReadOnly Property ReadingAndStoringSpectra() As Boolean
        Get
            Return mReadingAndStoringSpectra
        End Get
    End Property

    ' Note: When reading mzXML and mzData files the the FileReader classes, this value is not populated until after the first scan is read
    ' When using the FileAccessor classes, this value is populated after the file is indexed
    ' For .MGF and .DtaText files, this value will always be 0
    Public ReadOnly Property ScanCount() As Integer
        Get
            Return mInputFileStats.ScanCount
        End Get
    End Property

#End Region

    Public Sub AbortProcessingNow()
        mAbortProcessing = True
    End Sub

    Protected Function CBoolSafe(strValue As String, defaultValue As Boolean) As Boolean
        Try
            If String.IsNullOrWhiteSpace(strValue) Then
                Return defaultValue
            End If

            Dim dblValue As Double
            If Double.TryParse(strValue, dblValue) Then
                If Math.Abs(dblValue) < Single.Epsilon Then
                    Return False
                Else
                    Return True
                End If
            End If

            Dim blnValue As Boolean
            If Boolean.TryParse(strValue, blnValue) Then
                Return blnValue
            End If

            Return defaultValue

        Catch ex As Exception
            Return defaultValue
        End Try
    End Function

    Protected Function CDblSafe(strValue As String, defaultValue As Double) As Double
        Try
            Return Double.Parse(strValue)
        Catch ex As Exception
            Return defaultValue
        End Try
    End Function

    Protected Function CIntSafe(strValue As String, defaultValue As Integer) As Integer
        Try
            Return Integer.Parse(strValue)
        Catch ex As Exception
            Return defaultValue
        End Try
    End Function

    Protected Function CSngSafe(strValue As String, defaultValue As Single) As Single
        Try
            Return Single.Parse(strValue)
        Catch ex As Exception
            Return defaultValue
        End Try
    End Function

    Public MustOverride Sub CloseFile()

    Public Function ConvoluteMass(dblMassMZ As Double, intCurrentCharge As Integer, intDesiredCharge As Integer) As Double
        Return ConvoluteMass(dblMassMZ, intCurrentCharge, intDesiredCharge, mChargeCarrierMass)
    End Function

    ''' <summary>
    ''' Converts dblMassMZ to the MZ that would appear at the given intDesiredCharge
    ''' </summary>
    ''' <param name="dblMassMZ"></param>
    ''' <param name="intCurrentCharge"></param>
    ''' <param name="intDesiredCharge"></param>
    ''' <param name="dblChargeCarrierMass"></param>
    ''' <returns></returns>
    ''' <remarks>To return the neutral mass, set intDesiredCharge to 0</remarks>
    Public Shared Function ConvoluteMass(
      dblMassMZ As Double,
      intCurrentCharge As Integer,
      intDesiredCharge As Integer,
      dblChargeCarrierMass As Double) As Double

        Dim dblNewMZ As Double

        If intCurrentCharge = intDesiredCharge Then
            dblNewMZ = dblMassMZ
        Else
            If intCurrentCharge = 1 Then
                dblNewMZ = dblMassMZ
            ElseIf intCurrentCharge > 1 Then
                ' Convert dblMassMZ to M+H
                dblNewMZ = (dblMassMZ * intCurrentCharge) - dblChargeCarrierMass * (intCurrentCharge - 1)
            ElseIf intCurrentCharge = 0 Then
                ' Convert dblMassMZ (which is neutral) to M+H and store in dblNewMZ
                dblNewMZ = dblMassMZ + dblChargeCarrierMass
            Else
                ' Negative charges are not supported; return 0
                Return 0
            End If

            If intDesiredCharge > 1 Then
                dblNewMZ = (dblNewMZ + dblChargeCarrierMass * (intDesiredCharge - 1)) / intDesiredCharge
            ElseIf intDesiredCharge = 1 Then
                ' Return M+H, which is currently stored in dblNewMZ
            ElseIf intDesiredCharge = 0 Then
                ' Return the neutral mass
                dblNewMZ -= dblChargeCarrierMass
            Else
                ' Negative charges are not supported; return 0
                dblNewMZ = 0
            End If
        End If

        Return dblNewMZ
    End Function

    Public Shared Function DetermineFileType(
      strFileNameOrPath As String,
      <Out()> ByRef eFileType As dftDataFileTypeConstants) As Boolean

        ' Returns true if the file type is known
        ' Returns false if unknown or an error

        Dim strFileExtension As String
        Dim strFileName As String
        Dim blnKnownType As Boolean

        eFileType = dftDataFileTypeConstants.Unknown
        Try
            If String.IsNullOrWhiteSpace(strFileNameOrPath) Then
                Return False
            End If

            strFileName = Path.GetFileName(strFileNameOrPath.ToUpper)
            strFileExtension = Path.GetExtension(strFileName)
            If String.IsNullOrWhiteSpace(strFileExtension) Then
                Return False
            End If

            If Not strFileExtension.StartsWith(".") Then
                strFileExtension = "."c & strFileExtension
            End If

            ' Assume known file type for now
            blnKnownType = True

            Select Case strFileExtension
                Case clsMzDataFileReader.MZDATA_FILE_EXTENSION
                    eFileType = dftDataFileTypeConstants.mzData
                Case clsMzXMLFileReader.MZXML_FILE_EXTENSION
                    eFileType = dftDataFileTypeConstants.mzXML
                Case clsMGFFileReader.MGF_FILE_EXTENSION
                    eFileType = dftDataFileTypeConstants.MGF
                Case Else
                    ' See if the filename ends with MZDATA_FILE_EXTENSION_XML or MZXML_FILE_EXTENSION_XML
                    If strFileName.EndsWith(clsMzDataFileReader.MZDATA_FILE_EXTENSION_XML) Then
                        eFileType = dftDataFileTypeConstants.mzData
                    ElseIf strFileName.EndsWith(clsMzXMLFileReader.MZXML_FILE_EXTENSION_XML) Then
                        eFileType = dftDataFileTypeConstants.mzXML
                    ElseIf strFileName.EndsWith(clsDtaTextFileReader.DTA_TEXT_FILE_EXTENSION) Then
                        eFileType = dftDataFileTypeConstants.DtaText
                    Else
                        ' Unknown file type
                        blnKnownType = False
                    End If
            End Select

        Catch ex As Exception
            blnKnownType = False
        End Try

        Return blnKnownType
    End Function

    Public Shared Function GetFileReaderBasedOnFileType(strFileNameOrPath As String) As clsMSDataFileReaderBaseClass
        ' Returns a file reader based on strFileNameOrPath
        ' If the file type cannot be determined, then returns Nothing

        Dim eFileType As dftDataFileTypeConstants
        Dim objFileReader As clsMSDataFileReaderBaseClass = Nothing

        If DetermineFileType(strFileNameOrPath, eFileType) Then
            Select Case eFileType
                Case dftDataFileTypeConstants.DtaText
                    objFileReader = New clsDtaTextFileReader
                Case dftDataFileTypeConstants.MGF
                    objFileReader = New clsMGFFileReader
                Case dftDataFileTypeConstants.mzData
                    objFileReader = New clsMzDataFileReader
                Case dftDataFileTypeConstants.mzXML
                    objFileReader = New clsMzXMLFileReader
                Case Else
                    ' Unknown file type
            End Select
        End If

        Return objFileReader
    End Function

    Public Shared Function GetFileAccessorBasedOnFileType(strFileNameOrPath As String) As clsMSDataFileAccessorBaseClass
        ' Returns a file accessor based on strFileNameOrPath
        ' If the file type cannot be determined, then returns Nothing
        ' If the file type is _Dta.txt or .MGF then returns Nothing since those file types do not have file accessors

        Dim eFileType As dftDataFileTypeConstants
        Dim objFileAccessor As clsMSDataFileAccessorBaseClass = Nothing

        If DetermineFileType(strFileNameOrPath, eFileType) Then
            Select Case eFileType
                Case dftDataFileTypeConstants.mzData
                    objFileAccessor = New clsMzDataFileAccessor
                Case dftDataFileTypeConstants.mzXML
                    objFileAccessor = New clsMzXMLFileAccessor
                Case dftDataFileTypeConstants.DtaText, dftDataFileTypeConstants.MGF
                    ' These file types do not have file accessors
                Case Else
                    ' Unknown file type
            End Select
        End If

        Return objFileAccessor
    End Function

    Protected MustOverride Function GetInputFileLocation() As String

    Public Overridable Function GetScanNumberList(<Out()> ByRef ScanNumberList() As Integer) As Boolean
        ' Return the list of cached scan numbers (aka acquisition numbers)

        Dim blnSuccess As Boolean
        Dim intSpectrumIndex As Integer

        Try
            blnSuccess = False
            If mDataReaderMode = drmDataReaderModeConstants.Cached And Not mCachedSpectra Is Nothing Then
                ReDim ScanNumberList(mCachedSpectrumCount - 1)

                For intSpectrumIndex = 0 To ScanNumberList.Length - 1
                    ScanNumberList(intSpectrumIndex) = mCachedSpectra(intSpectrumIndex).ScanNumber
                Next intSpectrumIndex
                blnSuccess = True
            Else
                ReDim ScanNumberList(-1)
            End If
        Catch ex As Exception
            LogErrors("GetScanNumberList", ex.Message)
            ReDim ScanNumberList(-1)
        End Try

        Return blnSuccess
    End Function

    Public Overridable Function GetSpectrumByIndex(intSpectrumIndex As Integer,
                                                   <Out()> ByRef objSpectrumInfo As clsSpectrumInfo) As Boolean
        ' Returns True if success, False if failure
        ' Only valid if we have Cached data in memory

        Dim blnSuccess As Boolean

        blnSuccess = False
        If mDataReaderMode = drmDataReaderModeConstants.Cached AndAlso mCachedSpectrumCount > 0 Then
            If intSpectrumIndex >= 0 And intSpectrumIndex < mCachedSpectrumCount And Not mCachedSpectra Is Nothing Then
                objSpectrumInfo = mCachedSpectra(intSpectrumIndex)
                blnSuccess = True
            Else
                mErrorMessage = "Invalid spectrum index: " & intSpectrumIndex.ToString
                objSpectrumInfo = Nothing
            End If
        Else
            mErrorMessage = "Cached data not in memory"
            objSpectrumInfo = Nothing
        End If

        Return blnSuccess
    End Function

    Public Overridable Function GetSpectrumByScanNumber(intScanNumber As Integer,
                                                        <Out()> ByRef objSpectrumInfo As clsSpectrumInfo) As Boolean
        ' Looks for the first entry in mCachedSpectra with .ScanNumber = intScanNumber
        ' Returns True if success, False if failure
        ' Only valid if we have Cached data in memory

        Dim blnSuccess As Boolean
        objSpectrumInfo = Nothing

        Try
            blnSuccess = False
            mErrorMessage = String.Empty
            If mDataReaderMode = drmDataReaderModeConstants.Cached Then
                If mCachedSpectraScanToIndex Is Nothing OrElse mCachedSpectraScanToIndex.Count = 0 Then
                    For intSpectrumIndex = 0 To mCachedSpectrumCount - 1
                        If mCachedSpectra(intSpectrumIndex).ScanNumber = intScanNumber Then
                            objSpectrumInfo = mCachedSpectra(intSpectrumIndex)
                            blnSuccess = True
                            Exit For
                        End If
                    Next intSpectrumIndex
                Else
                    Dim objIndex = mCachedSpectraScanToIndex(intScanNumber)
                    If Not objIndex Is Nothing Then
                        objSpectrumInfo = mCachedSpectra(CType(objIndex, Integer))
                        blnSuccess = True
                    End If
                End If

                If Not blnSuccess AndAlso mErrorMessage.Length = 0 Then
                    mErrorMessage = "Invalid scan number: " & intScanNumber.ToString
                End If
            Else
                mErrorMessage = "Cached data not in memory"
            End If
        Catch ex As Exception
            LogErrors("GetSpectrumByScanNumber", ex.Message)
        End Try

        Return blnSuccess
    End Function

    Protected Overridable Sub InitializeLocalVariables()
        mChargeCarrierMass = CHARGE_CARRIER_MASS_MONOISO
        mErrorMessage = String.Empty
        mFileVersion = String.Empty

        mProgressStepDescription = String.Empty
        mProgressPercentComplete = 0

        mCachedSpectrumCount = 0
        ReDim mCachedSpectra(499)

        With mInputFileStats
            .ScanCount = 0
            .ScanNumberMinimum = 0
            .ScanNumberMaximum = 0
        End With

        If mCachedSpectraScanToIndex Is Nothing Then
            mCachedSpectraScanToIndex = New Hashtable
        Else
            mCachedSpectraScanToIndex.Clear()
        End If

        mAbortProcessing = False

        mAutoShrinkDataLists = True
    End Sub

    Public Shared Function IsNumber(strValue As String) As Boolean
        Try
            Return Double.TryParse(strValue, 0)
        Catch ex As Exception
            Return False
        End Try
    End Function

    Protected Overridable Sub LogErrors(strCallingFunction As String, strErrorDescription As String)
        Dim strLogFilePath As String

        Static LastCallingFunction As String
        Static LastErrorMessage As String
        Static LastSaveTime As DateTime

        Try
            If Not String.IsNullOrWhiteSpace(strErrorDescription) Then
                mErrorMessage = String.Copy(strErrorDescription)
            Else
                mErrorMessage = "Unknown error"
            End If

            If Not LastCallingFunction Is Nothing Then
                If LastCallingFunction = strCallingFunction AndAlso
                   LastErrorMessage = strErrorDescription Then
                    If DateTime.UtcNow.Subtract(LastSaveTime).TotalSeconds < 0.5 Then
                        ' Duplicate message, less than 500 milliseconds since the last save
                        ' Do not update the log file
                        Exit Sub
                    End If
                End If
            End If

            LastCallingFunction = String.Copy(strCallingFunction)
            LastErrorMessage = String.Copy(strErrorDescription)
            LastSaveTime = DateTime.UtcNow

            strLogFilePath = "MSDataFileReader_ErrorLog.txt"
            Using swErrorLog = New StreamWriter(New FileStream(strLogFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))

                swErrorLog.WriteLine(DateTime.Now & ControlChars.Tab &
                                     strCallingFunction & ControlChars.Tab &
                                     mErrorMessage & ControlChars.Tab &
                                     GetInputFileLocation())
            End Using

        Catch ex As Exception
            ' Ignore errors that occur while logging errors
        End Try
    End Sub

    Public MustOverride Function OpenFile(strInputFilePath As String) As Boolean

    Public MustOverride Function OpenTextStream(strTextStream As String) As Boolean

    ''' <summary>
    ''' Validates that strInputFilePath exists
    ''' </summary>
    ''' <param name="strInputFilePath"></param>
    ''' <returns>True if the file exists, otherwise false</returns>
    ''' <remarks>Updates mFilePath if the file is valid</remarks>
    Protected Function OpenFileInit(strInputFilePath As String) As Boolean

        ' Make sure any open file or text stream is closed
        CloseFile()

        If String.IsNullOrEmpty(strInputFilePath) Then
            mErrorMessage = "Error opening file: input file path is blank"
            Return False
        End If

        If Not File.Exists(strInputFilePath) Then
            mErrorMessage = "File not found: " & strInputFilePath
            Return False
        Else
            mInputFilePath = strInputFilePath
            Return True
        End If
    End Function

    Protected Sub OperationComplete()
        RaiseEvent ProgressComplete()
    End Sub

    Public MustOverride Function ReadNextSpectrum(<Out()> ByRef objSpectrumInfo As clsSpectrumInfo) As Boolean

    Public Overridable Function ReadAndCacheEntireFile() As Boolean
        Dim blnSuccess As Boolean

        Try
            mDataReaderMode = drmDataReaderModeConstants.Cached

            Dim objSpectrumInfo As clsSpectrumInfo = Nothing
            AutoShrinkDataLists = False

            mReadingAndStoringSpectra = True
            ResetProgress()

            Do While ReadNextSpectrum(objSpectrumInfo) And Not mAbortProcessing

                If mCachedSpectrumCount >= mCachedSpectra.Length Then
                    ReDim Preserve mCachedSpectra(mCachedSpectra.Length * 2 - 1)
                End If

                If Not objSpectrumInfo Is Nothing Then
                    mCachedSpectra(mCachedSpectrumCount) = objSpectrumInfo

                    If Not mCachedSpectraScanToIndex.Contains(objSpectrumInfo.ScanNumber) Then
                        mCachedSpectraScanToIndex.Add(objSpectrumInfo.ScanNumber, mCachedSpectrumCount)
                    End If

                    mCachedSpectrumCount += 1

                    With mInputFileStats
                        .ScanCount = mCachedSpectrumCount
                        Dim intScanNumber = objSpectrumInfo.ScanNumber
                        If .ScanCount = 1 Then
                            .ScanNumberMaximum = intScanNumber
                            .ScanNumberMinimum = intScanNumber
                        Else
                            If intScanNumber < .ScanNumberMinimum Then
                                .ScanNumberMinimum = intScanNumber
                            End If
                            If intScanNumber > .ScanNumberMaximum Then
                                .ScanNumberMaximum = intScanNumber
                            End If
                        End If
                    End With
                End If
            Loop

            If Not mAbortProcessing Then
                OperationComplete()
            End If

            blnSuccess = True

        Catch ex As Exception
            LogErrors("ReadAndCacheEntireFile", ex.Message)
            blnSuccess = False
        Finally
            mReadingAndStoringSpectra = False
        End Try

        Return blnSuccess
    End Function

    Protected Sub ResetProgress()
        RaiseEvent ProgressReset()
    End Sub

    Protected Sub ResetProgress(strProgressStepDescription As String)
        UpdateProgress(strProgressStepDescription, 0)
        RaiseEvent ProgressReset()
    End Sub

    Protected Sub UpdateFileStats(intScanNumber As Integer)
        UpdateFileStats(mInputFileStats.ScanCount + 1, intScanNumber)
    End Sub

    Protected Sub UpdateFileStats(intScanCount As Integer, intScanNumber As Integer)
        With mInputFileStats
            .ScanCount = intScanCount
            If intScanCount <= 1 Then
                .ScanNumberMinimum = intScanNumber
                .ScanNumberMaximum = intScanNumber
            Else
                If intScanNumber < .ScanNumberMinimum Then
                    .ScanNumberMinimum = intScanNumber
                End If
                If intScanNumber > .ScanNumberMaximum Then
                    .ScanNumberMaximum = intScanNumber
                End If
            End If
        End With
    End Sub

    Public Sub UpdateProgressDescription(strProgressStepDescription As String)
        mProgressStepDescription = strProgressStepDescription
    End Sub

    Protected Sub UpdateProgress(strProgressStepDescription As String)
        UpdateProgress(strProgressStepDescription, mProgressPercentComplete)
    End Sub

    Protected Sub UpdateProgress(dblPercentComplete As Double)
        UpdateProgress(Me.ProgressStepDescription, CSng(dblPercentComplete))
    End Sub

    Protected Sub UpdateProgress(sngPercentComplete As Single)
        UpdateProgress(Me.ProgressStepDescription, sngPercentComplete)
    End Sub

    Protected Sub UpdateProgress(strProgressStepDescription As String, sngPercentComplete As Single)
        mProgressStepDescription = strProgressStepDescription
        If sngPercentComplete < 0 Then
            sngPercentComplete = 0
        ElseIf sngPercentComplete > 100 Then
            sngPercentComplete = 100
        End If
        mProgressPercentComplete = sngPercentComplete

        RaiseEvent ProgressChanged(Me.ProgressStepDescription, Me.ProgressPercentComplete)
    End Sub
End Class
