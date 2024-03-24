#!/bin/bash -x
if [ ! -d $ANDROID_NDK ]; then
	echo "Set ANDROID_NDK to the root of the android NDK before proceeding."
	exit 1
fi
$ANDROID_NDK/ndk-build
cp -r libs/* ../../Assets/NSGIF/Android/
