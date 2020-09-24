using System;
using System.Collections.Generic;

namespace Unity.Tiny.ShaderCompiler
{
    class UnityHelper
    {
        internal const string kEmptyMessageError = "Internal error, empty message from the shader compiler process. Please report a bug including this shader and the editor log.";
        internal const string kUnknownMessageError = "Internal error, unrecognized message from the shader compiler process.  Please report a bug including this shader and the editor log.";
        internal const string kSocketExceptionError = "Internal error communicating with the shader compiler process.  Please report a bug including this shader and the editor log.";
        internal const string kSocketTimeoutError = "Compiler timed out. This can happen with extremely complex shaders or when the processing resources are limited. Try overriding the timeout with UNITY_SHADER_COMPILER_TASK_TIMEOUT_MINUTES environment variable.";

        internal enum ShaderCompilerCommand
        {
            kShaderCompilerInitialize,
            kShaderCompilerClearIncludesCache,
            kShaderCompilerShutdown,
            kShaderCompilerPreprocess,
            kShaderCompilerCompileComputeKernel,
            kShaderCompilerCompileSnippet,
            kShaderCompilerDisassembleShader,
            kShaderCompilerPreprocessCompute,
            kShaderCompilerCompileRayTracingShader,
            kShaderCompilerPreprocessRayTracingShader,

            kShaderCompilerCommandCount
        }

        const string commandPrefix = "c:";
        internal static Dictionary<ShaderCompilerCommand, string> commands = new Dictionary<ShaderCompilerCommand, string>
        {
            { ShaderCompilerCommand.kShaderCompilerInitialize, commandPrefix + "initializeCompiler"},
            { ShaderCompilerCommand.kShaderCompilerClearIncludesCache, commandPrefix + "clearIncludesCache"},
            { ShaderCompilerCommand.kShaderCompilerShutdown, commandPrefix + "shutdown"},
            { ShaderCompilerCommand.kShaderCompilerPreprocess, commandPrefix + "preprocess"},
            { ShaderCompilerCommand.kShaderCompilerCompileComputeKernel, commandPrefix + "compileComputeKernel"},
            { ShaderCompilerCommand.kShaderCompilerCompileSnippet, commandPrefix + "compileSnippet"},
            { ShaderCompilerCommand.kShaderCompilerDisassembleShader, commandPrefix + "disassembleShader"},
            { ShaderCompilerCommand.kShaderCompilerPreprocessCompute, commandPrefix + "preprocessCompute"},
            { ShaderCompilerCommand.kShaderCompilerCompileRayTracingShader, commandPrefix + "compileRayTracingShader"},
            { ShaderCompilerCommand.kShaderCompilerPreprocessRayTracingShader, commandPrefix + "preprocessRayTracingShader"}
        };

        internal enum ConstantType
        {
            kConstantTypeDefault = 0, // scale or vector
            kConstantTypeMatrix,
            kConstantTypeStruct,
        }

        // ShaderCompilerTypes.h ---------------------------------------------------------------------------------------------------------------------

        internal enum ShaderCompilerProgram
        {
            kProgramVertex = 0,
            kProgramFragment,
            kProgramHull,
            kProgramDomain,
            kProgramGeometry,
            kProgramSurface,
            kProgramRayTracing,
            kProgramCount
        }

        // Language that a shader snippet was written in

        internal enum ShaderSourceLanguage
        {
            kShaderSourceLanguageCg = 0,// HLSL/Cg ("CGPROGRAM") - translated into other languages if needed on various platforms
            kShaderSourceLanguageGLSL,  // GLSL - almost no processing, works only on OpenGL/ES
            kShaderSourceLanguageMetal, // Metal - almost no processing, works only on Metal
            kShaderSourceLanguageHLSL,  // HLSL ("HLSLPROGRAM") - same as Cg one, except some automatic #include files aren't added
            kShaderSourceLanguageCount,

            kShaderSourceLanguageNone = kShaderSourceLanguageCount
        }

        // values are serialised, if they change make sure to bump shader import
        // version (ShaderVersion.h) and Gpu Program version (GpuProgramManager.cpp).
        // Console platforms with their own GfxDevice implementation can use any
        // values above kShaderGpuProgramFirstConsole as the enum isn't used for
        // directing to the right place to create the GpuProgram data.
        internal enum ShaderGpuProgramType
        {
            kShaderGpuProgramUnknown = 0,

