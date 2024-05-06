using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace NSGIF
{
    public class NSGIF : IDisposable
    {
        public int frameCount { get; private set; }
        public int frame { get; private set; }
        public Texture2D texture { get; private set; }

        private void DestroyTexture()
        {
            if (null != texture)
            {
                GameObject.DestroyImmediate(texture);
                texture = null;
            }
            status = Status.Destroyed;
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
            frame = -1;
        }

        private static int DecodeFrame(IntPtr handle, int frame, out Status status)
        {
            int width;
            int height;
            int delay;
            IntPtr frameData = NSGIF_Decode(handle, frame, out width, out height, out delay, out status);
            return delay * 10;
        }

        public void Reset()
        {
            frame = 0;
        }

        public int DecodeNextFrame(bool apply = true)
        {
            if (Status.OK != status)
            {
                throw new Exception($"Trying to decode while in an invalid status: {status.ToString()}");
            }

            if (++frame >= frameCount)
            {
                Reset();
            }

            int delay = DecodeFrame(handle, frame, out status);

            if (Status.OK != status)
            {
                Dispose();
                throw new Exception($"Failed to decode frame {frame}/{frameCount}: {status.ToString()}");
            }

            if (apply)
            {
                texture.Apply(false, false);
            }

            return delay;
        }

        [AOT.MonoPInvokeCallback(typeof(BitmapCreate))]
        private static unsafe byte* BitmapCreateDelegate(int width, int height, IntPtr userData)
        {
            var instance = GCHandle.FromIntPtr(userData).Target as NSGIF;
            instance.DestroyTexture();
            instance.texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            if (null == instance.texture)
            {
                Debug.LogError($"BitmapCreateDelegate({instance}): Failed to create {width}x{height} Texture2D");
                return null;
            }

            instance.frameTextureRawData = instance.texture.GetRawTextureData<byte>();
            return (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(instance.frameTextureRawData);
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
            return (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(instance.frameTextureRawData);
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
        private Status status;
        private NativeArray<byte> frameTextureRawData;
    };
}
