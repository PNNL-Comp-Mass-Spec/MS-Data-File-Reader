Option Strict On

' This class can be used to open an MS Data file (currently .mzXML and .mzData) and 
' index the location of all of the spectra present.  This does not cache the mass spectra
' data in memory, and therefore uses little memory, but once the indexing is complete, 
' random access to the spectra is possible.  After the indexing is complete, spectra 
' can be obtained using GetSpectrumByScanNumber or GetSpectrumByIndex

' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2006, Battelle Memorial Institute.  All Rights Reserved.
' Program started April 16, 2006
'
' E-mail: matthew.monroe@pnl.gov or matt@alchemistmatt.com
' Website: http://ncrr.pnl.gov/ or http://www.sysbio.org/resources/staff/
' -------------------------------------------------------------------------------
'
' Licensed under the Apache License, Version 2.0; you may not use this file except
' in compliance with the License.  You may obtain a copy of the License at
' http://www.apache.org/licenses/LICENSE-2.0
'
' Notice: This computer software was prepared by Battelle Memorial Institute,
' hereinafter the Contractor, under Contract No. DE-AC05-76RL0 1830 with the
' Department of Energy (DOE).  All rights in the computer software are reserved
' by DOE on behalf of the United States Government and the Contractor as
' provided in the Contract.  NEITHER THE GOVERNMENT NOR THE CONTRACTOR MAKES ANY
' WARRANTY, EXPRESS OR IMPLIED, OR ASSUMES ANY LIABILITY FOR THE USE OF THIS
' SOFTWARE.  This notice including this sentence must appear on any copies of
' this computer software.
'
' Last modified May 23, 2006

Public MustInherit Class clsMSDataFileAccessorBaseClass
    Inherits clsMSDataFileReaderBaseClass

    Public Sub New()
        InitializeLocalVariables()
    End Sub

    Protected Overrides Sub Finalize()
        MyBase.Finalize()

        Try
            CloseFile()
		Catch ex As Exception
			' Ignore errors here
		End Try

    End Sub

#Region "Constants and Enums"
    Protected Const INITIAL_SCAN_RESERVE_COUNT As Integer = 1000

    Protected Enum emmElementMatchModeConstants
        StartElement = 0
        EndElement = 1
    End Enum
#End Region

#Region "Structures"
    Protected Structure udtIndexedSpectrumInfoType
        Public ScanNumber As Integer
        Public SpectrumID As Integer        ' Only used by mzData files
        Public ByteOffsetStart As Long
        Public ByteOffsetEnd As Long
    End Structure
#End Region

#Region "Classwide Variables"
	Protected mInputFileEncoding As clsBinaryTextReader.InputFileEncodingConstants
    Protected mCharSize As Byte
    Protected mIndexingComplete As Boolean

	Protected mBinaryReader As IO.FileStream
	Protected mBinaryTextReader As clsBinaryTextReader

	Protected mInFileCurrentLineText As String
	Protected mInFileCurrentCharIndex As Integer

	' This hash table maps scan number to index in mIndexedSpectrumInfo()
	' If more than one spectrum comes from the same scan, then tracks the first one read
	Protected mIndexedSpectraScanToIndex As Hashtable

	Protected mLastSpectrumIndexRead As Integer

	' These variables are used when mDataReaderMode = Indexed
	Protected mIndexedSpectrumInfoCount As Integer
	Protected mIndexedSpectrumInfo() As udtIndexedSpectrumInfoType
#End Region

#Region "Processing Options and Interface Functions"
	Public Overrides ReadOnly Property CachedSpectrumCount() As Integer
		Get
			If mDataReaderMode = clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Cached Then
				Return MyBase.CachedSpectrumCount
			Else
				Return mIndexedSpectrumInfoCount
			End If
		End Get
	End Property
