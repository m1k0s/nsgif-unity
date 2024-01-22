# nsgif-unity

Unity plugin for libnsgif (https://www.netsurf-browser.org/projects/libnsgif/).

TODO:
* Switch to using the newer Texture2D.GetPixelData NativeArray API and have GIF frames
  decode directly into Unity's texture memory - avoids the copy incurred by LoadRawTextureData
* Upgrade to libnsgif 1.0.0 (currently based on 0.1.4)
