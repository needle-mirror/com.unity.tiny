using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Unity.Assertions;
using Unity.Build.DotsRuntime;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Tiny.Rendering;
using Unity.TinyConversion;
using UnityEditor.Rendering;

namespace Unity.Tiny.ShaderCompiler
{
    enum Stage
    {
        Vertex,
        Fragment
    }

    struct CompiledShader
    {
        internal UnityHelper.ShaderGpuProgramType type;
        internal byte[] outputCode;
    }

    /// <summary>
    /// Compiled shader binary + reflection data
    /// </summary>
    struct CompiledShaderData
    {
        internal CompiledShader shader;
        internal List<UnityHelper.ShaderChannel> attributes;
        internal List<ConstantBuffer> constantBuffers;
        internal List<TextureSampler> samplers;

        // Map of texture registers to sampler registers that need to be patched
        internal Dictionary<uint, uint> texToSampler;

        internal void Init()
        {
            attributes = new List<UnityHelper.ShaderChannel>();
            constantBuffers = new List<ConstantBuffer>();
            samplers = new List<TextureSampler>();
            texToSampler = new Dictionary<uint, uint>();
        }
    }

    struct ConstantBuffer
    {
        internal string name;
        internal int size;
        internal List<Constant> constants;
    }

    struct Constant
    {
        internal string name;
        internal int idx;
        internal UnityHelper.ShaderParamType dataType;
        internal UnityHelper.ConstantType constantType;
        internal int rows;
        internal int cols;
        internal int arraySize;
    }

    struct TextureSampler
    {
        internal string name;
        internal int register;
        internal bool multisampled;
        internal UnityHelper.TextureDimension dim;
        internal int arraySize;
    }

    /// <summary>
    /// Mapping of Unity shader variable name to corresponding bgfx variable name
    /// </summary>
    struct VarName
    {
        internal string unityName;
        internal string bgfxName;
    }

    /// <summary>
    /// Functions for converting Unity types to corresponding Bgfx types
    /// </summary>
    static class UnityToBgfx
    {
        internal static BgfxHelper.Attrib ConvertAttribute(UnityHelper.ShaderChannel attribute)
        {
            switch (attribute)
            {
                case UnityHelper.ShaderChannel.kShaderChannelVertex:
                    return BgfxHelper.Attrib.Position;
                case UnityHelper.ShaderChannel.kShaderChannelNormal:
                    return BgfxHelper.Attrib.Normal;
                case UnityHelper.ShaderChannel.kShaderChannelTangent:
                    return BgfxHelper.Attrib.Tangent;
                case UnityHelper.ShaderChannel.kShaderChannelColor:
                    return BgfxHelper.Attrib.Color0;
                case UnityHelper.ShaderChannel.kShaderChannelTexCoord0:
                    return BgfxHelper.Attrib.TexCoord0;
                case UnityHelper.ShaderChannel.kShaderChannelTexCoord1:
                    return BgfxHelper.Attrib.TexCoord1;
                case UnityHelper.ShaderChannel.kShaderChannelTexCoord2:
                    return BgfxHelper.Attrib.TexCoord2;
                case UnityHelper.ShaderChannel.kShaderChannelTexCoord3:
                    return BgfxHelper.Attrib.TexCoord3;
                case UnityHelper.ShaderChannel.kShaderChannelTexCoord4:
                    return BgfxHelper.Attrib.TexCoord4;
                case UnityHelper.ShaderChannel.kShaderChannelTexCoord5:
                    return BgfxHelper.Attrib.TexCoord5;
                case UnityHelper.ShaderChannel.kShaderChannelTexCoord6:
                    return BgfxHelper.Attrib.TexCoord6;
                case UnityHelper.ShaderChannel.kShaderChannelTexCoord7:
                    return BgfxHelper.Attrib.TexCoord7;
                case UnityHelper.ShaderChannel.kShaderChannelBlendWeights:
                    return BgfxHelper.Attrib.Weight;
                case UnityHelper.ShaderChannel.kShaderChannelBlendIndices:
                    return BgfxHelper.Attrib.Indices;
                case UnityHelper.ShaderChannel.kShaderChannelCount:
                    return BgfxHelper.Attrib.Count;
                default:
                    throw new NotSupportedException(nameof(attribute));
            }
        }
        // TODO fix and expose HLSLCC_FLAG_TRANSLATE_MATRICES flag
        internal const string k_HlslccMtxPrefix = "hlslcc_mtx4x4";
        internal const string k_HlslccZcmpPrefix = "hlslcc_zcmp";

