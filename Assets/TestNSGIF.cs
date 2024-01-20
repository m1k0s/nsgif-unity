using UnityEngine;
using System.Collections;

[ExecuteAlways]
public class TestNSGIF : MonoBehaviour
{
	private static readonly int MAIN_TEX_ID = Shader.PropertyToID("_MainTex");

	public string filename = "";

	private NSGIF gif;
	private Material gifMaterial;

	void OnEnable()
	{
		gifMaterial = GetComponent<Renderer>()?.sharedMaterial;

		if(null == gifMaterial)
		{
			this.enabled = false;
			return;
		}

		try
		{
			if(!string.IsNullOrEmpty(filename))
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

				Texture frame = gif.texture;
				Debug.LogFormat($"{filename}: width={frame.width} height={frame.height} frames={gif.frameCount}");

				gifMaterial.SetTexture(MAIN_TEX_ID, frame);
			}
			else
			{
				gifMaterial.SetTexture(MAIN_TEX_ID, null);
			}
		}
		catch(System.Exception e)
		{
			Debug.LogError($"{GetType()}.OnEnable: {e}");
			gifMaterial.SetTexture(MAIN_TEX_ID, null);
		}
	}

	void OnDisable()
	{
		if(null != gifMaterial)
		{
			gifMaterial.SetTexture(MAIN_TEX_ID, null);
			gifMaterial = null;
		}

		if(null != gif)
		{
			gif.Destroy();
			gif = null;
		}
	}

	void Update()
	{
		if(null != gif)
		{
			gif.currentTime += Time.deltaTime;
			Texture frame = gif.texture;
			gifMaterial.SetTexture(MAIN_TEX_ID, frame);
		}
	}
}