            kShaderGpuProgramGLLegacy_Removed = 1,
            kShaderGpuProgramGLES31AEP = 2,
            kShaderGpuProgramGLES31 = 3,
            kShaderGpuProgramGLES3 = 4,
            kShaderGpuProgramGLES = 5,
            kShaderGpuProgramGLCore32 = 6,
            kShaderGpuProgramGLCore41 = 7,
            kShaderGpuProgramGLCore43 = 8,
            kShaderGpuProgramDX9VertexSM20_Removed = 9,
            kShaderGpuProgramDX9VertexSM30_Removed = 10,
            kShaderGpuProgramDX9PixelSM20_Removed = 11,
            kShaderGpuProgramDX9PixelSM30_Removed = 12,
            kShaderGpuProgramDX10Level9Vertex_Removed = 13,
            kShaderGpuProgramDX10Level9Pixel_Removed = 14,
            kShaderGpuProgramDX11VertexSM40 = 15,
            kShaderGpuProgramDX11VertexSM50 = 16,
            kShaderGpuProgramDX11PixelSM40 = 17,
            kShaderGpuProgramDX11PixelSM50 = 18,
            kShaderGpuProgramDX11GeometrySM40 = 19,
            kShaderGpuProgramDX11GeometrySM50 = 20,
            kShaderGpuProgramDX11HullSM50 = 21,
            kShaderGpuProgramDX11DomainSM50 = 22,
            kShaderGpuProgramMetalVS = 23,
            kShaderGpuProgramMetalFS = 24,
            kShaderGpuProgramSPIRV = 25,

            kShaderGpuProgramConsoleVS = 26,
            kShaderGpuProgramConsoleFS = 27,
            kShaderGpuProgramConsoleHS = 28,
            kShaderGpuProgramConsoleDS = 29,
            kShaderGpuProgramConsoleGS = 30,

            kShaderGpuProgramRayTracing = 31,

            kShaderGpuProgramCount
        }

        /// <summary>
        /// Requirements grouped into rough "shader model" sets, that match the shader compilation
        /// "#pragma target" syntax.
        /// </summary>
        internal static class ShaderRequirements
        {
            internal const int kShaderRequireShaderModel20 = (int)UnityEditor.Rendering.ShaderRequirements.BaseShaders;

            // Note that DX11 FL9.3 (which this is modeled at) also supports four render targets,
            // but we don't explicitly pull that in, since many people use "#pragma target 2.5" just to enable
            // longer shaders/derivatives, without explicitly needing MRT. And many GLES2.0 platforms do not support
            // MRTs. So yes this is a bit confusing, but oh well.
            internal const int kShaderRequireShaderModel25_93 = kShaderRequireShaderModel20 | (int) UnityEditor.Rendering.ShaderRequirements.Derivatives;

            // Does not pull in MRT flag either, see above.
            internal const int kShaderRequireShaderModel30 = kShaderRequireShaderModel25_93 | (int)(UnityEditor.Rendering.ShaderRequirements.Interpolators10 | UnityEditor.Rendering.ShaderRequirements.SampleLOD | UnityEditor.Rendering.ShaderRequirements.FragCoord);

            internal const int kShaderRequireShaderModel35_ES3 = kShaderRequireShaderModel30 | (int)(UnityEditor.Rendering.ShaderRequirements.Interpolators15Integers | UnityEditor.Rendering.ShaderRequirements.MRT4 | UnityEditor.Rendering.ShaderRequirements.Texture2DArray | UnityEditor.Rendering.ShaderRequirements.Instancing);