        /// <summary>
        /// Converting predefined Unity uniforms (defined in UnityShaderVariables.cginc) to bgfx uniforms (defined in bgfx_shader.sh)
        /// </summary>
        internal static bool ConvertBuiltInUniform(string unityUniform, out string bgfxUniform)
        {
            if (unityUniform == null)
            {
                bgfxUniform = null;
                return false;
            }

            if (unityUniform.StartsWith(k_HlslccMtxPrefix))
            {
                unityUniform = unityUniform.Remove(0, k_HlslccMtxPrefix.Length);
            }

            // TODO support mapping of all unity built-in shader variables https://docs.unity3d.com/Manual/SL-UnityShaderVariables.html
            if (unityUniform == "UNITY_MATRIX_M" || unityUniform == "unity_ObjectToWorld")
            {
                // Note: this is a mat4 array in Bgfx and not in Unity. Bgfx is still able recognize it by its name and bind it correctly
                bgfxUniform = "u_model";
            }
            else if (unityUniform == "UNITY_MATRIX_V" || unityUniform == "unity_MatrixV")
            {
                bgfxUniform = "u_view";
            }
            else if (unityUniform == "UNITY_MATRIX_I_V" || unityUniform == "unity_MatrixInvV")
            {
                bgfxUniform = "u_invView";
            }
            else if (unityUniform == "UNITY_MATRIX_P" || unityUniform == "glstate_matrix_projection")
            {
                bgfxUniform = "u_proj";
            }
            else if (unityUniform == "UNITY_MATRIX_VP" || unityUniform == "unity_MatrixVP")
            {
                bgfxUniform = "u_viewProj";
            }
            else if (unityUniform == "UNITY_MATRIX_MV" || unityUniform == "unity_MatrixMV")
            {
                bgfxUniform = "u_modelView";
            }
            else if (unityUniform == "UNITY_MATRIX_MVP" || unityUniform == "unity_MatrixMVP")
            {
                bgfxUniform = "u_modelViewProj";
            }
            else
            {
                bgfxUniform = unityUniform;
                return false;
            }

            return true;
        }

        internal static BgfxHelper.UniformType GetBgfxUniformDataType(Constant constant)
        {
            if (constant.constantType == UnityHelper.ConstantType.kConstantTypeDefault)
            {
                // Note: this could be a scalar coming from Unity
                Assert.IsTrue(constant.cols <= 4);
                return BgfxHelper.UniformType.Vec4;
            }
            if (constant.constantType == UnityHelper.ConstantType.kConstantTypeMatrix)
            {
                Assert.IsTrue(constant.rows == 3 || constant.rows == 4);
                if (constant.rows == 4)
                {
                    Assert.IsTrue(constant.cols == 4);
                    return BgfxHelper.UniformType.Mat4;
                }

                Assert.IsTrue(constant.cols == 3);
                return BgfxHelper.UniformType.Mat3;
            }

            throw new NotSupportedException("Structs in uniforms are not supported.");
        }
    }

    public class ShaderCompilerClient
    {
        Socket m_Socket;
        public bool IsOpen { get; private set; }

#if DEBUG
        bool k_DebugUnityShaderCompiler = false;
#endif

