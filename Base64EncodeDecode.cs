using System;
using System.IO;

// ReSharper disable UnusedMember.Global

namespace MSDataFileReader
{
    public class Base64EncodeDecode
    {
        // Ignore Spelling: endian

        public enum eEndianTypeConstants
        {
            LittleEndian = 0,
            BigEndian = 1
        }

        private static string B64encode(byte[] bytArray, bool removeTrailingPaddingChars = false)
        {
            return removeTrailingPaddingChars
                ? Convert.ToBase64String(bytArray).TrimEnd('=')
                : Convert.ToBase64String(bytArray);
        }

        /// <summary>
        /// Extracts an array of Bytes from a base-64 encoded string
        /// </summary>
        /// <param name="strBase64EncodedText"></param>
        /// <param name="dataArray"></param>
        /// <returns>True if successful, raises an exception if an error</returns>
        public static bool DecodeNumericArray(string strBase64EncodedText, out byte[] dataArray)
        {
            dataArray = Convert.FromBase64String(strBase64EncodedText);
            return true;
        }

        /// <summary>
        /// Extracts an array of 16-bit integers from a base-64 encoded string
        /// </summary>
        /// <param name="strBase64EncodedText"></param>
        /// <param name="dataArray"></param>
        /// <param name="zLibCompressed"></param>
        /// <param name="eEndianMode"></param>
        /// <returns>True if successful, raises an exception if an error</returns>
        public static bool DecodeNumericArray(string strBase64EncodedText, out short[] dataArray, bool zLibCompressed, eEndianTypeConstants eEndianMode = eEndianTypeConstants.LittleEndian)
        {
            const int DATA_TYPE_PRECISION_BYTES = 2;
            byte[] bytArray;
            var bytArrayOneValue = new byte[2];
            string conversionSource;

            if (zLibCompressed)
            {
                conversionSource = "DecompressZLib";
                bytArray = DecompressZLib(strBase64EncodedText);
            }
            else
            {
                conversionSource = "Convert.FromBase64String";
                bytArray = Convert.FromBase64String(strBase64EncodedText);
            }

            if (bytArray.Length % DATA_TYPE_PRECISION_BYTES != 0)
            {
                throw new Exception("Array length of the Byte Array returned by " + conversionSource + " is not divisible by " + DATA_TYPE_PRECISION_BYTES + " bytes;" + " not the correct length for an encoded array of 16-bit integers");
            }

            dataArray = new short[(int)Math.Round(bytArray.Length / (double)DATA_TYPE_PRECISION_BYTES)];
            var intIndexEnd = bytArray.Length - 1;

            for (var intIndex = 0; intIndex <= intIndexEnd; intIndex += DATA_TYPE_PRECISION_BYTES)
            {
                // I'm not sure if I've got Little and Big endian correct or not in the following If statement
                // What I do know is that mzXML works with what I'm calling emBigEndian
                // and mzData works with what I'm calling emLittleEndian
                if (eEndianMode == eEndianTypeConstants.LittleEndian)
                {
                    // Do not swap bytes
                    Array.Copy(bytArray, intIndex, bytArrayOneValue, 0, DATA_TYPE_PRECISION_BYTES);
                }
                else
                {
                    // eEndianTypeConstants.BigEndian
                    // Swap bytes before converting from DATA_TYPE_PRECISION_BYTES bits to one 16-bit integer
                    bytArrayOneValue[0] = bytArray[intIndex + 1];
                    bytArrayOneValue[1] = bytArray[intIndex + 0];
                }

                dataArray[(int)Math.Round(intIndex / (double)DATA_TYPE_PRECISION_BYTES)] = BitConverter.ToInt16(bytArrayOneValue, 0);
            }

            return true;
        }

