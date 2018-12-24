# .NET Library for GPU FFT on Raspberry
This is a VB .NET Library for calculating FFT (1D and 2D) using the GPU on the Raspberry Pi
* `Clone the project on your Raspberry and run sudo ./compile in the libgpufft directory. This will compile the GPU FFT example from [Andrew Holme]http://www.aholme.co.uk/GPU_FFT/Main.htm as a library.`
* `Open the gpu_fft.sln in Visual Studio and build the TestApplication project`
* `Transfer the TestApplication.exe and rpi_gpu_fft.dll file to your Raspberry`
* `Make the TestApplication.exe executable and run it using mono. If you don't have mono installed you can install it with: sudo apt-get install mono-runtime mono-vbnc`
