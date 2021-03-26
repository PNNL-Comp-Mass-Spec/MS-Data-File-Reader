Option Strict On

Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Text.RegularExpressions
Imports System.Xml

' This class can be used to open a .mzData file and index the location
' of all of the spectra present.  This does not cache the mass spectra data in
' memory, and therefore uses little memory, but once the indexing is complete,
' random access to the spectra is possible.  After the indexing is complete, spectra
' can be obtained using GetSpectrumByScanNumber or GetSpectrumByIndex

' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2006, Battelle Memorial Institute.  All Rights Reserved.
' Program started April 16, 2006
'
' E-mail: matthew.monroe@pnl.gov or proteomics@pnnl.gov
' Website: http://omics.pnl.gov/ or http://panomics.pnnl.gov/
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

Public Class clsMzDataFileAccessor
    Inherits clsMSDataFileAccessorBaseClass

    Public Sub New()
        InitializeObjectVariables()
        InitializeLocalVariables()
    End Sub

    Protected Overrides Sub Finalize()
        MyBase.Finalize()

        If mXmlFileReader IsNot Nothing Then
            mXmlFileReader = Nothing
        End If
    End Sub

#Region "Constants and Enums"

    Private Const SPECTRUM_LIST_START_ELEMENT As String = "<spectrumList"
    Private Const SPECTRUM_LIST_END_ELEMENT As String = "</spectrumList>"

    Private Const SPECTRUM_START_ELEMENT As String = "<spectrum"
    Private Const SPECTRUM_END_ELEMENT As String = "</spectrum>"

    Private Const MZDATA_START_ELEMENT As String = "<mzData"
    Private Const MZDATA_END_ELEMENT As String = "</mzData>"

#End Region

#Region "Classwide Variables"

    Private mXmlFileReader As clsMzDataFileReader

    Private mCurrentSpectrumInfo As clsSpectrumInfoMzData

    Private mInputFileStatsSpectrumIDMinimum As Integer
    Private mInputFileStatsSpectrumIDMaximum As Integer

    Private mXmlFileHeader As String
    Private mAddNewLinesToHeader As Boolean

    Private mSpectrumStartElementRegEx As Regex
    Private mSpectrumEndElementRegEx As Regex

    Private mSpectrumListRegEx As Regex
    Private mAcquisitionNumberRegEx As Regex
    Private mSpectrumIDRegEx As Regex

    ' This hash table maps spectrum ID to index in mCachedSpectra()
    ' If more than one spectrum has the same spectrum ID, then tracks the first one read
    Private mIndexedSpectraSpectrumIDToIndex As Hashtable

    Private mXMLReaderSettings As XmlReaderSettings

#End Region

#Region "Processing Options and Interface Functions"

    Public ReadOnly Property CachedSpectraSpectrumIDMinimum() As Integer
        Get
            Return mInputFileStatsSpectrumIDMinimum
        End Get
    End Property

    Public ReadOnly Property CachedSpectraSpectrumIDMaximum() As Integer
        Get
            Return mInputFileStatsSpectrumIDMaximum
        End Get
    End Property

    Public Overrides Property ParseFilesWithUnknownVersion() As Boolean
        Get
            Return MyBase.ParseFilesWithUnknownVersion
        End Get
        Set(Value As Boolean)
            MyBase.ParseFilesWithUnknownVersion = Value
            If mXmlFileReader IsNot Nothing Then
                mXmlFileReader.ParseFilesWithUnknownVersion = Value
            End If
        End Set
    End Property

