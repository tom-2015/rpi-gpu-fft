Imports System.Runtime.InteropServices
Imports System.Threading.Tasks

Public MustInherit Class GPU_FFT
    Public Enum FFTType As Integer
        GPU_FFT_FWD = 0
        GPU_FFT_REV = 1
    End Enum

    Public Structure GPU_FFT_COMPLEX
        Public Re As Single
        Public Im As Single

        Public Sub New(ByVal Re As Single, ByVal Im As Single)
            Me.Re = Re
            Me.Im = Im
        End Sub

        Public Overrides Function ToString() As String
            Return Re & "+j" & Im
        End Function
    End Structure

    Protected m_MB As Integer
    Protected m_FFTType As FFTType

    Protected Declare Auto Function gpu_fft_prepare Lib "gpufft" (ByVal MB As Integer, ByVal Log2_N As Integer, ByVal Direction As Integer, ByVal Jobs As Integer, ByRef GPU_FFT As IntPtr) As Integer
    Protected Declare Auto Function mbox_open Lib "gpufft" () As Integer
    Protected Declare Auto Sub mbox_close Lib "gpufft" (ByVal FilePtr As Integer)
    Protected Declare Auto Function gpu_fft_execute Lib "gpufft" (ByVal GPU_FFT As IntPtr) As UInt32
    Protected Declare Auto Sub gpu_fft_release Lib "gpufft" (ByVal GPU_FFT As IntPtr)
    Protected Declare Auto Sub gpu_fft_copy_data_in Lib "gpufft" (ByVal GPU_FFT As IntPtr, ByVal array() As GPU_FFT_COMPLEX, ByVal data_width As Integer, ByVal data_height As Integer)
    Protected Declare Auto Sub gpu_fft_copy_data_out Lib "gpufft" (ByVal GPU_FFT As IntPtr, ByVal array() As GPU_FFT_COMPLEX, ByVal data_width As Integer, ByVal data_height As Integer)
    Protected Declare Auto Sub gpu_fft_clear_data_in Lib "gpufft" (ByVal GPU_FFT As IntPtr)
    Protected Declare Auto Function gpu_fft_trans_prepare Lib "gpufft" (ByVal MB As Integer, ByVal GPU_FFT_Src As IntPtr, ByVal GPU_FFT_Dst As IntPtr, ByRef GPU_FFT_Trans_Out As IntPtr) As Integer
    Protected Declare Auto Function gpu_fft_trans_execute Lib "gpufft" (ByVal GPU_FFT_Trans As IntPtr) As Integer
    Protected Declare Auto Function gpu_fft_trans_release Lib "gpufft" (ByVal GPU_FFT_Trans As IntPtr) As Integer

    Public Class GPU_FFTException
        Inherits Exception
        Public Code As Integer
        Public Sub New(ByVal Code As Integer, ByVal Message As String)
            MyBase.New(Message)
            Me.Code = Code
        End Sub
    End Class

    Public MustOverride Sub SetDataIn(ByRef Data() As GPU_FFT_COMPLEX)
    Public MustOverride Function GetDataOut() As GPU_FFT_COMPLEX()
    Public MustOverride Function GetScaledDataOut() As GPU_FFT_COMPLEX()
    Public MustOverride Function Execute() As UInt32
    Public MustOverride Sub Release()

    Public Overridable Function GetClosestPow2(ByVal Number As Integer) As Integer
        Dim Pow As Integer = 2
        While Pow < Number
            Pow <<= 1
        End While
        Return Pow
    End Function

    Public Overridable Function Log2(ByVal Number As Integer) As Integer
        Dim Log As Integer = 0
        While Number > 0
            Number >>= 1
            Log += 1
        End While
        Return Log - 1
    End Function

    Protected Overridable Function CheckInitResult(ByVal Result As Integer) As Boolean
        Select Case Result
            Case -1
                Throw New GPU_FFTException(-1, "Unable to enable V3D. Please check your firmware is up to date.")
            Case -2
                Throw New GPU_FFTException(-2, "log2_N=%d not supported.  Try between 8 and 22.")
            Case -3
                Throw New GPU_FFTException(-3, "Out of memory.  Try a smaller batch or increase GPU memory.")
            Case -4
                Throw New GPU_FFTException(-4, "Unable to map Videocore peripherals into ARM memory space.")
            Case -5
                Throw New GPU_FFTException(-5, "Can't open libbcm_host.")
            Case Else
                Return True
        End Select
        Return False
    End Function



    Protected Overrides Sub Finalize()
        MyBase.Finalize()
        Release()
    End Sub

