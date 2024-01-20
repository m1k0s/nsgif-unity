LOCAL_PATH := $(call my-dir)/../../../Assets/NSGIF/iOS

include $(CLEAR_VARS)
LOCAL_C_INCLUDES += .
LOCAL_CFLAGS += -fvisibility=hidden
LOCAL_ARM_MODE := arm
LOCAL_MODULE    := nsgif
LOCAL_SRC_FILES := nsgif.cpp libnsgif.c
include $(BUILD_SHARED_LIBRARY)
