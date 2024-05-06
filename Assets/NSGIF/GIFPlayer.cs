using System;
using System.Collections;
using System.IO;
using System.Threading;
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
        [Range(0.0f, 10.0f)]
        public float speed = 1.0f;
        
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
        private Thread decodeThread;
        private EventWaitHandle waitHandleMain;
        private EventWaitHandle waitHandleDecode;
        private int delayMillis;
        private bool decoderRunning;

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
            
            DestroyDecodeThread();
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

            DestroyDecodeThread();
        }

        private void InitialiseDecodeThread()
        {
            waitHandleMain = new EventWaitHandle(false, EventResetMode.AutoReset);
            waitHandleDecode = new EventWaitHandle(false, EventResetMode.AutoReset);
            
            decodeThread = new Thread(Decode);
            decodeThread.Name = $"{GetType()}.{gameObject.name}";
            decodeThread.Start();
        }

        private void DestroyDecodeThread()
        {
            if (decodeThread != null)
            {
                // Let the thread naturally exit (rather than abort it)
                decoderRunning = false;
                waitHandleDecode?.Set();
                decodeThread = null;
            }

            if (waitHandleMain != null)
            {
                waitHandleMain.Dispose();
                waitHandleMain = null;
            }

            if (waitHandleDecode != null)
            {
                waitHandleDecode.Dispose();
                waitHandleDecode = null;
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

            InitialiseDecodeThread();

            float elapsedTime = 0.0f;

            while (true)
            {
                float currentTime = Time.time;
                elapsedTime += (currentTime - lastTime) * speed;
                lastTime = currentTime;

                float animationTime = animationTimeMillis / 1000.0f;
                while (elapsedTime < animationTime)
                {
                    // WaitForSeconds annoyingly allocates... Skip frames instead
                    yield return null;
                    if (null == gif)
                    {
                        // If the gif instance was destroyed, stop
                        goto abort;
                    }
                    currentTime = Time.time;
                    elapsedTime += (currentTime - lastTime) * speed;
                    lastTime = currentTime;
                }

                
                waitHandleDecode.Set();
                waitHandleMain.WaitOne();
                gif.texture.Apply(false, false);

                if (gif.frame == 0)
                {
                    if (!loop)
                    {
                        // Reached the end and not looping, stop
                        goto abort;
                    }
                    animationTimeMillis = delayMillis;
                    elapsedTime = 0.0f;
                }
                else
                {
                    animationTimeMillis += delayMillis;
                }
            }
            
        abort:
            DestroyDecodeThread();
            yield break;
        }

        private void Decode()
        {
            decoderRunning = true;
            
            waitHandleDecode.WaitOne();
            
            while (decoderRunning)
            {
                delayMillis = gif.DecodeNextFrame(false);
                WaitHandle.SignalAndWait(waitHandleMain, waitHandleDecode);
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
