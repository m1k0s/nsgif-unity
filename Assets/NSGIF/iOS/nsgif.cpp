#include <stdio.h>
#include <stdlib.h>
#include <assert.h>
#if !defined(_WIN32)
#include <sys/mman.h>
#endif
#include <sys/stat.h>

extern "C"
{
#include "libnsgif.h"
}

#if defined(_WIN32) && !defined(__SCITECH_SNAP__)

#	define PLUGIN_APICALL __declspec(dllexport)

#elif defined(__ANDROID__)

#	include <sys/cdefs.h>
// Copy what KHRONOS_APICALL does (in <>/usr/include/KHR/khrplatform.h)
#   ifdef __NDK_FPABI__
#	    define PLUGIN_APICALL __attribute__((visibility("default"))) __NDK_FPABI__
#   else
#	    define PLUGIN_APICALL __attribute__((visibility("default")))
#   endif

// Work out the Android ABI (https://github.com/android/ndk-samples/blob/master/hello-jni/app/src/main/cpp/hello-jni.c)
#   if defined(__arm__)
#      if defined(__ARM_ARCH_7A__)
#           if defined(__ARM_NEON__)
#               if defined(__ARM_PCS_VFP)
#                   define ABI "armeabi-v7a/NEON (hard-float)"
#               else
#                   define ABI "armeabi-v7a/NEON"
#               endif
#           else
#               if defined(__ARM_PCS_VFP)
#                   define ABI "armeabi-v7a (hard-float)"
#               else
#                   define ABI "armeabi-v7a"
#               endif
#           endif
#       else
#           define ABI "armeabi"
#       endif
#   elif defined(__i386__)
#       define ABI "x86"
#   elif defined(__x86_64__)
#       define ABI "x86_64"
#   elif defined(__aarch64__)
#       define ABI "arm64-v8a"
#   else
#       define ABI "unknown"
#   endif

#else

#	define PLUGIN_APICALL

#endif

#if defined(_WIN32) && !defined(_WIN32_WCE) && !defined(__SCITECH_SNAP__)
#	define PLUGIN_APIENTRY __stdcall
#else
#	define PLUGIN_APIENTRY
#endif

extern "C"
typedef PLUGIN_APICALL void* (PLUGIN_APIENTRY *BitmapCreate)(int width, int height, void* user_data);

extern "C"
typedef PLUGIN_APICALL void (PLUGIN_APIENTRY *BitmapDestroy)(void* bitmap, void* user_data);

extern "C"
typedef PLUGIN_APICALL unsigned char* (PLUGIN_APIENTRY *BitmapGetBuffer)(void* bitmap, void* user_data);

class NSGIF_context
{
public:
	enum Status
	{
		Status_OK = GIF_OK,
		Status_InsufficientFrameData = GIF_INSUFFICIENT_FRAME_DATA,
		Status_FrameDataError = GIF_FRAME_DATA_ERROR,
		Status_InsufficientData = GIF_INSUFFICIENT_DATA,
		Status_DataError = GIF_DATA_ERROR,
		Status_InsufficientMemory = GIF_INSUFFICIENT_MEMORY,
		Status_FrameNoDisplay = GIF_FRAME_NO_DISPLAY,
		Status_EndOfFrame = GIF_END_OF_FRAME,
		Status_FileOpenFailure = GIF_END_OF_FRAME - 1,
		Status_FileMapFailure = GIF_END_OF_FRAME - 2
	};
	
	NSGIF_context(BitmapCreate bitmap_create_cb, BitmapDestroy bitmap_destroy_cb, BitmapGetBuffer bitmap_get_buffer_cb, void* user_data) :
		size_(0),
		data_(NULL),
		dataIsMapped_(false)
	{
		gif_bitmap_callback_vt bitmap_callbacks = {
            user_data,
            bitmap_create_cb,
            bitmap_destroy_cb,
            bitmap_get_buffer_cb,
			NULL,
			NULL,
			NULL
		};
		
		gif_create(&gif_, &bitmap_callbacks);
	}
	
	~NSGIF_context()
	{
		gif_finalise(&gif_);
		
		if(dataIsMapped_)
		{
			assert(NULL != data_);
			assert(0 != size_);
#if !defined(_WIN32)
			::munmap(data_, size_);
#else
			delete[] data_;
#endif
		}
	}
	
