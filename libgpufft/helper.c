#include <stdlib.h>
#include <unistd.h>
#include <stdio.h>
#include <string.h>

#include "mailbox.h"
#include "gpu_fft.h"

void gpu_fft_clear_data_in(struct GPU_FFT * fft){
	memset((void*)fft->in, 0, fft->step * fft->y);
}

void gpu_fft_copy_data_in(struct GPU_FFT * fft, struct GPU_FFT_COMPLEX * data, int data_width, int data_height){
	int i;
	for (i=0;i<data_height;i++){
		memcpy((void*)(fft->in + fft->step * i), (void*) & data[data_width*i], sizeof(struct GPU_FFT_COMPLEX)*data_width);
	}
}

void gpu_fft_copy_data_out(struct GPU_FFT * fft, struct GPU_FFT_COMPLEX * data, int data_width, int data_height){
	int i;
	//data[0]=fft->in[0];
	//fft->out[1023].re=1000000;
	//fft->out[1023].im=0.05;
	for (i=0;i<data_height;i++){
		memcpy((void*) & data[data_width*i],(void*)(fft->out + fft->step * i) , sizeof(struct GPU_FFT_COMPLEX)*data_width);
	}	
	
}