        /// <summary>
        /// Extracts an array of 32-bit integers from a base-64 encoded string
        /// </summary>
        /// <param name="strBase64EncodedText"></param>
        /// <param name="dataArray"></param>
        /// <param name="zLibCompressed"></param>
        /// <param name="eEndianMode"></param>
        /// <returns>True if successful, raises an exception if an error</returns>
        public static bool DecodeNumericArray(string strBase64EncodedText, out int[] dataArray, bool zLibCompressed, eEndianTypeConstants eEndianMode = eEndianTypeConstants.LittleEndian)
        {
            const int DATA_TYPE_PRECISION_BYTES = 4;
            byte[] bytArray;
            var bytArrayOneValue = new byte[4];
            string conversionSource;

            if (zLibCompressed)
            {
                conversionSource = "DecompressZLib";
                bytArray = DecompressZLib(strBase64EncodedText);
            }
            else
            {
                conversionSource = "Convert.FromBase64String";
                bytArray = Convert.FromBase64String(strBase64EncodedText);
            }

            if (bytArray.Length % DATA_TYPE_PRECISION_BYTES != 0)
            {
                throw new Exception("Array length of the Byte Array returned by " + conversionSource + " is not divisible by " + DATA_TYPE_PRECISION_BYTES + " bytes;" + " not the correct length for an encoded array of 32-bit integers");
            }

            dataArray = new int[(int)Math.Round(bytArray.Length / (double)DATA_TYPE_PRECISION_BYTES)];
            var intIndexEnd = bytArray.Length - 1;

            for (var intIndex = 0; intIndex <= intIndexEnd; intIndex += DATA_TYPE_PRECISION_BYTES)
            {
                // I'm not sure if I've got Little and Big endian correct or not in the following If statement
                // What I do know is that mzXML works with what I'm calling emBigEndian
                // and mzData works with what I'm calling emLittleEndian
                if (eEndianMode == eEndianTypeConstants.LittleEndian)
                {
                    // Do not swap bytes
                    Array.Copy(bytArray, intIndex, bytArrayOneValue, 0, DATA_TYPE_PRECISION_BYTES);
                }
                else
                {
                    // eEndianTypeConstants.BigEndian
                    // Swap bytes before converting from DATA_TYPE_PRECISION_BYTES bits to one 32-bit integer
                    bytArrayOneValue[0] = bytArray[intIndex + 3];
                    bytArrayOneValue[1] = bytArray[intIndex + 2];
                    bytArrayOneValue[2] = bytArray[intIndex + 1];
                    bytArrayOneValue[3] = bytArray[intIndex + 0];
                }

                dataArray[(int)Math.Round(intIndex / (double)DATA_TYPE_PRECISION_BYTES)] = BitConverter.ToInt32(bytArrayOneValue, 0);
            }

            return true;
        }

        /// <summary>
        /// Extracts an array of Singles from a base-64 encoded string
        /// </summary>
        /// <param name="strBase64EncodedText"></param>
        /// <param name="dataArray"></param>
        /// <param name="zLibCompressed"></param>
        /// <param name="eEndianMode"></param>
        /// <returns>True if successful, raises an exception if an error</returns>
        public static bool DecodeNumericArray(
            string strBase64EncodedText,
            out float[] dataArray,
            bool zLibCompressed,
            eEndianTypeConstants eEndianMode = eEndianTypeConstants.LittleEndian)
        {
            const int DATA_TYPE_PRECISION_BYTES = 4;
            byte[] bytArray;
            var bytArrayOneValue = new byte[4];
            string conversionSource;

            if (zLibCompressed)
            {
                conversionSource = "DecompressZLib";
                bytArray = DecompressZLib(strBase64EncodedText);
            }
            else
            {
                conversionSource = "Convert.FromBase64String";
                bytArray = Convert.FromBase64String(strBase64EncodedText);
            }

            if (bytArray.Length % DATA_TYPE_PRECISION_BYTES != 0)
            {
                throw new Exception("Array length of the Byte Array returned by " + conversionSource + " is not divisible by " + DATA_TYPE_PRECISION_BYTES + " bytes;" + " not the correct length for an encoded array of floats (aka singles)");
            }

            dataArray = new float[(int)Math.Round(bytArray.Length / (double)DATA_TYPE_PRECISION_BYTES)];
            var intIndexEnd = bytArray.Length - 1;

            for (var intIndex = 0; intIndex <= intIndexEnd; intIndex += DATA_TYPE_PRECISION_BYTES)
            {
                // I'm not sure if I've got Little and Big endian correct or not in the following If statement
                // What I do know is that mzXML works with what I'm calling emBigEndian
                // and mzData works with what I'm calling emLittleEndian
                if (eEndianMode == eEndianTypeConstants.LittleEndian)
                {
                    // Do not swap bytes
                    Array.Copy(bytArray, intIndex, bytArrayOneValue, 0, DATA_TYPE_PRECISION_BYTES);
                }
                else
                {
                    // eEndianTypeConstants.BigEndian
                    // Swap bytes before converting from DATA_TYPE_PRECISION_BYTES bits to one single
                    bytArrayOneValue[0] = bytArray[intIndex + 3];
                    bytArrayOneValue[1] = bytArray[intIndex + 2];
                    bytArrayOneValue[2] = bytArray[intIndex + 1];
                    bytArrayOneValue[3] = bytArray[intIndex + 0];
                }

                dataArray[(int)Math.Round(intIndex / (double)DATA_TYPE_PRECISION_BYTES)] = BitConverter.ToSingle(bytArrayOneValue, 0);
            }

            return true;
        }

