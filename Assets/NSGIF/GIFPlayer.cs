using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace NSGIF
{
    [ExecuteAlways]
    public class GIFPlayer : MonoBehaviour
    {
        private static readonly int MAIN_TEX_ID = Shader.PropertyToID("_MainTex");

        public string filename = "";
        public bool playOnEnable = true;
        public bool loop = false;
        
        public Texture texture => gif?.texture;
        public int width => texture ? texture.width : 0;
        public int height => texture ? texture.height : 0;
        public bool isPaused => player == null;
        public bool isLooping => loop;
        public bool isPrepared => this.enabled && gif != null;
        public int frame => gif != null ? gif.frame : -1;
        public int frameCount => gif != null ? gif.frameCount : 0;
        public string url  { get; private set; }

        private Material gifMaterial;
        private NSGIF gif;
        private string tempPath;
        private Coroutine player;

        public void Play()
        {
            if (!this.enabled)
            {
                return;
            }

            Pause();
            player = StartCoroutine(Playback());
        }

        public void Pause()
        {
            if (null != player)
            {
                StopCoroutine(player);
                player = null;
            }
        }

        public void Stop()
        {
            Pause();
            gif?.Reset();
            gifMaterial?.SetTexture(MAIN_TEX_ID, null);
        }

        private void OnEnable()
        {
            gifMaterial = GetComponent<Renderer>()?.sharedMaterial;

            if (null == gifMaterial || string.IsNullOrEmpty(filename))
            {
                this.enabled = false;
                return;
            }

            StartCoroutine(Prepare());
        }

        private void OnDisable()
        {
            Stop();

            if (null != gif)
            {
                gif.Dispose();
                gif = null;
            }

            if (!string.IsNullOrEmpty(tempPath))
            {
                DeleteFile(tempPath);
                tempPath = null;
            }
        }

        private static void DeleteFile(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                }
                catch (Exception e)
                {
                    Debug.LogError($"DeleteFile({path}): {e}");
                }
            }
        }
        
        private IEnumerator Prepare()
        {
            string path = Application.streamingAssetsPath + "/" + filename;
            // path = "file://" + path;

            url = path;
            if (path.IndexOf("://", StringComparison.Ordinal) >= 0)
            {
                var req = UnityWebRequest.Get(path);
                path = Path.Combine(Application.temporaryCachePath, filename);
                req.downloadHandler = new DownloadHandlerFile(path);
                tempPath = path;
                yield return req.SendWebRequest();
                
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"{GetType()}.Prepare: uri={req.uri}, result = {req.result}, error={req.error}");
                    this.enabled = false;
                    yield break;
                }
            }
            
            try
            {
                gif = new NSGIF(path);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"{GetType()}.Prepare: {e}");
                this.enabled = false;
            }
            
            if (this.enabled && playOnEnable)
            {
                Play();
            }
        }
            
        private IEnumerator Playback()
        {
            if (null == gif)
            {
                yield break;
            }

            float lastTime = Time.time;
            int animationTimeMillis = gif.DecodeNextFrame();

            // After the first frame has been decoded, set it on the material
            gifMaterial.SetTexture(MAIN_TEX_ID, gif.texture);

            // Only one frame; stop
            if (gif.frameCount == 1)
            {
                yield break;
            }

            float elapsedTime = 0.0f;

            while (true)
            {
                float currentTime = Time.time;
                elapsedTime += currentTime - lastTime;
                lastTime = currentTime;

                float animationTime = animationTimeMillis / 1000.0f;
                while (elapsedTime < animationTime)
                {
                    // WaitForSeconds annoyingly allocates... Skip frames instead
                    yield return null;
                    if (null == gif)
                    {
                        // If the gif instance was destroyed, stop
                        yield break;
                    }
                    currentTime = Time.time;
                    elapsedTime += currentTime - lastTime;
                    lastTime = currentTime;
                }

                int delayMillis = gif.DecodeNextFrame();
                if (gif.frame == 0)
                {
                    if (!loop)
                    {
                        // Reached the end and not looping, stop
                        yield break;
                    }
                    animationTimeMillis = delayMillis;
                    elapsedTime = 0.0f;
                }
                else
                {
                    animationTimeMillis += delayMillis;
                }
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