End Class

Public Class GPU_FFT_1D
    Inherits GPU_FFT

    Protected m_GPU_FFT As IntPtr 'Pointer to FFT Handle
    Protected m_DataOut As GPU_FFT_COMPLEX()
    Protected m_WindowSize As Integer
    Protected m_Size As Integer
    Protected m_Jobs As Integer
    Protected m_DataInSize As Integer
    Protected m_DataOutSize As Integer

    ''' <summary>
    ''' Initializes a 1D FFT object
    ''' For FFTType=GPU_FFT_FWD
    ''' If size is not a power of 2 the input data will be zero padded to a power of 2
    ''' The output data size will always be a power of 2 >= size
    ''' For FFType=GPU_FFT_REV
    ''' Size determines the size of the output data, input data must always be a power of 2 >=size, complex numbers representing the frequencies, amplitude and phase
    ''' if size is not a power of 2 the output data size will be truncated to size
    ''' </summary>
    ''' <param name="Size">The x size of the: input data for FWD or output data for REV</param>
    ''' <param name="FFTType">Forward or reverse FFT</param>
    ''' <param name="Jobs">Number of FFTs to calculate, input data size must be: 2^log2N * jobs</param>
    Public Sub New(ByVal Size As Integer, ByVal FFTType As FFTType, ByVal Jobs As Integer)
        m_FFTType = FFTType
        m_Size = Size
        m_WindowSize = GetClosestPow2(Size)
        m_MB = mbox_open()
        m_Jobs = Jobs
        If CheckInitResult(gpu_fft_prepare(m_MB, Log2(m_WindowSize), FFTType, Jobs, m_GPU_FFT)) Then
            If Size < m_WindowSize Then gpu_fft_clear_data_in(m_GPU_FFT)
            If FFTType = FFTType.GPU_FFT_FWD Then
                m_DataOutSize = m_WindowSize
                m_DataInSize = Size
            Else
                m_DataOutSize = Size
                m_DataInSize = m_WindowSize
            End If
            ReDim m_DataOut(0 To m_DataOutSize * Jobs - 1)
        End If
    End Sub

    ''' <summary>
    ''' Copies input data array of the FFT
    ''' </summary>
    ''' <param name="Data"></param>
    Public Overrides Sub SetDataIn(ByRef Data() As GPU_FFT_COMPLEX)
        gpu_fft_copy_data_in(m_GPU_FFT, Data, m_DataInSize, m_Jobs)
    End Sub

    ''' <summary>
    ''' Copies the output data of the FFT to an array
    ''' </summary>
    ''' <returns></returns>
    Public Overrides Function GetDataOut() As GPU_FFT_COMPLEX()
        gpu_fft_copy_data_out(m_GPU_FFT, m_DataOut, m_DataOutSize, m_Jobs)
        Return m_DataOut
    End Function

    ''' <summary>
    ''' Returns a scaled output after a forward FFT
    ''' Setting the scaled output data as input data for a reverse FFT will result in the original data
    ''' </summary>
    ''' <returns></returns>
    Public Overrides Function GetScaledDataOut() As GPU_FFT_COMPLEX()
        Dim Data() As GPU_FFT_COMPLEX = GetDataOut()
        If m_FFTType = FFTType.GPU_FFT_FWD Then
            Parallel.ForEach(Data, Sub(ByVal Sample As GPU_FFT_COMPLEX, loopstate As ParallelLoopState, Index As Integer)
                                       With Data(Index)
                                           .Re = Sample.Re / m_WindowSize
                                           .Im = Sample.Im / m_WindowSize
                                       End With
                                   End Sub)
        End If
        Return Data
    End Function


    ''' <summary>
    ''' Executes the FFT
    ''' </summary>
    ''' <returns></returns>
    Public Overrides Function Execute() As UInt32
        Return gpu_fft_execute(m_GPU_FFT)
    End Function

    ''' <summary>
    ''' Returns the FFT window size
    ''' Usually this is the closest power of 2 in size
    ''' </summary>
    ''' <returns></returns>
    Public Overridable ReadOnly Property WindowSize()
        Get
            Return m_WindowSize
        End Get
    End Property

    ''' <summary>
    ''' Returns the x size of the input data
    ''' total array size is DataInSize * Jobs
    ''' </summary>
    ''' <returns></returns>
    Public Overridable ReadOnly Property DataInSize()
        Get
            Return m_DataInSize
        End Get
    End Property

    ''' <summary>
    ''' Returns the x size of the output data
    ''' total array size is DataOutSize * jobs
    ''' </summary>
    ''' <returns></returns>
    Public Overridable ReadOnly Property DataOutSize()
        Get
            Return m_DataOutSize
        End Get
    End Property

    ''' <summary>
    ''' For forward FFT this returns the x size of the input data
    ''' for reverse FFT this returns the x size of the output data
    ''' </summary>
    ''' <returns></returns>
    Public Overridable ReadOnly Property Size()
        Get
            Return m_Size
        End Get
    End Property

    ''' <summary>
    ''' Returns the Y-size of the input/output arrays
    ''' a.k.a. the number of FFT's to execute in one execute() call
    ''' </summary>
    ''' <returns></returns>
    Public Overridable ReadOnly Property Jobs()
        Get
            Return m_Jobs
        End Get
    End Property

    Public Overrides Sub Release()
        If m_GPU_FFT <> 0 Then
            gpu_fft_release(m_GPU_FFT)
            m_GPU_FFT = 0
        End If
    End Sub

    Protected Overrides Sub Finalize()
        MyBase.Finalize()
        Release()
    End Sub
