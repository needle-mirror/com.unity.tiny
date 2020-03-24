# Known Issues

The following issues will be fixed in a future release:

* asm.js builds: low performance in Chrome on all build modes.

    Workaround: use WebAssembly

* Desktop Safari: visual colorspace issues due to sRGB implementation.  (The rendering will look darker than expected.)

* Older iOS devices may crash on startup, due to a missing feature in their Metal implementation.

* Auto-rotate is enabled by default on all platforms.

    Will be made configurable in a future release.

* Android: audio continues to play even if the app is not active.

* Rendering is done at 1080p internal resolution and then scaled up/down to the target display.

    Will be made configurable and more flexible in a future release.

* In TinyRacing project, when entering play mode in the editor the scene will look different than it does in a built player.

    Ongoing work to visually unify the renderers.  Will be addressed over time in future releases.

* Currently you need Desktop development with c++, .NET desktop development in Visual Studio installed even for building non-Windows targets (e.g. web).

    This will be fixed soon.

* The editor platform must be set to `PC, Mac & Linux Standalone`, unexpected errors may show up if the editor is switched to different platforms.