        public void Open(string buildOutputPath)
        {
            if (IsOpen)
            {
                UnityEngine.Debug.LogWarning("Shader compiler process is already running");
                return;
            }

            var editorDir = Path.GetDirectoryName(UnityEditor.EditorApplication.applicationPath);
#if DEBUG
            if (k_DebugUnityShaderCompiler)
                editorDir = @"C:\Users\christine.johnson\src\gits\unity\build\WindowsEditor";
#endif
            Assert.IsNotNull(editorDir);
#if UNITY_EDITOR_WIN
            // e.g. "C:\Program Files\Unity\Hub\Editor\<version>\Editor\Data\Tools\UnityShaderCompiler.exe"
            string baseDir = Path.Combine(editorDir, "Data");
            string shaderCompilerFilePath = Path.Combine(baseDir, "Tools", "UnityShaderCompiler.exe");
#elif UNITY_EDITOR_OSX
            // e.g. "/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/Tools/UnityShaderCompiler"
            string baseDir = Path.Combine(editorDir, "Unity.app", "Contents");
            string shaderCompilerFilePath = Path.Combine(baseDir, "Tools", "UnityShaderCompiler");
#elif UNITY_EDITOR_LINUX
            // e.g. "~/Unity/Hub/Editor/<version>/Editor/Data/Tools/UnityShaderCompiler"
            string baseDir = Path.Combine(editorDir, "Data");
            string shaderCompilerFilePath = Path.Combine(baseDir, "Tools", "UnityShaderCompiler");
#else
            string baseDir = null;
            string shaderCompilerFilePath = null;
#endif
            if (!File.Exists(shaderCompilerFilePath))
                throw new InvalidDataException($"Invalid filepath for Unity Shader Compiler '{shaderCompilerFilePath}'");

            string compilerLogPath = string.Empty;
            if (buildOutputPath != null)
            {
                string logsPath = Path.Combine(buildOutputPath, "Logs");
                Directory.CreateDirectory(logsPath);
                compilerLogPath = Path.Combine(logsPath, "UnityShaderCompiler.log");
            }

            try
            {
                IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Loopback, 0);
                using (var listener = new Socket(SocketType.Stream, ProtocolType.Tcp))
                {
                    listener.Bind(localEndPoint);
                    var port = ((IPEndPoint)listener.LocalEndPoint).Port;
                    listener.Listen(1);

                    using (Process process = new Process())
                    {
                        process.StartInfo.FileName = shaderCompilerFilePath;
                        process.StartInfo.Arguments = $"\"{baseDir}\" \"{compilerLogPath}\" {port}";
                        process.StartInfo.CreateNoWindow = true;
                        process.StartInfo.UseShellExecute = false;
                        process.Start();
                        if (process.Id == 0 || process.HasExited)
                            throw new Exception("Could not launch Unity shader compiler");
                    }

                    m_Socket = listener.Accept(); // TODO timeout
                    listener.Close();
                }

                int timeoutMs = 10 * 1000; // 10 seconds
#if DEBUG
                if (k_DebugUnityShaderCompiler)
                    timeoutMs *= 1000;
#endif
                m_Socket.ReceiveTimeout = timeoutMs;
                m_Socket.SendTimeout = timeoutMs;

                string unityIncludes = Path.Combine(baseDir, "CGIncludes");
                if (!Directory.Exists(unityIncludes))
                    throw new InvalidDataException($"Invalid path for Unity built-in shader include files: {unityIncludes}");

                ShaderCompilerProcessInitialize(new List<string> { ".", unityIncludes});
            }
            catch (Exception e)
            {
                // TODO retry?
                UnityEngine.Debug.LogError($"Exception occurred when attempting to connect to the Unity shader compiler: {e.Message}");
            }

            IsOpen = true;
        }

        public void Close()
        {
            if (!IsOpen)
                return;

            m_Socket.SendLine(UnityHelper.commands[UnityHelper.ShaderCompilerCommand.kShaderCompilerShutdown]);
            m_Socket?.Close();
            m_Socket?.Dispose();
        }

        /// <remarks>
        /// The Unity shader compiler compiles all shader stages in a single step for some backends, so all compiled stages for a pipeline are stored in a single BlobAsset
        /// </remarks>
        public unsafe BlobAssetReference<PrecompiledShaderPipeline> CompileShaderForPlatforms(string src, ShaderCompilerPlatform[] platforms, string filepath, int startLine = 0, string name = null)
        {
            using (var allocator = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref allocator.ConstructRoot<PrecompiledShaderPipeline>();

                List<string> errors = new List<string>();
                foreach (var platform in platforms)
                {
                    Compile(src, platform, name, filepath, startLine, out byte[] compiledVert, out byte[] compiledFrag, errors);

                    if (errors.Count > 0)
                    {
                        foreach (var error in errors)
                            UnityEngine.Debug.LogError(error);
                        return BlobAssetReference<PrecompiledShaderPipeline>.Null;
                    }

                    UnityEngine.Assertions.Assert.IsNotNull(compiledVert);
                    UnityEngine.Assertions.Assert.IsNotNull(compiledFrag);
                    fixed (byte* data = compiledVert)
                    {
                        byte* dest = (byte*)allocator.Allocate(ref root.vertex.PrecompiledShaderForPlatform(platform), compiledVert.Length).GetUnsafePtr();
                        UnsafeUtility.MemCpy(dest, data, compiledVert.Length);
                    }
                    fixed (byte* data = compiledFrag)
                    {
                        byte* dest = (byte*)allocator.Allocate(ref root.fragment.PrecompiledShaderForPlatform(platform), compiledFrag.Length).GetUnsafePtr();
                        UnsafeUtility.MemCpy(dest, data, compiledFrag.Length);
                    }
                }
                return allocator.CreateBlobAssetReference<PrecompiledShaderPipeline>(Allocator.Persistent);
            }
        }

        public BlobAssetReference<PrecompiledShaderPipeline> CompileShaderForPlatforms(string filepath, ShaderCompilerPlatform[] platforms)
        {
            if (!File.Exists(filepath))
            {
                throw new InvalidDataException($"Could not open shader file '{filepath}'");
            }

            return CompileShaderForPlatforms(File.ReadAllText(filepath), platforms, filepath);
        }

