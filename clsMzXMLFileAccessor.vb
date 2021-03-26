Option Strict On

Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Xml

' This class can be used to open a .mzXML file and index the location
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

Public Class clsMzXMLFileAccessor
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

    Private Const MSRUN_START_ELEMENT As String = "<msRun"
    Private Const MSRUN_END_ELEMENT As String = "</msRun>"

    Private Const SCAN_START_ELEMENT As String = "<scan"
    Public Const SCAN_END_ELEMENT As String = "</scan>"
    Private Const PEAKS_END_ELEMENT As String = "</peaks>"

    Private Const MZXML_START_ELEMENT As String = "<mzXML"
    Private Const MZXML_END_ELEMENT As String = "</mzXML>"

    Private Const INDEX_OFFSET_ELEMENT_NAME As String = "indexOffset"
    Private Const INDEX_OFFSET_START_ELEMENT As String = "<" & INDEX_OFFSET_ELEMENT_NAME
    Private Const INDEX_OFFSET_END_ELEMENT As String = "</indexOffset>"

    Private Const INDEX_ELEMENT_NAME As String = "index"
    Private Const INDEX_START_ELEMENT As String = "<" & INDEX_ELEMENT_NAME
    Private Const INDEX_END_ELEMENT As String = "</index>"
    Private Const INDEX_ATTRIBUTE_NAME As String = "name"

    Private Const OFFSET_ELEMENT_NAME As String = "offset"
    Private Const OFFSET_ATTRIBUTE_ID As String = "id"

#End Region

#Region "Classwide Variables"

    Private mXmlFileReader As clsMzXMLFileReader

    Private mCurrentSpectrumInfo As clsSpectrumInfoMzXML

    Private mXmlFileHeader As String
    Private mAddNewLinesToHeader As Boolean

    Private mMSRunFound As Boolean
    Private mIgnoreEmbeddedIndex As Boolean

    Private mMSRunRegEx As Regex
    Private mScanStartElementRegEx As Regex
    Private mPeaksEndElementRegEx As Regex

    Private mScanNumberRegEx As Regex

    Private mXMLReaderSettings As XmlReaderSettings

#End Region

