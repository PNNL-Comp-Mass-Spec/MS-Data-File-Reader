using System;
using System.IO;
using PRISM;

// ReSharper disable UnusedMember.Global

namespace MSDataFileReader
{
    public class Base64EncodeDecode
    {
        // Ignore Spelling: endian

        public enum EndianType
        {
            LittleEndian = 0,
            BigEndian = 1
        }

        private static string B64encode(byte[] array, bool removeTrailingPaddingChars = false)
        {
            return removeTrailingPaddingChars
                ? Convert.ToBase64String(array).TrimEnd('=')
                : Convert.ToBase64String(array);
        }

        /// <summary>
        /// Extracts an array of Bytes from a base-64 encoded string
        /// </summary>
        /// <param name="base64EncodedText"></param>
        /// <param name="dataArray"></param>
        /// <returns>True if successful, raises an exception if an error</returns>
        public static bool DecodeNumericArray(string base64EncodedText, out byte[] dataArray)
        {
            dataArray = Convert.FromBase64String(base64EncodedText);
            return true;
        }

        /// <summary>
        /// Extracts an array of 16-bit integers from a base-64 encoded string
        /// </summary>
        /// <param name="base64EncodedText"></param>
        /// <param name="dataArray"></param>
        /// <param name="zLibCompressed"></param>
        /// <param name="endianMode"></param>
        /// <returns>True if successful, raises an exception if an error</returns>
        public static bool DecodeNumericArray(string base64EncodedText, out short[] dataArray, bool zLibCompressed, EndianType endianMode = EndianType.LittleEndian)
        {
            const int DATA_TYPE_PRECISION_BYTES = 2;
            byte[] byteArray;
            var byteArrayOneValue = new byte[2];
            string conversionSource;

            if (zLibCompressed)
            {
                conversionSource = "DecompressZLib";
                byteArray = DecompressZLib(base64EncodedText);
            }
            else
            {
                conversionSource = "Convert.FromBase64String";
                byteArray = Convert.FromBase64String(base64EncodedText);
            }

            if (byteArray.Length % DATA_TYPE_PRECISION_BYTES != 0)
            {
                throw new Exception(string.Format(
                    "Array length of the Byte Array returned by {0} is not divisible by {1} bytes; not the correct length for an encoded array of 16-bit integers",
                    conversionSource, DATA_TYPE_PRECISION_BYTES));
            }

            dataArray = new short[(int)Math.Round(byteArray.Length / (double)DATA_TYPE_PRECISION_BYTES)];
            var indexEnd = byteArray.Length - 1;

            for (var index = 0; index <= indexEnd; index += DATA_TYPE_PRECISION_BYTES)
            {
                // I'm not sure if I've got Little and Big endian correct or not in the following If statement
                // What I do know is that mzXML works with what I'm calling emBigEndian
                // and mzData works with what I'm calling emLittleEndian
                if (endianMode == EndianType.LittleEndian)
                {
                    // Do not swap bytes
                    Array.Copy(byteArray, index, byteArrayOneValue, 0, DATA_TYPE_PRECISION_BYTES);
                }
                else
                {
                    // EndianType.BigEndian
                    // Swap bytes before converting from DATA_TYPE_PRECISION_BYTES bits to one 16-bit integer
                    byteArrayOneValue[0] = byteArray[index + 1];
                    byteArrayOneValue[1] = byteArray[index + 0];
                }

                dataArray[(int)Math.Round(index / (double)DATA_TYPE_PRECISION_BYTES)] = BitConverter.ToInt16(byteArrayOneValue, 0);
            }

            return true;
        }

