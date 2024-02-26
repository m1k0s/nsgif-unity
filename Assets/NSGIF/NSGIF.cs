using UnityEngine;
using System;
using System.Runtime.InteropServices;

namespace NSGIF
{
	public class NSGIF : IDisposable
	{
		public int frameCount { get; private set; }
		public int frameIndex { get; private set; }
		public Texture2D frameTexture { get; private set; }

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

			if (null != frameTexture)
			{
				GameObject.DestroyImmediate(frameTexture);
				frameTexture = null;
			}

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

			handle = NSGIF_Create();

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

			frameTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
			if (null == frameTexture)
			{
				throw new Exception($"Failed to create {width}x{height} Texture2D");
			}

			frameTexture.LoadRawTextureData(frameData, width * height * 4);
			frameTexture.Apply(false, false);
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

			// if (width != frameTexture.width || height != frameTexture.height)
			// {
			// 	throw new Exception($"Size mismatch {width}x{height} (was {frameTexture.width}x{frameTexture.height})");
			// }

			frameTexture.LoadRawTextureData(frameData, width * height * 4);
			frameTexture.Apply(false, false);

			return delay * 10;
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

		[DllImport(__importName)] extern private static IntPtr NSGIF_Create();
		[DllImport(__importName)] extern private static void NSGIF_InitializeFile(IntPtr handle, string filename, out int frameCount, out Status status);
		[DllImport(__importName)] extern private static void NSGIF_InitializeBuffer(IntPtr handle, IntPtr data, int size, out int frameCount, out Status status);
		[DllImport(__importName)] extern private static void NSGIF_Destroy(IntPtr handle);
		[DllImport(__importName)] extern private static IntPtr NSGIF_Decode(IntPtr handle, int frameIndex, out int width, out int height, out int delay, out Status status);

		private IntPtr handle;
	};
}
