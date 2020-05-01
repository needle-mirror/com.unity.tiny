using System;
using Unity.Entities;

namespace Unity.Tiny
{
    [Flags]
    public enum ScreenOrientation
    {
        Unknown = 0,
        Portrait = 1,
        PortraitUpsideDown = 2,
        ReversePortrait = 2,
        Landscape = 4,
        LandscapeRight = 4,
        LandscapeLeft = 8,
        ReverseLandscape = 8,
        AutoRotationPortrait = Portrait | ReversePortrait,
        AutoRotationLandscape = Landscape | ReverseLandscape,
        AutoRotation = Portrait | ReversePortrait | Landscape | ReverseLandscape
    }

    public enum ColorSpace
    {
        Linear = 0,
        Gamma = 1
    }

    /// <summary>
    ///  Configures display-related parameters. You can access this component via
    ///  TinyEnvironment.Get/SetConfigData&lt;DisplayInfo&gt;()
    /// </summary>
    //[HideInInspector]
    public struct DisplayInfo : IComponentData
    {
        public static DisplayInfo Default { get; } = new DisplayInfo
        {
            width = 1280,
            height = 720,
            autoSizeToFrame = true
        };

        /// <summary>
        /// Specifies the output width, in logical pixels. Writing will resize the window, where supported.
        /// </summary>
        public int width;

        /// <summary>
        /// Specifies the output height, in logical pixels.
        /// Writing will resize the window, where supported.
        /// </summary>
        public int height;

        /// <summary>
        /// Specifies the output height, in physical pixels.
        /// Read-only, but it can be useful for shaders or texture allocations.
        /// </summary>
        public int framebufferHeight;

        /// <summary>
        /// Specifies the output width, in physical pixels.
        /// Read-only, but it can be useful for shaders or texture allocations.
        /// </summary>
        public int framebufferWidth;

        /// <summary>
        ///  If set to true, the output automatically resizes to fill the frame
        ///  (the browser or application window), and match the orientation.
        ///  Changing output width or height manually has no effect.
        /// </summary>
        public bool autoSizeToFrame;

        /// <summary>
        ///  Specifies the frame width, in pixels. This is the width of the browser
        ///  or application window.
        /// </summary>
        public int frameWidth;

        /// <summary>
        ///  Specifies the frame height, in pixels. This is the height of the browser
        ///  or application window.
        /// </summary>
        public int frameHeight;

        /// <summary>
        ///  Specifies the device display (screen) width, in pixels.
        /// </summary>
        public int screenWidth;

        /// <summary>
        ///  Specifies the device display (screen) height, in pixels.
        /// </summary>
        public int screenHeight;

        /// <summary>
        ///  Specifies the scale of the device display (screen) DPI relative to.
        ///  96 DPI. For example, a value of 2.0 yields 192 DPI (200% of 96).
        /// </summary>
        public float screenDpiScale;

        /// <summary>
        ///  Specifies the device display (screen) orientation.
        /// </summary>
        public ScreenOrientation orientation;

        /// <summary>
        ///  Specifies whether the browser or application window has focus.
        ///  Read only; setting this value has no effect.
        /// </summary>
        public bool focused;

        /// <summary>
        ///  Specifies whether the browser or application window is currently visible
        ///  on the screen/device display.
        ///  Read only; setting this value has no effect.
        /// </summary>
        public bool visible;

        /// <summary>
        ///  Specifies whether swapping should not wait for vertical sync.
        ///  By default rendering will wait for sync is.
        ///  Disabling the wait for sync might not be possible on some platforms, like inside a web browser.
        /// </summary>
        public bool disableVSync;

        /// <summary>
        ///  Color space to use for rendering.
        ///  The default and recommended color space is Linear, where shader math and belnding is correct.
        ///  In linear space srgb encodings are properly handled, and the backbuffer is srgb.
        ///  Gamma space disables all srgb de and encoding.
        ///  Using gamma space is required to run on older devices and some web browsers that do not
        ///  support srgb textures.
        /// </summary>
        public ColorSpace colorSpace;

        /// <summary>
        /// Color to clear the background when rendering at fixed aspect
        /// This color is the "black bars" where there is no rendering and the rendering aspect is not the same
        /// as the display aspect.
        /// Cameras still clear with their own color
        /// </summary>
        public Color backgroundBorderColor;  // linear color
    }
}