#End Region

	Protected MustOverride Function AdvanceFileReaders(ByVal eElementMatchMode As emmElementMatchModeConstants) As Boolean

	Public Overrides Sub CloseFile()

		If Not mBinaryReader Is Nothing Then
			mBinaryReader.Close()
			mBinaryReader = Nothing
		End If

		If Not mBinaryTextReader Is Nothing Then
			mBinaryTextReader.Close()
			mBinaryTextReader = Nothing
		End If

		mInputFilePath = String.Empty
		mReadingAndStoringSpectra = False

	End Sub

	Protected Overridable Function ExtractTextBetweenOffsets(ByVal strFilePath As String, ByVal lngStartByteOffset As Long, ByVal lngEndByteOffset As Long) As String
		' Extracts the text between lngStartByteOffset and lngEndByteOffset in strFilePath and returns it
		' This function is typically called by ExtractTextFromFile in a derived class

		Dim bytData() As Byte
		Dim intBytesToRead As Integer

		Try
			If Not mBinaryReader Is Nothing AndAlso mBinaryReader.CanRead Then
				mBinaryReader.Seek(lngStartByteOffset, IO.SeekOrigin.Begin)

				intBytesToRead = CInt(lngEndByteOffset - lngStartByteOffset + 1)

				If intBytesToRead > 0 Then
					ReDim bytData(intBytesToRead - 1)
					intBytesToRead = mBinaryReader.Read(bytData, 0, intBytesToRead)

					Select Case mInputFileEncoding
						Case clsBinaryTextReader.InputFileEncodingConstants.Ascii
							Return Convert.ToString(System.Text.Encoding.ASCII.GetChars(bytData, 0, intBytesToRead))
						Case clsBinaryTextReader.InputFileEncodingConstants.UTF8
							Return Convert.ToString(System.Text.Encoding.UTF8.GetChars(bytData, 0, intBytesToRead))
						Case clsBinaryTextReader.InputFileEncodingConstants.UnicodeNormal
							Return Convert.ToString(System.Text.Encoding.Unicode.GetChars(bytData, 0, intBytesToRead))
						Case clsBinaryTextReader.InputFileEncodingConstants.UnicodeNormal
							Return Convert.ToString(System.Text.Encoding.BigEndianUnicode.GetChars(bytData, 0, intBytesToRead))
						Case Else
							' Unknown encoding
							Return String.Empty
					End Select
				End If
			End If

		Catch ex As Exception
			LogErrors("ExtractXMLText", ex.Message)
		End Try

		' If we get here, then no match was found, so return an empty string
		Return String.Empty

	End Function

	' Note: Note that sngStartTimeMinutesAllScans and sngEndTimeMinutesAllScans are really only appropriate for mzXML files
	Protected Function ExtractTextFromFile(ByVal strFilePath As String, ByVal lngStartByteOffset As Long, ByVal lngEndByteOffset As Long, ByRef strExtractedText As String, ByVal intScanCountTotal As Integer, ByVal sngStartTimeMinutesAllScans As Single, ByVal sngEndTimeMinutesAllScans As Single) As Boolean
		' Extract the text between lngStartByteOffset and lngEndByteOffset in strFilePath, then append it to
		' mXmlFileHeader, add the closing element tags, and return ByRef in strExtractedText

		Dim blnSuccess As Boolean

		Try
			strExtractedText = GetSourceXMLHeader(intScanCountTotal, sngStartTimeMinutesAllScans, sngEndTimeMinutesAllScans) & ControlChars.NewLine & _
							   ExtractTextBetweenOffsets(strFilePath, lngStartByteOffset, lngEndByteOffset) & ControlChars.NewLine & _
							   GetSourceXMLFooter()

			blnSuccess = True
		Catch ex As Exception
			LogErrors("ExtractTextFromFile", ex.Message)
			strExtractedText = String.Empty
		End Try

		Return blnSuccess

	End Function

	Protected Overrides Function GetInputFileLocation() As String
		Try
			If mBinaryTextReader Is Nothing Then
				Return String.Empty
			Else
				Return "Line " & mBinaryTextReader.LineNumber & ", Byte Offset " & mBinaryTextReader.CurrentLineByteOffsetStart
			End If
		Catch ex As Exception
			' Ignore errors here
			Return String.Empty
		End Try
	End Function

	Public Overrides Function GetScanNumberList(ByRef ScanNumberList() As Integer) As Boolean
		' Return the list of indexed scan numbers (aka acquisition numbers)

		Dim intSpectrumIndex As Integer
		Dim blnSuccess As Boolean

		Try
			blnSuccess = False
			If mDataReaderMode = clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Cached Then
				blnSuccess = MyBase.GetScanNumberList(ScanNumberList)
			Else
				If GetSpectrumReadyStatus(True) Then
					If mIndexedSpectrumInfo Is Nothing OrElse mIndexedSpectrumInfoCount = 0 Then
						ReDim ScanNumberList(-1)
					Else
						ReDim ScanNumberList(mIndexedSpectrumInfoCount - 1)
						For intSpectrumIndex = 0 To ScanNumberList.Length - 1
							ScanNumberList(intSpectrumIndex) = mIndexedSpectrumInfo(intSpectrumIndex).ScanNumber
						Next intSpectrumIndex
						blnSuccess = True
					End If
				End If
			End If
		Catch ex As Exception
			LogErrors("GetScanNumberList", ex.Message)
		End Try

		Return blnSuccess

	End Function

	Public MustOverride Function GetSourceXMLFooter() As String

	Public MustOverride Function GetSourceXMLHeader(ByVal intScanCountTotal As Integer, ByVal sngStartTimeMinutesAllScans As Single, ByVal sngEndTimeMinutesAllScans As Single) As String

	Public Function GetSourceXMLByIndex(ByVal intSpectrumIndex As Integer, ByRef strSourceXML As String) As Boolean
		' Returns the XML for the given spectrum
		' This does not include the header or footer XML for the file
		' Only valid if we have Indexed data in memory

		Dim blnSuccess As Boolean

		Try
			blnSuccess = False
			mErrorMessage = String.Empty
			If mDataReaderMode = clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Indexed Then
				If GetSpectrumReadyStatus(True) Then
					If mIndexedSpectrumInfoCount = 0 Then
						mErrorMessage = "Indexed data not in memory"

					ElseIf intSpectrumIndex >= 0 And intSpectrumIndex < mIndexedSpectrumInfoCount Then
						' Move the binary file reader to .ByteOffsetStart and populate strXMLText with the text for the given spectrum
						strSourceXML = ExtractTextBetweenOffsets(mInputFilePath, mIndexedSpectrumInfo(intSpectrumIndex).ByteOffsetStart, mIndexedSpectrumInfo(intSpectrumIndex).ByteOffsetEnd)
						If strSourceXML Is Nothing OrElse strSourceXML.Length = 0 Then
							blnSuccess = False
						Else
							blnSuccess = True
						End If

					Else
						mErrorMessage = "Invalid spectrum index: " & intSpectrumIndex.ToString
					End If
				End If
			Else
				mErrorMessage = "Indexed data not in memory"
			End If
		Catch ex As Exception
			LogErrors("GetSourceXMLByIndex", ex.Message)
		End Try

		Return blnSuccess

	End Function

	Public Function GetSourceXMLByScanNumber(ByVal intScanNumber As Integer, ByRef strSourceXML As String) As Boolean
		' Returns the XML for the given spectrum
		' This does not include the header or footer XML for the file
		' Only valid if we have Indexed data in memory

		Dim intSpectrumIndex As Integer
		Dim objIndex As Object

		Dim blnSuccess As Boolean

		Try
			blnSuccess = False
			mErrorMessage = String.Empty
			If mDataReaderMode = clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Indexed Then
				If GetSpectrumReadyStatus(True) Then
					If mIndexedSpectraScanToIndex Is Nothing OrElse mIndexedSpectraScanToIndex.Count = 0 Then
						For intSpectrumIndex = 0 To mIndexedSpectrumInfoCount - 1
							If mIndexedSpectrumInfo(intSpectrumIndex).ScanNumber = intScanNumber Then
								blnSuccess = GetSourceXMLByIndex(intSpectrumIndex, strSourceXML)
								Exit For
							End If
						Next intSpectrumIndex
					Else
						' Look for intScanNumber in mIndexedSpectraScanToIndex
						objIndex = mIndexedSpectraScanToIndex(intScanNumber)
						If Not objIndex Is Nothing Then
							intSpectrumIndex = CType(objIndex, Integer)
							blnSuccess = GetSourceXMLByIndex(intSpectrumIndex, strSourceXML)
						End If
					End If

					If Not blnSuccess AndAlso mErrorMessage.Length = 0 Then
						mErrorMessage = "Invalid scan number: " & intScanNumber.ToString
					End If
				End If
			Else
				mErrorMessage = "Indexed data not in memory"
			End If
		Catch ex As Exception
			LogErrors("GetSourceXMLByScanNumber", ex.Message)
		End Try

		Return blnSuccess

	End Function

	Public Overrides Function GetSpectrumByIndex(ByVal intSpectrumIndex As Integer, ByRef objSpectrumInfo As clsSpectrumInfo) As Boolean
		' Returns True if success, False if failure
		' Only valid if we have Cached or Indexed data in memory

		Dim blnSuccess As Boolean

		Try
			If mDataReaderMode = clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Cached Then
				blnSuccess = MyBase.GetSpectrumByIndex(intSpectrumIndex, objSpectrumInfo)
			ElseIf mDataReaderMode = clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Indexed Then
				blnSuccess = GetSpectrumByIndexWork(intSpectrumIndex, objSpectrumInfo, False)
			Else
				mErrorMessage = "Cached or indexed data not in memory"
				blnSuccess = False
			End If
		Catch ex As Exception
			LogErrors("GetSpectrumByIndex", ex.Message)
		End Try

		Return blnSuccess

	End Function

	Protected MustOverride Function GetSpectrumByIndexWork(ByVal intSpectrumIndex As Integer, ByRef objSpectrumInfo As clsSpectrumInfo, ByVal blnHeaderInfoOnly As Boolean) As Boolean

	Public Overrides Function GetSpectrumByScanNumber(ByVal intScanNumber As Integer, ByRef objSpectrumInfo As clsSpectrumInfo) As Boolean
		Return GetSpectrumByScanNumberWork(intScanNumber, objSpectrumInfo, False)
	End Function

	Protected Function GetSpectrumByScanNumberWork(ByVal intScanNumber As Integer, ByRef objSpectrumInfo As clsSpectrumInfo, ByVal blnHeaderInfoOnly As Boolean) As Boolean
		' Return the data for scan intScanNumber in mIndexedSpectrumInfo
		' Returns True if success, False if failure
		' Only valid if we have Cached or Indexed data in memory

		Dim intSpectrumIndex As Integer
		Dim objIndex As Object

		Dim blnSuccess As Boolean

		Try
			blnSuccess = False
			mErrorMessage = String.Empty
			If mDataReaderMode = clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Cached Then
				blnSuccess = MyBase.GetSpectrumByScanNumber(intScanNumber, objSpectrumInfo)
			ElseIf mDataReaderMode = clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Indexed Then
				If GetSpectrumReadyStatus(True) Then
					If mIndexedSpectraScanToIndex Is Nothing OrElse mIndexedSpectraScanToIndex.Count = 0 Then
						For intSpectrumIndex = 0 To mIndexedSpectrumInfoCount - 1
							If mIndexedSpectrumInfo(intSpectrumIndex).ScanNumber = intScanNumber Then
								blnSuccess = GetSpectrumByIndex(intSpectrumIndex, objSpectrumInfo)
								Exit For
							End If
						Next intSpectrumIndex
					Else
						' Look for intScanNumber in mIndexedSpectraScanToIndex
						objIndex = mIndexedSpectraScanToIndex(intScanNumber)
						If Not objIndex Is Nothing Then
							intSpectrumIndex = CType(objIndex, Integer)
							blnSuccess = GetSpectrumByIndexWork(intSpectrumIndex, objSpectrumInfo, blnHeaderInfoOnly)
						End If
					End If

					If Not blnSuccess AndAlso mErrorMessage.Length = 0 Then
						mErrorMessage = "Invalid scan number: " & intScanNumber.ToString
					End If
				End If
			Else
				mErrorMessage = "Cached or indexed data not in memory"
			End If
		Catch ex As Exception
			LogErrors("GetSpectrumByScanNumberWork", ex.Message)
		End Try

		Return blnSuccess

	End Function

	Public Function GetSpectrumHeaderInfoByIndex(ByVal intSpectrumIndex As Integer, ByRef objSpectrumInfo As clsSpectrumInfo) As Boolean
		Return GetSpectrumByIndexWork(intSpectrumIndex, objSpectrumInfo, True)
	End Function

	Public Function GetSpectrumHeaderInfoByScanNumber(ByVal intScanNumber As Integer, ByRef objSpectrumInfo As clsSpectrumInfo) As Boolean
		Return GetSpectrumByScanNumberWork(intScanNumber, objSpectrumInfo, True)
	End Function

	Protected Function GetSpectrumReadyStatus(ByVal blnAllowConcurrentReading As Boolean) As Boolean
		' If blnAllowConcurrentReading = True, then returns True if mBinaryReader is ready for reading
		' If blnAllowConcurrentReading = False, then Returns True only after the file is fully indexed
		' Otherwise, returns false

		Dim blnReady As Boolean

		If mBinaryReader Is Nothing OrElse Not mBinaryReader.CanRead Then
			mErrorMessage = "Data file not currently open"
			blnReady = False
		ElseIf blnAllowConcurrentReading Then
			blnReady = True
		Else
			blnReady = Not mReadingAndStoringSpectra
		End If

		Return blnReady

	End Function

	Protected Sub InitializeFileTrackingVariables()

		InitializeLocalVariables()

		' Reset the tracking variables for the text file
		mInFileCurrentLineText = String.Empty
		mInFileCurrentCharIndex = -1

		' Reset the indexed spectrum info
		mIndexedSpectrumInfoCount = 0
		ReDim mIndexedSpectrumInfo(INITIAL_SCAN_RESERVE_COUNT - 1)

		If mIndexedSpectraScanToIndex Is Nothing Then
			mIndexedSpectraScanToIndex = New Hashtable
		Else
			mIndexedSpectraScanToIndex.Clear()
		End If
	End Sub

	Protected Overrides Sub InitializeLocalVariables()
		MyBase.InitializeLocalVariables()

		mErrorMessage = String.Empty
		mLastSpectrumIndexRead = 0

		mDataReaderMode = clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Indexed
		mInputFileEncoding = clsBinaryTextReader.InputFileEncodingConstants.Ascii
		mCharSize = 1
		mIndexingComplete = False

	End Sub

	Protected Function InitializeRegEx(ByVal strPattern As String) As Text.RegularExpressions.Regex
		Return New Text.RegularExpressions.Regex(strPattern, Text.RegularExpressions.RegexOptions.Compiled Or _
																	Text.RegularExpressions.RegexOptions.IgnoreCase)
	End Function

	' This function should be defined to look for an existing byte offset index and, if found,
	'  populate mIndexedSpectrumInfo() and set mIndexingComplete = True
	Protected MustOverride Function LoadExistingIndex() As Boolean

	Protected Overrides Sub LogErrors(ByVal strCallingFunction As String, ByVal strErrorDescription As String)
		MyBase.LogErrors("clsMSDataFileAccessorBaseClass." & strCallingFunction, strErrorDescription)
	End Sub

	Public Overrides Function OpenFile(ByVal strInputFilePath As String) As Boolean
		' Returns true if the file is successfully opened

		Dim blnSuccess As Boolean

		Try
			blnSuccess = OpenFileInit(strInputFilePath)
			If Not blnSuccess Then Return False

			InitializeFileTrackingVariables()

			mDataReaderMode = clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Indexed
			mInputFilePath = String.Copy(strInputFilePath)

			' Initialize the binary text reader
			' Even if an existing index is present, this is needed to determine 
			'  the input file encoding and the character size
			mBinaryTextReader = New clsBinaryTextReader

			blnSuccess = False
			If mBinaryTextReader.OpenFile(mInputFilePath, IO.FileShare.Read) Then
				mInputFileEncoding = mBinaryTextReader.InputFileEncoding
				mCharSize = mBinaryTextReader.CharSize
				blnSuccess = True

				' Initialize the binary reader (which is used to extract individual spectra from the XML file)
				mBinaryReader = New IO.FileStream(mInputFilePath, IO.FileMode.Open, IO.FileAccess.Read, IO.FileShare.Read)

				' Look for a byte offset index, present either inside the .XML file (e.g. .mzXML)
				'  or in a separate file (future capability)
				' If an index is found, then set mIndexingComplete to True
				If LoadExistingIndex() Then
					mIndexingComplete = True
				End If

				If mBinaryTextReader.ByteBufferFileOffsetStart > 0 OrElse _
					   mBinaryTextReader.CurrentLineByteOffsetStart > mBinaryTextReader.ByteOrderMarkLength Then
					mBinaryTextReader.MoveToBeginning()
				End If

				mErrorMessage = String.Empty
			End If

		Catch ex As Exception
			mErrorMessage = "Error opening file: " & strInputFilePath & "; " & ex.Message
			blnSuccess = False
		End Try

		Return blnSuccess

	End Function

	''' <summary>
	''' This reading mode is not appropriate for the MS Data File Accessor
	''' </summary>
	''' <param name="strTextStream"></param>
	''' <returns>Always returns false</returns>
	''' <remarks></remarks>
    Public Overrides Function OpenTextStream(ByRef strTextStream As String) As Boolean
		mErrorMessage = "The OpenTextStream method is not valid for clsMSDataFileAccessorBaseClass"
        CloseFile()
        Return False
    End Function

    Public Function ReadAndCacheEntireFileNonIndexed() As Boolean
        ' Provides the option to cache the entire file rather than indexing it and accessing it with a binary reader

        Dim blnSuccess As Boolean

        blnSuccess = MyBase.ReadAndCacheEntireFile

        If blnSuccess Then
            mDataReaderMode = clsMSDataFileReaderBaseClass.drmDataReaderModeConstants.Cached
        End If

        Return blnSuccess

    End Function

    Public Overrides Function ReadNextSpectrum(ByRef objSpectrumInfo As clsSpectrumInfo) As Boolean
        If GetSpectrumReadyStatus(False) AndAlso mLastSpectrumIndexRead < mIndexedSpectrumInfoCount Then
            mLastSpectrumIndexRead += 1
            Return GetSpectrumByIndex(mLastSpectrumIndexRead, objSpectrumInfo)
        Else
            Return False
        End If
    End Function

    Protected Sub StoreIndexEntry(ByVal intScanNumber As Integer, ByVal lngByteOffsetStart As Long, ByVal lngByteOffsetEnd As Long)

        If mIndexedSpectrumInfoCount >= mIndexedSpectrumInfo.Length Then
            ' Double the amount of space reserved for mIndexedSpectrumInfo
            ReDim Preserve mIndexedSpectrumInfo(mIndexedSpectrumInfo.Length * 2 - 1)
        End If

        With mIndexedSpectrumInfo(mIndexedSpectrumInfoCount)
            .ScanNumber = intScanNumber

            .ByteOffsetStart = lngByteOffsetStart
            .ByteOffsetEnd = lngByteOffsetEnd

            UpdateFileStats(mIndexedSpectrumInfoCount + 1, .ScanNumber)

            If Not mIndexedSpectraScanToIndex.Contains(.ScanNumber) Then
                mIndexedSpectraScanToIndex.Add(.ScanNumber, mIndexedSpectrumInfoCount)
            End If

        End With

        ' Increment mIndexedSpectrumInfoCount
        mIndexedSpectrumInfoCount += 1

    End Sub

End Class
