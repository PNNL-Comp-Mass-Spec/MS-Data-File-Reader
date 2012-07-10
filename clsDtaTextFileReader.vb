Option Strict On

' This class can be used to open a _Dta.txt file and return each spectrum present
'
' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2005, Battelle Memorial Institute.  All Rights Reserved.
' Started November 14, 2003
'
' E-mail: matthew.monroe@pnl.gov or matt@alchemistmatt.com
' Website: http://ncrr.pnl.gov/ or http://www.sysbio.org/resources/staff/
' -------------------------------------------------------------------------------
'
' Last modified February 16, 2012

Public Class clsDtaTextFileReader
    Inherits clsMSTextFileReaderBaseClass

    Public Sub New()
        Me.New(True)
    End Sub

    Public Sub New(ByVal blnCombineIdenticalSpectra As Boolean)
        mCombineIdenticalSpectra = blnCombineIdenticalSpectra
        InitializeLocalVariables()
    End Sub

#Region "Constants and Enums"
    ' Note: The extension must be in all caps
    Public Const DTA_TEXT_FILE_EXTENSION As String = "_DTA.TXT"

    Protected Const COMMENT_LINE_START_CHAR As Char = "="c        ' The comment character is an Equals sign
#End Region

#Region "Classwide Variables"
    Protected mCombineIdenticalSpectra As Boolean

    ' mHeaderSaved is used to store the previous header title; it is needed when the next
    '  header was read for comparison with the current scan, but it didn't match, and thus
    '  wasn't used for grouping
    Protected mHeaderSaved As String
#End Region

#Region "Processing Options and Interface Functions"
    Public Property CombineIdenticalSpectra() As Boolean
        Get
            Return mCombineIdenticalSpectra
        End Get
        Set(ByVal Value As Boolean)
            mCombineIdenticalSpectra = Value
        End Set
    End Property