        /// <summary>
        /// Extracts an array of 32-bit integers from a base-64 encoded string
        /// </summary>
        /// <param name="base64EncodedText"></param>
        /// <param name="dataArray"></param>
        /// <param name="zLibCompressed"></param>
        /// <param name="endianMode"></param>
        /// <returns>True if successful, raises an exception if an error</returns>
        public static bool DecodeNumericArray(string base64EncodedText, out int[] dataArray, bool zLibCompressed, EndianType endianMode = EndianType.LittleEndian)
        {
            const int DATA_TYPE_PRECISION_BYTES = 4;
            byte[] byteArray;
            var byteArrayOneValue = new byte[4];
            string conversionSource;

            if (zLibCompressed)
            {
                conversionSource = "DecompressZLib";
                byteArray = DecompressZLib(base64EncodedText);
            }
            else
            {
                conversionSource = "Convert.FromBase64String";
                byteArray = Convert.FromBase64String(base64EncodedText);
            }

            if (byteArray.Length % DATA_TYPE_PRECISION_BYTES != 0)
            {
                throw new Exception(string.Format(
                    "Array length of the Byte Array returned by {0} is not divisible by {1} bytes; not the correct length for an encoded array of 32-bit integers",
                    conversionSource, DATA_TYPE_PRECISION_BYTES));
            }

            dataArray = new int[(int)Math.Round(byteArray.Length / (double)DATA_TYPE_PRECISION_BYTES)];
            var indexEnd = byteArray.Length - 1;

            for (var index = 0; index <= indexEnd; index += DATA_TYPE_PRECISION_BYTES)
            {
                // I'm not sure if I've got Little and Big endian correct or not in the following If statement
                // What I do know is that mzXML works with what I'm calling emBigEndian
                // and mzData works with what I'm calling emLittleEndian
                if (endianMode == EndianType.LittleEndian)
                {
                    // Do not swap bytes
                    Array.Copy(byteArray, index, byteArrayOneValue, 0, DATA_TYPE_PRECISION_BYTES);
                }
                else
                {
                    // EndianType.BigEndian
                    // Swap bytes before converting from DATA_TYPE_PRECISION_BYTES bits to one 32-bit integer
                    byteArrayOneValue[0] = byteArray[index + 3];
                    byteArrayOneValue[1] = byteArray[index + 2];
                    byteArrayOneValue[2] = byteArray[index + 1];
                    byteArrayOneValue[3] = byteArray[index + 0];
                }

                dataArray[(int)Math.Round(index / (double)DATA_TYPE_PRECISION_BYTES)] = BitConverter.ToInt32(byteArrayOneValue, 0);
            }

            return true;
        }

        /// <summary>
        /// Extracts an array of Singles from a base-64 encoded string
        /// </summary>
        /// <param name="base64EncodedText"></param>
        /// <param name="dataArray"></param>
        /// <param name="zLibCompressed"></param>
        /// <param name="endianMode"></param>
        /// <returns>True if successful, raises an exception if an error</returns>
        public static bool DecodeNumericArray(
            string base64EncodedText,
            out float[] dataArray,
            bool zLibCompressed,
            EndianType endianMode = EndianType.LittleEndian)
        {
            const int DATA_TYPE_PRECISION_BYTES = 4;
            byte[] byteArray;
            var byteArrayOneValue = new byte[4];
            string conversionSource;

            if (zLibCompressed)
            {
                conversionSource = "DecompressZLib";
                byteArray = DecompressZLib(base64EncodedText);
            }
            else
            {
                conversionSource = "Convert.FromBase64String";
                byteArray = Convert.FromBase64String(base64EncodedText);
            }

            if (byteArray.Length % DATA_TYPE_PRECISION_BYTES != 0)
            {
                throw new Exception(string.Format(
                    "Array length of the Byte Array returned by {0} is not divisible by {1} bytes; not the correct length for an encoded array of floats",
                    conversionSource, DATA_TYPE_PRECISION_BYTES));
            }

            dataArray = new float[(int)Math.Round(byteArray.Length / (double)DATA_TYPE_PRECISION_BYTES)];
            var indexEnd = byteArray.Length - 1;

            for (var index = 0; index <= indexEnd; index += DATA_TYPE_PRECISION_BYTES)
            {
                // I'm not sure if I've got Little and Big endian correct or not in the following If statement
                // What I do know is that mzXML works with what I'm calling emBigEndian
                // and mzData works with what I'm calling emLittleEndian
                if (endianMode == EndianType.LittleEndian)
                {
                    // Do not swap bytes
                    Array.Copy(byteArray, index, byteArrayOneValue, 0, DATA_TYPE_PRECISION_BYTES);
                }
                else
                {
                    // EndianType.BigEndian
                    // Swap bytes before converting from DATA_TYPE_PRECISION_BYTES bits to one single
                    byteArrayOneValue[0] = byteArray[index + 3];
                    byteArrayOneValue[1] = byteArray[index + 2];
                    byteArrayOneValue[2] = byteArray[index + 1];
                    byteArrayOneValue[3] = byteArray[index + 0];
                }

                dataArray[(int)Math.Round(index / (double)DATA_TYPE_PRECISION_BYTES)] = BitConverter.ToSingle(byteArrayOneValue, 0);
            }

            return true;
        }

