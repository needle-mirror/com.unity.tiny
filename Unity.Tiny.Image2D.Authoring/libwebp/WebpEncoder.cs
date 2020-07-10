using System;
using System.Runtime.InteropServices;

namespace WebP
{
    public enum WebPImageHint {
        WEBP_HINT_DEFAULT = 0,  // default preset.
        WEBP_HINT_PICTURE,      // digital picture, like portrait, inner shot
        WEBP_HINT_PHOTO,        // outdoor photograph, with natural lighting
        WEBP_HINT_GRAPH,        // Discrete tone image (graph, map-tile etc).
        WEBP_HINT_LAST
    }

    public enum WebPPreset
    {
        WEBP_PRESET_DEFAULT = 0,  // default preset.
        WEBP_PRESET_PICTURE,      // digital picture, like portrait, inner shot
        WEBP_PRESET_PHOTO,        // outdoor photograph, with natural lighting
        WEBP_PRESET_DRAWING,      // hand or line drawing, with high-contrast details
        WEBP_PRESET_ICON,         // small-sized colorful images
        WEBP_PRESET_TEXT          // text-like
    }

    public enum WebPEncCSP {
        // chroma sampling
        WEBP_YUV420  = 0,        // 4:2:0
        WEBP_YUV420A = 4,        // alpha channel variant
        WEBP_CSP_UV_MASK = 3,    // bit-mask to get the UV sampling factors
        WEBP_CSP_ALPHA_BIT = 4   // bit that is set if alpha is present
    }

    public enum WebPEncodingError {
        VP8_ENC_OK = 0,
        VP8_ENC_ERROR_OUT_OF_MEMORY,            // memory error allocating objects
        VP8_ENC_ERROR_BITSTREAM_OUT_OF_MEMORY,  // memory error while flushing bits
        VP8_ENC_ERROR_NULL_PARAMETER,           // a pointer parameter is NULL
        VP8_ENC_ERROR_INVALID_CONFIGURATION,    // configuration is invalid
        VP8_ENC_ERROR_BAD_DIMENSION,            // picture has invalid width/height
        VP8_ENC_ERROR_PARTITION0_OVERFLOW,      // partition is bigger than 512k
        VP8_ENC_ERROR_PARTITION_OVERFLOW,       // partition is bigger than 16M
        VP8_ENC_ERROR_BAD_WRITE,                // error while flushing bytes
        VP8_ENC_ERROR_FILE_TOO_BIG,             // file is bigger than 4G
        VP8_ENC_ERROR_USER_ABORT,               // abort request by user
        VP8_ENC_ERROR_LAST                      // list terminator. always last.
    }

    [StructLayoutAttribute(LayoutKind.Sequential)]
    public unsafe struct WebPConfig
    {
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
        public int lossless;           // Lossless encoding (0=lossy(default), 1=lossless).
        public float quality;          // between 0 and 100. For lossy, 0 gives the smallest
                              // size and 100 the largest. For lossless, this
                              // parameter is the amount of effort put into the
                              // compression: 0 is the fastest but gives larger
                              // files compared to the slowest, but best, 100.
        public int method;             // quality/speed trade-off (0=fast, 6=slower-better)

        public WebPImageHint image_hint;  // Hint for image type (lossless only for now).

        public int target_size;        // if non-zero, set the desired target size in bytes.
                              // Takes precedence over the 'compression' parameter.
        public float target_PSNR;      // if non-zero, specifies the minimal distortion to
                              // try to achieve. Takes precedence over target_size.
        public int segments;           // maximum number of segments to use, in [1..4]
        public int sns_strength;       // Spatial Noise Shaping. 0=off, 100=maximum.
        public int filter_strength;    // range: [0 = off .. 100 = strongest]
        public int filter_sharpness;   // range: [0 = off .. 7 = least sharp]
        public int filter_type;        // filtering type: 0 = simple, 1 = strong (only used
                                // if filter_strength > 0 or autofilter > 0)
        public int autofilter;         // Auto adjust filter's strength [0 = off, 1 = on]
        public int alpha_compression;  // Algorithm for encoding the alpha plane (0 = none,
                              // 1 = compressed with WebP lossless). Default is 1.
        public int alpha_filtering;    // Predictive filtering method for alpha plane.
                              //  0: none, 1: fast, 2: best. Default if 1.
        public int alpha_quality;      // Between 0 (smallest size) and 100 (lossless).
                              // Default is 100.
        public int pass;               // number of entropy-analysis passes (in [1..10]).

        public int show_compressed;    // if true, export the compressed picture back.
                              // In-loop filtering is not applied.
        public int preprocessing;      // preprocessing filter:
                              // 0=none, 1=segment-smooth, 2=pseudo-random dithering
        public int partitions;         // log2(number of token partitions) in [0..3]. Default
                              // is set to 0 for easier progressive decoding.
        public int partition_limit;    // quality degradation allowed to fit the 512k limit
                              // on prediction modes coding (0: no degradation,
                              // 100: maximum possible degradation).
        public int emulate_jpeg_size;  // If true, compression parameters will be remapped
                              // to better match the expected output size from
                              // JPEG compression. Generally, the output size will
                              // be similar but the degradation will be lower.
        public int thread_level;       // If non-zero, try and use multi-threaded encoding.
        public int low_memory;         // If set, reduce memory usage (but increase CPU use).

        public int near_lossless;      // Near lossless encoding [0 = max loss .. 100 = off
                              // (default)].
        public int exact;              // if non-zero, preserve the exact RGB values under
                              // transparent area. Otherwise, discard this invisible
                              // RGB information for better compression. The default
                              // value is 0.