            // Note: SM4.0 and up on DX11/PC does guarantee MRT8 and 32 varyings support; however on mobile GLES3.1/Metal/Vulkan
            // only guarantee MRT4 and 15 varyings. So let's not pull them in, to make life easier for people who just
            // write "#pragma target 5.0" in some shader (really only requiring compute or tessellation), and expect that to work
            // e.g. on Android GLES3.1+AEP (has both compute & tessellation, but only 4MRT).
            //
            // Similar for CubeArray requirement; don't make it be required in SM4.6/5.0.
            // Similar for MSAATex requirement; don't make it be required in SM4.0 (only starting with shader models that guarantee Compute).
            internal const int kShaderRequireShaderModel40 = kShaderRequireShaderModel35_ES3 | (int) UnityEditor.Rendering.ShaderRequirements.Geometry;
            internal const int kShaderRequireShaderModel50 = kShaderRequireShaderModel40 | (int)(UnityEditor.Rendering.ShaderRequirements.Compute | UnityEditor.Rendering.ShaderRequirements.RandomWrite | UnityEditor.Rendering.ShaderRequirements.TessellationCompute | UnityEditor.Rendering.ShaderRequirements.TessellationShaders | UnityEditor.Rendering.ShaderRequirements.MSAATextureSamples);
            internal const int kShaderRequireShaderModel50_Metal = kShaderRequireShaderModel50 & ~((int)UnityEditor.Rendering.ShaderRequirements.Geometry);

            internal const int kShaderRequireShaderModel40_PC = kShaderRequireShaderModel40 | (int)(UnityEditor.Rendering.ShaderRequirements.Interpolators32 | UnityEditor.Rendering.ShaderRequirements.MRT8);
            internal const int kShaderRequireShaderModel41_PC = kShaderRequireShaderModel40_PC | (int)(UnityEditor.Rendering.ShaderRequirements.CubeArray | UnityEditor.Rendering.ShaderRequirements.MSAATextureSamples);
            internal const int kShaderRequireShaderModel50_PC = kShaderRequireShaderModel41_PC | kShaderRequireShaderModel50;

            // "strange" shader model sets that aren't strictly increasing supersets of previous ones
            internal const int kShaderRequireShaderModel45_ES31 = kShaderRequireShaderModel35_ES3 | (int)(UnityEditor.Rendering.ShaderRequirements.Compute | UnityEditor.Rendering.ShaderRequirements.RandomWrite | UnityEditor.Rendering.ShaderRequirements.MSAATextureSamples); // "4.5": GLES3.1 / MobileMetal (or DX10 SM5 without geometry, tessellation, cube arrays)
            internal const int kShaderRequireShaderModel46_GL41 = kShaderRequireShaderModel40 | (int)(UnityEditor.Rendering.ShaderRequirements.TessellationCompute | UnityEditor.Rendering.ShaderRequirements.TessellationShaders | UnityEditor.Rendering.ShaderRequirements.MSAATextureSamples); // "4.6": DX10 SM4 + tessellation (but without compute)
        }

        // -------------------------------------------------------------------------------------------------------------------------------------------
        // GfxDeviceTypes.h --------------------------------------------------------------------------------------------------------------------------
        // VertexDataLayout in script uses same order and must be kept in sync.
        // Must be kept in sync with VertexChannelCompressionFlags. Range must fit into an SInt8.

        internal enum ShaderChannel
        {
            kShaderChannelNone = -1,
            kShaderChannelVertex = 0,   // Vertex (vector3)
            kShaderChannelNormal,       // Normal (vector3)
            kShaderChannelTangent,      // Tangent (vector4)
            kShaderChannelColor,        // Vertex color
            kShaderChannelTexCoord0,    // Texcoord 0
            kShaderChannelTexCoord1,    // Texcoord 1
            kShaderChannelTexCoord2,    // Texcoord 2
            kShaderChannelTexCoord3,    // Texcoord 3
            kShaderChannelTexCoord4,    // Texcoord 4
            kShaderChannelTexCoord5,    // Texcoord 5
            kShaderChannelTexCoord6,    // Texcoord 6
            kShaderChannelTexCoord7,    // Texcoord 7
            kShaderChannelBlendWeights, // Blend weights
            kShaderChannelBlendIndices, // Blend indices
            kShaderChannelCount,        // Keep this last!
        }

        internal enum ShaderParamType
        {
            kShaderParamFloat = 0,
            kShaderParamInt,
            kShaderParamBool,
            kShaderParamHalf, // 16 bit float
            kShaderParamShort, // 16 bit int
            kShaderParamUInt, // 32 bit unsigned int
            kShaderParamTypeCount
        }