        /// <summary>
        /// Extracts an array of Doubles from a base-64 encoded string
        /// </summary>
        /// <param name="strBase64EncodedText"></param>
        /// <param name="dataArray"></param>
        /// <param name="zLibCompressed"></param>
        /// <param name="eEndianMode"></param>
        /// <returns>True if successful, raises an exception if an error</returns>
        public static bool DecodeNumericArray(string strBase64EncodedText, out double[] dataArray, bool zLibCompressed, eEndianTypeConstants eEndianMode = eEndianTypeConstants.LittleEndian)
        {
            const int DATA_TYPE_PRECISION_BYTES = 8;
            byte[] bytArray;
            var bytArrayOneValue = new byte[8];
            string conversionSource;

            if (zLibCompressed)
            {
                conversionSource = "DecompressZLib";
                bytArray = DecompressZLib(strBase64EncodedText);
            }
            else
            {
                conversionSource = "Convert.FromBase64String";
                bytArray = Convert.FromBase64String(strBase64EncodedText);
            }

            if (bytArray.Length % DATA_TYPE_PRECISION_BYTES != 0)
            {
                throw new Exception("Array length of the Byte Array returned by " + conversionSource + " is not divisible by " + DATA_TYPE_PRECISION_BYTES + " bytes;" + " not the correct length for an encoded array of doubles");
            }

            dataArray = new double[(int)Math.Round(bytArray.Length / (double)DATA_TYPE_PRECISION_BYTES)];
            var intIndexEnd = bytArray.Length - 1;

            for (var intIndex = 0; intIndex <= intIndexEnd; intIndex += DATA_TYPE_PRECISION_BYTES)
            {
                // I'm not sure if I've got Little and Big endian correct or not in the following If statement
                // What I do know is that mzXML works with what I'm calling emBigEndian
                // and mzData works with what I'm calling emLittleEndian
                if (eEndianMode == eEndianTypeConstants.LittleEndian)
                {
                    // Do not swap bytes
                    Array.Copy(bytArray, intIndex, bytArrayOneValue, 0, DATA_TYPE_PRECISION_BYTES);
                }
                else
                {
                    // eEndianTypeConstants.BigEndian
                    // Swap bytes before converting from DATA_TYPE_PRECISION_BYTES bits to one double
                    bytArrayOneValue[0] = bytArray[intIndex + 7];
                    bytArrayOneValue[1] = bytArray[intIndex + 6];
                    bytArrayOneValue[2] = bytArray[intIndex + 5];
                    bytArrayOneValue[3] = bytArray[intIndex + 4];
                    bytArrayOneValue[4] = bytArray[intIndex + 3];
                    bytArrayOneValue[5] = bytArray[intIndex + 2];
                    bytArrayOneValue[6] = bytArray[intIndex + 1];
                    bytArrayOneValue[7] = bytArray[intIndex];
                }

                dataArray[(int)Math.Round(intIndex / (double)DATA_TYPE_PRECISION_BYTES)] = BitConverter.ToDouble(bytArrayOneValue, 0);
            }

            return true;
        }