        public void Compile(string src, ShaderCompilerPlatform platform, string name, string filepath, int startLine, out byte[] serializedVert, out byte[] serializedFrag, List<string> errors)
        {
            Assert.IsTrue(IsOpen);
            serializedVert = null;
            serializedFrag = null;
            string filename = name ?? Path.GetFileName(filepath);
            string includesDir = Path.GetDirectoryName(filepath);
            string shaderName = Path.GetFileNameWithoutExtension(filename);

            try
            {
                if (platform == ShaderCompilerPlatform.D3D)
                {
                    if (!ShaderCompilerCompileSnippet(UnityHelper.ShaderCompilerProgram.kProgramVertex, platform, src, filename, includesDir, startLine, out CompiledShaderData vert, errors))
                        return;
                    if (!ShaderCompilerCompileSnippet(UnityHelper.ShaderCompilerProgram.kProgramFragment, platform, src, filename, includesDir, startLine, out CompiledShaderData frag, errors))
                        return;

                    SerializeShaderHLSL.Serialize(vert, frag, shaderName, ref serializedVert, ref serializedFrag);
                }
                else if (platform == ShaderCompilerPlatform.OpenGLCore)
                {
                    // All shader stages are included in Vertex stage for GL
                    if (ShaderCompilerCompileSnippet(UnityHelper.ShaderCompilerProgram.kProgramVertex, platform, src, filename, includesDir, startLine, out CompiledShaderData compiled, errors))
                    {
                        SerializeShaderGLSL.Serialize(compiled, shaderName, false, ref serializedVert, ref serializedFrag);
                    }
                }
                else if (platform == ShaderCompilerPlatform.GLES20)
                {
                    if (ShaderCompilerCompileSnippet(UnityHelper.ShaderCompilerProgram.kProgramVertex, platform, src, filename, includesDir, startLine, out CompiledShaderData compiled, errors))
                    {
                        SerializeShaderGLSL.Serialize(compiled, shaderName, true, ref serializedVert, ref serializedFrag);
                    }
                }
                else if (platform == ShaderCompilerPlatform.Vulkan)
                {
                    // TODO support Vulkan
                    errors.Add("Vulkan is not currently supported. Skipping shader compilation.");
                }
                else if (platform == ShaderCompilerPlatform.Metal)
                {
                    if (!ShaderCompilerCompileSnippet(UnityHelper.ShaderCompilerProgram.kProgramVertex, platform, src, filename, includesDir, startLine, out CompiledShaderData vert, errors))
                        return;
                    if (!ShaderCompilerCompileSnippet(UnityHelper.ShaderCompilerProgram.kProgramFragment, platform, src, filename, includesDir, startLine, out CompiledShaderData frag, errors))
                        return;

                    SerializeShaderMSL.Serialize(vert, frag, shaderName, ref serializedVert, ref serializedFrag);
                }
                else
                {
                    errors.Add($"Platform {platform} is not supported. Skipping shader compilation.");
                }
            }
            catch (SocketException)
            {
                string msg = FormatErrorMessage("Compile", filename, platform, UnityHelper.kSocketExceptionError);
                UnityEngine.Debug.LogError($"Shader Compiler Socket Exception: Terminating shader compiler process\n{msg}");
                Close();
            }
        }

        public bool Preprocess(string shaderLabSrc, string filepath, string name, out string hlslSrc, out int startLine, out uint[] includeHash)
        {
            string filename = name ?? Path.GetFileName(filepath);
            string includesDir = Path.GetDirectoryName(filepath);
            if (!ShaderCompilerPreprocess(shaderLabSrc, includesDir, filename, out hlslSrc, out startLine, out includeHash, out List<string> errors) || errors.Count > 0)
            {
                foreach (var error in errors)
                    UnityEngine.Debug.LogError(error);
                return false;
            }

            return true;
        }

        void ShaderCompilerProcessInitialize(List<string> includePaths)
        {
            m_Socket.SendLine(UnityHelper.commands[UnityHelper.ShaderCompilerCommand.kShaderCompilerInitialize]);
            m_Socket.SendInt(includePaths.Count);
            foreach (var path in includePaths)
            {
                m_Socket.SendLine(path);
            }

            // Redirected include paths
            m_Socket.SendInt(0);

            // Additional shader compiler configuration
            m_Socket.SendLine(string.Empty);

            m_Socket.ReceiveInt(); // platform mask
            //var platforms = Enum.GetValues(typeof(UnityEditor.Rendering.ShaderCompilerPlatform)).Cast<int>().Max() + 1; // TODO: C# ShaderCompilerPlatform enum does not match native one in 2020.2
            var platforms = UnityHelper.k_ShaderCompilerPlatformCount;
            for (int i = 0; i < platforms; i++)
            {
                m_Socket.ReceiveInt();  // ShaderRequirements
                m_Socket.ReceiveUint(); // shader compiler version
            }
        }

