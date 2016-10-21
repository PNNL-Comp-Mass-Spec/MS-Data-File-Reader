Option Strict On

Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Text.RegularExpressions
Imports System.Xml

' This is the base class for the mzXML and mzData readers
'
' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2006, Battelle Memorial Institute.  All Rights Reserved.
' Started March 26, 2006
'
' E-mail: matthew.monroe@pnl.gov or matt@alchemistmatt.com
' Website: http://omics.pnl.gov/ or http://www.sysbio.org/resources/staff/
' -------------------------------------------------------------------------------
'

Public MustInherit Class clsMSXMLFileReaderBaseClass
    Inherits clsMSDataFileReaderBaseClass

    Public Sub New()
        Me.InitializeLocalVariables()
    End Sub

    Protected Overrides Sub Finalize()
        MyBase.Finalize()

        Try
            If Not mDataFileOrTextStream Is Nothing Then
                mDataFileOrTextStream.Close()
            End If
        Catch ex As Exception
        End Try

        Try
            If Not mXMLReader Is Nothing Then
                mXMLReader.Close()
            End If
        Catch ex As Exception
        End Try
    End Sub

#Region "Constants and Enums"

#End Region

#Region "Structures"

    Private Structure udtElementInfoType
        Public Name As String
        Public Depth As Integer
    End Structure

#End Region

#Region "Classwide Variables"

    Protected mDataFileOrTextStream As TextReader
    Protected mXMLReader As XmlReader

    Protected mSpectrumFound As Boolean

    ' When this is set to True, then the base-64 encoded data in the file is not parsed;
    ' This speeds up the reader
    Protected mSkipBinaryData As Boolean = False

    Protected mSkipNextReaderAdvance As Boolean
    Protected mSkippedStartElementAdvance As Boolean

    ' Last element name handed off from reader; set to "" when an End Element is encountered
    Protected mCurrentElement As String

    Protected mParentElementStack As Stack

#End Region

#Region "Processing Options and Interface Functions"

    Public ReadOnly Property SAXParserLineNumber() As Integer
        Get
            If mXMLReader Is Nothing Then
                Dim xmlReader = TryCast(mXMLReader, XmlTextReader)
                If Not xmlReader Is Nothing Then
                    Return xmlReader.LineNumber
                End If
            End If
            Return 0
        End Get
    End Property

    Public ReadOnly Property SAXParserColumnNumber() As Integer
        Get
            If mXMLReader Is Nothing Then
                Dim xmlReader = TryCast(mXMLReader, XmlTextReader)
                If Not xmlReader Is Nothing Then
                    Return xmlReader.LinePosition
                End If
            End If
            Return 0
        End Get
    End Property

    Public Property SkipBinaryData() As Boolean
        Get
            Return mSkipBinaryData
        End Get
        Set(Value As Boolean)
            mSkipBinaryData = Value
        End Set
    End Property