        protected static byte[] DecompressZLib(string strBase64EncodedText)
        {
            var msCompressed = new MemoryStream(Convert.FromBase64String(strBase64EncodedText));
            var msInflated = new MemoryStream(strBase64EncodedText.Length * 2);

            // We must skip the first two bytes
            // See http://george.chiramattel.com/blog/2007/09/deflatestream-block-length-does-not-match.html
            msCompressed.ReadByte();
            msCompressed.ReadByte();

            using (var inflater = new System.IO.Compression.DeflateStream(msCompressed, System.IO.Compression.CompressionMode.Decompress))
            {
                var bytBuffer = new byte[4096];

                while (inflater.CanRead)
                {
                    var intBytesRead = inflater.Read(bytBuffer, 0, bytBuffer.Length);

                    if (intBytesRead == 0)
                        break;
                    msInflated.Write(bytBuffer, 0, intBytesRead);
                }

                msInflated.Seek(0L, SeekOrigin.Begin);
            }

            byte[] bytArray;
            var intTotalBytesDecompressed = (int)msInflated.Length;

            if (intTotalBytesDecompressed > 0)
            {
                bytArray = new byte[intTotalBytesDecompressed];
                msInflated.Read(bytArray, 0, intTotalBytesDecompressed);
            }
            else
            {
                bytArray = Array.Empty<byte>();
            }

            return bytArray;
        }

        /// <summary>
        /// Converts an array of Bytes to a base-64 encoded string
        /// </summary>
        /// <param name="dataArray"></param>
        /// <param name="intPrecisionBitsReturn">Output: Bits of precision</param>
        /// <param name="strDataTypeNameReturn">Output: Data type name</param>
        /// <param name="removeTrailingPaddingChars"></param>
        /// <returns>Base-64 encoded string</returns>
        public static string EncodeNumericArray(byte[] dataArray, out int intPrecisionBitsReturn, out string strDataTypeNameReturn, bool removeTrailingPaddingChars = false)
        {
            const int DATA_TYPE_PRECISION_BYTES = 1;
            const string DATA_TYPE_NAME = "byte";
            intPrecisionBitsReturn = DATA_TYPE_PRECISION_BYTES * 8;
            strDataTypeNameReturn = DATA_TYPE_NAME;

            if (dataArray is null || dataArray.Length == 0)
            {
                return string.Empty;
            }

            return B64encode(dataArray, removeTrailingPaddingChars);
        }

        /// <summary>
        /// Converts an array of 16-bit integers to a base-64 encoded string
        /// In addition, returns the bits of precision and data type name for the given data type
        /// </summary>
        /// <param name="dataArray"></param>
        /// <param name="intPrecisionBitsReturn">Output: Bits of precision</param>
        /// <param name="strDataTypeNameReturn">Output: Data type name</param>
        /// <param name="removeTrailingPaddingChars"></param>
        /// <param name="eEndianMode"></param>
        /// <returns>Base-64 encoded string</returns>
        public static string EncodeNumericArray(short[] dataArray, out int intPrecisionBitsReturn, out string strDataTypeNameReturn, bool removeTrailingPaddingChars = false, eEndianTypeConstants eEndianMode = eEndianTypeConstants.LittleEndian)
        {
            const int DATA_TYPE_PRECISION_BYTES = 2;
            const string DATA_TYPE_NAME = "int";

            intPrecisionBitsReturn = DATA_TYPE_PRECISION_BYTES * 8;
            strDataTypeNameReturn = DATA_TYPE_NAME;

            if (dataArray is null || dataArray.Length == 0)
            {
                return string.Empty;
            }

            var bytArray = new byte[dataArray.Length * DATA_TYPE_PRECISION_BYTES];
            var intIndexEnd = dataArray.Length - 1;

            for (var intIndex = 0; intIndex <= intIndexEnd; intIndex++)
            {
                var intBaseIndex = intIndex * DATA_TYPE_PRECISION_BYTES;
                var bytNewBytes = BitConverter.GetBytes(dataArray[intIndex]);

                // I'm not sure if I've got Little and Big endian correct or not in the following If statement
                // What I do know is that mzXML works with what I'm calling emBigEndian
                // and mzData works with what I'm calling emLittleEndian
                if (eEndianMode == eEndianTypeConstants.LittleEndian)
                {
                    // Do not swap bytes
                    Array.Copy(bytNewBytes, 0, bytArray, intBaseIndex, DATA_TYPE_PRECISION_BYTES);
                }
                else
                {
                    // eEndianTypeConstants.BigEndian
                    // Swap bytes when copying into bytArray
                    bytArray[intBaseIndex + 0] = bytNewBytes[1];
                    bytArray[intBaseIndex + 1] = bytNewBytes[0];
                }
            }

            return B64encode(bytArray, removeTrailingPaddingChars);
        }

