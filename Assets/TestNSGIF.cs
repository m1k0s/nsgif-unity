using UnityEngine;
using System.Collections;

[ExecuteAlways]
public class TestNSGIF : MonoBehaviour
{
	private static readonly int MAIN_TEX_ID = Shader.PropertyToID("_MainTex");

	public string filename = "";

	private Material gifMaterial;
	private IEnumerator playback;
	private NSGIF gif = null;

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
#if UNITY_ANDROID  && !UNITY_EDITOR
			// On Android streaming assets path is in the compressed jar; use WWW to uncompress.
			WWW www = new WWW(path);
			while(!www.isDone)
				;
			gif = new NSGIF(www.bytes);
#else
			gif = new NSGIF(path);
			//gif = new NSGIF(System.IO.File.ReadAllBytes(path));
#endif

			playback = Play();
			StartCoroutine(playback);
		}
		catch(System.Exception e)
		{
			Debug.LogError($"{GetType()}.OnEnable: {e}");
			this.enabled = false;
		}
	}

	void OnDisable()
	{
		if (null != playback)
		{
			StopCoroutine(playback);
			playback = null;
		}

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
	}

	IEnumerator Play()
	{
		(Texture2D frame, int delayMillis) = gif.DecodeNextFrame();
		gifMaterial.SetTexture(MAIN_TEX_ID, frame);

		while (true)
		{
			yield return new WaitForSeconds(delayMillis / 1000.0f);
			(frame, delayMillis) = gif.DecodeNextFrame();
		}
	}
}