        /// <summary>
        /// Extracts an array of Doubles from a base-64 encoded string
        /// </summary>
        /// <param name="base64EncodedText"></param>
        /// <param name="dataArray"></param>
        /// <param name="zLibCompressed"></param>
        /// <param name="endianMode"></param>
        /// <returns>True if successful, raises an exception if an error</returns>
        public static bool DecodeNumericArray(string base64EncodedText, out double[] dataArray, bool zLibCompressed, EndianType endianMode = EndianType.LittleEndian)
        {
            const int DATA_TYPE_PRECISION_BYTES = 8;
            byte[] byteArray;
            var byteArrayOneValue = new byte[8];
            string conversionSource;

            if (zLibCompressed)
            {
                conversionSource = "DecompressZLib";
                byteArray = DecompressZLib(base64EncodedText);
            }
            else
            {
                conversionSource = "Convert.FromBase64String";
                byteArray = Convert.FromBase64String(base64EncodedText);
            }

            if (byteArray.Length % DATA_TYPE_PRECISION_BYTES != 0)
            {
                throw new Exception(string.Format(
                    "Array length of the Byte Array returned by {0} is not divisible by {1} bytes; not the correct length for an encoded array of doubles",
                    conversionSource, DATA_TYPE_PRECISION_BYTES));
            }

            dataArray = new double[(int)Math.Round(byteArray.Length / (double)DATA_TYPE_PRECISION_BYTES)];
            var indexEnd = byteArray.Length - 1;

            for (var index = 0; index <= indexEnd; index += DATA_TYPE_PRECISION_BYTES)
            {
                // I'm not sure if I've got Little and Big endian correct or not in the following If statement
                // What I do know is that mzXML works with what I'm calling emBigEndian
                // and mzData works with what I'm calling emLittleEndian
                if (endianMode == EndianType.LittleEndian)
                {
                    // Do not swap bytes
                    Array.Copy(byteArray, index, byteArrayOneValue, 0, DATA_TYPE_PRECISION_BYTES);
                }
                else
                {
                    // EndianType.BigEndian
                    // Swap bytes before converting from DATA_TYPE_PRECISION_BYTES bits to one double
                    byteArrayOneValue[0] = byteArray[index + 7];
                    byteArrayOneValue[1] = byteArray[index + 6];
                    byteArrayOneValue[2] = byteArray[index + 5];
                    byteArrayOneValue[3] = byteArray[index + 4];
                    byteArrayOneValue[4] = byteArray[index + 3];
                    byteArrayOneValue[5] = byteArray[index + 2];
                    byteArrayOneValue[6] = byteArray[index + 1];
                    byteArrayOneValue[7] = byteArray[index];
                }

                dataArray[(int)Math.Round(index / (double)DATA_TYPE_PRECISION_BYTES)] = BitConverter.ToDouble(byteArrayOneValue, 0);
            }

            return true;
        }