        /// <summary>
        /// Converts an array of 32-bit integers to a base-64 encoded string
        /// </summary>
        /// <param name="dataArray"></param>
        /// <param name="intPrecisionBitsReturn">Output: Bits of precision</param>
        /// <param name="strDataTypeNameReturn">Output: Data type name</param>
        /// <param name="removeTrailingPaddingChars"></param>
        /// <param name="eEndianMode"></param>
        /// <returns>Base-64 encoded string</returns>
        public static string EncodeNumericArray(int[] dataArray, out int intPrecisionBitsReturn, out string strDataTypeNameReturn, bool removeTrailingPaddingChars = false, eEndianTypeConstants eEndianMode = eEndianTypeConstants.LittleEndian)
        {
            const int DATA_TYPE_PRECISION_BYTES = 4;
            const string DATA_TYPE_NAME = "int";
            intPrecisionBitsReturn = DATA_TYPE_PRECISION_BYTES * 8;
            strDataTypeNameReturn = DATA_TYPE_NAME;

            if (dataArray is null || dataArray.Length == 0)
            {
                return string.Empty;
            }

            var bytArray = new byte[dataArray.Length * DATA_TYPE_PRECISION_BYTES];
            var intIndexEnd = dataArray.Length - 1;

            for (var intIndex = 0; intIndex <= intIndexEnd; intIndex++)
            {
                var intBaseIndex = intIndex * DATA_TYPE_PRECISION_BYTES;
                var bytNewBytes = BitConverter.GetBytes(dataArray[intIndex]);

                // I'm not sure if I've got Little and Big endian correct or not in the following If statement
                // What I do know is that mzXML works with what I'm calling emBigEndian
                // and mzData works with what I'm calling emLittleEndian
                if (eEndianMode == eEndianTypeConstants.LittleEndian)
                {
                    // Do not swap bytes
                    Array.Copy(bytNewBytes, 0, bytArray, intBaseIndex, DATA_TYPE_PRECISION_BYTES);
                }
                else
                {
                    // eEndianTypeConstants.BigEndian
                    // Swap bytes when copying into bytArray
                    bytArray[intBaseIndex + 0] = bytNewBytes[3];
                    bytArray[intBaseIndex + 1] = bytNewBytes[2];
                    bytArray[intBaseIndex + 2] = bytNewBytes[1];
                    bytArray[intBaseIndex + 3] = bytNewBytes[0];
                }
            }

            return B64encode(bytArray, removeTrailingPaddingChars);
        }

