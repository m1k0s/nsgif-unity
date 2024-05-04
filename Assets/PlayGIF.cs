using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace NSGIF
{
	[ExecuteAlways]
	public class PlayGIF : MonoBehaviour
	{
		private static readonly int MAIN_TEX_ID = Shader.PropertyToID("_MainTex");

		public string filename = "";

		private Material gifMaterial;
		private NSGIF gif = null;
		private string tempPath = null;

		private float elapsedTime = 0;
		private int animationTimeMillis = 0;

		void OnEnable()
		{
			gifMaterial = GetComponent<Renderer>()?.sharedMaterial;

			if (null == gifMaterial || string.IsNullOrEmpty(filename))
			{
				this.enabled = false;
				return;
			}

			try
			{
				string path = Application.streamingAssetsPath + "/" + filename;
				// path = "file://" + path;
				if (path.IndexOf("://", StringComparison.Ordinal) >= 0)
				{
					var req = UnityWebRequest.Get(path);
					tempPath =  Path.Combine(Application.temporaryCachePath, filename);
					path = tempPath;
					req.downloadHandler = new DownloadHandlerFile(path);
					req.SendWebRequest();

					while (!req.isDone)
						;

					if (req.result != UnityWebRequest.Result.Success)
					{
						throw new IOException($"uri={req.uri}, result = {req.result}, error={req.error}");
					}
				}

				gif = new NSGIF(path);
				gifMaterial.SetTexture(MAIN_TEX_ID, gif.frameTexture);
			}
			catch(System.Exception e)
			{
				Debug.LogError($"{GetType()}.OnEnable: {e}");
				this.enabled = false;
			}
		}

		void OnDisable()
		{
			if (null != gifMaterial)
			{
				gifMaterial.SetTexture(MAIN_TEX_ID, null);
				gifMaterial = null;
			}

			if (null != gif)
			{
				gif.Dispose();
				gif = null;
			}

			if (!string.IsNullOrEmpty(tempPath))
			{
				if (File.Exists(tempPath))
				{
					try
					{
						File.SetAttributes(tempPath, FileAttributes.Normal);
						File.Delete(tempPath);
					}
					catch (Exception e)
					{
						Debug.LogError($"{GetType()}.OnDisable: {e}");
					}
				}

				tempPath = null;
			}
		}

		void Update()
		{
			if (gif.frameCount == 1)
			{
				return;
			}

			elapsedTime += Time.deltaTime;

			float animationTime = animationTimeMillis / 1000.0f;
			float waitTime = animationTime - elapsedTime;
			if (waitTime > 0)
			{
				return;
			}

			int delayMillis = gif.DecodeNextFrame();

			if (gif.frameIndex == 0)
			{
				animationTimeMillis = delayMillis;
				elapsedTime = 0.0f;
			}
			else
			{
				animationTimeMillis += delayMillis;
			}
		}

#if UNITY_EDITOR
		void OnDrawGizmosSelected()
		{
			if (!Application.isPlaying)
			{
				UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
			}
		}
#endif
	}
}