        bool ShaderCompilerCompileSnippet(UnityHelper.ShaderCompilerProgram programType,
            ShaderCompilerPlatform platform,
            string src,
            string filename,
            string includesDir,
            int startLine,
            out CompiledShaderData compiled,
            List<string> errors)
        {
            Assert.IsTrue(IsOpen);
            bool success = true;
            m_Socket.SendLine(UnityHelper.commands[UnityHelper.ShaderCompilerCommand.kShaderCompilerCompileSnippet]);
            m_Socket.SendLine(src);
            m_Socket.SendLine(includesDir); // input path
#if UNITY_2020_2_OR_NEWER
            m_Socket.SendBool(true); // Use new preprocessor. Matches default in Unity
#else
            m_Socket.SendBool(false);
#endif
            m_Socket.SendBool(false); // preprocess only
#if UNITY_2020_2_OR_NEWER
            m_Socket.SendBool(false); // strip line directives
#endif
            m_Socket.SendInt(0); // root signature length
            m_Socket.SendInt(0); // platform keywords
            m_Socket.SendInt(0); // user keywords
            m_Socket.SendInt(0); // disabled keywords
            m_Socket.SendInt(0); // options
            m_Socket.SendInt((int)UnityHelper.ShaderSourceLanguage.kShaderSourceLanguageHLSL);
            m_Socket.SendInt((int)programType);
            m_Socket.SendInt((int)platform);
            m_Socket.SendInt(UnityHelper.ShaderRequirements.kShaderRequireShaderModel25_93);
            m_Socket.SendInt(0); // program mask
            m_Socket.SendInt(startLine); // code start line

            compiled = new CompiledShaderData();
            compiled.Init();

            while (success)
            {
                string message = m_Socket.ReceiveLine();
                var tokens = message.Split(' ');
                if (tokens.Length == 0)
                {
                    errors.Add(FormatErrorMessage("Compile", filename, platform, UnityHelper.kEmptyMessageError));
                    success = false;
                    break;
                }

                if (tokens[0] == "err:" && tokens.Length == 4)
                {
                    int type = Convert.ToInt32(tokens[1]);
                    int plat = Convert.ToInt32(tokens[2]);
                    int line = Convert.ToInt32(tokens[3]);
                    string fileStr = m_Socket.ReceiveLine();
                    string msgStr = m_Socket.ReceiveLine();
                    string loc = fileStr == string.Empty ? $"line {line}" : $"{Path.GetFileName(fileStr)}({line})";
                    string error = $"Shader error in '{filename}': {msgStr.Substring(0, msgStr.IndexOf("\\n"))} at {loc} (on {UnityHelper.ShaderCompilerGetPlatformName(platform)})";
                    errors.Add(error);
                }
                else if (tokens[0] == "input:" && tokens.Length == 3)
                {
                    UnityHelper.ShaderChannel input = (UnityHelper.ShaderChannel)Convert.ToInt32(tokens[1]);
                    // OnInputBinding
                    compiled.attributes.Add(input);
                }

                // Note: the 'cb' data will show the size/variable count for the entire declared constant buffer while the 'const' data is only sent for uniforms that are actually used in the shader

                // For DX:
                // Cb data matches cbuffer declarations in shader 

                // For Metal:
                // All built-in uniforms are grouped into a single cb. Any custom uniforms are grouped in separate cb's that match the cbuffer declarations in the shader

                // For GLCore:
                // All uniforms for a shader stage are grouped into a single cb but there is no information indicating which stage the cb belongs to (GL shader stages are compiled collectively with a single invocation of the Unity compiler)

                // For GLES:
                // No uniform or sampler data is received. Must be generated manually from shader source
                else if (tokens[0] == "cb:" && tokens.Length == 4)
                {
                    string name = tokens[1];
                    int size = Convert.ToInt32(tokens[2]);
                    int varCount = Convert.ToInt32(tokens[3]); // number of declared constants in buffer
                    compiled.constantBuffers.Add(new ConstantBuffer { name = name, size = size, constants = new List<Constant>(varCount) });
                }
                else if (tokens[0] == "const:" && tokens.Length == 8)
                {
                    if (compiled.constantBuffers.Count == 0)
                        throw new InvalidDataException("ShaderCompilerCompileSnippet: Receiving constant data without constant buffer.");

                    string name = tokens[1];
                    int idx = Convert.ToInt32(tokens[2]); // byte offset in buffer
                    UnityHelper.ShaderParamType dataType = (UnityHelper.ShaderParamType)Convert.ToInt32(tokens[3]);
                    UnityHelper.ConstantType constantType = (UnityHelper.ConstantType)Convert.ToInt32(tokens[4]);
                    int rows = Convert.ToInt32(tokens[5]);
                    int cols = Convert.ToInt32(tokens[6]);
                    int arraySize = Convert.ToInt32(tokens[7]);
                    // All constants in a constant buffer are sent (in order of offset in buffer) after the constant buffer data
                    var constantBuffer = compiled.constantBuffers[compiled.constantBuffers.Count - 1];
                    constantBuffer.constants.Add(new Constant { name = name, idx = idx, dataType = dataType, constantType = constantType, rows = rows, cols = cols, arraySize = arraySize });
                    compiled.constantBuffers[compiled.constantBuffers.Count - 1] = constantBuffer;
                }
                else if (tokens[0] == "cbbind:" && tokens.Length == 3)
                {
                    string name = tokens[1];
                    int idx = Convert.ToInt32(tokens[2]); // register

                    // Bind info is sent after constant buffer data
                    // TODO bind has other data packed into uint for Vulkan. See FromUint in VKBinding.h
                    if (!IsGL(platform) && (idx >= compiled.constantBuffers.Count || compiled.constantBuffers[idx].name != name))
                        throw new InvalidDataException($"ShaderCompilerCompileSnippet: Invalid constant buffer register {idx} for {name}. Registers are expected to be in incremental order.");
                }
                else if (tokens[0] == "texbind:" && tokens.Length == 6)
                {
                    string name = tokens[1];
                    int textureBind = Convert.ToInt32(tokens[2]);
                    int samplerBind = Convert.ToInt32(tokens[3]);
                    if (samplerBind < 0) // -1 if texture is not coupled with a sampler
                        throw new NotSupportedException("Decoupled samplers and textures are not supported.");

                    if (textureBind != samplerBind)
                    {
                        Assert.IsTrue(!compiled.texToSampler.ContainsKey((uint)textureBind));
                        compiled.texToSampler[(uint)textureBind] = (uint)samplerBind;
                    }

                    bool multisampled = Convert.ToInt32(tokens[4]) != 0;
                    UnityHelper.TextureDimension dim = (UnityHelper.TextureDimension)Convert.ToInt32(tokens[5]);
                    compiled.samplers.Add(new TextureSampler { name = name, register = samplerBind, multisampled = multisampled, dim = dim });
                }
                else if (tokens[0] == "sampler:" && tokens.Length == 3)
                {
                    // Inline samplers https://docs.unity3d.com/Manual/SL-SamplerStates.html
                    throw new NotSupportedException("Decoupled samplers and textures are not supported.");
                }
                else if (tokens[0] == "uavbind:" && tokens.Length == 4) // TODO see OnUAVBinding
                {
                    string name = tokens[1];
                    int idx = Convert.ToInt32(tokens[2]);
                    int origIdx = Convert.ToInt32(tokens[3]);
                }
                else if (tokens[0] == "bufferbind:" && tokens.Length == 4) // TODO see OnBufferBinding
                {
                    string name = tokens[1];
                    int idx = Convert.ToInt32(tokens[2]);
                    int bindCount = Convert.ToInt32(tokens[3]);
                }
                else if (tokens[0] == "stats:" && tokens.Length == 5) // TODO see OnStatsInfo
                {
                    int alu = Convert.ToInt32(tokens[1]);
                    int tex = Convert.ToInt32(tokens[2]);
                    int flow = Convert.ToInt32(tokens[3]);
                    int tempRegister = Convert.ToInt32(tokens[4]);
                }
                else if (tokens[0] == "shader:" && tokens.Length == 2)
                {
                    success = int.Parse(tokens[1]) != 0;
                    var outGpuProgramType = (UnityHelper.ShaderGpuProgramType)m_Socket.ReceiveInt();
                    byte[] outputCode = m_Socket.ReceiveBuffer();
                    compiled.shader = new CompiledShader { type = outGpuProgramType, outputCode = outputCode };
                    break;
                }
                else
                {
                    errors.Add(FormatErrorMessage("Compile", filename, platform, UnityHelper.kUnknownMessageError));
                    success = false;
                    break;
                }
            }

            return errors.Count == 0 && success;
        }