        /// <summary>
        /// Converts an array of singles (floats) to a base-64 encoded string
        /// </summary>
        /// <param name="dataArray"></param>
        /// <param name="intPrecisionBitsReturn">Output: Bits of precision</param>
        /// <param name="strDataTypeNameReturn">Output: Data type name</param>
        /// <param name="removeTrailingPaddingChars"></param>
        /// <param name="eEndianMode"></param>
        /// <returns>Base-64 encoded string</returns>
        public static string EncodeNumericArray(float[] dataArray, out int intPrecisionBitsReturn, out string strDataTypeNameReturn, bool removeTrailingPaddingChars = false, eEndianTypeConstants eEndianMode = eEndianTypeConstants.LittleEndian)
        {
            const int DATA_TYPE_PRECISION_BYTES = 4;
            const string DATA_TYPE_NAME = "float";
            intPrecisionBitsReturn = DATA_TYPE_PRECISION_BYTES * 8;
            strDataTypeNameReturn = DATA_TYPE_NAME;

            if (dataArray is null || dataArray.Length == 0)
            {
                return string.Empty;
            }

            var bytArray = new byte[dataArray.Length * DATA_TYPE_PRECISION_BYTES];
            var intIndexEnd = dataArray.Length - 1;

            for (var intIndex = 0; intIndex <= intIndexEnd; intIndex++)
            {
                var intBaseIndex = intIndex * DATA_TYPE_PRECISION_BYTES;
                var bytNewBytes = BitConverter.GetBytes(dataArray[intIndex]);

                // I'm not sure if I've got Little and Big endian correct or not in the following If statement
                // What I do know is that mzXML works with what I'm calling emBigEndian
                // and mzData works with what I'm calling emLittleEndian
                if (eEndianMode == eEndianTypeConstants.LittleEndian)
                {
                    // Do not swap bytes
                    Array.Copy(bytNewBytes, 0, bytArray, intBaseIndex, DATA_TYPE_PRECISION_BYTES);
                }
                else
                {
                    // eEndianTypeConstants.BigEndian
                    // Swap bytes when copying into bytArray
                    bytArray[intBaseIndex + 0] = bytNewBytes[3];
                    bytArray[intBaseIndex + 1] = bytNewBytes[2];
                    bytArray[intBaseIndex + 2] = bytNewBytes[1];
                    bytArray[intBaseIndex + 3] = bytNewBytes[0];
                }
            }

            return B64encode(bytArray, removeTrailingPaddingChars);
        }

        /// <summary>
        /// Converts an array of doubles to a base-64 encoded string
        /// </summary>
        /// <param name="dataArray"></param>
        /// <param name="intPrecisionBitsReturn">Output: Bits of precision</param>
        /// <param name="strDataTypeNameReturn">Output: Data type name</param>
        /// <param name="removeTrailingPaddingChars"></param>
        /// <param name="eEndianMode"></param>
        /// <returns>Base-64 encoded string</returns>
        public static string EncodeNumericArray(double[] dataArray, out int intPrecisionBitsReturn, out string strDataTypeNameReturn, bool removeTrailingPaddingChars = false, eEndianTypeConstants eEndianMode = eEndianTypeConstants.LittleEndian)
        {
            const int DATA_TYPE_PRECISION_BYTES = 8;
            const string DATA_TYPE_NAME = "float";
            intPrecisionBitsReturn = DATA_TYPE_PRECISION_BYTES * 8;
            strDataTypeNameReturn = DATA_TYPE_NAME;

            if (dataArray is null || dataArray.Length == 0)
            {
                return string.Empty;
            }

            var bytArray = new byte[dataArray.Length * DATA_TYPE_PRECISION_BYTES];
            var intIndexEnd = dataArray.Length - 1;

            for (var intIndex = 0; intIndex <= intIndexEnd; intIndex++)
            {
                var intBaseIndex = intIndex * DATA_TYPE_PRECISION_BYTES;
                var bytNewBytes = BitConverter.GetBytes(dataArray[intIndex]);

                // I'm not sure if I've got Little and Big endian correct or not in the following If statement
                // What I do know is that mzXML works with what I'm calling emBigEndian
                // and mzData works with what I'm calling emLittleEndian
                if (eEndianMode == eEndianTypeConstants.LittleEndian)
                {
                    // Do not swap bytes
                    Array.Copy(bytNewBytes, 0, bytArray, intBaseIndex, DATA_TYPE_PRECISION_BYTES);
                }
                else
                {
                    // eEndianTypeConstants.BigEndian
                    // Swap bytes when copying into bytArray
                    bytArray[intBaseIndex + 0] = bytNewBytes[7];
                    bytArray[intBaseIndex + 1] = bytNewBytes[6];
                    bytArray[intBaseIndex + 2] = bytNewBytes[5];
                    bytArray[intBaseIndex + 3] = bytNewBytes[4];
                    bytArray[intBaseIndex + 4] = bytNewBytes[3];
                    bytArray[intBaseIndex + 5] = bytNewBytes[2];
                    bytArray[intBaseIndex + 6] = bytNewBytes[1];
                    bytArray[intBaseIndex + 7] = bytNewBytes[0];
                }
            }

            return B64encode(bytArray, removeTrailingPaddingChars);
        }
    }
}