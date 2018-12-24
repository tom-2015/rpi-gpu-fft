Imports System.Drawing
Imports System.IO
Imports rpi_gpu_fft

Module Module1

    Sub Main()
        Test1D()
        'Test2D()
    End Sub

    ''' <summary>
    ''' Test of 1D FFT
    ''' Creates 3 text files:
    ''' input.txt with sine wave samples
    ''' output.txt with the FFT result (amplitude of each frequency)
    ''' rev_output.txt the result of a reverse FFT on the output.txt (this is normally the same as the input.txt)
    ''' </summary>
    Private Sub Test1D()
        Dim FFTTest As GPU_FFT_1D
        Dim N As Integer = 1024 'number of samples
        Dim Freq As Integer = 20 'frequency
        Dim Dc As Integer = 100 'dc component
        Dim A As Integer = 1 'amplitude of signal

        'number of samples must alway be a power of 2 if not zeros will be added to the end of your input to the nearest power of 2, this will mess up the frequency spectrum!!

        Dim Data(0 To N - 1) As GPU_FFT.GPU_FFT_COMPLEX
        FFTTest = New GPU_FFT_1D(N, GPU_FFT.FFTType.GPU_FFT_FWD, 1)

        Console.WriteLine("FFT initialized")

        Dim InputFile As New FileStream("input.txt", IO.FileMode.Create)
        Dim InputWriter As New StreamWriter(InputFile)

        For i As Integer = 0 To N - 1
            Data(i).Re = Dc + A * Math.Cos(2 * Math.PI * i / (N - 1) * Freq)
            InputWriter.WriteLine(Data(i).Re)
        Next

        InputWriter.Close()

        Console.WriteLine("Setting data in")

        FFTTest.SetDataIn(Data)

        Console.WriteLine("Data input set")

        FFTTest.Execute()

        Console.WriteLine("FFT executed")

        Dim DataOut() As GPU_FFT.GPU_FFT_COMPLEX = FFTTest.GetScaledDataOut()

        Console.WriteLine("Data in read OK")

        Dim OutputFile As New FileStream("output.txt", IO.FileMode.Create)
        Dim OutputWriter As New StreamWriter(OutputFile)

        'result:
        'dataout(0) = dc * N
        'dataout(freq) = A * N/2
        'dataout(N-freq) = A * N/2
        'when using the GetScaledDataout() everything is devided by N

        For i As Integer = 0 To UBound(DataOut)
            Console.WriteLine(i & "=>" & DataOut(i).ToString())
            OutputWriter.WriteLine(Math.Sqrt(DataOut(i).Re ^ 2 + DataOut(i).Im ^ 2))
        Next

        OutputWriter.Close()
        Console.ReadKey()

        FFTTest.Release()



        '---------------------------
        'Now prepare the reverse FFT
        Console.WriteLine("Reverse FFT!")

        InputFile = New FileStream("rev_input.txt", IO.FileMode.Create)
        InputWriter = New StreamWriter(InputFile)

        FFTTest = New GPU_FFT_1D(N, GPU_FFT.FFTType.GPU_FFT_REV, 1)

        Data = DataOut

        For i As Integer = 0 To N - 1
            InputWriter.WriteLine(Data(i).Re)
        Next

        FFTTest.SetDataIn(Data)

        FFTTest.Execute()

        DataOut = FFTTest.GetDataOut()

        OutputFile = New FileStream("rev_output.txt", IO.FileMode.Create)
        OutputWriter = New StreamWriter(OutputFile)

        For i As Integer = 0 To UBound(DataOut)
            OutputWriter.WriteLine(DataOut(i).Re)
            Console.WriteLine(i & "=>" & DataOut(i).ToString())
        Next

        OutputWriter.Close()
        InputWriter.Close()


    End Sub

    ''' <summary>
    ''' Reads test.bmp image file and generates:
    ''' gray.bmp = the gray scale of test.bmp
    ''' fft.bmp the FFT image of the grayscale image
    ''' rev_fft.bmp the reverse FFT of the FFT image, this should be equal to the gray.bmp
    ''' </summary>
    Private Sub Test2D()
        Dim Pic As New Bitmap("test.bmp")

        Dim X As Integer, Y As Integer
        X = Pic.Width
        Y = Pic.Height

        'frist the bitmap is converted from RGB color to grayscale complex number:
        Dim DataIn(0 To X * Y - 1) As GPU_FFT.GPU_FFT_COMPLEX

        For i As Integer = 0 To Y - 1
            For j As Integer = 0 To X - 1
                Dim Pixel As Color = Pic.GetPixel(j, i)
                Dim Gray As Byte = (Pixel.R * 0.2989 + Pixel.G * 0.587 + Pixel.B * 0.114)
                DataIn(i * X + j).Re = Gray / 255
                Pic.SetPixel(j, i, Color.FromArgb(Gray, Gray, Gray))
            Next
        Next

        Pic.Save("gray.bmp") 'save grayscale picture

        Dim FFTTest As New GPU_FFT_2D(X, Y, GPU_FFT.FFTType.GPU_FFT_FWD)

        Console.WriteLine("Copy data in")

        FFTTest.SetDataIn(DataIn)

        Console.WriteLine("Execute FFT")

        FFTTest.Execute()

        Console.WriteLine("Get data out")
        Dim DataOut() As GPU_FFT.GPU_FFT_COMPLEX = FFTTest.GetScaledDataOut()

        For i As Integer = 0 To FFTTest.DataOutSizeY - 1
            For j As Integer = 0 To FFTTest.DataOutSizeX - 1
                Dim Pixel As Integer = (i * X + j)

                Dim Value As Single = Math.Min(Math.Sqrt(DataOut(Pixel).Re ^ 2 + DataOut(Pixel).Im ^ 2) * 255 * 35, 255) '*35 is just to scale the output a bit or you will not notice anything in the output bmp

                If Value >= 0 Then
                    Pic.SetPixel(j, i, Drawing.Color.FromArgb(Value, Value, Value))
                End If
            Next
        Next

        Pic.Save("fft.bmp")

        FFTTest.Release()

        Console.WriteLine("Reverse FFT")

        FFTTest = New GPU_FFT_2D(X, Y, GPU_FFT.FFTType.GPU_FFT_REV)

        FFTTest.SetDataIn(DataOut)
        FFTTest.Execute()
        DataOut = FFTTest.GetDataOut()


        For i As Integer = 0 To Y - 1
            For j As Integer = 0 To X - 1
                Dim Pixel As Integer = (i * X + j)
                Dim Value As Integer = DataOut(Pixel).Re * 255
                If Value <= 255 AndAlso Value >= 0 Then
                    Pic.SetPixel(j, i, Drawing.Color.FromArgb(Value, Value, Value))
                Else
                    Pic.SetPixel(j, i, Color.Red) 'invalid?
                End If
            Next
        Next

        Console.WriteLine("ReverseFFT OK")

        FFTTest.Release()
        Pic.Save("rev_fft.bmp")


    End Sub


End Module