#End Region

    Public Overrides Sub CloseFile()
        If Not mXMLReader Is Nothing Then
            mXMLReader.Close()
        End If

        mDataFileOrTextStream = Nothing
        mInputFilePath = String.Empty
    End Sub

    Public Shared Function ConvertTimeFromTimespanToXmlDuration(
      dtTimeSpan As TimeSpan,
      blnTrimLeadingZeroValues As Boolean,
      Optional bytSecondsValueDigitsAfterDecimal As Byte = 3) As String

        ' XML duration value is typically of the form "PT249.559S" or "PT4M9.559S"
        '  where the S indicates seconds and M indicates minutes
        ' Thus, "PT249.559S" means 249.559 seconds while
        '       "PT4M9.559S" means 4 minutes plus 9.559 seconds

        ' Official definition:
        '  A length of time given in the ISO 8601 extended format: PnYnMnDTnHnMnS. The number of seconds
        '  can be a decimal or an integer. All the other values must be non-negative integers. For example,
        '  P1Y2M3DT4H5M6.7S is one year, two months, three days, four hours, five minutes, and 6.7 seconds.

        ' If blnTrimLeadingZeroValues = False, then will return the full specification.
        ' If blnTrimLeadingZeroValues = True, then removes any leading zero-value entries, for example,
        '  for TimeSpan 3 minutes, returns P3M0S rather than P0Y0M0DT0H3M0S

        Const ZERO_SOAP_DURATION_FULL = "P0Y0M0DT0H0M0S"
        Const ZERO_SOAP_DURATION_SHORT = "PT0S"

        Dim strXMLDuration As String = String.Empty

        Dim intCharIndex As Integer
        Dim intCharIndex2 As Integer
        Dim intDateIndex As Integer
        Dim intTimeIndex As Integer

        Dim blnReturnZero As Boolean
        Dim dblSeconds As Double

        Dim reSecondsRegEx As Regex
        Dim objMatch As Match

        Try
            If dtTimeSpan.Equals(TimeSpan.Zero) Then
                blnReturnZero = True
            Else
                strXMLDuration = Runtime.Remoting.Metadata.W3cXsd2001.SoapDuration.ToString(dtTimeSpan)
                If strXMLDuration.Length = 0 Then
                    blnReturnZero = True
                    Exit Try
                End If

                If strXMLDuration.Chars(0) = "-"c Then
                    strXMLDuration = strXMLDuration.Substring(1)
                End If

                If bytSecondsValueDigitsAfterDecimal < 9 Then
                    ' Look for "M\.\d+S"
                    reSecondsRegEx = New Regex("M(\d+\.\d+)S")
                    objMatch = reSecondsRegEx.Match(strXMLDuration)
                    If objMatch.Success Then
                        If objMatch.Groups.Count > 1 Then
                            With objMatch.Groups(1)
                                If IsNumber(.Captures(0).Value) Then
                                    dblSeconds = CDbl(.Captures(0).Value)
                                    strXMLDuration = strXMLDuration.Substring(0, .Index) &
                                                     Math.Round(dblSeconds, bytSecondsValueDigitsAfterDecimal).ToString &
                                                     "S"
                                End If
                            End With
                        End If
                    End If
                End If

                If blnTrimLeadingZeroValues Then

                    intDateIndex = strXMLDuration.IndexOf("P"c)
                    intTimeIndex = strXMLDuration.IndexOf("T"c)
                    intCharIndex = strXMLDuration.IndexOf("P0Y", StringComparison.Ordinal)
                    If intCharIndex >= 0 AndAlso intCharIndex < intTimeIndex Then
                        intCharIndex += 1
                        intCharIndex2 = strXMLDuration.IndexOf("Y0M", intCharIndex, StringComparison.Ordinal)
                        If intCharIndex2 > 0 AndAlso intCharIndex < intTimeIndex Then
                            intCharIndex = intCharIndex2 + 1
                            intCharIndex2 = strXMLDuration.IndexOf("M0D", intCharIndex, StringComparison.Ordinal)
                            If intCharIndex2 > 0 AndAlso intCharIndex < intTimeIndex Then
                                intCharIndex = intCharIndex2 + 1
                            End If
                        End If
                    End If

                    If intCharIndex > 0 Then
                        strXMLDuration = strXMLDuration.Substring(0, intDateIndex + 1) &
                                         strXMLDuration.Substring(intCharIndex + 2)

                        intTimeIndex = strXMLDuration.IndexOf("T"c)
                        intCharIndex = strXMLDuration.IndexOf("T0H", intTimeIndex, StringComparison.Ordinal)
                        If intCharIndex > 0 Then
                            intCharIndex += 1
                            intCharIndex2 = strXMLDuration.IndexOf("H0M", intCharIndex, StringComparison.Ordinal)
                            If intCharIndex2 > 0 Then
                                intCharIndex = intCharIndex2 + 1
                            End If
                        End If

                        If intCharIndex > 0 Then
                            strXMLDuration = strXMLDuration.Substring(0, intTimeIndex + 1) &
                                             strXMLDuration.Substring(intCharIndex + 2)
                        End If
                    End If
                End If
            End If
        Catch ex As Exception
            blnReturnZero = True
        End Try

        If blnReturnZero Then
            If blnTrimLeadingZeroValues Then
                strXMLDuration = ZERO_SOAP_DURATION_SHORT
            Else
                strXMLDuration = ZERO_SOAP_DURATION_FULL
            End If
        End If

        Return strXMLDuration
    End Function

    Public Shared Function ConvertTimeFromXmlDurationToTimespan(strTime As String, dtDefaultTimeSpan As TimeSpan) As TimeSpan
        ' XML duration value is typically of the form "PT249.559S" or "PT4M9.559S"
        '  where the S indicates seconds and M indicates minutes
        ' Thus, "PT249.559S" means 249.559 seconds while
        '       "PT4M9.559S" means 4 minutes plus 9.559 seconds

        ' Official definition:
        ' A length of time given in the ISO 8601 extended format: PnYnMnDTnHnMnS. The number of seconds
        ' can be a decimal or an integer. All the other values must be non-negative integers. For example,
        ' P1Y2M3DT4H5M6.7S is one year, two months, three days, four hours, five minutes, and 6.7 seconds.

        Dim dtTimeSpan As TimeSpan

        Try
            dtTimeSpan = Runtime.Remoting.Metadata.W3cXsd2001.SoapDuration.Parse(strTime)
        Catch ex As Exception
            dtTimeSpan = dtDefaultTimeSpan
        End Try

        Return dtTimeSpan
    End Function

    Protected Function GetAttribTimeValueMinutes(strAttributeName As String) As Single
        Dim dtTimeSpan As TimeSpan

        Try
            dtTimeSpan = ConvertTimeFromXmlDurationToTimespan(GetAttribValue(strAttributeName, "PT0S"), New TimeSpan(0))
            Return CSng(dtTimeSpan.TotalMinutes)
        Catch ex As Exception
            Return 0
        End Try
    End Function

    Protected Function GetAttribValue(strAttributeName As String, DefaultValue As String) As String
        Dim strValue As String
        Try
            If mXMLReader.HasAttributes Then
                strValue = mXMLReader.GetAttribute(strAttributeName)
                If strValue Is Nothing Then strValue = String.Copy(DefaultValue)
            Else
                strValue = String.Copy(DefaultValue)
            End If
            Return strValue
        Catch ex As Exception
            Return DefaultValue
        End Try
    End Function

    Protected Function GetAttribValue(strAttributeName As String, DefaultValue As Integer) As Integer
        Try
            Return Integer.Parse(GetAttribValue(strAttributeName, DefaultValue.ToString))
        Catch ex As Exception
            Return DefaultValue
        End Try
    End Function

    Protected Function GetAttribValue(strAttributeName As String, DefaultValue As Single) As Single
        Try
            Return Single.Parse(GetAttribValue(strAttributeName, DefaultValue.ToString))
        Catch ex As Exception
            Return DefaultValue
        End Try
    End Function

    Protected Function GetAttribValue(strAttributeName As String, DefaultValue As Boolean) As Boolean
        Try
            Return CBoolSafe(GetAttribValue(strAttributeName, DefaultValue.ToString), DefaultValue)
        Catch ex As Exception
            Return DefaultValue
        End Try
    End Function

    Protected Function GetAttribValue(strAttributeName As String, DefaultValue As Double) As Double
        Try
            Return Double.Parse(GetAttribValue(strAttributeName, DefaultValue.ToString))
        Catch ex As Exception
            Return DefaultValue
        End Try
    End Function

    Protected MustOverride Function GetCurrentSpectrum() As clsSpectrumInfo

    Protected Function GetParentElement(Optional intElementDepth As Integer = 0) As String
        ' Returns the element name one level up from intDepth
        ' If intDepth = 0, then returns the element name one level up from the last entry in mParentElementStack

        Dim udtElementInfo As udtElementInfoType

        If intElementDepth = 0 Then
            intElementDepth = mParentElementStack.Count
        End If

        If intElementDepth >= 2 And intElementDepth <= mParentElementStack.Count Then
            Try
                udtElementInfo = CType(mParentElementStack.ToArray(mParentElementStack.Count - intElementDepth + 1),
                                       udtElementInfoType)
                Return udtElementInfo.Name
            Catch ex As Exception
                Return String.Empty
            End Try
        Else
            Return String.Empty
        End If
    End Function

    Protected Overrides Function GetInputFileLocation() As String
        Return "Line " & Me.SAXParserLineNumber.ToString & ", Column " & Me.SAXParserColumnNumber.ToString
    End Function

    Protected MustOverride Sub InitializeCurrentSpectrum(blnAutoShrinkDataLists As Boolean)

    Protected Overrides Sub InitializeLocalVariables()
        ' Note: This sub is called from OpenFile and OpenTextStream,
        '        so do not update mSkipBinaryData

        MyBase.InitializeLocalVariables()

        mSkipNextReaderAdvance = False
        mSkippedStartElementAdvance = False
        mSpectrumFound = False

        mCurrentElement = String.Empty
        If mParentElementStack Is Nothing Then
            mParentElementStack = New Stack
        Else
            mParentElementStack.Clear()
        End If
    End Sub

    Public Overrides Function OpenFile(strInputFilePath As String) As Boolean
        ' Returns true if the file is successfully opened

        Dim blnSuccess As Boolean

        Try
            blnSuccess = OpenFileInit(strInputFilePath)
            If Not blnSuccess Then Return False

            ' Initialize the stream reader and the XML Text Reader
            mDataFileOrTextStream = New StreamReader(strInputFilePath)
            Dim reader = New XmlTextReader(mDataFileOrTextStream)

            ' Skip all whitespace
            reader.WhitespaceHandling = WhitespaceHandling.None
            mXMLReader = reader

            mErrorMessage = String.Empty

            InitializeLocalVariables()

            MyBase.ResetProgress("Parsing " & Path.GetFileName(strInputFilePath))

            blnSuccess = True

        Catch ex As Exception
            mErrorMessage = "Error opening file: " & strInputFilePath & "; " & ex.Message
            blnSuccess = False
        End Try

        Return blnSuccess
    End Function

    Public Overrides Function OpenTextStream(strTextStream As String) As Boolean
        ' Returns true if the text stream is successfully opened

        Dim blnSuccess As Boolean

        ' Make sure any open file or text stream is closed
        CloseFile()

        Try
            mInputFilePath = "TextStream"

            ' Initialize the stream reader and the XML Text Reader
            mDataFileOrTextStream = New StringReader(strTextStream)
            Dim reader = New XmlTextReader(mDataFileOrTextStream)

            ' Skip all whitespace
            reader.WhitespaceHandling = WhitespaceHandling.None
            mXMLReader = reader

            mErrorMessage = String.Empty

            InitializeLocalVariables()

            MyBase.ResetProgress("Parsing text stream")

            blnSuccess = True

        Catch ex As Exception
            mErrorMessage = "Error opening text stream"
            blnSuccess = False
        End Try

        Return blnSuccess
    End Function

    Protected Function ParentElementStackRemove() As String
        Dim udtElementInfo As udtElementInfoType

        ' Removes the most recent entry from mParentElementStack and returns it
        If mParentElementStack.Count = 0 Then
            Return String.Empty
        Else
            udtElementInfo = CType(mParentElementStack.Pop(), udtElementInfoType)
            Return udtElementInfo.Name
        End If
    End Function

    Protected Sub ParentElementStackAdd(objXMLReader As XmlReader)
        ' Adds a new entry to the end of mParentElementStack
        ' Since the XML Text Reader doesn't recognize implicit end elements (e.g. the "/>" characters at 
        '  the end of <City name="Laramie" />) we need to compare the depth of the current element with 
        '  the depth of the element at the top of the stack
        ' If the depth values are the same, then we pop the top element off and push the new element on
        ' If the depth values are not the same, then we push the new element on

        Dim udtElementInfo As udtElementInfoType

        If mParentElementStack.Count > 0 Then
            udtElementInfo = CType(mParentElementStack.Peek(), udtElementInfoType)
            If udtElementInfo.Depth = objXMLReader.Depth Then
                mParentElementStack.Pop()
            End If
        End If

        With udtElementInfo
            .Name = objXMLReader.Name
            .Depth = objXMLReader.Depth
        End With
        mParentElementStack.Push(udtElementInfo)
    End Sub

    Protected MustOverride Sub ParseStartElement()

    Protected MustOverride Sub ParseElementContent()

    Protected MustOverride Sub ParseEndElement()

    Public Overrides Function ReadNextSpectrum(<Out()> ByRef objSpectrumInfo As clsSpectrumInfo) As Boolean
        ' Reads the next spectrum from an mzXML or mzData file
        ' Returns True if a spectrum is found, otherwise, returns False

        Dim blnReadSuccessful As Boolean

        Try
            InitializeCurrentSpectrum(mAutoShrinkDataLists)

            mSpectrumFound = False
            If mXMLReader Is Nothing Then
                objSpectrumInfo = New clsSpectrumInfo()
                mErrorMessage = "Data file not currently open"
            Else

                If Not mDataFileOrTextStream Is Nothing Then

                    If TypeOf mDataFileOrTextStream Is StreamReader Then
                        With CType(mDataFileOrTextStream, StreamReader)
                            MyBase.UpdateProgress(.BaseStream.Position / .BaseStream.Length * 100.0)
                        End With
                    Else
                        Dim xmlReader = TryCast(mXMLReader, XmlTextReader)
                        If Not xmlReader Is Nothing Then
                            ' Note that 1000 is an arbitrary value for the number of lines in the input stream 
                            ' (only needed if mDataFileOrTextStream is a StringReader)
                            MyBase.UpdateProgress((xmlReader.LineNumber Mod 1000) / 1000 * 100.0)
                        End If
                    End If
                End If

                blnReadSuccessful = True
                Do While Not mSpectrumFound AndAlso
                         blnReadSuccessful AndAlso
                         Not mAbortProcessing AndAlso
                         (mXMLReader.ReadState = ReadState.Initial Or
                          mXMLReader.ReadState = ReadState.Interactive)

                    mSpectrumFound = False

                    If mSkipNextReaderAdvance Then
                        mSkipNextReaderAdvance = False
                        Try
                            If mXMLReader.NodeType = XmlNodeType.Element Then
                                mSkippedStartElementAdvance = True
                            End If
                        Catch ex As Exception
                            ' Ignore Errors Here
                        End Try
                        blnReadSuccessful = True
                    Else
                        mSkippedStartElementAdvance = False
                        blnReadSuccessful = mXMLReader.Read()
                        XMLTextReaderSkipWhitespace()
                    End If

                    If blnReadSuccessful AndAlso mXMLReader.ReadState = ReadState.Interactive Then
                        If mXMLReader.NodeType = XmlNodeType.Element Then
                            ParseStartElement()
                        ElseIf mXMLReader.NodeType = XmlNodeType.EndElement Then
                            ParseEndElement()
                        ElseIf mXMLReader.NodeType = XmlNodeType.Text Then
                            ParseElementContent()
                        End If

                    End If
                Loop

                objSpectrumInfo = GetCurrentSpectrum()

                If mSpectrumFound AndAlso Not MyBase.ReadingAndStoringSpectra Then
                    If mInputFileStats.ScanCount = 0 Then mInputFileStats.ScanCount = 1
                    MyBase.UpdateFileStats(mInputFileStats.ScanCount, objSpectrumInfo.ScanNumber)
                End If
            End If

        Catch ex As Exception
            LogErrors("ReadNextSpectrum", ex.Message)
            objSpectrumInfo = New clsSpectrumInfo()
        End Try

        Return mSpectrumFound
    End Function

    Protected Function XMLTextReaderGetInnerText() As String
        Dim strValue As String = String.Empty
        Dim blnSuccess As Boolean

        If mXMLReader.NodeType = XmlNodeType.Element Then
            ' Advance the reader so that we can read the value
            blnSuccess = mXMLReader.Read()
        Else
            blnSuccess = True
        End If

        If blnSuccess AndAlso Not mXMLReader.NodeType = XmlNodeType.Whitespace And mXMLReader.HasValue Then
            strValue = mXMLReader.Value
        End If

        Return strValue
    End Function

    Private Sub XMLTextReaderSkipWhitespace()
        Try
            If mXMLReader.NodeType = XmlNodeType.Whitespace Then
                ' Whitspace; read the next node
                mXMLReader.Read()
            End If
        Catch ex As Exception
            ' Ignore Errors Here
        End Try
    End Sub
End Class
