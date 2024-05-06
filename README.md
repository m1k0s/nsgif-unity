# nsgif-unity

Unity plugin for libnsgif (https://www.netsurf-browser.org/projects/libnsgif/).

The GIFPlayer component exposes a VideoPlayer-like API where it makes sense.

Decoding is done:
* directly into the Texture2D NativeArray raw data
* in a separate decode thread leave the Unity main thread free

TODO:
* Upgrade to libnsgif 1.0.0 (currently based on 0.1.4)