#Region "Processing Options and Interface Functions"

    Public Property IgnoreEmbeddedIndex() As Boolean
        Get
            Return mIgnoreEmbeddedIndex
        End Get
        Set(Value As Boolean)
            mIgnoreEmbeddedIndex = Value
        End Set
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
        ' Uses the BinaryTextReader to look for the given element type (as specified by eElementMatchMode)

        Dim blnMatchFound As Boolean
        Dim blnAppendingText As Boolean
        Dim lngByteOffsetForRewind As Long

        Dim intCharIndex As Integer

        Dim strInFileCurrentLineSubstring As String

        Dim objMatch As Match

        Try
            If mInFileCurrentLineText Is Nothing Then
                mInFileCurrentLineText = String.Empty
            End If

            strInFileCurrentLineSubstring = String.Empty
            blnAppendingText = False

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
                        ' We haven't yet found the first scan; look for "<scan"
                        intCharIndex = mInFileCurrentLineText.IndexOf(SCAN_START_ELEMENT, mInFileCurrentCharIndex + 1,
                                                                      StringComparison.Ordinal)

                        If intCharIndex >= 0 Then
                            ' Only add a portion of mInFileCurrentLineText to mXmlFileHeader
                            '  since it contains SCAN_START_ELEMENT

                            If intCharIndex > 0 Then
                                mXmlFileHeader &= mInFileCurrentLineText.Substring(0, intCharIndex)
                            End If
                            mAddNewLinesToHeader = False
                            mMSRunFound = True

                            UpdateXmlFileHeaderScanCount(mXmlFileHeader)
                        Else
                            ' Append mInFileCurrentLineText to mXmlFileHeader
                            mXmlFileHeader &= mInFileCurrentLineText & ControlChars.NewLine
                        End If
                    End If

                    If Not mMSRunFound Then
                        ' We haven't yet found msRun; look for "<msRun" and the Scan Count value
                        objMatch = mMSRunRegEx.Match(mXmlFileHeader)

                        If objMatch.Success Then
                            ' Record the Scan Count value
                            If objMatch.Groups.Count > 1 Then
                                Try
                                    mInputFileStats.ScanCount = CInt(objMatch.Groups(1).Captures(0).Value)
                                Catch ex As Exception
                                End Try
                            End If
                            mMSRunFound = True
                        End If
                    End If

                    ' Look for the appropriate search text in mInFileCurrentLineText, starting at mInFileCurrentCharIndex + 1
                    Select Case eElementMatchMode
                        Case emmElementMatchModeConstants.StartElement
                            objMatch = mScanStartElementRegEx.Match(strInFileCurrentLineSubstring)
                        Case emmElementMatchModeConstants.EndElement
                            ' Since mzXml files can have scans embedded within another scan, we'll look for </peaks>
                            ' rather than looking for </scan>
                            objMatch = mPeaksEndElementRegEx.Match(strInFileCurrentLineSubstring)
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
                            ' Look for the scan number after <scan
                            objMatch = mScanNumberRegEx.Match(strInFileCurrentLineSubstring)
                            If objMatch.Success Then
                                If objMatch.Groups.Count > 1 Then
                                    Try
                                        mCurrentSpectrumInfo.ScanNumber = CInt(objMatch.Groups(1).Captures(0).Value)
                                    Catch ex As Exception
                                    End Try
                                End If
                            Else
                                ' Could not find the num attribute
                                ' If strInFileCurrentLineSubstring does not contain PEAKS_END_ELEMENT, then
                                '  set blnAppendingText to True and continue reading
                                If strInFileCurrentLineSubstring.IndexOf(PEAKS_END_ELEMENT, StringComparison.Ordinal) < 0 Then
                                    blnMatchFound = False
                                    If Not blnAppendingText Then
                                        blnAppendingText = True
                                        ' Record the byte offset of the start of the current line
                                        ' We will use this offset to "rewind" the file pointer once the num attribute is found
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

    Private Function ExtractIndexOffsetFromTextStream(strTextStream As String) As Long
        ' Looks for the number between "<indexOffset" and "</indexOffset" in strTextStream
        ' Returns the number if found, or 0 if an error

        Dim intMatchIndex As Integer
        Dim intIndex As Integer
        Dim strNumber As String

        Dim lngIndexOffset As Long

        Try
            ' Look for <indexOffset in strTextStream
            intMatchIndex = strTextStream.IndexOf(INDEX_OFFSET_START_ELEMENT, StringComparison.Ordinal)
            If intMatchIndex >= 0 Then
                ' Look for the next >
                intMatchIndex = strTextStream.IndexOf(">"c, intMatchIndex + 1)
                If intMatchIndex >= 0 Then
                    ' Remove the leading text
                    strTextStream = strTextStream.Substring(intMatchIndex + 1)

                    ' Look for the next <
                    intMatchIndex = strTextStream.IndexOf("<"c)

                    If intMatchIndex >= 0 Then
                        strTextStream = strTextStream.Substring(0, intMatchIndex)

                        ' Try to convert strTextStream to a number
                        Try
                            lngIndexOffset = Integer.Parse(strTextStream)
                        Catch ex As Exception
                            lngIndexOffset = 0
                        End Try

                        If lngIndexOffset = 0 Then
                            ' Number conversion failed; probably have carriage returns in the text
                            ' Look for the next number in strTextStream
                            For intIndex = 0 To strTextStream.Length - 1
                                If IsNumber(strTextStream.Chars(intIndex)) Then
                                    ' First number found
                                    strNumber = strTextStream.Chars(intIndex)

                                    ' Append any additional numbers to strNumber
                                    Do While intIndex + 1 < strTextStream.Length
                                        intIndex += 1
                                        If IsNumber(strTextStream.Chars(intIndex)) Then
                                            strNumber &= strTextStream.Chars(intIndex)
                                        Else
                                            Exit Do
                                        End If
                                    Loop

                                    Try
                                        lngIndexOffset = Integer.Parse(strNumber)
                                    Catch ex As Exception
                                        lngIndexOffset = 0
                                    End Try
                                    Exit For
                                End If
                            Next intIndex
                        End If
                    End If
                End If
            End If
        Catch ex As Exception
            LogErrors("ExtractIndexOffsetFromTextStream", ex.Message)
            lngIndexOffset = 0
        End Try

        Return lngIndexOffset
    End Function

    <Obsolete("No longer used")>
    Protected Overrides Function ExtractTextBetweenOffsets(strFilePath As String, lngStartByteOffset As Long,
                                                           lngEndByteOffset As Long) As String
        Dim strExtractedText As String

        Dim intMatchIndex As Integer
        Dim blnAddScanEndElement As Boolean
        Dim intAsciiValue As Integer

        strExtractedText = MyBase.ExtractTextBetweenOffsets(strFilePath, lngStartByteOffset, lngEndByteOffset)

        If Not String.IsNullOrWhiteSpace(strExtractedText) Then

            blnAddScanEndElement = False

            ' Check for the occurrence of two or more </scan> elements after </peaks>
            ' This will be the case if the byte offset values were read from the end of the mzXML file
            intMatchIndex = strExtractedText.IndexOf(PEAKS_END_ELEMENT, StringComparison.Ordinal)
            If intMatchIndex >= 0 Then
                intMatchIndex = strExtractedText.IndexOf(SCAN_END_ELEMENT, intMatchIndex, StringComparison.Ordinal)
                If intMatchIndex >= 0 Then
                    ' Replace all but the first occurrence of </scan> with ""
                    strExtractedText = strExtractedText.Substring(0, intMatchIndex + 1) &
                                       strExtractedText.Substring(intMatchIndex + 1).Replace(SCAN_END_ELEMENT,
                                                                                             String.Empty)
                Else
                    blnAddScanEndElement = True
                End If
            Else
                blnAddScanEndElement = True
            End If

            If blnAddScanEndElement Then
                intAsciiValue = Convert.ToInt32(strExtractedText.Chars(strExtractedText.Length - 1))
                If Not (intAsciiValue = 10 OrElse intAsciiValue = 13 OrElse intAsciiValue = 9 OrElse intAsciiValue = 32) Then
                    strExtractedText &= ControlChars.NewLine & SCAN_END_ELEMENT
                Else
                    strExtractedText &= SCAN_END_ELEMENT
                End If

            End If
            strExtractedText &= ControlChars.NewLine

        End If

        Return strExtractedText
    End Function

    <Obsolete("No longer used")>
    Public Overrides Function GetSourceXMLFooter() As String
        Return MSRUN_END_ELEMENT & ControlChars.NewLine & MZXML_END_ELEMENT & ControlChars.NewLine
    End Function

    <Obsolete("No longer used")>
    Public Overrides Function GetSourceXMLHeader(intScanCountTotal As Integer, sngStartTimeMinutesAllScans As Single,
                                                 sngEndTimeMinutesAllScans As Single) As String
        Dim strHeaderText As String
        Dim intAsciiValue As Integer

        Dim reStartTime As Regex
        Dim reEndTime As Regex
        Dim objMatch As Match

        Dim strStartTimeSOAP As String
        Dim strEndTimeSOAP As String

        Try
            If mXmlFileHeader Is Nothing Then mXmlFileHeader = String.Empty
            strHeaderText = String.Copy(mXmlFileHeader)

            strStartTimeSOAP = clsMSXMLFileReaderBaseClass.ConvertTimeFromTimespanToXmlDuration(
                New TimeSpan(CLng(sngStartTimeMinutesAllScans * TimeSpan.TicksPerMinute)), True)

            strEndTimeSOAP = clsMSXMLFileReaderBaseClass.ConvertTimeFromTimespanToXmlDuration(
                New TimeSpan(CLng(sngEndTimeMinutesAllScans * TimeSpan.TicksPerMinute)), True)

            If strHeaderText.Length = 0 Then
                strHeaderText = "<?xml version=""1.0"" encoding=""ISO-8859-1""?>" & ControlChars.NewLine &
                                MZXML_START_ELEMENT &
                                " xmlns=""http://sashimi.sourceforge.net/schema_revision/mzXML_2.0""" &
                                " xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""" &
                                " xsi:schemaLocation=""http://sashimi.sourceforge.net/schema_revision/mzXML_2.0" &
                                " http://sashimi.sourceforge.net/schema_revision/mzXML_2.0/mzXML_idx_2.0.xsd"">" &
                                ControlChars.NewLine &
                                MSRUN_START_ELEMENT & " scanCount=""" & intScanCountTotal.ToString & """" &
                                " startTime = """ & strStartTimeSOAP & """" &
                                " endTime = """ & strEndTimeSOAP & """>" & ControlChars.NewLine
            Else
                ' Replace ScanCount, StartTime, and EndTime values with intScanCountTotal, sngStartTimeMinutesAllScans, and sngEndTimeMinutesAllScans

                UpdateXmlFileHeaderScanCount(strHeaderText, intScanCountTotal)

                reStartTime = InitializeRegEx("startTime\s*=\s*""[A-Z0-9.]+""")
                reEndTime = InitializeRegEx("endTime\s*=\s*""[A-Z0-9.]+""")

                objMatch = reStartTime.Match(strHeaderText)
                If objMatch.Success Then
                    ' Replace the start time with strStartTimeSOAP
                    strHeaderText = strHeaderText.Substring(0, objMatch.Index) &
                                    "startTime=""" & strStartTimeSOAP & """" &
                                    strHeaderText.Substring(objMatch.Index + objMatch.Value.Length)
                End If


                objMatch = reEndTime.Match(strHeaderText)
                If objMatch.Success Then
                    ' Replace the start time with strEndTimeSOAP
                    strHeaderText = strHeaderText.Substring(0, objMatch.Index) &
                                    "endTime=""" & strEndTimeSOAP & """" &
                                    strHeaderText.Substring(objMatch.Index + objMatch.Value.Length)
                End If

            End If

            intAsciiValue = Convert.ToInt32(strHeaderText.Chars(strHeaderText.Length - 1))
            If Not (intAsciiValue = 10 OrElse intAsciiValue = 13 OrElse intAsciiValue = 9 OrElse intAsciiValue = 32) Then
                strHeaderText &= ControlChars.NewLine
            End If

        Catch ex As Exception
            mErrorMessage = "Error opening obtaining source XML header: " & Err.Description
            strHeaderText = String.Empty
        End Try


        Return strHeaderText
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
                    mXmlFileReader = New clsMzXMLFileReader With {
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
                    ElseIf String.IsNullOrWhiteSpace(mFileVersion) AndAlso Not String.IsNullOrWhiteSpace(mXmlFileHeader) Then
                        If Not clsMzXMLFileReader.ExtractMzXmlFileVersion(mXmlFileHeader, mFileVersion) Then
                            LogErrors("ValidateMZXmlFileVersion",
                                      "Unknown mzXML file version; expected text not found in mXmlFileHeader")
                        End If
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

    Protected Overrides Sub InitializeLocalVariables()
        MyBase.InitializeLocalVariables()

        mXmlFileHeader = String.Empty
        mAddNewLinesToHeader = True

        mMSRunFound = False
    End Sub

    Private Sub InitializeObjectVariables()
        ' Note: This form of the RegEx allows the <scan element to be followed by a space or present at the end of the line
        mScanStartElementRegEx = InitializeRegEx(SCAN_START_ELEMENT & "\s+|" & SCAN_START_ELEMENT & "$")

        mPeaksEndElementRegEx = InitializeRegEx(PEAKS_END_ELEMENT)

        ' Note: This form of the RegEx allows for the scanCount attribute to occur on a separate line from <msRun
        '       It also allows for other attributes to be present between <msRun and the scanCount attribute
        mMSRunRegEx = InitializeRegEx(MSRUN_START_ELEMENT & "[^/]+scanCount\s*=\s*""([0-9]+)""")

        ' Note: This form of the RegEx allows for the num attribute to occur on a separate line from <scan
        '       It also allows for other attributes to be present between <scan and the num attribute
        mScanNumberRegEx = InitializeRegEx(SCAN_START_ELEMENT & "[^/]+num\s*=\s*""([0-9]+)""")

        mXMLReaderSettings = New XmlReaderSettings With {
            .IgnoreWhitespace = True
        }
    End Sub

    Protected Overrides Function LoadExistingIndex() As Boolean
        ' Use the mBinaryTextReader to jump to the end of the file and read the data line-by-line backward
        '  looking for the <indexOffset> element or the <index
        ' If found, and if the index elements are successfully loaded, then returns True
        ' Otherwise, returns False

        Dim strCurrentLine As String
        Dim intCharIndex As Integer
        Dim intCharIndexEnd As Integer

        Dim lngByteOffsetSaved As Long
        Dim lngIndexOffset As Long

        Dim blnExtractTextToEOF As Boolean
        Dim blnIndexLoaded As Boolean

        Dim strExtractedText As String
        Dim intStartElementIndex As Integer
        Dim intFirstBracketIndex As Integer

        Dim objStringBuilder As StringBuilder

        Try
            If mIgnoreEmbeddedIndex Then
                Return False
            End If

            blnIndexLoaded = False

            ' Move to the end of the file
            mBinaryTextReader.MoveToEnd()

            Do While mBinaryTextReader.ReadLine(clsBinaryTextReader.ReadDirectionConstants.Reverse)
                strCurrentLine = mBinaryTextReader.CurrentLine

                intCharIndex = strCurrentLine.IndexOf(INDEX_OFFSET_START_ELEMENT, StringComparison.Ordinal)
                If intCharIndex >= 0 Then
                    ' The offset to the index has been specified
                    ' Parse out the number between <indexOffset> and </indexOffset>
                    ' (normally on the same line, though this code can handle white space between the tags)

                    lngByteOffsetSaved = mBinaryTextReader.CurrentLineByteOffsetStart +
                                         intCharIndex * mBinaryTextReader.CharSize

                    intCharIndexEnd = strCurrentLine.IndexOf(INDEX_OFFSET_END_ELEMENT,
                                                             intCharIndex + INDEX_OFFSET_START_ELEMENT.Length,
                                                             StringComparison.Ordinal)
                    If intCharIndexEnd <= 0 Then
                        ' Need to read the next few lines to find </indexOffset>
                        mBinaryTextReader.MoveToByteOffset(mBinaryTextReader.CurrentLineByteOffsetEndWithTerminator + 1)
                        Do While mBinaryTextReader.ReadLine(clsBinaryTextReader.ReadDirectionConstants.Forward)
                            strCurrentLine &= " " & mBinaryTextReader.CurrentLine
                            intCharIndexEnd = strCurrentLine.IndexOf(INDEX_OFFSET_END_ELEMENT,
                                                                     intCharIndex + INDEX_OFFSET_START_ELEMENT.Length,
                                                                     StringComparison.Ordinal)
                            If intCharIndexEnd > 0 Then
                                Exit Do
                            End If
                        Loop
                    End If

                    If intCharIndexEnd > 0 Then
                        lngIndexOffset = ExtractIndexOffsetFromTextStream(strCurrentLine)

                        If lngIndexOffset > 0 Then
                            ' Move the binary reader to lngIndexOffset
                            mBinaryTextReader.MoveToByteOffset(lngIndexOffset)

                            ' Read the text at offset lngIndexOffset
                            mBinaryTextReader.ReadLine(clsBinaryTextReader.ReadDirectionConstants.Forward)
                            strCurrentLine = mBinaryTextReader.CurrentLine

                            ' Verify that strCurrentLine contains "<index"
                            If strCurrentLine.IndexOf(INDEX_START_ELEMENT, StringComparison.Ordinal) >= 0 Then
                                strCurrentLine = MZXML_START_ELEMENT & ">" & ControlChars.NewLine & strCurrentLine
                                blnExtractTextToEOF = True
                            Else
                                ' Corrupt index offset value; move back to byte offset
                            End If
                        End If
                    End If

                    If Not blnExtractTextToEOF Then
                        ' Move the reader back to byte lngByteOffsetSaved
                        mBinaryTextReader.MoveToByteOffset(lngByteOffsetSaved)
                        strCurrentLine = String.Empty
                    End If
                End If

                If Not blnExtractTextToEOF Then
                    intCharIndex = strCurrentLine.IndexOf(MSRUN_END_ELEMENT, StringComparison.Ordinal)
                    If intCharIndex >= 0 Then
                        ' </msRun> element found
                        ' Extract the text from here to the end of the file and parse with ParseMzXMLOffsetIndex
                        blnExtractTextToEOF = True
                        strCurrentLine = MZXML_START_ELEMENT & ">"
                    End If
                End If

                If blnExtractTextToEOF Then
                    objStringBuilder = New StringBuilder With {
                        .Length = 0
                    }

                    If strCurrentLine.Length > 0 Then
                        objStringBuilder.Append(strCurrentLine & mBinaryTextReader.CurrentLineTerminator)
                    End If

                    ' Read all of the lines to the end of the file
                    Do While mBinaryTextReader.ReadLine(clsBinaryTextReader.ReadDirectionConstants.Forward)
                        strCurrentLine = mBinaryTextReader.CurrentLine
                        objStringBuilder.Append(strCurrentLine & mBinaryTextReader.CurrentLineTerminator)
                    Loop

                    blnIndexLoaded = ParseMzXMLOffsetIndex(objStringBuilder.ToString)

                    If blnIndexLoaded Then
                        ' Validate the first entry of the index to make sure the index is valid

                        ' For now, set blnIndexLoaded to False
                        ' If the test read works, then we'll set blnIndexLoaded to True
                        blnIndexLoaded = False

                        If mIndexedSpectrumInfoCount > 0 Then
                            ' Set up the default error message
                            mErrorMessage = "Index embedded in the input file (" & Path.GetFileName(mInputFilePath) &
                                            ") is corrupt: first byte offset (" &
                                            mIndexedSpectrumInfo(0).ByteOffsetStart.ToString & ") does not point to a " &
                                            SCAN_START_ELEMENT & " element"

                            strExtractedText = MyBase.ExtractTextBetweenOffsets(mInputFilePath,
                                                                                mIndexedSpectrumInfo(0).ByteOffsetStart,
                                                                                mIndexedSpectrumInfo(0).ByteOffsetEnd)
                            If strExtractedText IsNot Nothing AndAlso strExtractedText.Length > 0 Then
                                ' Make sure the first text in strExtractedText is <scan
                                intStartElementIndex = strExtractedText.IndexOf(SCAN_START_ELEMENT,
                                                                                StringComparison.Ordinal)

                                If intStartElementIndex >= 0 Then
                                    intFirstBracketIndex = strExtractedText.IndexOf("<"c)
                                    If intFirstBracketIndex = intStartElementIndex Then
                                        blnIndexLoaded = True
                                        mErrorMessage = String.Empty
                                    End If
                                End If
                            End If
                        End If
                    End If

                    If blnIndexLoaded Then
                        ' Move back to the beginning of the file and extract the header tags
                        mBinaryTextReader.MoveToBeginning()

                        mXmlFileHeader = String.Empty
                        Do While mBinaryTextReader.ReadLine(clsBinaryTextReader.ReadDirectionConstants.Forward)
                            strCurrentLine = mBinaryTextReader.CurrentLine

                            intCharIndex = strCurrentLine.IndexOf(SCAN_START_ELEMENT, StringComparison.Ordinal)
                            If intCharIndex >= 0 Then
                                ' SCAN_START_ELEMENT found
                                If intCharIndex > 0 Then
                                    ' Only add a portion of strCurrentLine to mXmlFileHeader
                                    '  since it contains SCAN_START_ELEMENT
                                    mXmlFileHeader &= strCurrentLine.Substring(0, intCharIndex)
                                End If
                                Exit Do
                            Else
                                ' Append strCurrentLine to mXmlFileHeader
                                mXmlFileHeader &= strCurrentLine & ControlChars.NewLine
                            End If
                        Loop

                    Else
                        ' Index not loaded (or not valid)

                        If mErrorMessage IsNot Nothing AndAlso mErrorMessage.Length > 0 Then
                            LogErrors("LoadExistingIndex", mErrorMessage)
                        End If

                        If mIndexedSpectrumInfoCount > 0 Then
                            ' Reset the indexed spectrum info
                            mIndexedSpectrumInfoCount = 0
                            ReDim mIndexedSpectrumInfo(INITIAL_SCAN_RESERVE_COUNT - 1)
                        End If
                    End If

                    Exit Do
                End If

            Loop
        Catch ex As Exception
            LogErrors("LoadExistingIndex", ex.Message)
            blnIndexLoaded = False
        End Try

        Return blnIndexLoaded
    End Function

    Protected Overrides Sub LogErrors(strCallingFunction As String, strErrorDescription As String)
        MyBase.LogErrors("clsMzXMLFileAccessor." & strCallingFunction, strErrorDescription)
    End Sub

    Private Function ParseMzXMLOffsetIndex(strTextStream As String) As Boolean

        Dim blnIndexLoaded = False

        Try

            Dim blnParseIndexValues = True

            Dim intCurrentScanNumber = -1
            Dim intPreviousScanNumber = -1

            Dim lngCurrentScanByteOffsetStart = CLng(-1)
            Dim lngPreviousScanByteOffsetStart As Long

            Dim strCurrentElement = String.Empty

            Using objXMLReader = New XmlTextReader(New StringReader(strTextStream))

                ' Skip all whitespace
                objXMLReader.WhitespaceHandling = WhitespaceHandling.None

                Dim blnReadSuccessful = True
                Do While blnReadSuccessful AndAlso
                         (objXMLReader.ReadState = ReadState.Initial Or
                          objXMLReader.ReadState = ReadState.Interactive)

                    blnReadSuccessful = objXMLReader.Read()

                    If blnReadSuccessful AndAlso objXMLReader.ReadState = ReadState.Interactive Then
                        If objXMLReader.NodeType = XmlNodeType.Element Then
                            strCurrentElement = objXMLReader.Name
                            If strCurrentElement = INDEX_ELEMENT_NAME Then
                                If objXMLReader.HasAttributes Then
                                    ' Validate that this is the "scan" index

                                    Dim strValue As String
                                    Try
                                        strValue = objXMLReader.GetAttribute(INDEX_ATTRIBUTE_NAME)
                                    Catch ex As Exception
                                        strValue = String.Empty
                                    End Try

                                    If strValue IsNot Nothing AndAlso strValue.Length > 0 Then
                                        If strValue = "scan" Then
                                            blnParseIndexValues = True
                                        Else
                                            blnParseIndexValues = False
                                        End If
                                    End If
                                End If
                            ElseIf strCurrentElement = OFFSET_ELEMENT_NAME Then
                                If blnParseIndexValues AndAlso objXMLReader.HasAttributes Then
                                    ' Extract the scan number from the id attribute
                                    Try
                                        intPreviousScanNumber = intCurrentScanNumber
                                        intCurrentScanNumber = CInt(objXMLReader.GetAttribute(OFFSET_ATTRIBUTE_ID))
                                    Catch ex As Exception
                                        ' Index is corrupted (or of an unknown format); do not continue parsing
                                        Exit Do
                                    End Try
                                End If
                            End If

                        ElseIf objXMLReader.NodeType = XmlNodeType.EndElement Then
                            If blnParseIndexValues AndAlso objXMLReader.Name = INDEX_ELEMENT_NAME Then
                                ' Store the final index value
                                ' This is tricky since we don't know the ending offset for the given scan
                                ' Thus, need to use the binary text reader to jump to lngCurrentScanByteOffsetStart and then read line-by-line until the next </peaks> tag is found
                                StoreFinalIndexEntry(intCurrentScanNumber, lngCurrentScanByteOffsetStart)
                                blnIndexLoaded = True
                                Exit Do
                            End If

                            strCurrentElement = String.Empty

                        ElseIf objXMLReader.NodeType = XmlNodeType.Text Then
                            If blnParseIndexValues AndAlso strCurrentElement = OFFSET_ELEMENT_NAME Then
                                If Not objXMLReader.NodeType = XmlNodeType.Whitespace And objXMLReader.HasValue Then
                                    Try
                                        lngPreviousScanByteOffsetStart = lngCurrentScanByteOffsetStart
                                        lngCurrentScanByteOffsetStart = CLng(objXMLReader.Value)

                                        If lngPreviousScanByteOffsetStart >= 0 AndAlso intCurrentScanNumber >= 0 Then
                                            ' Store the previous scan info
                                            StoreIndexEntry(intPreviousScanNumber, lngPreviousScanByteOffsetStart,
                                                            lngCurrentScanByteOffsetStart - 1)
                                        End If
                                    Catch ex As Exception
                                        ' Index is corrupted (or of an unknown format); do not continue parsing
                                        Exit Do
                                    End Try
                                End If
                            End If

                        End If
                    End If
                Loop

            End Using

        Catch ex As Exception
            LogErrors("ParseMzXMLOffsetIndex", ex.Message)
            blnIndexLoaded = False
        End Try

        Return blnIndexLoaded
    End Function

    Public Overrides Function ReadAndCacheEntireFile() As Boolean
        ' Indexes the location of each of the spectra in the input file

        Dim blnSuccess As Boolean

        Try
            If mBinaryTextReader Is Nothing Then
                blnSuccess = mIndexingComplete
            Else
                mReadingAndStoringSpectra = True
                mErrorMessage = String.Empty

                MyBase.ResetProgress("Indexing " & Path.GetFileName(mInputFilePath))

                ' Read and parse the input file to determine:
                '  a) The header XML (text before the first occurrence of <scan)
                '  b) The start and end byte offset of each spectrum
                '     (text between "<scan" and "</peaks>")

                blnSuccess = ReadMZXmlFile()

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

                    mIndexingComplete = True
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

    Private Function ReadMZXmlFile() As Boolean
        ' This function uses the Binary Text Reader to determine
        '  the location of the "<scan" and "</peaks>" elements in the .Xml file
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
                    mCurrentSpectrumInfo = New clsSpectrumInfoMzXML()
                Else
                    mCurrentSpectrumInfo.Clear()
                End If

                blnSpectrumFound = AdvanceFileReaders(emmElementMatchModeConstants.StartElement)
                If blnSpectrumFound Then
                    If mInFileCurrentCharIndex < 0 Then
                        ' This shouldn't normally happen
                        lngCurrentSpectrumByteOffsetStart = mBinaryTextReader.CurrentLineByteOffsetStart
                        LogErrors("ReadMZXmlFile", "Unexpected condition: mInFileCurrentCharIndex < 0")
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
                        LogErrors("ReadMZXmlFile",
                                  "Unexpected condition: mAddNewLinesToHeader was True; changing to False")
                        mAddNewLinesToHeader = False
                    End If

                    StoreIndexEntry(mCurrentSpectrumInfo.ScanNumber, lngCurrentSpectrumByteOffsetStart,
                                    lngCurrentSpectrumByteOffsetEnd)

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
            LogErrors("ReadMZXmlFile", ex.Message)
            blnSuccess = False
        End Try

        Return blnSuccess
    End Function

    Private Sub StoreFinalIndexEntry(intScanNumber As Integer, lngByteOffsetStart As Long)
        ' Use the binary text reader to jump to lngByteOffsetStart, then read line-by-line until the next </peaks> tag is found
        ' The end of this tag is equal to lngByteOffsetEnd

        Dim strCurrentLine As String
        Dim intMatchIndex As Integer
        Dim lngByteOffsetEnd As Long

        mBinaryTextReader.MoveToByteOffset(lngByteOffsetStart)

        Do While mBinaryTextReader.ReadLine(clsBinaryTextReader.ReadDirectionConstants.Forward)
            strCurrentLine = mBinaryTextReader.CurrentLine

            intMatchIndex = strCurrentLine.IndexOf(PEAKS_END_ELEMENT, StringComparison.Ordinal)
            If intMatchIndex >= 0 Then
                lngByteOffsetEnd = mBinaryTextReader.CurrentLineByteOffsetStart +
                                   (intMatchIndex + PEAKS_END_ELEMENT.Length) * mBinaryTextReader.CharSize - 1
                StoreIndexEntry(intScanNumber, lngByteOffsetStart, lngByteOffsetEnd)
                Exit Do
            End If
        Loop
    End Sub

    Private Sub UpdateXmlFileHeaderScanCount(ByRef strHeaderText As String)
        UpdateXmlFileHeaderScanCount(strHeaderText, 1)
    End Sub

    Private Sub UpdateXmlFileHeaderScanCount(ByRef strHeaderText As String, intScanCountTotal As Integer)
        ' Examine strHeaderText to look for the number after the scanCount attribute of msRun
        ' Replace the number with intScanCountTotal

        Dim objMatch As Match

        If strHeaderText IsNot Nothing AndAlso strHeaderText.Length > 0 Then
            objMatch = mMSRunRegEx.Match(strHeaderText)
            If objMatch.Success Then
                ' Replace the scan count value with intScanCountTotal
                If objMatch.Groups.Count > 1 Then
                    Try
                        strHeaderText = strHeaderText.Substring(0, objMatch.Groups(1).Index) &
                                        intScanCountTotal.ToString &
                                        strHeaderText.Substring(objMatch.Groups(1).Index + objMatch.Groups(1).Length)

                    Catch ex As Exception
                    End Try
                End If
            End If
        End If
    End Sub
End Class