End Class

Public Class GPU_FFT_2D
    Inherits GPU_FFT

    Protected m_GPU_FFT(0 To 1) As IntPtr
    Protected m_GPU_Trans As IntPtr
    Protected m_DataOut() As GPU_FFT_COMPLEX
    Protected m_WindowSizeX As Integer
    Protected m_WindowSizeY As Integer
    Protected m_DataOutSizeX As Integer
    Protected m_DataOutSizeY As Integer
    Protected m_DataInSizeX As Integer
    Protected m_DataInSizeY As Integer
    Protected m_y As Integer
    Protected m_x As Integer
    Protected m_InitOK As Boolean


    ''' <summary>
    ''' Initializes a 2D FFT object
    ''' For FFTType=GPU_FFT_FWD
    ''' If x/y not a power of 2 the input data will be zero padded to a power of 2
    ''' The output data size will always be a power of 2 >= size (and a square)
    ''' For FFType=GPU_FFT_REV
    ''' x,y determines the size of the output data, input data must always be a power of 2 >=size (and a square), complex numbers representing the frequencies, amplitude and phase
    ''' if x,y is not a power of 2 the output data will be truncated to x*y
    ''' </summary>
    ''' <param name="x">The x size of the: input data for forward FFT, size of output data for reverse FFT</param>
    ''' <param name="y">The y size of the: input data for forward FFT, size of the output data for reverse FFT</param>
    ''' <param name="FFTType">Forward or reverse FFT</param>
    Public Sub New(ByVal x As Integer, ByVal y As Integer, ByVal FFTType As FFTType)
        m_FFTType = FFTType
        m_WindowSizeX = Math.Max(GetClosestPow2(x), GetClosestPow2(y))
        m_WindowSizeY = m_WindowSizeX
        m_MB = mbox_open()
        m_x = x
        m_y = y

        If Not CheckInitResult(gpu_fft_prepare(m_MB, Log2(m_WindowSizeX), FFTType, m_WindowSizeY, m_GPU_FFT(0))) Then
            Throw New Exception("GPU FFT prepare 1 failed!")
            Exit Sub
        End If

        If Not CheckInitResult(gpu_fft_prepare(m_MB, Log2(m_WindowSizeY), FFTType, m_WindowSizeX, m_GPU_FFT(1))) Then
            gpu_fft_release(m_GPU_FFT(0))
            Throw New Exception("GPU FFT Prepare 2 failed")
        End If

        If gpu_fft_trans_prepare(m_MB, m_GPU_FFT(0), m_GPU_FFT(1), m_GPU_Trans) <> 0 Then
            gpu_fft_release(m_GPU_FFT(0))
            gpu_fft_release(m_GPU_FFT(1))
            Throw New Exception("Initialization of GPU Trans failed!")
        End If

        If FFTType = FFTType.GPU_FFT_FWD Then
            m_DataOutSizeX = m_WindowSizeX
            m_DataOutSizeY = m_WindowSizeY
            m_DataInSizeX = x
            m_DataInSizeY = y
        Else
            m_DataOutSizeX = x
            m_DataOutSizeY = y
            m_DataInSizeX = m_WindowSizeX
            m_DataInSizeY = m_WindowSizeY
        End If

        gpu_fft_clear_data_in(m_GPU_FFT(0))
        gpu_fft_clear_data_in(m_GPU_FFT(1))

        ReDim m_DataOut(0 To m_DataOutSizeX * m_DataOutSizeY - 1)
        m_InitOK = True
    End Sub

    Public Overrides Sub SetDataIn(ByRef Data() As GPU_FFT_COMPLEX)
        gpu_fft_copy_data_in(m_GPU_FFT(0), Data, m_DataInSizeX, m_DataInSizeY)
    End Sub

    Public Overrides Sub Release()
        If m_InitOK Then
            gpu_fft_release(m_GPU_FFT(0))
            gpu_fft_release(m_GPU_FFT(1))
            gpu_fft_trans_release(m_GPU_Trans)
            m_InitOK = False
        End If
    End Sub

    Public Overrides Function GetDataOut() As GPU_FFT_COMPLEX()
        gpu_fft_copy_data_out(m_GPU_FFT(1), m_DataOut, m_DataOutSizeX, m_DataOutSizeY)
        Return m_DataOut
    End Function

    ''' <summary>
    ''' Returns a scaled output after a forward FFT
    ''' Setting the scaled output data as input data for a reverse FFT will result in the original image
    ''' </summary>
    ''' <returns></returns>
    Public Overrides Function GetScaledDataOut() As GPU_FFT_COMPLEX()
        Dim Data() As GPU_FFT_COMPLEX = GetDataOut()
        Dim ScaleFactor As Single = m_WindowSizeX * m_WindowSizeY
        If m_FFTType = FFTType.GPU_FFT_FWD Then
            Parallel.ForEach(Data, Sub(ByVal Sample As GPU_FFT_COMPLEX, loopstate As ParallelLoopState, Index As Integer)
                                       With Data(Index)
                                           .Re = Sample.Re / ScaleFactor
                                           .Im = Sample.Im / ScaleFactor
                                       End With
                                   End Sub)
        End If
        Return Data
    End Function

    Public Overrides Function Execute() As UInteger
        gpu_fft_execute(m_GPU_FFT(0))
        gpu_fft_trans_execute(m_GPU_Trans)
        gpu_fft_execute(m_GPU_FFT(1))
        Return 0
    End Function

    Public Overridable ReadOnly Property SizeX() As Integer
        Get
            Return m_x
        End Get
    End Property

    Public Overridable ReadOnly Property SizeY() As Integer
        Get
            Return m_y
        End Get
    End Property

    Public Overridable ReadOnly Property WindowSizeX() As Integer
        Get
            Return m_WindowSizeX
        End Get
    End Property

    Public Overridable ReadOnly Property WindowSizeY() As Integer
        Get
            Return m_WindowSizeY
        End Get
    End Property

    Public Overridable ReadOnly Property DataInSizeX() As Integer
        Get
            Return m_DataInSizeX
        End Get
    End Property

    Public Overridable ReadOnly Property DataInSizey() As Integer
        Get
            Return m_DataInSizeY
        End Get
    End Property

    Public Overridable ReadOnly Property DataOutSizeX() As Integer
        Get
            Return m_DataOutSizeX
        End Get
    End Property

    Public Overridable ReadOnly Property DataOutSizeY() As Integer
        Get
            Return m_DataOutSizeY
        End Get
    End Property
End Class