        internal enum TextureDimension
        {
            kTexDimUnknown = -1, // unknown
            kTexDimNone = 0, // no texture
            kTexDimAny, // special value that indicates "any" texture type can be used here; used very rarely in shader property metadata
            kTexDim2D, kTexDimFirst = kTexDim2D,
            kTexDim3D,
            kTexDimCUBE,
            kTexDim2DArray,
            kTexDimCubeArray,
            kTexDimLast = kTexDimCubeArray,

            kTexDimCount, // keep this last!
            kTexDimForce32Bit = 0x7fffffff
        }

        /// <remarks>
        /// TODO: We should be able to get this value from <see cref="UnityEditor.Rendering.ShaderCompilerPlatform"/> but it doesn't match the native version as of 2020.2
        /// </remarks>
#if UNITY_2020_2_OR_NEWER
        internal const int k_ShaderCompilerPlatformCount = 23;
#else
        internal const int k_ShaderCompilerPlatformCount = 21;
#endif

        /// <remarks>
        /// See kKnownHLSLccInputs in CompilerHLSLcc.cpp
        /// </remarks>
        internal static ShaderChannel ConvertHLSLccAttributeName(string name)
        {
            switch (name)
            {
                case "POSITION0":
                case "in_POSITION0":
                    return ShaderChannel.kShaderChannelVertex;
                case "VCOLOR0":
                case "in_VCOLOR0":
                    return ShaderChannel.kShaderChannelColor;
                case "NORMAL0":
                case "in_NORMAL0":
                    return ShaderChannel.kShaderChannelNormal;
                case "TANGENT0":
                case "in_TANGENT0":
                    return ShaderChannel.kShaderChannelTangent;
                case "COLOR0":
                case "in_COLOR0":
                    return ShaderChannel.kShaderChannelColor;
                case "TEXCOORD0":
                case "in_TEXCOORD0":
                    return ShaderChannel.kShaderChannelTexCoord0;
                case "TEXCOORD1":
                case "in_TEXCOORD1":
                    return ShaderChannel.kShaderChannelTexCoord1;
                case "TEXCOORD2":
                case "in_TEXCOORD2":
                    return ShaderChannel.kShaderChannelTexCoord2;
                case "TEXCOORD3":
                case "in_TEXCOORD3":
                    return ShaderChannel.kShaderChannelTexCoord3;
                case "TEXCOORD4":
                case "in_TEXCOORD4":
                    return ShaderChannel.kShaderChannelTexCoord4;
                case "TEXCOORD5":
                case "in_TEXCOORD5":
                    return ShaderChannel.kShaderChannelTexCoord5;
                case "TEXCOORD6":
                case "in_TEXCOORD6":
                    return ShaderChannel.kShaderChannelTexCoord6;
                case "TEXCOORD7":
                case "in_TEXCOORD7":
                    return ShaderChannel.kShaderChannelTexCoord7;
                case "BLENDWEIGHTS0":
                case "in_BLENDWEIGHTS0":
                    return ShaderChannel.kShaderChannelBlendWeights;
                case "BLENDINDICES0":
                case "in_BLENDINDICES0":
                    return ShaderChannel.kShaderChannelBlendIndices;
                default:
                    throw new NotSupportedException($"Unrecognized attribute {name} from Unity shader compiler.");
            }
        }

        /// <remarks>
        /// See ShaderCompilerGetPlatformName in ShaderCompilerClient.cpp
        /// </remarks>
        internal static string ShaderCompilerGetPlatformName(UnityEditor.Rendering.ShaderCompilerPlatform platform)
        {
            switch (platform)
            {
                case UnityEditor.Rendering.ShaderCompilerPlatform.D3D:
                    return "d3d11";
                case UnityEditor.Rendering.ShaderCompilerPlatform.GLES20:
                    return "gles";
                case UnityEditor.Rendering.ShaderCompilerPlatform.GLES3x:
                    return "gles3";
                case UnityEditor.Rendering.ShaderCompilerPlatform.Metal:
                    return "metal";
                case UnityEditor.Rendering.ShaderCompilerPlatform.OpenGLCore:
                    return "glcore";
                case UnityEditor.Rendering.ShaderCompilerPlatform.Vulkan:
                    return "vulkan";
                default:
                    return $"Unsupported platform {platform}";
            }
        }
    }
}
