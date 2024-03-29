using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace NSGIF
{
	public class NSGIF : IDisposable
	{
		public int frameCount { get; private set; }
		public int frameIndex { get; private set; }
		public Texture2D frameTexture { get; private set; }

		private void DestroyTexture()
		{
			if (null != frameTexture)
			{
				GameObject.DestroyImmediate(frameTexture);
				frameTexture = null;
			}
		}

		private unsafe byte* GetTextureBuffer()
		{
			var nativeArray = frameTexture.GetRawTextureData<byte>();
			return (byte*)Unity.Collections.LowLevel.Unsafe.NativeArrayUnsafeUtility.GetUnsafePtr(nativeArray);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (handle != IntPtr.Zero)
			{
				NSGIF_Destroy(handle);
				handle = IntPtr.Zero;
			}

			if (instance.IsAllocated)
			{
				instance.Free();
			}

			DestroyTexture();

			if (disposing)
			{
				// No managed stuff to dispose...
			}
		}

		~NSGIF()
		{
			Dispose(false);
		}

		public NSGIF(string filename)
		{
			if (null == filename)
			{
				throw new NullReferenceException("Filename is null");
			}

			instance = GCHandle.Alloc(this);
			unsafe
			{
				handle = NSGIF_Create(BitmapCreateDelegate, BitmapDestroyDelegate, BitmapGetBufferDelegate, GCHandle.ToIntPtr(instance));
			}

			if (IntPtr.Zero == handle)
			{
				throw new Exception("Failed to create NSGIF plugin");
			}

			int count;
			Status status;
			NSGIF_InitializeFile(handle, filename, out count, out status);

			if (Status.OK != status)
			{
				Dispose();
				throw new Exception($"Failed to initialise NSGIF for {filename}: {status.ToString()}");
			}

			if (0 == count)
			{
				Dispose();
				throw new Exception($"Empty gif {filename}");
			}

			frameCount = count;
			frameIndex = 0;

			int width;
			int height;
			int delay;
			IntPtr frameData = NSGIF_Decode(handle, frameIndex, out width, out height, out delay, out status);

			if(Status.OK != status)
			{
				Dispose();
				throw new Exception($"Failed to decode frame {frameIndex}/{frameCount}: {status.ToString()}");
			}
		}

		public int DecodeNextFrame()
		{
			if (++frameIndex >= frameCount)
			{
				frameIndex = 0;
			}

			Status status;
			int width;
			int height;
			int delay; // Decoded frame delay (in 1/100th of a second as per GIF spec)
			IntPtr frameData = NSGIF_Decode(handle, frameIndex, out width, out height, out delay, out status);

			if(Status.OK != status)
			{
				throw new Exception($"Failed to decode frame {frameIndex}/{frameCount}: {status.ToString()}");
			}

			frameTexture.Apply(false, false);

			return delay * 10;
		}

		[AOT.MonoPInvokeCallback(typeof(BitmapCreate))] 
		private static unsafe byte* BitmapCreateDelegate(int width, int height, IntPtr userData)
		{
			var instance = GCHandle.FromIntPtr(userData).Target as NSGIF;
			instance.DestroyTexture();
			instance.frameTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
			if (null == instance.frameTexture)
			{
				Debug.LogError($"BitmapCreateDelegate({instance}): Failed to create {width}x{height} Texture2D");
				return null;
			}

			return instance.GetTextureBuffer();
		}

		[AOT.MonoPInvokeCallback(typeof(BitmapDestroy))] 
		private static void BitmapDestroyDelegate(IntPtr bitmap, IntPtr userData)
		{
			var instance = GCHandle.FromIntPtr(userData).Target as NSGIF;
			instance.DestroyTexture();
		}

		[AOT.MonoPInvokeCallback(typeof(BitmapGetBuffer))] 
		private static unsafe byte* BitmapGetBufferDelegate(IntPtr bitmap, IntPtr userData)
		{
			var instance = GCHandle.FromIntPtr(userData).Target as NSGIF;
			return instance.GetTextureBuffer();
		}

		private enum Status
		{
			OK = 0,
			InsufficientFrameData = -1,
			FrameDataError = -2,
			InsufficientData = -3,
			DataError = -4,
			InsufficientMemory = -5,
			FrameNoDisplay = -6,
			EndOfFrame = -7,
			FileOpenFailure = -8,
			FileMapFailure = -9,
			DecodingFinished = -10,
			Destroyed = -11
		}

#if UNITY_IPHONE && !UNITY_EDITOR
		private const string __importName = "__Internal";
#else
		private const string __importName = "nsgif";
#endif

		private unsafe delegate byte* BitmapCreate(int width, int height, IntPtr userData);
		private delegate void BitmapDestroy(IntPtr bitmap, IntPtr userData);
		private unsafe delegate byte* BitmapGetBuffer(IntPtr bitmap, IntPtr userData);

		[DllImport(__importName)] extern private static IntPtr NSGIF_Create(BitmapCreate bitmapCreate, BitmapDestroy bitmapDestroy, BitmapGetBuffer bitmapGetBuffer, IntPtr userData);
		[DllImport(__importName)] extern private static void NSGIF_InitializeFile(IntPtr handle, string filename, out int frameCount, out Status status);
		[DllImport(__importName)] extern private static void NSGIF_InitializeBuffer(IntPtr handle, IntPtr data, int size, out int frameCount, out Status status);
		[DllImport(__importName)] extern private static void NSGIF_Destroy(IntPtr handle);
		[DllImport(__importName)] extern private static IntPtr NSGIF_Decode(IntPtr handle, int frameIndex, out int width, out int height, out int delay, out Status status);

		private IntPtr handle;
		private GCHandle instance;
	};
}