        bool ShaderCompilerPreprocess(string shaderLabSrc, string includesDir, string filename, out string hlslSrc, out int startLine, out uint[] includeHash, out List<string> errors)
        {
            Assert.IsTrue(IsOpen);
            bool success = true;
            hlslSrc = null;
            startLine = 0;
            errors = new List<string>();
            includeHash = new uint[4];
            m_Socket.SendLine(UnityHelper.commands[UnityHelper.ShaderCompilerCommand.kShaderCompilerPreprocess]);
            m_Socket.SendLine(shaderLabSrc);
            m_Socket.SendLine(includesDir);
            m_Socket.SendBool(false); // surfaceGenerationOnly
            m_Socket.SendBool(true); // useNewPreprocessor
            m_Socket.SendInt(0); // platformKeywords
            m_Socket.SendInt(0); // disabledKeywords

            int numErrMsgs = 0;
            while (success)
            {
                string message = m_Socket.ReceiveLine();
                var tokens = message.Split(' ');
                if (tokens.Length == 0)
                {
                    Console.WriteLine("ShaderCompilerPreprocess: received invalid message.");
                    success = false;
                    break;
                }
                if (tokens[0] == "err:" && tokens.Length == 4)
                {
                    int type = Convert.ToInt32(tokens[1]);
                    int plat = Convert.ToInt32(tokens[2]);
                    int line = Convert.ToInt32(tokens[3]);
                    string fileStr = m_Socket.ReceiveLine();
                    string msgStr = m_Socket.ReceiveLine();
                    // NOTE: Compiler always sends a message labeled as an error stating how long the preprocess took?
                    if (numErrMsgs > 0)
                    {
                        string error = $"Shader preprocess error in '{filename}': {msgStr} at line {line})";
                        errors.Add(error);
                    }

                    numErrMsgs++;
                }
                else if (tokens[0] == "snip:" && tokens.Length == 12)
                {
                    int id = int.Parse(tokens[1]);
                    int platformsMask = int.Parse(tokens[2]);
                    int hardwareTierVariantsMask = int.Parse(tokens[3]);
                    int typesMask = int.Parse(tokens[4]);
                    int compFlags = int.Parse(tokens[5]);
                    UnityHelper.ShaderSourceLanguage language = (UnityHelper.ShaderSourceLanguage)int.Parse(tokens[6]);
                    includeHash[0] = (uint)int.Parse(tokens[7]);
                    includeHash[1] = (uint)int.Parse(tokens[8]);
                    includeHash[2] = (uint)int.Parse(tokens[9]);
                    includeHash[3] = (uint)int.Parse(tokens[10]);
                    startLine = int.Parse(tokens[11]);

                    // unparsed HLSL block
                    hlslSrc = m_Socket.ReceiveLine();
                }
                else if (tokens[0] == "includes:" && tokens.Length == 2)
                {
                    int count = int.Parse(tokens[1]);
                    for (int i = 0; i < count; i++)
                    {
                        string include = m_Socket.ReceiveLine();
                    }
                }
                else if (tokens[0] == "keywordsUserGlobal:" && tokens.Length == 4)
                {
                    if (int.Parse(tokens[3]) != 0)
                    {
                        break;
                    }
                }
                else if (tokens[0] == "keywordsUserLocal:" && tokens.Length == 4)
                {
                    if (int.Parse(tokens[3]) != 0)
                    {
                        break;
                    }
                }
                else if (tokens[0] == "keywordsBuiltin:" && tokens.Length == 4)
                {
                    if (int.Parse(tokens[3]) != 0)
                    {
                        break;
                    }
                }
                else if (tokens[0] == "keywordsEnd:" && tokens.Length == 2)
                {
                    string msg = m_Socket.ReceiveLine();
                    if (msg != String.Empty)
                    {
                        break;
                    }
                    msg = m_Socket.ReceiveLine();
                    if (msg != String.Empty)
                    {
                        break;
                    }

                    m_Socket.ReceiveInt();
                    uint count = m_Socket.ReceiveUint();
                    for (int i = 0; i < count; i++)
                    {
                        string kw = m_Socket.ReceiveLine();
                        int kwMask = m_Socket.ReceiveInt();
                    }
                }
                else if (tokens[0] == "shader:" && tokens.Length == 3)
                {
                    // Note: this is the remaining ShaderLab with the HLSL removed
                    success = int.Parse(tokens[1]) != 0;
                    bool outHadSurfaceShaders = int.Parse(tokens[2]) != 0;
                    string shaderLab = m_Socket.ReceiveLine();
                    break;
                }
                else
                {
                    Console.WriteLine("ShaderCompilerPreprocess: unrecognized message.");
                    success = false;
                    break;
                }
            }

            return errors.Count == 0 && success;
        }