        protected static byte[] DecompressZLib(string base64EncodedText)
        {
            var msCompressed = new MemoryStream(Convert.FromBase64String(base64EncodedText));
            var msInflated = new MemoryStream(base64EncodedText.Length * 2);

            // We must skip the first two bytes
            // See http://george.chiramattel.com/blog/2007/09/deflatestream-block-length-does-not-match.html
            msCompressed.ReadByte();
            msCompressed.ReadByte();

            using (var inflater = new System.IO.Compression.DeflateStream(msCompressed, System.IO.Compression.CompressionMode.Decompress))
            {
                var buffer = new byte[4096];

                while (inflater.CanRead)
                {
                    var bytesRead = inflater.Read(buffer, 0, buffer.Length);

                    if (bytesRead == 0)
                        break;

                    msInflated.Write(buffer, 0, bytesRead);
                }

                msInflated.Seek(0L, SeekOrigin.Begin);
            }

            var totalBytesDecompressed = (int)msInflated.Length;

            if (totalBytesDecompressed <= 0)
                return Array.Empty<byte>();

            var byteArray = new byte[totalBytesDecompressed];
            var inflatedByteCount = msInflated.Read(byteArray, 0, totalBytesDecompressed);

            if (inflatedByteCount == totalBytesDecompressed)
            {
                return byteArray;
            }

            ConsoleMsgUtils.ShowWarning(
                "Number of bytes read from the memory stream in DecompressZLib did not match the expected value: {0} vs. {1}",
                inflatedByteCount, totalBytesDecompressed);

            var byteArray2 = new byte[inflatedByteCount];

            for (var i = 0; i < inflatedByteCount; i++)
            {
                byteArray2[i] = byteArray[i];
            }

            return byteArray2;
        }

