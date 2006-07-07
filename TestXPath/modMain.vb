Option Strict On

' This module can be used to test XPath
'
' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2006, Battelle Memorial Institute.  All Rights Reserved.
' Program started April 16, 2006
'
' E-mail: matthew.monroe@pnl.gov or matt@alchemistmatt.com
' Website: http://ncrr.pnl.gov/ or http://www.sysbio.org/resources/staff/
' -------------------------------------------------------------------------------
'
' Last modified April 16, 2006

Module modMain

    Public Sub Main()
        TestXPathMZData()
        TestXPathMZXML()
    End Sub

    Private Sub TestXPathMZData()
        Const MZDATA_FILE_PATH As String = "SampleData_myo_excerpt_1.05cv.mzdata"

        Dim strXPathMatch As String

        'Dim objXMLReader As System.Xml.XmlTextReader
        Dim objXPathDoc As System.Xml.XPath.XPathDocument
        Dim objXPathNav As System.Xml.XPath.XPathNavigator
        Dim objNodes As System.Xml.XPath.XPathNodeIterator

        Dim strAttrib As String
        Dim intSpectrumID As Integer

        Try
            Console.WriteLine()
            Console.WriteLine("Opening " & MZDATA_FILE_PATH)

            'objXMLReader = New System.Xml.XmlTextReader(MZDATA_FILE_PATH)

            objXPathDoc = New System.Xml.XPath.XPathDocument(MZDATA_FILE_PATH, Xml.XmlSpace.Default)
            objXPathNav = objXPathDoc.CreateNavigator()

            strXPathMatch = "//mzData/spectrumList/spectrum"
            objNodes = objXPathNav.Select(strXPathMatch)

            Do While objNodes.MoveNext
                If objNodes.Current.HasAttributes Then
                    strAttrib = objNodes.Current.GetAttribute("id", "")
                    Console.WriteLine("Spectrum ID " & strAttrib)
                End If
            Loop

            For intSpectrumID = 300 To 1 Step -5
                strXPathMatch = "//mzData/spectrumList/spectrum[@id='" & intSpectrumID.ToString & "']"
                objNodes = objXPathNav.Select(strXPathMatch)

                Do While objNodes.MoveNext
                    If objNodes.Current.HasAttributes Then
                        strAttrib = objNodes.Current.GetAttribute("id", "")
                        Console.WriteLine("Spectrum ID " & strAttrib)
                    End If
                Loop
            Next intSpectrumID

        Catch ex As Exception
            Console.WriteLine(ex.Message)
        End Try

    End Sub

    Private Sub TestXPathMZXML()
        Const USE_VALIDATING_READER As Boolean = False
        Const MZXML_FILE_PATH As String = "Gsulf326_LTQFT_run2_23Aug05_Andro_0705-06.mzXML"

        Dim strXPathMatch As String

        'Dim objNameTable As System.Xml.NameTable
        Dim objXMLReader As System.Xml.XmlTextReader
        Dim objXMLReaderValidating As System.Xml.XmlValidatingReader

        Dim objXPathDoc As System.Xml.XPath.XPathDocument
        Dim objXPathNav As System.Xml.XPath.XPathNavigator
        Dim objNodes As System.Xml.XPath.XPathNodeIterator

        Dim strAttrib As String
        Dim intScanNumber As Integer

        Try
            'objNameTable = New System.Xml.NameTable
            'objNameTable.Add("test")

            Console.WriteLine()
            Console.WriteLine("Opening " & MZXML_FILE_PATH)
            objXMLReader = New System.Xml.XmlTextReader(MZXML_FILE_PATH)

            If USE_VALIDATING_READER Then
                objXMLReaderValidating = New System.Xml.XmlValidatingReader(objXMLReader)
                objXMLReaderValidating.Schemas.Add("http://sashimi.sourceforge.net/schema_revision/mzXML_2.0", "mzXML_idx_2.0.xsd")
                objXPathDoc = New System.Xml.XPath.XPathDocument(objXMLReaderValidating)
            Else
                objXPathDoc = New System.Xml.XPath.XPathDocument(objXMLReader)
            End If

            objXPathNav = objXPathDoc.CreateNavigator()

            strXPathMatch = "//mzXML/msRun/scan"
            objNodes = objXPathNav.Select(strXPathMatch)

            Do While objNodes.MoveNext
                If objNodes.Current.HasAttributes Then
                    strAttrib = objNodes.Current.GetAttribute("num", "")
                    Console.WriteLine("Scan Number " & strAttrib)
                End If
            Loop

            For intScanNumber = 300 To 1 Step -5
                strXPathMatch = "//mzXML/msRun/scan[@num='" & intScanNumber.ToString & "']"
                objNodes = objXPathNav.Select(strXPathMatch)

                Do While objNodes.MoveNext
                    If objNodes.Current.HasAttributes Then
                        strAttrib = objNodes.Current.GetAttribute("num", "")
                        Console.WriteLine("Scan Number " & strAttrib)
                    End If
                Loop
            Next intScanNumber
        Catch ex As Exception
            Console.WriteLine(ex.Message)
        End Try

    End Sub

End Module
