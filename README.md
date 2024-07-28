# nsgif-unity

Unity native plugin for libnsgif (https://www.netsurf-browser.org/projects/libnsgif/).

[NSGIF](Assets/NSGIF/NSGIF.cs) exposes the c# bindings to `libnsgif` and is responsible for decoding successive frames into a `Texture2D`. Uses `NativeArrayUnsafeUtility` to decode pixels straight into texture memory. Seeking is currently not supported; the only API offered is `DecodeNextFrame` and it is the responsibility of the caller to manage timing. The native plugin supports:
* iOS (gets build from source as part of the normal IL2CPP build process)
* Android (includes prebuild .so files for v7a, v8a, x86, x86_64)
* macOS (includes prebuilt .bundle for intel & Apple silicon)
* Windows (includes prebuilt .dll for x86 & x86_64)
See [BUILD.md](BUILD.md) if you need to rebuild the native code.

The [GIFPlayer](Assets/NSGIF/GIFPlayer.cs) component exposes a VideoPlayer-like API where it makes sense:
* loads a gif file from `StreamingAssets`
* needs to be added to a `GameObject` with a `Renderer` component as it replaces the renderer `sharedMaterial` with one using the decoded texture as `_MainTex`
* manages a separate decode thread leaving the Unity main thread free
* manages sleep intervals between decoded frames

The sample Unity project includes a test scene, a couple of sample gifs and a simple Start/Pause/Stop test UI.

TODO:
* Support loading from other places, like Addressables.
* Upgrade to libnsgif 1.0.0 (currently based on 0.1.4).

Prerquisites:
* Tested with Unity 2021.3.16f1