        /// <summary>
        /// Converts an array of Bytes to a base-64 encoded string
        /// </summary>
        /// <param name="dataArray"></param>
        /// <param name="precisionBitsReturn">Output: Bits of precision</param>
        /// <param name="dataTypeNameReturn">Output: Data type name</param>
        /// <param name="removeTrailingPaddingChars"></param>
        /// <returns>Base-64 encoded string</returns>
        public static string EncodeNumericArray(byte[] dataArray, out int precisionBitsReturn, out string dataTypeNameReturn, bool removeTrailingPaddingChars = false)
        {
            const int DATA_TYPE_PRECISION_BYTES = 1;
            const string DATA_TYPE_NAME = "byte";
            precisionBitsReturn = DATA_TYPE_PRECISION_BYTES * 8;
            dataTypeNameReturn = DATA_TYPE_NAME;

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
        /// <param name="precisionBitsReturn">Output: Bits of precision</param>
        /// <param name="dataTypeNameReturn">Output: Data type name</param>
        /// <param name="removeTrailingPaddingChars"></param>
        /// <param name="endianMode"></param>
        /// <returns>Base-64 encoded string</returns>
        public static string EncodeNumericArray(short[] dataArray, out int precisionBitsReturn, out string dataTypeNameReturn, bool removeTrailingPaddingChars = false, EndianType endianMode = EndianType.LittleEndian)
        {
            const int DATA_TYPE_PRECISION_BYTES = 2;
            const string DATA_TYPE_NAME = "int";

            precisionBitsReturn = DATA_TYPE_PRECISION_BYTES * 8;
            dataTypeNameReturn = DATA_TYPE_NAME;

            if (dataArray is null || dataArray.Length == 0)
            {
                return string.Empty;
            }

            var byteArray = new byte[dataArray.Length * DATA_TYPE_PRECISION_BYTES];
            var indexEnd = dataArray.Length - 1;

            for (var index = 0; index <= indexEnd; index++)
            {
                var baseIndex = index * DATA_TYPE_PRECISION_BYTES;
                var newBytes = BitConverter.GetBytes(dataArray[index]);

                // I'm not sure if I've got Little and Big endian correct or not in the following If statement
                // What I do know is that mzXML works with what I'm calling emBigEndian
                // and mzData works with what I'm calling emLittleEndian
                if (endianMode == EndianType.LittleEndian)
                {
                    // Do not swap bytes
                    Array.Copy(newBytes, 0, byteArray, baseIndex, DATA_TYPE_PRECISION_BYTES);
                }
                else
                {
                    // EndianType.BigEndian
                    // Swap bytes when copying into byteArray
                    byteArray[baseIndex + 0] = newBytes[1];
                    byteArray[baseIndex + 1] = newBytes[0];
                }
            }

            return B64encode(byteArray, removeTrailingPaddingChars);
        }

        /// <summary>
        /// Converts an array of 32-bit integers to a base-64 encoded string
        /// </summary>
        /// <param name="dataArray"></param>
        /// <param name="precisionBitsReturn">Output: Bits of precision</param>
        /// <param name="dataTypeNameReturn">Output: Data type name</param>
        /// <param name="removeTrailingPaddingChars"></param>
        /// <param name="endianMode"></param>
        /// <returns>Base-64 encoded string</returns>
        public static string EncodeNumericArray(int[] dataArray, out int precisionBitsReturn, out string dataTypeNameReturn, bool removeTrailingPaddingChars = false, EndianType endianMode = EndianType.LittleEndian)
        {
            const int DATA_TYPE_PRECISION_BYTES = 4;
            const string DATA_TYPE_NAME = "int";
            precisionBitsReturn = DATA_TYPE_PRECISION_BYTES * 8;
            dataTypeNameReturn = DATA_TYPE_NAME;

            if (dataArray is null || dataArray.Length == 0)
            {
                return string.Empty;
            }

            var byteArray = new byte[dataArray.Length * DATA_TYPE_PRECISION_BYTES];
            var indexEnd = dataArray.Length - 1;

            for (var index = 0; index <= indexEnd; index++)
            {
                var baseIndex = index * DATA_TYPE_PRECISION_BYTES;
                var newBytes = BitConverter.GetBytes(dataArray[index]);

                // I'm not sure if I've got Little and Big endian correct or not in the following If statement
                // What I do know is that mzXML works with what I'm calling emBigEndian
                // and mzData works with what I'm calling emLittleEndian
                if (endianMode == EndianType.LittleEndian)
                {
                    // Do not swap bytes
                    Array.Copy(newBytes, 0, byteArray, baseIndex, DATA_TYPE_PRECISION_BYTES);
                }
                else
                {
                    // EndianType.BigEndian
                    // Swap bytes when copying into byteArray
                    byteArray[baseIndex + 0] = newBytes[3];
                    byteArray[baseIndex + 1] = newBytes[2];
                    byteArray[baseIndex + 2] = newBytes[1];
                    byteArray[baseIndex + 3] = newBytes[0];
                }
            }

            return B64encode(byteArray, removeTrailingPaddingChars);
        }

        /// <summary>
        /// Converts an array of singles (floats) to a base-64 encoded string
        /// </summary>
        /// <param name="dataArray"></param>
        /// <param name="precisionBitsReturn">Output: Bits of precision</param>
        /// <param name="dataTypeNameReturn">Output: Data type name</param>
        /// <param name="removeTrailingPaddingChars"></param>
        /// <param name="endianMode"></param>
        /// <returns>Base-64 encoded string</returns>
        public static string EncodeNumericArray(float[] dataArray, out int precisionBitsReturn, out string dataTypeNameReturn, bool removeTrailingPaddingChars = false, EndianType endianMode = EndianType.LittleEndian)
        {
            const int DATA_TYPE_PRECISION_BYTES = 4;
            const string DATA_TYPE_NAME = "float";
            precisionBitsReturn = DATA_TYPE_PRECISION_BYTES * 8;
            dataTypeNameReturn = DATA_TYPE_NAME;

            if (dataArray is null || dataArray.Length == 0)
            {
                return string.Empty;
            }

            var byteArray = new byte[dataArray.Length * DATA_TYPE_PRECISION_BYTES];
            var indexEnd = dataArray.Length - 1;

            for (var index = 0; index <= indexEnd; index++)
            {
                var baseIndex = index * DATA_TYPE_PRECISION_BYTES;
                var newBytes = BitConverter.GetBytes(dataArray[index]);

                // I'm not sure if I've got Little and Big endian correct or not in the following If statement
                // What I do know is that mzXML works with what I'm calling emBigEndian
                // and mzData works with what I'm calling emLittleEndian
                if (endianMode == EndianType.LittleEndian)
                {
                    // Do not swap bytes
                    Array.Copy(newBytes, 0, byteArray, baseIndex, DATA_TYPE_PRECISION_BYTES);
                }
                else
                {
                    // EndianType.BigEndian
                    // Swap bytes when copying into byteArray
                    byteArray[baseIndex + 0] = newBytes[3];
                    byteArray[baseIndex + 1] = newBytes[2];
                    byteArray[baseIndex + 2] = newBytes[1];
                    byteArray[baseIndex + 3] = newBytes[0];
                }
            }

            return B64encode(byteArray, removeTrailingPaddingChars);
        }

        /// <summary>
        /// Converts an array of doubles to a base-64 encoded string
        /// </summary>
        /// <param name="dataArray"></param>
        /// <param name="precisionBitsReturn">Output: Bits of precision</param>
        /// <param name="dataTypeNameReturn">Output: Data type name</param>
        /// <param name="removeTrailingPaddingChars"></param>
        /// <param name="endianMode"></param>
        /// <returns>Base-64 encoded string</returns>
        public static string EncodeNumericArray(double[] dataArray, out int precisionBitsReturn, out string dataTypeNameReturn, bool removeTrailingPaddingChars = false, EndianType endianMode = EndianType.LittleEndian)
        {
            const int DATA_TYPE_PRECISION_BYTES = 8;
            const string DATA_TYPE_NAME = "float";
            precisionBitsReturn = DATA_TYPE_PRECISION_BYTES * 8;
            dataTypeNameReturn = DATA_TYPE_NAME;

            if (dataArray is null || dataArray.Length == 0)
            {
                return string.Empty;
            }

            var byteArray = new byte[dataArray.Length * DATA_TYPE_PRECISION_BYTES];
            var indexEnd = dataArray.Length - 1;

            for (var index = 0; index <= indexEnd; index++)
            {
                var baseIndex = index * DATA_TYPE_PRECISION_BYTES;
                var newBytes = BitConverter.GetBytes(dataArray[index]);

                // I'm not sure if I've got Little and Big endian correct or not in the following If statement
                // What I do know is that mzXML works with what I'm calling emBigEndian
                // and mzData works with what I'm calling emLittleEndian
                if (endianMode == EndianType.LittleEndian)
                {
                    // Do not swap bytes
                    Array.Copy(newBytes, 0, byteArray, baseIndex, DATA_TYPE_PRECISION_BYTES);
                }
                else
                {
                    // EndianType.BigEndian
                    // Swap bytes when copying into byteArray
                    byteArray[baseIndex + 0] = newBytes[7];
                    byteArray[baseIndex + 1] = newBytes[6];
                    byteArray[baseIndex + 2] = newBytes[5];
                    byteArray[baseIndex + 3] = newBytes[4];
                    byteArray[baseIndex + 4] = newBytes[3];
                    byteArray[baseIndex + 5] = newBytes[2];
                    byteArray[baseIndex + 6] = newBytes[1];
                    byteArray[baseIndex + 7] = newBytes[0];
                }
            }

            return B64encode(byteArray, removeTrailingPaddingChars);
        }
    }
}