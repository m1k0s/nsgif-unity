using UnityEngine;
using System.Runtime.InteropServices;

public class NSGIF : System.IDisposable
{
	public enum Status
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

	[DllImport(__importName)] extern private static System.IntPtr NSGIF_Create();
	[DllImport(__importName)] extern private static void NSGIF_InitializeFile(System.IntPtr handle, string filename, out int frameCount, out Status status);
	[DllImport(__importName)] extern private static void NSGIF_InitializeBuffer(System.IntPtr handle, System.IntPtr data, int size, out int frameCount, out Status status);
	[DllImport(__importName)] extern private static void NSGIF_Destroy(System.IntPtr handle);
	[DllImport(__importName)] extern private static System.IntPtr NSGIF_Decode(System.IntPtr handle, int frameIndex, out int width, out int height, out int delay, out Status status);

	private System.IntPtr _handle;	// Native plugin handle
	private GCHandle _bufferHandle;	// Buffer handle (if the the byte[] ctor was used)

	private int _decodedFrameCount;	// Decoded frame count
	private Texture2D[] _frames;	// Decoded frames
	private float[] _times;	// Decoded frame time stamps
	private float _duration;	// Sequence duration; note that this it not known until all frames have been decoded

	private float _currentTime;	// Current time through the sequence
	private Status _status;	// Current decoding status

	private Texture _lastSample;	// Last sample; used to bypass the binary search
	private float _lastSampleLow;
	private float _lastSampleHigh;

	public Status status { get { return _status; } }

	public int frameCount { get { return null != _frames ? _frames.Length : 0; } }

	public int frameCountPartial { get { return _decodedFrameCount; } }

	public float duration { get { return _duration; } }

	public float currentTime
	{
		get
		{
			return _currentTime;
		}
		set
		{
			_currentTime = value < 0.0f ? 0.0f : value;
		}
	}

	public Texture texture { get { return Sample(); } }

	void System.IDisposable.Dispose()
	{
		Dispose(true);
		System.GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if(disposing)
		{
			_frames = null;
			_times = null;
			_lastSample = null;
		}

		DisposeNative();

		_status = Status.Destroyed;
	}

	private void DisposeNative()
	{
		if(_handle != System.IntPtr.Zero)
		{
			NSGIF_Destroy(_handle);
			_handle = System.IntPtr.Zero;
		}

		if(_bufferHandle.IsAllocated)
		{
			_bufferHandle.Free();
		}
	}

	~NSGIF()
	{
		Dispose(false);
	}

	public NSGIF(string filename)
	{
		if(null != filename)
		{
			_handle = NSGIF_Create();

			if(System.IntPtr.Zero != _handle)
			{
				int frameCount;
				NSGIF_InitializeFile(_handle, filename, out frameCount, out _status);

				if(Status.OK == _status)
				{
					Initialize(frameCount);
				}
				else
				{
					// Failed to initialize; just cleanup
					Destroy();
				}
			}
			else
			{
				_status = Status.InsufficientMemory;
			}
		}
		else
		{
			_status = Status.FileOpenFailure;
		}
	}

	public NSGIF(byte[] data)
	{
		if(null != data && 0 != data.Length)
		{
			_handle = NSGIF_Create();

			if(System.IntPtr.Zero != _handle)
			{
				_bufferHandle = GCHandle.Alloc(data, GCHandleType.Pinned);

				int frameCount;
				NSGIF_InitializeBuffer(_handle, _bufferHandle.AddrOfPinnedObject(), data.Length, out frameCount, out _status);

				if(Status.OK == _status)
				{
					Initialize(frameCount);
				}
				else
				{
					// Failed to initialize; just cleanup
					Destroy();
				}
			}
			else
			{
				_status = Status.InsufficientMemory;
			}
		}
		else
		{
			_status = Status.InsufficientData;
		}
	}

	private void Initialize(int frameCount)
	{
		_frames = new Texture2D[frameCount];
		_currentTime = 0.0f;
		_duration = 0.0f;
		_times = new float[frameCount + 1];
		_decodedFrameCount = 0;
		_times[_decodedFrameCount] = _duration;
		_lastSample = null;
		_lastSampleLow = float.MaxValue;
		_lastSampleHigh = float.MinValue;
	}

	public void Destroy()
	{
		if(null != _frames)
		{
			for(int i = 0; i < _decodedFrameCount; ++i)
			{
				GameObject.DestroyImmediate(_frames[i]);
			}
		}

		Dispose(false);
	}

	private void DecodeNextFrame()
	{
		int width;
		int height;
		int delay; // Decoded frame delay (in 1/100th of a second as per GIF spec)
		System.IntPtr frameData = NSGIF_Decode(_handle, _decodedFrameCount, out width, out height, out delay, out _status);

		if(Status.OK == _status)
		{
			// New frame; add it to the list
			try
			{
				// Create a Unity Texture2D; decoded buffer is RGBA32
				Texture2D frameTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);

				// Load it with the decoded data; no copies
				frameTexture.LoadRawTextureData(frameData, width * height * 4);
				frameTexture.Apply(false, true);
				_frames[_decodedFrameCount++] = frameTexture;

				// Clamp delay to 3/100th of a second (30ms)
				if(delay < 3)
				{
					delay = 3;
				}

				// Keep track of the total sequence duration (in seconds)
				_duration += delay  / 100.0f;

				// And individual frame timestamps (in seconds); note that is is intentionally one index ahead
				_times[_decodedFrameCount] = _duration;
			}
			catch(System.Exception)
			{
				// More likely to have Unity crash before getting here...
				_status = Status.InsufficientMemory;
			}
		}

		if(_frames.Length == _decodedFrameCount)
		{
			// Decoded all frames
			_status = Status.DecodingFinished;
		}

		if(Status.OK != _status)
		{
			// Shutdown native side; either decoding finished or there was an error
			DisposeNative();
		}
	}

	private Texture Sample()
	{
		while(Status.OK == _status && _currentTime >= _duration)
		{
			// Keep decoding frames until either status becomes DecodingFinished or
			// we have enough frames to display the one for the requested timestamp
			DecodeNextFrame();
		}

		if(Status.OK != _status && Status.DecodingFinished != _status || 0 == _decodedFrameCount)
		{
			// Decoding error or empty gif
			return null;
		}

		// Ensure _currentTime E [0, _duration)
		// The division is always safe as the minimum possible duration is 0.03s
		float key = _currentTime - _duration * Mathf.Floor(_currentTime / _duration);

		if(_lastSampleLow <= key && key < _lastSampleHigh)
		{
			// In the same interval; bypass the binary search
			return _lastSample;
		}

		// Timestamps are sorted; binary search for the correct key
		int low = 0;
		int high = _decodedFrameCount - 1;
		int mid;
		while((mid = high - low) > 1)
		{
			mid >>= 1;
			mid += low;

			if(key < _times[mid])
			{
				high = mid;
			}
			else
			{
				low = mid;
			}
		}

		// Save timestamps for next time
		_lastSampleLow = _times[low];
		_lastSampleHigh = _times[high];

		// Always display the frame at the begining of the interval
		_lastSample = _frames[low];

		return _lastSample;
	}
};
