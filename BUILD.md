# nsgif-unity

All the native plugin build projects live in [nsgif-projects](nsgif-projects). The native source (from which all platforms are built) lives in [Assets/NSGIF/iOS/](Assets/NSGIF/iOS/).

# iOS

Nothing special needed - the source gets compiled as part of the normal IL2CPP build process.

# Android

On a mac, run [nsgif-projects/android/build.sh](nsgif-projects/android/build.sh).

# macOS

Xcode project in [nsgif-projects/mac/](nsgif-projects/mac/). Build artifacts will need to be manually copied to [Assets/NSGIF/](Assets/NSGIF/). Note that the Unity editor does not unload DLLs once loaded and will need restarting to pick up new changes.

# Windows

VisualStudio project in [nsgif-projects/windows/](nsgif-projects/windows/). Build artifacts will need to be manually copied to [Assets/NSGIF/x86](Assets/NSGIF/x86) and [Assets/NSGIF/x86_64](Assets/NSGIF/x86_64). Note that the Unity editor does not unload DLLs once loaded and will need restarting to pick up new changes.