#End Region

    Protected Overrides Function AdvanceFileReaders(eElementMatchMode As emmElementMatchModeConstants) As Boolean
        ' Uses the BinaryTextReader to look for strTextToFind

        Dim blnMatchFound As Boolean
        Dim blnAppendingText As Boolean
        Dim lngByteOffsetForRewind As Long

        Dim blnLookForScanCountOnNextRead As Boolean
        Dim strScanCountSearchText As String = String.Empty

        Dim intCharIndex As Integer

        Dim strAcqNumberSearchText As String
        Dim blnAcqNumberFound As Boolean

        Dim strInFileCurrentLineSubstring As String

        Dim objMatch As Match

        Try
            If mInFileCurrentLineText Is Nothing Then
                mInFileCurrentLineText = String.Empty
            End If

            strInFileCurrentLineSubstring = String.Empty
            blnAppendingText = False

            strAcqNumberSearchText = String.Empty
            blnAcqNumberFound = False

            blnMatchFound = False
            Do While Not (blnMatchFound Or mAbortProcessing)

                If mInFileCurrentCharIndex + 1 < mInFileCurrentLineText.Length Then

                    If blnAppendingText Then
                        strInFileCurrentLineSubstring &= ControlChars.NewLine &
                                                         mInFileCurrentLineText.Substring(mInFileCurrentCharIndex + 1)
                    Else
                        strInFileCurrentLineSubstring = mInFileCurrentLineText.Substring(mInFileCurrentCharIndex + 1)
                    End If

                    If mAddNewLinesToHeader Then
                        ' We haven't yet found the first scan; look for "<spectrumList"
                        intCharIndex = mInFileCurrentLineText.IndexOf(SPECTRUM_LIST_START_ELEMENT,
                                                                      mInFileCurrentCharIndex + 1,
                                                                      StringComparison.Ordinal)

                        If intCharIndex >= 0 Then
                            ' Only add a portion of mInFileCurrentLineText to mXmlFileHeader
                            '  since it contains SPECTRUM_LIST_START_ELEMENT

                            If intCharIndex > 0 Then
                                mXmlFileHeader &= mInFileCurrentLineText.Substring(0, intCharIndex)
                            End If
                            mAddNewLinesToHeader = False

                            strScanCountSearchText = strInFileCurrentLineSubstring.Substring(intCharIndex)
                            blnLookForScanCountOnNextRead = True
                        Else
                            ' Append mInFileCurrentLineText to mXmlFileHeader
                            mXmlFileHeader &= mInFileCurrentLineText & ControlChars.NewLine
                        End If
                    ElseIf blnLookForScanCountOnNextRead Then
                        strScanCountSearchText &= ControlChars.NewLine & strInFileCurrentLineSubstring
                    End If

                    If blnLookForScanCountOnNextRead Then
                        ' Look for the Scan Count value in strScanCountSearchText
                        objMatch = mSpectrumListRegEx.Match(strScanCountSearchText)

                        If objMatch.Success Then
                            ' Record the Scan Count value
                            If objMatch.Groups.Count > 1 Then
                                Try
                                    mInputFileStats.ScanCount = CInt(objMatch.Groups(1).Captures(0).Value)
                                Catch ex As Exception
                                End Try
                            End If
                            blnLookForScanCountOnNextRead = False
                        Else
                            ' The count attribute is not on the same line as the <spectrumList element
                            ' Set blnLookForScanCountOnNextRead to true if strScanCountSearchText does not contain the end element symbol, i.e. >
                            If strScanCountSearchText.IndexOf(">"c) >= 0 Then
                                blnLookForScanCountOnNextRead = False
                            End If
                        End If
                    End If

                    If eElementMatchMode = emmElementMatchModeConstants.EndElement AndAlso Not blnAcqNumberFound Then
                        strAcqNumberSearchText &= ControlChars.NewLine & strInFileCurrentLineSubstring

                        ' Look for the acquisition number
                        ' Because strAcqNumberSearchText contains all of the text from <spectrum on (i.e. not just the text for the current line)
                        '  the test by mAcquisitionNumberRegEx should match the acqNumber attribute even if it is not
                        '  on the same line as <acquisition or if it is not the first attribute following <acquisition
                        objMatch = mAcquisitionNumberRegEx.Match(strAcqNumberSearchText)
                        If objMatch.Success Then
                            If objMatch.Groups.Count > 1 Then
                                Try
                                    blnAcqNumberFound = True
                                    mCurrentSpectrumInfo.ScanNumber = CInt(objMatch.Groups(1).Captures(0).Value)
                                Catch ex As Exception
                                End Try
                            End If
                        End If
                    End If

                    ' Look for the appropriate search text in mInFileCurrentLineText, starting at mInFileCurrentCharIndex + 1
                    Select Case eElementMatchMode
                        Case emmElementMatchModeConstants.StartElement
                            objMatch = mSpectrumStartElementRegEx.Match(strInFileCurrentLineSubstring)
                        Case emmElementMatchModeConstants.EndElement
                            objMatch = mSpectrumEndElementRegEx.Match(strInFileCurrentLineSubstring)
                        Case Else
                            ' Unknown mode
                            LogErrors("AdvanceFileReaders",
                                      "Unknown mode for eElementMatchMode: " & eElementMatchMode.ToString)
                            Return False
                    End Select

                    If objMatch.Success Then
                        ' Match Found
                        blnMatchFound = True
                        intCharIndex = objMatch.Index + 1 + mInFileCurrentCharIndex

                        If eElementMatchMode = emmElementMatchModeConstants.StartElement Then
                            ' Look for the id value after <spectrum
                            objMatch = mSpectrumIDRegEx.Match(strInFileCurrentLineSubstring)
                            If objMatch.Success Then
                                If objMatch.Groups.Count > 1 Then
                                    Try
                                        mCurrentSpectrumInfo.SpectrumID = CInt(objMatch.Groups(1).Captures(0).Value)
                                    Catch ex As Exception
                                    End Try
                                End If
                            Else
                                ' Could not find the id attribute
                                ' If strInFileCurrentLineSubstring does not contain SPECTRUM_END_ELEMENT, then
                                '  set blnAppendingText to True and continue reading
                                If strInFileCurrentLineSubstring.IndexOf(SPECTRUM_END_ELEMENT, StringComparison.Ordinal) < 0 Then
                                    blnMatchFound = False
                                    If Not blnAppendingText Then
                                        blnAppendingText = True
                                        ' Record the byte offset of the start of the current line
                                        ' We will use this offset to "rewind" the file pointer once the id attribute is found
                                        lngByteOffsetForRewind = mBinaryTextReader.CurrentLineByteOffsetStart
                                    End If
                                End If
                            End If

                        ElseIf eElementMatchMode = emmElementMatchModeConstants.EndElement Then
                            ' Move to the end of the element
                            intCharIndex += objMatch.Value.Length - 1
                            If intCharIndex >= mInFileCurrentLineText.Length Then
                                ' This shouldn't happen
                                LogErrors("AdvanceFileReaders",
                                          "Unexpected condition: intCharIndex >= mInFileCurrentLineText.Length")
                                intCharIndex = mInFileCurrentLineText.Length - 1
                            End If
                        End If

                        mInFileCurrentCharIndex = intCharIndex
                        If blnMatchFound Then
                            If blnAppendingText Then
                                mBinaryTextReader.MoveToByteOffset(lngByteOffsetForRewind)
                                mBinaryTextReader.ReadLine()
                                mInFileCurrentLineText = mBinaryTextReader.CurrentLine
                            End If

                            Exit Do
                        End If
                    End If

                End If

                ' Read the next line from the BinaryTextReader
                If Not mBinaryTextReader.ReadLine Then
                    Exit Do
                End If

                mInFileCurrentLineText = mBinaryTextReader.CurrentLine
                mInFileCurrentCharIndex = -1
            Loop
        Catch ex As Exception
            LogErrors("AdvanceFileReaders", ex.Message)
            blnMatchFound = False
        End Try

        Return blnMatchFound
    End Function

    <Obsolete("No longer used")>
    Public Overrides Function GetSourceXMLFooter() As String
        Return SPECTRUM_LIST_END_ELEMENT & ControlChars.NewLine & MZDATA_END_ELEMENT & ControlChars.NewLine
    End Function

    <Obsolete("No longer used")>
    Public Overrides Function GetSourceXMLHeader(intScanCountTotal As Integer, sngStartTimeMinutesAllScans As Single,
                                                 sngEndTimeMinutesAllScans As Single) As String
        Dim strHeaderText As String
        Dim intAsciiValue As Integer

        If mXmlFileHeader Is Nothing Then mXmlFileHeader = String.Empty
        strHeaderText = String.Copy(mXmlFileHeader)

        If strHeaderText.Length = 0 Then
            strHeaderText = "<?xml version=""1.0"" encoding=""UTF-8""?>" & ControlChars.NewLine &
                            MZDATA_START_ELEMENT & " version=""1.05"" accessionNumber=""psi-ms:100""" &
                            " xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">" & ControlChars.NewLine
        End If

        intAsciiValue = Convert.ToInt32(strHeaderText.Chars(strHeaderText.Length - 1))
        If Not (intAsciiValue = 10 OrElse intAsciiValue = 13 OrElse intAsciiValue = 9 OrElse intAsciiValue = 32) Then
            strHeaderText &= ControlChars.NewLine
        End If

        Return strHeaderText & " " & SPECTRUM_LIST_START_ELEMENT & " count=""" & intScanCountTotal & """>"
    End Function

    Protected Overrides Function GetSpectrumByIndexWork(
                                                        intSpectrumIndex As Integer,
                                                        <Out()> ByRef objCurrentSpectrumInfo As clsSpectrumInfo,
                                                        blnHeaderInfoOnly As Boolean) As Boolean

        Dim blnSuccess As Boolean
        objCurrentSpectrumInfo = Nothing

        Try
            blnSuccess = False
            If GetSpectrumReadyStatus(True) Then

                If mXmlFileReader Is Nothing Then
                    mXmlFileReader = New clsMzDataFileReader With {
                        .ParseFilesWithUnknownVersion = mParseFilesWithUnknownVersion
                    }
                End If

                If mIndexedSpectrumInfoCount = 0 Then
                    mErrorMessage = "Indexed data not in memory"
                ElseIf intSpectrumIndex >= 0 And intSpectrumIndex < mIndexedSpectrumInfoCount Then
                    ' Move the binary file reader to .ByteOffsetStart and instantiate an XMLReader at that position
                    mBinaryReader.Position = mIndexedSpectrumInfo(intSpectrumIndex).ByteOffsetStart

                    MyBase.UpdateProgress((mBinaryReader.Position / mBinaryReader.Length * 100.0))

                    ' Create a new XmlTextReader
                    Using reader = XmlReader.Create(mBinaryReader, mXMLReaderSettings)
                        reader.MoveToContent()

                        mXmlFileReader.SetXMLReaderForSpectrum(reader.ReadSubtree())

                        blnSuccess = mXmlFileReader.ReadNextSpectrum(objCurrentSpectrumInfo)

                    End Using

                    If Not String.IsNullOrWhiteSpace(mXmlFileReader.FileVersion) Then
                        mFileVersion = mXmlFileReader.FileVersion
                    End If

                Else
                    mErrorMessage = "Invalid spectrum index: " & intSpectrumIndex.ToString
                End If
            End If
        Catch ex As Exception
            LogErrors("GetSpectrumByIndexWork", ex.Message)
        End Try

        Return blnSuccess
    End Function

    Public Function GetSpectrumBySpectrumID(intSpectrumID As Integer, <Out()> ByRef objSpectrumInfo As clsSpectrumInfo) As Boolean
        Return GetSpectrumBySpectrumIDWork(intSpectrumID, objSpectrumInfo, False)
    End Function

    Private Function GetSpectrumBySpectrumIDWork(
      intSpectrumID As Integer,
      <Out()> ByRef objSpectrumInfo As clsSpectrumInfo,
      blnHeaderInfoOnly As Boolean) As Boolean

        ' Returns True if success, False if failure
        ' Only valid if we have Indexed data in memory

        Dim intSpectrumIndex As Integer

        Dim blnSuccess As Boolean
        objSpectrumInfo = Nothing

        Try
            blnSuccess = False
            mErrorMessage = String.Empty
            If mDataReaderMode = drmDataReaderModeConstants.Cached Then
                mErrorMessage =
                    "Cannot obtain spectrum by spectrum ID when data is cached in memory; only valid when the data is indexed"
            ElseIf mDataReaderMode = drmDataReaderModeConstants.Indexed Then
                If GetSpectrumReadyStatus(True) Then
                    If mIndexedSpectraSpectrumIDToIndex Is Nothing OrElse mIndexedSpectraSpectrumIDToIndex.Count = 0 Then
                        For intSpectrumIndex = 0 To mIndexedSpectrumInfoCount - 1
                            If mIndexedSpectrumInfo(intSpectrumIndex).SpectrumID = intSpectrumID Then
                                blnSuccess = GetSpectrumByIndexWork(intSpectrumIndex, objSpectrumInfo, blnHeaderInfoOnly)
                                Exit For
                            End If
                        Next intSpectrumIndex
                    Else
                        ' Look for intSpectrumID in mIndexedSpectraSpectrumIDToIndex
                        Dim objIndex = mIndexedSpectraSpectrumIDToIndex(intSpectrumID)
                        If objIndex IsNot Nothing Then
                            intSpectrumIndex = CType(objIndex, Integer)
                            blnSuccess = GetSpectrumByIndexWork(intSpectrumIndex, objSpectrumInfo, blnHeaderInfoOnly)
                        End If
                    End If

                    If Not blnSuccess AndAlso mErrorMessage.Length = 0 Then
                        mErrorMessage = "Invalid spectrum ID: " & intSpectrumID.ToString
                    End If
                End If
            Else
                mErrorMessage = "Cached or indexed data not in memory"
            End If
        Catch ex As Exception
            LogErrors("GetSpectrumBySpectrumID", ex.Message)
        End Try

        Return blnSuccess
    End Function

    Public Function GetSpectrumHeaderInfoBySpectrumID(
      intSpectrumID As Integer,
      <Out()> ByRef objSpectrumInfo As clsSpectrumInfo) As Boolean
        Return GetSpectrumBySpectrumIDWork(intSpectrumID, objSpectrumInfo, True)
    End Function

    Public Function GetSpectrumIDList(<Out()> ByRef SpectrumIDList() As Integer) As Boolean
        ' Return the list of indexed spectrumID values

        Dim intSpectrumIndex As Integer
        Dim blnSuccess As Boolean

        Try
            blnSuccess = False
            If mDataReaderMode = drmDataReaderModeConstants.Cached Then
                ' Cannot get the spectrum ID list when mDataReaderMode = Cached
                ReDim SpectrumIDList(-1)
            Else
                If GetSpectrumReadyStatus(True) Then
                    If mIndexedSpectrumInfo Is Nothing OrElse mIndexedSpectrumInfoCount = 0 Then
                        ReDim SpectrumIDList(-1)
                    Else
                        ReDim SpectrumIDList(mIndexedSpectrumInfoCount - 1)
                        For intSpectrumIndex = 0 To SpectrumIDList.Length - 1
                            SpectrumIDList(intSpectrumIndex) = mIndexedSpectrumInfo(intSpectrumIndex).SpectrumID
                        Next intSpectrumIndex
                        blnSuccess = True
                    End If
                Else
                    ReDim SpectrumIDList(-1)
                End If
            End If
        Catch ex As Exception
            LogErrors("GetSpectrumIDList", ex.Message)
            ReDim SpectrumIDList(-1)
        End Try

        Return blnSuccess
    End Function

    Protected Overrides Sub InitializeLocalVariables()
        MyBase.InitializeLocalVariables()

        mInputFileStatsSpectrumIDMinimum = 0
        mInputFileStatsSpectrumIDMaximum = 0

        mXmlFileHeader = String.Empty
        mAddNewLinesToHeader = True

        If mIndexedSpectraSpectrumIDToIndex Is Nothing Then
            mIndexedSpectraSpectrumIDToIndex = New Hashtable
        Else
            mIndexedSpectraSpectrumIDToIndex.Clear()
        End If
    End Sub

    Private Sub InitializeObjectVariables()
        ' Note: This form of the RegEx allows the <spectrum element to be followed by a space or present at the end of the line
        mSpectrumStartElementRegEx = InitializeRegEx(SPECTRUM_START_ELEMENT & "\s+|" & SPECTRUM_START_ELEMENT & "$")

        mSpectrumEndElementRegEx = InitializeRegEx(SPECTRUM_END_ELEMENT)

        ' Note: This form of the RegEx allows for the count attribute to occur on a separate line from <spectrumList
        '       It also allows for other attributes to be present between <spectrumList and the count attribute
        mSpectrumListRegEx = InitializeRegEx(SPECTRUM_LIST_START_ELEMENT & "[^/]+count\s*=\s*""([0-9]+)""")

        ' Note: This form of the RegEx allows for the id attribute to occur on a separate line from <spectrum
        '       It also allows for other attributes to be present between <spectrum and the id attribute
        mSpectrumIDRegEx = InitializeRegEx(SPECTRUM_START_ELEMENT & "[^/]+id\s*=\s*""([0-9]+)""")

        ' Note: This form of the RegEx allows for the acqNumber attribute to occur on a separate line from <acquisition
        '       It also allows for other attributes to be present between <acquisition and the acqNumber attribute
        mAcquisitionNumberRegEx = InitializeRegEx("<acquisition[^/]+acqNumber\s*=\s*""([0-9]+)""")

        mXMLReaderSettings = New XmlReaderSettings With {
            .IgnoreWhitespace = True
        }
    End Sub

    Protected Overrides Function LoadExistingIndex() As Boolean
        ' Returns True if an existing index is found, False if not
        ' mzData files do not have existing indices so always return False
        Return False
    End Function

    Protected Overrides Sub LogErrors(strCallingFunction As String, strErrorDescription As String)
        MyBase.LogErrors("clsMzDataFileAccessor." & strCallingFunction, strErrorDescription)
    End Sub

    Public Overrides Function ReadAndCacheEntireFile() As Boolean
        ' Indexes the location of each of the spectra in the input file

        Dim blnSuccess As Boolean

        Try
            If mBinaryTextReader Is Nothing Then
                blnSuccess = False
            Else
                mReadingAndStoringSpectra = True
                mErrorMessage = String.Empty

                MyBase.ResetProgress("Indexing " & Path.GetFileName(mInputFilePath))

                ' Read and parse the input file to determine:
                '  a) The header XML (text before <spectrumList)
                '  b) The start and end byte offset of each spectrum
                '     (text between "<spectrum" and "</spectrum>")

                blnSuccess = ReadMZDataFile()

                mBinaryTextReader.Close()
                mBinaryTextReader = Nothing

                If blnSuccess Then
                    ' Note: Even if we aborted reading the data mid-file, the cached information is still valid
                    If mAbortProcessing Then
                        mErrorMessage = "Aborted processing"
                    Else
                        UpdateProgress(100)
                        OperationComplete()
                    End If
                End If

            End If
        Catch ex As Exception
            LogErrors("ReadAndCacheEntireFile", ex.Message)
            blnSuccess = False
        Finally
            mReadingAndStoringSpectra = False
        End Try

        Return blnSuccess
    End Function

    Private Function ReadMZDataFile() As Boolean
        ' This function uses the Binary Text Reader to determine
        '  the location of the "<spectrum" and "</spectrum>" elements in the .Xml file
        ' If mIndexingComplete is already True, then simply returns True

        Dim lngCurrentSpectrumByteOffsetStart As Long
        Dim lngCurrentSpectrumByteOffsetEnd As Long

        Dim blnSuccess As Boolean
        Dim blnSpectrumFound As Boolean

        Try
            If mIndexingComplete Then
                Return True
            End If

            Do
                If mCurrentSpectrumInfo Is Nothing Then
                    mCurrentSpectrumInfo = New clsSpectrumInfoMzData()
                Else
                    mCurrentSpectrumInfo.Clear()
                End If

                blnSpectrumFound = AdvanceFileReaders(emmElementMatchModeConstants.StartElement)
                If blnSpectrumFound Then
                    If mInFileCurrentCharIndex < 0 Then
                        ' This shouldn't normally happen
                        lngCurrentSpectrumByteOffsetStart = mBinaryTextReader.CurrentLineByteOffsetStart
                        LogErrors("ReadMZDataFile", "Unexpected condition: mInFileCurrentCharIndex < 0")
                    Else
                        lngCurrentSpectrumByteOffsetStart = mBinaryTextReader.CurrentLineByteOffsetStart +
                                                            mInFileCurrentCharIndex * mCharSize
                    End If

                    blnSpectrumFound = AdvanceFileReaders(emmElementMatchModeConstants.EndElement)
                    If blnSpectrumFound Then
                        If mCharSize > 1 Then
                            lngCurrentSpectrumByteOffsetEnd = mBinaryTextReader.CurrentLineByteOffsetStart +
                                                              mInFileCurrentCharIndex * mCharSize + (mCharSize - 1)
                        Else
                            lngCurrentSpectrumByteOffsetEnd = mBinaryTextReader.CurrentLineByteOffsetStart +
                                                              mInFileCurrentCharIndex
                        End If
                    End If
                End If

                If blnSpectrumFound Then
                    ' Make sure mAddNewLinesToHeader is now false
                    If mAddNewLinesToHeader Then
                        LogErrors("ReadMZDataFile",
                                  "Unexpected condition: mAddNewLinesToHeader was True; changing to False")
                        mAddNewLinesToHeader = False
                    End If

                    StoreIndexEntry(mCurrentSpectrumInfo.ScanNumber, lngCurrentSpectrumByteOffsetStart,
                                    lngCurrentSpectrumByteOffsetEnd)

                    ' Note that StoreIndexEntry will have incremented mIndexedSpectrumInfoCount
                    With mIndexedSpectrumInfo(mIndexedSpectrumInfoCount - 1)
                        .SpectrumID = mCurrentSpectrumInfo.SpectrumID

                        UpdateFileStats(mIndexedSpectrumInfoCount, .ScanNumber, .SpectrumID)

                        If Not mIndexedSpectraSpectrumIDToIndex.Contains(.SpectrumID) Then
                            mIndexedSpectraSpectrumIDToIndex.Add(.SpectrumID, mIndexedSpectrumInfoCount - 1)
                        End If
                    End With

                    ' Update the progress
                    If mBinaryTextReader.FileLengthBytes > 0 Then
                        UpdateProgress(
                            mBinaryTextReader.CurrentLineByteOffsetEnd / CDbl(mBinaryTextReader.FileLengthBytes) * 100)
                    End If

                    If mAbortProcessing Then
                        Exit Do
                    End If

                End If
            Loop While blnSpectrumFound

            blnSuccess = True

        Catch ex As Exception
            LogErrors("ReadMZDataFile", ex.Message)
            blnSuccess = False
        End Try

        Return blnSuccess
    End Function

    Private Overloads Sub UpdateFileStats(intScanCount As Integer, intScanNumber As Integer, intSpectrumID As Integer)
        MyBase.UpdateFileStats(intScanCount, intScanNumber)

        If intScanCount <= 1 Then
            mInputFileStatsSpectrumIDMinimum = intSpectrumID
            mInputFileStatsSpectrumIDMaximum = intSpectrumID
        Else
            If intSpectrumID < mInputFileStatsSpectrumIDMinimum Then
                mInputFileStatsSpectrumIDMinimum = intSpectrumID
            End If
            If intSpectrumID > mInputFileStatsSpectrumIDMaximum Then
                mInputFileStatsSpectrumIDMaximum = intSpectrumID
            End If
        End If
    End Sub
End Class
