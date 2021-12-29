namespace MSDataFileReader
{
    public class IndexedSpectrumInfo
    {
        public int ScanNumber { get; }

        /// <summary>
        /// Spectrum ID
        /// </summary>
        /// <remarks>
        /// Only used by mzData files
        /// </remarks>
        public int SpectrumID { get; set; }

        public long ByteOffsetStart { get; }

        public long ByteOffsetEnd { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        public IndexedSpectrumInfo(int scanNumber, long byteOffsetStart, long byteOffsetEnd)
        {
            ScanNumber = scanNumber;
            ByteOffsetStart = byteOffsetStart;
            ByteOffsetEnd = byteOffsetEnd;
        }

        public override string ToString()
        {
            return "Scan " + ScanNumber + ", bytes " + ByteOffsetStart + " to " + ByteOffsetEnd;
        }
    }
}