        public static ShaderCompilerPlatform[] GetSupportedPlatforms(BuildTarget target, bool forceIncludeAllPlatform = false)
        {
            // TODO: provide more customized options
            if (forceIncludeAllPlatform)
            {
                return new[] { ShaderCompilerPlatform.Metal, ShaderCompilerPlatform.OpenGLCore, ShaderCompilerPlatform.GLES20, /* ShaderCompilerPlatform.Vulkan,*/ ShaderCompilerPlatform.D3D };
            }

            // TODO we need to move ths logic into the platforms packages; they should ultimately determine what shader types are needed
            var targetName = target.UnityPlatformName;
            // d3d12 uses d3d11 shaders
            if (targetName == UnityEditor.BuildTarget.StandaloneWindows.ToString() ||
                targetName == UnityEditor.BuildTarget.StandaloneWindows64.ToString())
                return new[] { ShaderCompilerPlatform.D3D, ShaderCompilerPlatform.OpenGLCore, /*ShaderCompilerPlatform.Vulkan*/ };
            if (targetName == UnityEditor.BuildTarget.StandaloneLinux64.ToString())
                return new[] { ShaderCompilerPlatform.OpenGLCore, /*ShaderCompilerPlatform.Vulkan*/ };
            if (targetName == UnityEditor.BuildTarget.StandaloneOSX.ToString())
                return new[] { ShaderCompilerPlatform.Metal, ShaderCompilerPlatform.OpenGLCore, /*ShaderCompilerPlatform.Vulkan*/ };
            // TODO: get rid of OpenGLES for iOS when problem with Metal on A7/A8 based devices is fixed
            if (targetName == UnityEditor.BuildTarget.iOS.ToString())
                return new[] { ShaderCompilerPlatform.Metal, ShaderCompilerPlatform.GLES20, ShaderCompilerPlatform.OpenGLCore };
            if (targetName == UnityEditor.BuildTarget.Android.ToString())
                return new[] { ShaderCompilerPlatform.GLES20, ShaderCompilerPlatform.OpenGLCore, /*ShaderCompilerPlatform.Vulkan*/ };
            if (targetName == UnityEditor.BuildTarget.WebGL.ToString())
                return new[] { ShaderCompilerPlatform.GLES20 };

            //TODO: Should we default to a specific shader type?
            throw new InvalidOperationException($"Target: {targetName} is not supported. No shaders will be exported");
        }