        public int use_delta_palette;  // reserved for future lossless feature
        public int use_sharp_yuv;      // if needed, use sharp (and slow) RGB->YUV conversion

        fixed UInt32 pad[2];        // padding for later use
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value
    }

    // Signature for output function. Should return true if writing was successful.
    // data/data_size is the segment of data to write, and 'picture' is for
    // reference (and so one can make use of picture->custom_ptr).
    public delegate int WebPWriterFunction([InAttribute()] IntPtr data, UIntPtr data_size, ref WebPPicture picture);

    // Progress hook, called from time to time to report progress. It can return
    // false to request an abort of the encoding process, or true otherwise if
    // everything is OK.
    public delegate int WebPProgressHook(int percent, ref WebPPicture picture);

    [StructLayoutAttribute(LayoutKind.Sequential)]
    public struct WebPPicture {
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
        public int use_argb;
        public WebPEncCSP colorspace; // colorspace: should be YUV420 for now (=Y'CbCr).
        public int width;             // dimensions (less or equal to WEBP_MAX_DIMENSION)
        public int height;
        public IntPtr y;              // pointers to luma/chroma planes.
        public IntPtr u;
        public IntPtr v;
        public int y_stride;          // luma/chroma strides.
        public int uv_stride;
        public IntPtr a;              // pointer to the alpha plane
        public int a_stride;          // stride of the alpha plane
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 2, ArraySubType = UnmanagedType.U4)]
        public uint[] pad1;           // padding for later use
        public IntPtr argb;           // Pointer to argb (32 bit) plane.
        public int argb_stride;       // This is stride in pixels units, not bytes.
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3, ArraySubType = UnmanagedType.U4)]
        public uint[] pad2;           // padding for later use
        public WebPWriterFunction writer;// can be NULL
        public IntPtr custom_ptr;     // can be used by the writer.
        public int extra_info_type;   // 1: intra type, 2: segment, 3: quant
                                      // 4: intra-16 prediction mode,
                                      // 5: chroma prediction mode,
                                      // 6: bit cost, 7: distortion
        public IntPtr extra_info;     // if not NULL, points to an array of size
                                    // ((width + 15) / 16) * ((height + 15) / 16) that
                                    // will be filled with a macroblock map, depending
                                    // on extra_info_type.
        public IntPtr stats;            // Pointer to side statistics (updated only if not NULL)
        public WebPEncodingError error_code;  // Error code for the latest error encountered during encoding
        public WebPProgressHook progress_hook; // If not NULL, report progress during encoding.
        public IntPtr user_data;        // this field is free to be set to any value and
                                        // used during callbacks (like progress-report e.g.).
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3, ArraySubType = UnmanagedType.U4)]
        public uint[] pad3;             // padding for later use
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 7, ArraySubType = UnmanagedType.U4)]
        public IntPtr pad4;            // padding for later use
        public IntPtr pad5;            // padding for later use
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 8, ArraySubType = UnmanagedType.U4)]
        public uint[] pad6;
        IntPtr memory_;
        IntPtr memory_argb_;
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 2, ArraySubType = UnmanagedType.SysUInt)]
        IntPtr[] pad7;
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value
    };

    [StructLayoutAttribute(LayoutKind.Sequential)]
    public struct WebPMemoryWriter
    {
        public IntPtr mem;
        public UIntPtr size;
        public UIntPtr max_size;
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 1, ArraySubType = UnmanagedType.U4)]
        public uint[] pad;
    }

    //WebpEncoding c# wrapper
    public static class WebpEncoderNativeCalls
    {
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
        const string LibName = "libwebp";
#elif UNITY_STANDALONE_LINUX
	    const string LibName = "webp";
#endif

        //Advanced encoding API
        [DllImport(LibName, EntryPoint = "WebPGetEncoderVersion")]
        public static extern int WebPGetEncoderVersion();

        [DllImport(LibName, EntryPoint = "WebPConfigInitInternal")]
        public static extern int WebPConfigInitInternal(ref WebPConfig config, WebPPreset preset, float quality, int version);

        [DllImport(LibName, EntryPoint = "WebPConfigLosslessPreset")]
        public static extern int WebPConfigLosslessPreset(ref WebPConfig config, int level);

        [DllImport(LibName, EntryPoint = "WebPValidateConfig")]
        public static extern int WebPValidateConfig(ref WebPConfig config);

        [DllImport(LibName, EntryPoint = "WebPPictureAlloc")]
        public static extern int WebPPictureAlloc(ref WebPPicture picture);

        [DllImport(LibName, EntryPoint = "WebPPictureFree")]
        public static extern void WebPPictureFree(ref WebPPicture picture);

        [DllImport(LibName, EntryPoint = "WebPPictureInitInternal")]
        public static extern int WebPPictureInitInternal(ref WebPPicture picture, int version);

        [DllImport(LibName, EntryPoint = "WebPPictureImportRGB")]
        public static extern unsafe int WebPPictureImportRGB(ref WebPPicture picture, byte* rgb, int rgb_stride);

        [DllImport(LibName, EntryPoint = "WebPPictureImportRGBA")]
        public static extern unsafe int WebPPictureImportRGBA(ref WebPPicture picture, byte* rgba, int rgba_stride);

        [DllImport(LibName, EntryPoint = "WebPEncode")]
        public static extern int WebPEncode(ref WebPConfig config, ref WebPPicture picture);
    }
}
