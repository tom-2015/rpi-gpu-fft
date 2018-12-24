#!/bin/sh
gcc -g3 -Wall -lrt -lm -ldl -fPIC -c *.c
gcc -g3 -shared -Wl,-soname,libgpufft.so -o libgpufft.so.1 *.o
sudo cp libgpufft.so.1 /usr/local/lib
sudo ldconfig