	Status Initialise(const char* filename, int& frameCount)
	{
		FILE* fd = ::fopen(filename, "rb");
		
		if(NULL != fd)
		{
			struct stat stat;
			::fstat(fileno(fd), &stat);
			
			if(0 != stat.st_size)
			{
#if !defined(_WIN32)
				void* data = ::mmap(NULL, stat.st_size, PROT_READ, MAP_PRIVATE, fileno(fd), 0);
				::fclose(fd);
				
				if(MAP_FAILED != data)
				{
					dataIsMapped_ = true;
					
					return Initialise(data, stat.st_size, frameCount);
				}
				
				return Status_FileMapFailure;
#else
				void* data = new char[stat.st_size];
				if(NULL != data)
				{
					::fread(data, 1, stat.st_size, fd);
					::fclose(fd);

					dataIsMapped_ = true;

					return Initialise(data, stat.st_size, frameCount);
				}

				::fclose(fd);
				return Status_InsufficientMemory;
#endif
			}
			
			::fclose(fd);
			return Status_InsufficientData;
		}
		
		return Status_FileOpenFailure;
	}

	Status Initialise(void* data, size_t size, int& frameCount)
	{
		if(NULL != data && 0 != size)
		{
			size_ = size;
			data_ = data;
		
			gif_result res;
			
			while(GIF_WORKING == (res = gif_initialise(&gif_, size_, reinterpret_cast<unsigned char*>(data_))))
				;
			
			if(GIF_OK == res)
			{
				frameCount = gif_.frame_count_partial;
			}
			
			return static_cast<Status>(res);
		}
		
		return Status_InsufficientData;
	}
	
	Status Decode(int frameIndex, void*& frame, int& width, int& height, int& delay)
	{
		if(0 <= frameIndex && frameIndex < gif_.frame_count)
		{
			gif_result res = gif_decode_frame(&gif_, frameIndex);
			
			if(GIF_OK == res)
			{
				frame = gif_.frame_image;
				width = gif_.width;
				height = gif_.height;
				delay = gif_.frames[frameIndex].frame_delay;
			}
			
			return static_cast<Status>(res);
		}
		
		return Status_InsufficientData;
	}

private:
	gif_animation gif_;
	size_t size_;
	void* data_;
	bool dataIsMapped_;
};

extern "C"
PLUGIN_APICALL intptr_t PLUGIN_APIENTRY NSGIF_Create(BitmapCreate bitmap_create_cb, BitmapDestroy bitmap_destroy_cb, BitmapGetBuffer bitmap_get_buffer_cb, void* user_data)
{
	return reinterpret_cast<intptr_t>(new NSGIF_context(bitmap_create_cb, bitmap_destroy_cb, bitmap_get_buffer_cb, user_data));
}

extern "C"
PLUGIN_APICALL void PLUGIN_APIENTRY NSGIF_InitializeFile(intptr_t handle, const char* filename, int* frameCount, int* status)
{
	assert(0 != handle);
	NSGIF_context* context = reinterpret_cast<NSGIF_context*>(handle);
	
	assert(NULL != filename);
	assert(NULL != frameCount);
	assert(NULL != status);
	*status = context->Initialise(filename, *frameCount);
}

extern "C"
PLUGIN_APICALL void PLUGIN_APIENTRY NSGIF_InitializeBuffer(intptr_t handle, void* data, int size, int* frameCount, int* status)
{
	assert(0 != handle);
	NSGIF_context* context = reinterpret_cast<NSGIF_context*>(handle);
	
	assert(NULL != data);
	assert(NULL != frameCount);
	assert(NULL != status);
	*status = context->Initialise(data, size, *frameCount);
}

extern "C"
PLUGIN_APICALL void PLUGIN_APIENTRY NSGIF_Destroy(intptr_t handle)
{
	assert(0 != handle);
	delete reinterpret_cast<NSGIF_context*>(handle);
}

extern "C"
PLUGIN_APICALL intptr_t PLUGIN_APIENTRY NSGIF_Decode(intptr_t handle, int frameIndex, int* width, int* height, int* delay, int* status)
{
	assert(0 != handle);
	NSGIF_context* context = reinterpret_cast<NSGIF_context*>(handle);
	
	assert(NULL != width);
	assert(NULL != height);
	assert(NULL != delay);
	assert(NULL != status);
	void* frame;
	*status = context->Decode(frameIndex, frame, *width, *height, *delay);
	return reinterpret_cast<intptr_t>(frame);
}
