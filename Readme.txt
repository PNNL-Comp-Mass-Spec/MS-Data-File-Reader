The MsDataFileReader DLL is a VB.NET DLL that can be used to read 
mass spectrum data from four file formats:
 * mzXML
 * mzData
 * Concatenated .Dta files (_dta.txt)
 * Mascot Generic Format files (.mgf)

The mzXML and mzData file reader classes can be used to read the files in a 
forward-only fashion, or they can pre-cache the location of each spectrum's 
information in the source file to allow for random access.  Key classes are:

* clsDtaTextFileReader
Reads spectra from concatenated .dta files (_dta.txt format).
This is a forward-only reader, returning one spectrum at a time.

* clsMGFFileReader
Reads spectra from Mascot Generic Format (.mgf) files
This is a forward-only reader.

* clsMzXMLFileReader
Uses a SAX Parser to read an mzXML file (.mzXML or _mzXML.xml).
This is a forward-only reader.

* clsMzDataFileReader
Uses a SAX Parser to read an mzData file (.mzData or _mzData.xml).
This is a forward-only reader.

* clsMzXMLFileAccessor
Opens an mzXML file and indexes the location of each of the spectra present.  
This does not cache the mass spectra data in memory, and therefore uses 
little memory, but once the indexing is complete, random access to the 
spectra is possible.  After the indexing is complete, spectra can be 
obtained using GetSpectrumByScanNumber or GetSpectrumByIndex

* clsMzDataFileAccessor
Similar to clsMzXMLFileAccessor, but used for reading mzData files

* clsSpectrumInfo
Class used to store details of each mass spectrum

* clsBase64EncodeDecode
Class for decoding and encoding binary 64 data, as required for mzXML and mzData

* clsBinaryTextReader
Class that can be used to open a Text file and read each of the lines from the 
file, where a line of text ends with CRLF or simply LF.  In addition, the byte 
offset at the start and end of the line is also returned.  The user can then
use these byte offsets to jump to a specific line in a file, allowing for 
random access.  Note that this class is compatible with UTF-16 Unicode files; 
it looks for byte order mark FF FE or FE FF in the first two bytes of the file 
to determine if a file is Unicode (though you can override this using the 
InputFileEncoding property after calling .OpenFile().  This class will also 
look for the byte order mark for UTF-8 files (EF BB BF) though it may not 
properly decode UTF-8 characters (not fully tested).

See Test_MSDataFileReader\Test_MSDataFileReader.sln for example code 
demonstrating the use of the various classes.

-------------------------------------------------------------------------------
Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
Copyright 2006, Battelle Memorial Institute.  All Rights Reserved.

E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov
Website: http://panomics.pnnl.gov/ or http://omics.pnl.gov
-------------------------------------------------------------------------------

Licensed under the Apache License, Version 2.0; you may not use this file except 
in compliance with the License.  You may obtain a copy of the License at 
http://www.apache.org/licenses/LICENSE-2.0

All publications that result from the use of this software should include 
the following acknowledgment statement:
 Portions of this research were supported by the W.R. Wiley Environmental 
 Molecular Science Laboratory, a national scientific user facility sponsored 
 by the U.S. Department of Energy's Office of Biological and Environmental 
 Research and located at PNNL.  PNNL is operated by Battelle Memorial Institute 
 for the U.S. Department of Energy under contract DE-AC05-76RL0 1830.

Notice: This computer software was prepared by Battelle Memorial Institute, 
hereinafter the Contractor, under Contract No. DE-AC05-76RL0 1830 with the 
Department of Energy (DOE).  All rights in the computer software are reserved 
by DOE on behalf of the United States Government and the Contractor as 
provided in the Contract.  NEITHER THE GOVERNMENT NOR THE CONTRACTOR MAKES ANY 
WARRANTY, EXPRESS OR IMPLIED, OR ASSUMES ANY LIABILITY FOR THE USE OF THIS 
SOFTWARE.  This notice including this sentence must appear on any copies of 
this computer software.
