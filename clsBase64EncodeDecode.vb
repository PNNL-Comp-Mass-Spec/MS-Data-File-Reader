Option Strict On

' The functions in this class can be used to decode a base-64 encoded array of numbers,
' or to encode an array of numbers into a base-64 string
'
' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2005, Battelle Memorial Institute.  All Rights Reserved.
' Started November 2004
'
' E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com
' Website: http://omics.pnl.gov/ or http://www.sysbio.org/resources/staff/
' -------------------------------------------------------------------------------
'
' Last modified March 23, 2015
Imports System.Runtime.InteropServices

Public Class clsBase64EncodeDecode

    Public Enum eEndianTypeConstants As Integer
        LittleEndian = 0
        BigEndian = 1
    End Enum

    Private Shared Function B64encode(bytArray() As Byte, Optional ByVal removeTrailingPaddingChars As Boolean = False) As String
        If removeTrailingPaddingChars Then
            Return Convert.ToBase64String(bytArray).TrimEnd("="c)
        Else
            Return Convert.ToBase64String(bytArray)
        End If
    End Function

    Public Shared Function DecodeNumericArray(strBase64EncodedText As String, <Out()> ByRef dataArray() As Byte) As Boolean
        ' Extracts an array of Bytes from a base-64 encoded string

        dataArray = Convert.FromBase64String(strBase64EncodedText)

        Return True

    End Function

    Public Shared Function DecodeNumericArray(strBase64EncodedText As String, <Out()> ByRef dataArray() As Int16, zLibCompressed As Boolean, Optional ByVal eEndianMode As eEndianTypeConstants = eEndianTypeConstants.LittleEndian) As Boolean
        ' Extracts an array of 16-bit integers from a base-64 encoded string

        Const DATA_TYPE_PRECISION_BYTES = 2
        Dim bytArray() As Byte
        Dim bytArrayOneValue(DATA_TYPE_PRECISION_BYTES - 1) As Byte

        Dim intIndex As Integer
        Dim conversionSource As String

        If zLibCompressed Then
            conversionSource = "DecompressZLib"
            bytArray = DecompressZLib(strBase64EncodedText)
        Else
            conversionSource = "Convert.FromBase64String"
            bytArray = Convert.FromBase64String(strBase64EncodedText)
        End If

        If Not bytArray.Length Mod DATA_TYPE_PRECISION_BYTES = 0 Then
            Throw New Exception("Array length of the Byte Array returned by " & conversionSource &
                                " is not divisible by " & DATA_TYPE_PRECISION_BYTES & " bytes;" &
                                " not the correct length for an encoded array of 16-bit integers")
        End If

        ReDim dataArray(CInt(bytArray.Length / DATA_TYPE_PRECISION_BYTES) - 1)

        For intIndex = 0 To bytArray.Length - 1 Step DATA_TYPE_PRECISION_BYTES

            ' I'm not sure if I've got Little and Big endian correct or not in the following If statement
            ' What I do know is that mzXML works with what I'm calling emBigEndian
            '  and mzData works with what I'm calling emLittleEndian
            If eEndianMode = eEndianTypeConstants.LittleEndian Then
                ' Do not swap bytes
                Array.Copy(bytArray, intIndex, bytArrayOneValue, 0, DATA_TYPE_PRECISION_BYTES)
            Else
                ' eEndianTypeConstants.BigEndian
                ' Swap bytes before converting from DATA_TYPE_PRECISION_BYTES bits to one 16-bit integer
                bytArrayOneValue(0) = bytArray(intIndex + 1)
                bytArrayOneValue(1) = bytArray(intIndex + 0)
            End If

            dataArray(CInt(intIndex / DATA_TYPE_PRECISION_BYTES)) = BitConverter.ToInt16(bytArrayOneValue, 0)
        Next intIndex

        Return True

    End Function

    Public Shared Function DecodeNumericArray(strBase64EncodedText As String, <Out()> ByRef dataArray() As Int32, zLibCompressed As Boolean, Optional ByVal eEndianMode As eEndianTypeConstants = eEndianTypeConstants.LittleEndian) As Boolean
        ' Extracts an array of 32-bit integers from a base-64 encoded string

        Const DATA_TYPE_PRECISION_BYTES = 4
        Dim bytArray() As Byte
        Dim bytArrayOneValue(DATA_TYPE_PRECISION_BYTES - 1) As Byte

        Dim intIndex As Integer
        Dim conversionSource As String

        If zLibCompressed Then
            conversionSource = "DecompressZLib"
            bytArray = DecompressZLib(strBase64EncodedText)
        Else
            conversionSource = "Convert.FromBase64String"
            bytArray = Convert.FromBase64String(strBase64EncodedText)
        End If

        If Not bytArray.Length Mod DATA_TYPE_PRECISION_BYTES = 0 Then
            Throw New Exception("Array length of the Byte Array returned by " & conversionSource &
                                " is not divisible by " & DATA_TYPE_PRECISION_BYTES & " bytes;" &
                                " not the correct length for an encoded array of 32-bit integers")
        End If

        ReDim dataArray(CInt(bytArray.Length / DATA_TYPE_PRECISION_BYTES) - 1)

        For intIndex = 0 To bytArray.Length - 1 Step DATA_TYPE_PRECISION_BYTES

            ' I'm not sure if I've got Little and Big endian correct or not in the following If statement
            ' What I do know is that mzXML works with what I'm calling emBigEndian
            '  and mzData works with what I'm calling emLittleEndian
            If eEndianMode = eEndianTypeConstants.LittleEndian Then
                ' Do not swap bytes
                Array.Copy(bytArray, intIndex, bytArrayOneValue, 0, DATA_TYPE_PRECISION_BYTES)
            Else
                ' eEndianTypeConstants.BigEndian
                ' Swap bytes before converting from DATA_TYPE_PRECISION_BYTES bits to one 32-bit integer
                bytArrayOneValue(0) = bytArray(intIndex + 3)
                bytArrayOneValue(1) = bytArray(intIndex + 2)
                bytArrayOneValue(2) = bytArray(intIndex + 1)
                bytArrayOneValue(3) = bytArray(intIndex + 0)
            End If

            dataArray(CInt(intIndex / DATA_TYPE_PRECISION_BYTES)) = BitConverter.ToInt32(bytArrayOneValue, 0)
        Next intIndex

        Return True

    End Function

    Public Shared Function DecodeNumericArray(strBase64EncodedText As String, <Out()> ByRef dataArray() As Single, zLibCompressed As Boolean, Optional ByVal eEndianMode As eEndianTypeConstants = eEndianTypeConstants.LittleEndian) As Boolean
        ' Extracts an array of Singles from a base-64 encoded string

        Const DATA_TYPE_PRECISION_BYTES = 4
        Dim bytArray() As Byte
        Dim bytArrayOneValue(DATA_TYPE_PRECISION_BYTES - 1) As Byte

        Dim intIndex As Integer
        Dim conversionSource As String

        If zLibCompressed Then
            conversionSource = "DecompressZLib"
            bytArray = DecompressZLib(strBase64EncodedText)
        Else
            conversionSource = "Convert.FromBase64String"
            bytArray = Convert.FromBase64String(strBase64EncodedText)
        End If

        If Not bytArray.Length Mod DATA_TYPE_PRECISION_BYTES = 0 Then
            Throw New Exception("Array length of the Byte Array returned by " & conversionSource &
                                " is not divisible by " & DATA_TYPE_PRECISION_BYTES & " bytes;" &
                                " not the correct length for an encoded array of floats (aka singles)")
        End If

        ReDim dataArray(CInt(bytArray.Length / DATA_TYPE_PRECISION_BYTES) - 1)

        For intIndex = 0 To bytArray.Length - 1 Step DATA_TYPE_PRECISION_BYTES

            ' I'm not sure if I've got Little and Big endian correct or not in the following If statement
            ' What I do know is that mzXML works with what I'm calling emBigEndian
            '  and mzData works with what I'm calling emLittleEndian
            If eEndianMode = eEndianTypeConstants.LittleEndian Then
                ' Do not swap bytes
                Array.Copy(bytArray, intIndex, bytArrayOneValue, 0, DATA_TYPE_PRECISION_BYTES)
            Else
                ' eEndianTypeConstants.BigEndian
                ' Swap bytes before converting from DATA_TYPE_PRECISION_BYTES bits to one single
                bytArrayOneValue(0) = bytArray(intIndex + 3)
                bytArrayOneValue(1) = bytArray(intIndex + 2)
                bytArrayOneValue(2) = bytArray(intIndex + 1)
                bytArrayOneValue(3) = bytArray(intIndex + 0)
            End If

            dataArray(CInt(intIndex / DATA_TYPE_PRECISION_BYTES)) = BitConverter.ToSingle(bytArrayOneValue, 0)
        Next intIndex

        Return True

    End Function

    Public Shared Function DecodeNumericArray(strBase64EncodedText As String, <Out()> ByRef dataArray() As Double, zLibCompressed As Boolean, Optional ByVal eEndianMode As eEndianTypeConstants = eEndianTypeConstants.LittleEndian) As Boolean
        ' Extracts an array of Doubles from a base-64 encoded string

        Const DATA_TYPE_PRECISION_BYTES = 8
        Dim bytArray() As Byte
        Dim bytArrayOneValue(DATA_TYPE_PRECISION_BYTES - 1) As Byte

        Dim intIndex As Integer
        Dim conversionSource As String

        If zLibCompressed Then
            conversionSource = "DecompressZLib"
            bytArray = DecompressZLib(strBase64EncodedText)
        Else
            conversionSource = "Convert.FromBase64String"
            bytArray = Convert.FromBase64String(strBase64EncodedText)
        End If

        If Not bytArray.Length Mod DATA_TYPE_PRECISION_BYTES = 0 Then
            Throw New Exception("Array length of the Byte Array returned by " & conversionSource &
                                " is not divisible by " & DATA_TYPE_PRECISION_BYTES & " bytes;" &
                                " not the correct length for an encoded array of doubles")
        End If

        ReDim dataArray(CInt(bytArray.Length / DATA_TYPE_PRECISION_BYTES) - 1)

        For intIndex = 0 To bytArray.Length - 1 Step DATA_TYPE_PRECISION_BYTES

            ' I'm not sure if I've got Little and Big endian correct or not in the following If statement
            ' What I do know is that mzXML works with what I'm calling emBigEndian
            '  and mzData works with what I'm calling emLittleEndian
            If eEndianMode = eEndianTypeConstants.LittleEndian Then
                ' Do not swap bytes
                Array.Copy(bytArray, intIndex, bytArrayOneValue, 0, DATA_TYPE_PRECISION_BYTES)
            Else
                ' eEndianTypeConstants.BigEndian
                ' Swap bytes before converting from DATA_TYPE_PRECISION_BYTES bits to one double
                bytArrayOneValue(0) = bytArray(intIndex + 7)
                bytArrayOneValue(1) = bytArray(intIndex + 6)
                bytArrayOneValue(2) = bytArray(intIndex + 5)
                bytArrayOneValue(3) = bytArray(intIndex + 4)
                bytArrayOneValue(4) = bytArray(intIndex + 3)
                bytArrayOneValue(5) = bytArray(intIndex + 2)
                bytArrayOneValue(6) = bytArray(intIndex + 1)
                bytArrayOneValue(7) = bytArray(intIndex)
            End If

            dataArray(CInt(intIndex / DATA_TYPE_PRECISION_BYTES)) = BitConverter.ToDouble(bytArrayOneValue, 0)
        Next intIndex

        Return True

    End Function

    Protected Shared Function DecompressZLib(strBase64EncodedText As String) As Byte()

        Dim msCompressed As IO.MemoryStream
        msCompressed = New IO.MemoryStream(Convert.FromBase64String(strBase64EncodedText))

        Dim msInflated = New IO.MemoryStream(strBase64EncodedText.Length * 2)

        ' We must skip the first two bytes
        ' See http://george.chiramattel.com/blog/2007/09/deflatestream-block-length-does-not-match.html
        msCompressed.ReadByte()
        msCompressed.ReadByte()

        Using inflater = New IO.Compression.DeflateStream(msCompressed, IO.Compression.CompressionMode.Decompress)

            Dim bytBuffer() As Byte
            Dim intBytesRead As Integer

            ReDim bytBuffer(4095)

            While inflater.CanRead
                intBytesRead = inflater.Read(bytBuffer, 0, bytBuffer.Length)
                If intBytesRead = 0 Then Exit While
                msInflated.Write(bytBuffer, 0, intBytesRead)
            End While

            msInflated.Seek(0, IO.SeekOrigin.Begin)
        End Using

        Dim bytArray() As Byte
        Dim intTotalBytesDecompressed = CInt(msInflated.Length)

        If intTotalBytesDecompressed > 0 Then
            ReDim bytArray(intTotalBytesDecompressed - 1)
            msInflated.Read(bytArray, 0, intTotalBytesDecompressed)
        Else
            ReDim bytArray(-1)
        End If

        Return bytArray

    End Function

    Public Shared Function EncodeNumericArray(
      dataArray() As Byte,
      <Out()> ByRef intPrecisionBitsReturn As Int32,
      <Out()> ByRef strDataTypeNameReturn As String,
      Optional ByVal removeTrailingPaddingChars As Boolean = False) As String

        ' Converts an array of Bytes to a base-64 encoded string
        ' In addition, returns the bits of precision and datatype name for the given data type

        Const DATA_TYPE_PRECISION_BYTES = 1
        Const DATA_TYPE_NAME = "byte"

        intPrecisionBitsReturn = DATA_TYPE_PRECISION_BYTES * 8
        strDataTypeNameReturn = DATA_TYPE_NAME

        If dataArray Is Nothing OrElse dataArray.Length = -1 Then
            Return String.Empty
        Else
            Return B64encode(dataArray, removeTrailingPaddingChars)
        End If

    End Function

    Public Shared Function EncodeNumericArray(
      dataArray() As Int16,
      <Out()> ByRef intPrecisionBitsReturn As Int32,
      <Out()> ByRef strDataTypeNameReturn As String,
      Optional ByVal removeTrailingPaddingChars As Boolean = False,
      Optional ByVal eEndianMode As eEndianTypeConstants = eEndianTypeConstants.LittleEndian) As String

        ' Converts an array of 16-bit integers to a base-64 encoded string
        ' In addition, returns the bits of precision and datatype name for the given data type

        Const DATA_TYPE_PRECISION_BYTES = 2
        Const DATA_TYPE_NAME = "int"

        Dim bytArray() As Byte
        Dim bytNewBytes(DATA_TYPE_PRECISION_BYTES - 1) As Byte

        Dim intIndex As Integer
        Dim intBaseIndex As Integer

        intPrecisionBitsReturn = DATA_TYPE_PRECISION_BYTES * 8
        strDataTypeNameReturn = DATA_TYPE_NAME

        If dataArray Is Nothing OrElse dataArray.Length = -1 Then
            Return String.Empty
        Else
            ReDim bytArray(dataArray.Length * DATA_TYPE_PRECISION_BYTES - 1)

            For intIndex = 0 To dataArray.Length - 1
                intBaseIndex = intIndex * DATA_TYPE_PRECISION_BYTES

                bytNewBytes = BitConverter.GetBytes(dataArray(intIndex))

                ' I'm not sure if I've got Little and Big endian correct or not in the following If statement
                ' What I do know is that mzXML works with what I'm calling emBigEndian
                '  and mzData works with what I'm calling emLittleEndian
                If eEndianMode = eEndianTypeConstants.LittleEndian Then
                    ' Do not swap bytes
                    Array.Copy(bytNewBytes, 0, bytArray, intBaseIndex, DATA_TYPE_PRECISION_BYTES)
                Else
                    ' eEndianTypeConstants.BigEndian
                    ' Swap bytes when copying into bytArray
                    bytArray(intBaseIndex + 0) = bytNewBytes(1)
                    bytArray(intBaseIndex + 1) = bytNewBytes(0)
                End If

            Next intIndex

            Return B64encode(bytArray, removeTrailingPaddingChars)
        End If

    End Function

    Public Shared Function EncodeNumericArray(
      dataArray() As Int32,
      <Out()> ByRef intPrecisionBitsReturn As Int32,
      <Out()> ByRef strDataTypeNameReturn As String,
      Optional ByVal removeTrailingPaddingChars As Boolean = False,
      Optional ByVal eEndianMode As eEndianTypeConstants = eEndianTypeConstants.LittleEndian) As String

        ' Converts an array of 32-bit integers to a base-64 encoded string
        ' In addition, returns the bits of precision and datatype name for the given data type

        Const DATA_TYPE_PRECISION_BYTES = 4
        Const DATA_TYPE_NAME = "int"

        Dim bytArray() As Byte
        Dim bytNewBytes(DATA_TYPE_PRECISION_BYTES - 1) As Byte

        Dim intIndex As Integer
        Dim intBaseIndex As Integer

        intPrecisionBitsReturn = DATA_TYPE_PRECISION_BYTES * 8
        strDataTypeNameReturn = DATA_TYPE_NAME

        If dataArray Is Nothing OrElse dataArray.Length = -1 Then
            Return String.Empty
        Else
            ReDim bytArray(dataArray.Length * DATA_TYPE_PRECISION_BYTES - 1)

            For intIndex = 0 To dataArray.Length - 1
                intBaseIndex = intIndex * DATA_TYPE_PRECISION_BYTES

                bytNewBytes = BitConverter.GetBytes(dataArray(intIndex))

                ' I'm not sure if I've got Little and Big endian correct or not in the following If statement
                ' What I do know is that mzXML works with what I'm calling emBigEndian
                '  and mzData works with what I'm calling emLittleEndian
                If eEndianMode = eEndianTypeConstants.LittleEndian Then
                    ' Do not swap bytes
                    Array.Copy(bytNewBytes, 0, bytArray, intBaseIndex, DATA_TYPE_PRECISION_BYTES)
                Else
                    ' eEndianTypeConstants.BigEndian
                    ' Swap bytes when copying into bytArray
                    bytArray(intBaseIndex + 0) = bytNewBytes(3)
                    bytArray(intBaseIndex + 1) = bytNewBytes(2)
                    bytArray(intBaseIndex + 2) = bytNewBytes(1)
                    bytArray(intBaseIndex + 3) = bytNewBytes(0)
                End If

            Next intIndex

            Return B64encode(bytArray, removeTrailingPaddingChars)
        End If

    End Function

    Public Shared Function EncodeNumericArray(
      dataArray() As Single,
      <Out()> ByRef intPrecisionBitsReturn As Int32,
      <Out()> ByRef strDataTypeNameReturn As String,
      Optional ByVal removeTrailingPaddingChars As Boolean = False,
      Optional ByVal eEndianMode As eEndianTypeConstants = eEndianTypeConstants.LittleEndian) As String

        ' Converts an array of singles to a base-64 encoded string
        ' In addition, returns the bits of precision and datatype name for the given data type

        Const DATA_TYPE_PRECISION_BYTES = 4
        Const DATA_TYPE_NAME = "float"

        Dim bytArray() As Byte
        Dim bytNewBytes(DATA_TYPE_PRECISION_BYTES - 1) As Byte

        Dim intIndex As Integer
        Dim intBaseIndex As Integer

        intPrecisionBitsReturn = DATA_TYPE_PRECISION_BYTES * 8
        strDataTypeNameReturn = DATA_TYPE_NAME

        If dataArray Is Nothing OrElse dataArray.Length = -1 Then
            Return String.Empty
        Else
            ReDim bytArray(dataArray.Length * DATA_TYPE_PRECISION_BYTES - 1)

            For intIndex = 0 To dataArray.Length - 1
                intBaseIndex = intIndex * DATA_TYPE_PRECISION_BYTES

                bytNewBytes = BitConverter.GetBytes(dataArray(intIndex))

                ' I'm not sure if I've got Little and Big endian correct or not in the following If statement
                ' What I do know is that mzXML works with what I'm calling emBigEndian
                '  and mzData works with what I'm calling emLittleEndian
                If eEndianMode = eEndianTypeConstants.LittleEndian Then
                    ' Do not swap bytes
                    Array.Copy(bytNewBytes, 0, bytArray, intBaseIndex, DATA_TYPE_PRECISION_BYTES)
                Else
                    ' eEndianTypeConstants.BigEndian
                    ' Swap bytes when copying into bytArray
                    bytArray(intBaseIndex + 0) = bytNewBytes(3)
                    bytArray(intBaseIndex + 1) = bytNewBytes(2)
                    bytArray(intBaseIndex + 2) = bytNewBytes(1)
                    bytArray(intBaseIndex + 3) = bytNewBytes(0)
                End If

            Next intIndex

            Return B64encode(bytArray, removeTrailingPaddingChars)
        End If

    End Function

    Public Shared Function EncodeNumericArray(
      dataArray() As Double,
      <Out()> ByRef intPrecisionBitsReturn As Int32,
      <Out()> ByRef strDataTypeNameReturn As String,
      Optional ByVal removeTrailingPaddingChars As Boolean = False,
      Optional ByVal eEndianMode As eEndianTypeConstants = eEndianTypeConstants.LittleEndian) As String

        ' Converts an array of doubles to a base-64 encoded string
        ' In addition, returns the bits of precision and datatype name for the given data type

        Const DATA_TYPE_PRECISION_BYTES = 8
        Const DATA_TYPE_NAME = "float"

        Dim bytArray() As Byte
        Dim bytNewBytes(DATA_TYPE_PRECISION_BYTES - 1) As Byte

        Dim intIndex As Integer
        Dim intBaseIndex As Integer

        intPrecisionBitsReturn = DATA_TYPE_PRECISION_BYTES * 8
        strDataTypeNameReturn = DATA_TYPE_NAME

        If dataArray Is Nothing OrElse dataArray.Length = -1 Then
            Return String.Empty
        Else
            ReDim bytArray(dataArray.Length * DATA_TYPE_PRECISION_BYTES - 1)

            For intIndex = 0 To dataArray.Length - 1
                intBaseIndex = intIndex * DATA_TYPE_PRECISION_BYTES

                bytNewBytes = BitConverter.GetBytes(dataArray(intIndex))

                ' I'm not sure if I've got Little and Big endian correct or not in the following If statement
                ' What I do know is that mzXML works with what I'm calling emBigEndian
                '  and mzData works with what I'm calling emLittleEndian
                If eEndianMode = eEndianTypeConstants.LittleEndian Then
                    ' Do not swap bytes
                    Array.Copy(bytNewBytes, 0, bytArray, intBaseIndex, DATA_TYPE_PRECISION_BYTES)
                Else
                    ' eEndianTypeConstants.BigEndian
                    ' Swap bytes when copying into bytArray
                    bytArray(intBaseIndex + 0) = bytNewBytes(7)
                    bytArray(intBaseIndex + 1) = bytNewBytes(6)
                    bytArray(intBaseIndex + 2) = bytNewBytes(5)
                    bytArray(intBaseIndex + 3) = bytNewBytes(4)
                    bytArray(intBaseIndex + 4) = bytNewBytes(3)
                    bytArray(intBaseIndex + 5) = bytNewBytes(2)
                    bytArray(intBaseIndex + 6) = bytNewBytes(1)
                    bytArray(intBaseIndex + 7) = bytNewBytes(0)
                End If

            Next intIndex

            Return B64encode(bytArray, removeTrailingPaddingChars)
        End If

    End Function

End Class