        static string FormatErrorMessage(string command, string filename, ShaderCompilerPlatform platform, string errorMsg) =>
            $"Shader compiler: {command} {filename}: {errorMsg}\nPlatform: {UnityHelper.ShaderCompilerGetPlatformName(platform)}";

        static bool IsGL(ShaderCompilerPlatform platform) =>
            platform == ShaderCompilerPlatform.GLES20 || platform == ShaderCompilerPlatform.GLES3x || platform == ShaderCompilerPlatform.OpenGLCore;
    }

    static class SocketExt
    {
        const uint k_CompilerProtocolMagic = 201905290; // Indicates protocol version

        static void SendHeader(this Socket socket, int bufferSize)
        {
            socket.SendUint(k_CompilerProtocolMagic);
            socket.SendUint((uint)bufferSize);
        }

        static uint ReceiveHeader(this Socket socket)
        {
            var magic = socket.ReceiveUint();
            if (magic != k_CompilerProtocolMagic)
            {
                throw new Exception("Protocol error - failed to read correct magic number.");
            }

            // data length
            return socket.ReceiveUint();
        }

        /// <remarks>
        /// Includes header
        /// </remarks>
        internal static void SendLine(this Socket socket, string message)
        {
            socket.SendBuffer(Encoding.UTF8.GetBytes(message));
        }

        /// <remarks>
        /// Includes header
        /// </remarks>
        internal static void SendBuffer(this Socket socket, byte[] buffer)
        {
            socket.SendHeader(buffer.Length);
            socket.Send(buffer);
        }

        internal static void SendBool(this Socket socket, bool message)
        {
            socket.SendUint(Convert.ToUInt32(message));
        }

        internal static void SendInt(this Socket socket, int message)
        {
            socket.Send(BitConverter.GetBytes(message));
        }
        internal static void SendUint(this Socket socket, uint message)
        {
            socket.Send(BitConverter.GetBytes(message));
        }

        /// <remarks>
        /// Includes header
        /// </remarks>
        internal static string ReceiveLine(this Socket socket)
        {
            return Encoding.UTF8.GetString(socket.ReceiveBuffer());
        }

        /// <remarks>
        /// Includes header
        /// </remarks>
        internal static byte[] ReceiveBuffer(this Socket socket)
        {
            byte[] buffer = new byte[0];
            uint size = socket.ReceiveHeader();
            if (size > 0)
            {
                buffer = new byte[size];
                socket.Receive(buffer, buffer.Length, SocketFlags.None);
            }

            return buffer;
        }

        internal static bool ReceiveBool(this Socket socket)
        {
            return Convert.ToBoolean(socket.ReceiveUint());
        }

        internal static int ReceiveInt(this Socket socket)
        {
            return (int)socket.ReceiveUint();
        }

        internal static uint ReceiveUint(this Socket socket)
        {
            byte[] buffer = new byte[sizeof(uint)];
            socket.Receive(buffer, buffer.Length, SocketFlags.None);
            return BitConverter.ToUInt32(buffer, 0);
        }
    }
}