#End Region

    Protected Overrides Sub InitializeLocalVariables()
        MyBase.InitializeLocalVariables()

        mCommentLineStartChar = COMMENT_LINE_START_CHAR
        mHeaderSaved = String.Empty
    End Sub

    Protected Overrides Sub LogErrors(ByVal strCallingFunction As String, ByVal strErrorDescription As String)
        MyBase.LogErrors("clsDtaTextFileReader." & strCallingFunction, strErrorDescription)
    End Sub

    Public Overrides Function ReadNextSpectrum(ByRef objSpectrumInfo As clsSpectrumInfo) As Boolean
        ' Reads the next spectrum from a _Dta.txt file
        ' Returns True if a spectrum is found, otherwise, returns False
        ' If blnCombineIdenticalSpectra = True, then combines spectra that only differ by their charge state

        Dim strLineIn As String
        Dim strMostRecentLineIn As String = String.Empty
        Dim intLastProgressUpdateLine As Integer
        Dim strCompareTitle As String

        Dim blnSpectrumFound As Boolean

        Try
            If MyBase.ReadingAndStoringSpectra OrElse mCurrentSpectrum Is Nothing Then
                mCurrentSpectrum = New clsSpectrumInfoMsMsText
            Else
                mCurrentSpectrum.Clear()
            End If
            If Not objSpectrumInfo Is Nothing Then
                mCurrentSpectrum.AutoShrinkDataLists = objSpectrumInfo.AutoShrinkDataLists
            End If

            blnSpectrumFound = False
            If srInFile Is Nothing Then
                If objSpectrumInfo Is Nothing Then
                    objSpectrumInfo = New clsSpectrumInfoMsMsText
                Else
                    objSpectrumInfo.Clear()
                End If
                mErrorMessage = "Data file not currently open"
            Else
                MyBase.AddNewRecentFileText(String.Empty, True, False)

                intLastProgressUpdateLine = mInFileLineNumber
				Do While Not blnSpectrumFound And srInFile.Peek() > -1 And Not mAbortProcessing

					If mHeaderSaved.Length > 0 Then
						strLineIn = String.Copy(mHeaderSaved)
						mHeaderSaved = String.Empty
					Else
						strLineIn = srInFile.ReadLine
						If Not strLineIn Is Nothing Then mTotalBytesRead += strLineIn.Length + 2
						mInFileLineNumber += 1
					End If

					' See if strLineIn is nothing or starts with the comment line character (equals sign)
					If Not strLineIn Is Nothing AndAlso strLineIn.Trim.StartsWith(mCommentLineStartChar) Then
						MyBase.AddNewRecentFileText(strLineIn)

						With mCurrentSpectrum
							.SpectrumTitleWithCommentChars = strLineIn
							.SpectrumTitle = MyBase.CleanupComment(strLineIn, mCommentLineStartChar, True)
							MyBase.ExtractScanInfoFromDtaHeader(.SpectrumTitle, .ScanNumber, .ScanNumberEnd, .ScanCount)

							.MSLevel = 2
							.SpectrumID = .ScanNumber
						End With

						' Read the next line, which should have the parent ion MH value and charge
						strLineIn = srInFile.ReadLine
						If Not strLineIn Is Nothing Then mTotalBytesRead += strLineIn.Length + 2
						mInFileLineNumber += 1

						If strLineIn Is Nothing OrElse strLineIn.Trim.Length = 0 Then
							' Spectrum header is not followed by a parent ion value and charge; ignore the line
						Else
							MyBase.AddNewRecentFileText(strLineIn)

							' Parse the parent ion info and read the MsMs Data
							blnSpectrumFound = ReadSingleSpectrum(srInFile, strLineIn, mCurrentMsMsDataList, mCurrentSpectrum, mInFileLineNumber, intLastProgressUpdateLine, strMostRecentLineIn)

							If blnSpectrumFound Then
								If MyBase.mReadTextDataOnly Then
									' Do not parse the text data to populate .MZList and .IntensityList
									mCurrentSpectrum.DataCount = 0
								Else
									With mCurrentSpectrum
										Try
											.DataCount = MyBase.ParseMsMsDataList(mCurrentMsMsDataList, .MZList, .IntensityList, .AutoShrinkDataLists)

											.Validate(blnComputeBasePeakAndTIC:=True, blnUpdateMZRange:=True)

										Catch ex As Exception
											.DataCount = 0
											blnSpectrumFound = False
										End Try
									End With
								End If
							End If

							If blnSpectrumFound And mCombineIdenticalSpectra And mCurrentSpectrum.ParentIonCharges(0) = 2 Then
								' See if the next spectrum is the identical data, but the charge is 3 (this is a common situation with .dta files prepared by Lcq_Dta)

								strLineIn = String.Copy(strMostRecentLineIn)
								If Not strLineIn Is Nothing AndAlso strLineIn.Trim.Length = 0 AndAlso srInFile.Peek() > -1 Then
									' Read the next line
									strLineIn = srInFile.ReadLine
									If Not strLineIn Is Nothing Then mTotalBytesRead += strLineIn.Length + 2
									mInFileLineNumber += 1
								End If

								If Not strLineIn Is Nothing AndAlso strLineIn.StartsWith(mCommentLineStartChar) Then
									mHeaderSaved = String.Copy(strLineIn)
									strCompareTitle = MyBase.CleanupComment(mHeaderSaved, mCommentLineStartChar, True)

									If strCompareTitle.ToLower.EndsWith("3.dta") Then
										If mCurrentSpectrum.SpectrumTitle.Substring(0, mCurrentSpectrum.SpectrumTitle.Length - 5) = strCompareTitle.Substring(0, strCompareTitle.Length - 5) Then
											' Yes, the spectra match

											With mCurrentSpectrum
												.ParentIonChargeCount = 2
												.ParentIonCharges(1) = 3
												.ChargeIs2And3Plus = True
											End With

											mHeaderSaved = String.Empty

											' Read the next set of lines until the next blank line or comment line is found
											Do While srInFile.Peek() > -1
												strLineIn = srInFile.ReadLine
												mInFileLineNumber += 1

												' See if strLineIn is blank or starts with an equals sign
												If Not strLineIn Is Nothing Then
													mTotalBytesRead += strLineIn.Length + 2
													If strLineIn.Trim.Length = 0 Then
														Exit Do
													ElseIf strLineIn.Trim.StartsWith(mCommentLineStartChar) Then
														mHeaderSaved = String.Copy(strLineIn)
														Exit Do
													End If
												End If
											Loop
										End If
									End If
								End If
							Else
								If strMostRecentLineIn.StartsWith(mCommentLineStartChar) Then
									mHeaderSaved = String.Copy(strMostRecentLineIn)
								End If
							End If	' EndIf for blnSpectrumFound = True
						End If
					End If	' EndIf for strLineIn.Trim.StartsWith(mCommentLineStartChar)

					If mInFileLineNumber - intLastProgressUpdateLine >= 250 Or blnSpectrumFound Then
						intLastProgressUpdateLine = mInFileLineNumber
						UpdateStreamReaderProgress()
					End If
				Loop

                objSpectrumInfo = mCurrentSpectrum

                If blnSpectrumFound AndAlso Not MyBase.ReadingAndStoringSpectra Then
                    MyBase.UpdateFileStats(objSpectrumInfo.ScanNumber)
                End If
            End If

        Catch ex As Exception
            LogErrors("ReadNextSpectrum", ex.Message)
        End Try

        Return blnSpectrumFound

    End Function

    Public Function ReadSingleDtaFile(ByVal strInputFilePath As String, ByRef strMsMsDataList() As String, ByRef intMsMsDataCount As Integer, ByRef objSpectrumInfoMsMsText As clsSpectrumInfoMsMsText) As Boolean

        ' Open the .Dta file and read the spectrum
		Dim intLastProgressUpdateLine As Integer

        Dim strLineIn As String
        Dim blnSpectrumFound As Boolean

		Dim lstMsMsDataList As System.Collections.Generic.List(Of String) = New System.Collections.Generic.List(Of String)

        If objSpectrumInfoMsMsText Is Nothing Then
            objSpectrumInfoMsMsText = New clsSpectrumInfoMsMsText
        Else
            objSpectrumInfoMsMsText.Clear()
        End If

		Try
			intMsMsDataCount = 0

			Using srInFile As System.IO.StreamReader = New System.IO.StreamReader(strInputFilePath)

				mTotalBytesRead = 0
				MyBase.ResetProgress("Parsing " & System.IO.Path.GetFileName(strInputFilePath))

				mInFileLineNumber = 0
				intLastProgressUpdateLine = mInFileLineNumber
				Do While srInFile.Peek() > -1 And Not mAbortProcessing
					strLineIn = srInFile.ReadLine
					mInFileLineNumber += 1

					If Not strLineIn Is Nothing Then mTotalBytesRead += strLineIn.Length + 2
					If Not strLineIn Is Nothing AndAlso strLineIn.Trim.Length > 0 Then
						If Char.IsDigit(strLineIn.Trim, 0) Then
							blnSpectrumFound = ReadSingleSpectrum(srInFile, strLineIn, lstMsMsDataList, objSpectrumInfoMsMsText, mInFileLineNumber, intLastProgressUpdateLine)
							Exit Do
						End If
					End If

					If mInFileLineNumber - intLastProgressUpdateLine >= 100 Then
						intLastProgressUpdateLine = mInFileLineNumber
						'MyBase.UpdateProgress(srInFile.BaseStream.Position / srInFile.BaseStream.Length * 100.0)
						MyBase.UpdateProgress(mTotalBytesRead / srInFile.BaseStream.Length * 100.0)
					End If
				Loop

				If blnSpectrumFound Then
					' Try to determine the scan numbers by parsing strInputFilePath
					With objSpectrumInfoMsMsText
						MyBase.ExtractScanInfoFromDtaHeader(System.IO.Path.GetFileName(strInputFilePath), .ScanNumber, .ScanNumberEnd, .ScanCount)
					End With

					ReDim strMsMsDataList(lstMsMsDataList.Count - 1)
					lstMsMsDataList.CopyTo(strMsMsDataList)
					
				Else
					ReDim strMsMsDataList(0)
				End If

				If Not mAbortProcessing Then
					MyBase.OperationComplete()
				End If

			End Using

		Catch ex As Exception
			LogErrors("ReadSingleDtaFile", ex.Message)
		End Try

		Return blnSpectrumFound

    End Function

	Private Function ReadSingleSpectrum(ByVal srInFile As System.IO.TextReader, ByVal strParentIonLineText As String, ByRef lstMsMsDataList As System.Collections.Generic.List(Of String), ByRef objSpectrumInfoMsMsText As clsSpectrumInfoMsMsText, ByRef intLinesRead As Integer, ByRef intLastProgressUpdateLine As Integer, Optional ByRef strMostRecentLineIn As String = "") As Boolean
		' Returns True if a valid spectrum is found, otherwise, returns False

		Dim intCharIndex As Integer
		Dim strLineIn As String
		Dim strValue As String
		Dim dblValue As Double

		Dim blnSpectrumFound As Boolean
		Dim intCharge As Integer

		objSpectrumInfoMsMsText.ParentIonLineText = String.Copy(strParentIonLineText)
		strParentIonLineText = strParentIonLineText.Trim

		' Look for the first space
		intCharIndex = strParentIonLineText.IndexOf(" ")
		If intCharIndex >= 1 Then
			strValue = strParentIonLineText.Substring(0, intCharIndex)
			If Double.TryParse(strValue, dblValue) Then
				With objSpectrumInfoMsMsText
					.ParentIonMH = dblValue

					strValue = strParentIonLineText.Substring(intCharIndex + 1)

					' See if strValue contains another space
					intCharIndex = strValue.IndexOf(" ")
					If intCharIndex > 0 Then
						strValue = strValue.Substring(0, intCharIndex)
					End If

					If Integer.TryParse(strValue, intCharge) Then
						.ParentIonChargeCount = 1
						.ParentIonCharges(0) = intCharge

						' Note: Dta files have Parent Ion MH defined by not Parent Ion m/z
						' Thus, compute .ParentIonMZ using .ParentIonMH
						If .ParentIonCharges(0) <= 1 Then
							.ParentIonMZ = .ParentIonMH
							.ParentIonCharges(0) = 1
						Else
							.ParentIonMZ = MyBase.ConvoluteMass(.ParentIonMH, 1, .ParentIonCharges(0))
						End If

						blnSpectrumFound = True
					End If
				End With
			End If
		End If

		strMostRecentLineIn = String.Empty
		If blnSpectrumFound Then
			' Read all of the MS/MS spectrum ions up to the next blank line or up to the next line starting with COMMENT_LINE_START_CHAR
			If lstMsMsDataList Is Nothing Then
				lstMsMsDataList = New System.Collections.Generic.List(Of String)
			Else
				lstMsMsDataList.Clear()
			End If

			Do While srInFile.Peek() > -1
				strLineIn = srInFile.ReadLine
				intLinesRead += 1

				' See if strLineIn is blank
				If Not strLineIn Is Nothing Then
					mTotalBytesRead += strLineIn.Length + 2
					strMostRecentLineIn = String.Copy(strLineIn)

					If strLineIn.Trim.Length = 0 OrElse strLineIn.StartsWith(COMMENT_LINE_START_CHAR) Then
						Exit Do
					Else

						' Add to MS/MS data string list
						lstMsMsDataList.Add(strLineIn.Trim)

						MyBase.AddNewRecentFileText(strLineIn)
					End If
				End If

				If intLinesRead - intLastProgressUpdateLine >= 250 Then
					intLastProgressUpdateLine = intLinesRead
					UpdateStreamReaderProgress()
				End If

			Loop

		End If

		Return blnSpectrumFound

	End Function

End Class
