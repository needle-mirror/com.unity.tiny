using System;
using System.IO;
using Unity.Entities;
using Unity.Entities.Runtime.Build;
using Unity.Tiny.Rendering;
using Unity.Tiny.ShaderCompiler;
using UnityEditor.Rendering;
using BuiltInShader = Unity.Tiny.Rendering.BuiltInShader;

namespace Unity.TinyConversion
{
    public static class PrecompiledShaderExtention
    {
        public static ref BlobArray<byte> PrecompiledShaderForPlatform(ref this PrecompiledShader precompiled, ShaderCompilerPlatform platform)
        {
            switch (platform)
            {
                case ShaderCompilerPlatform.D3D: return ref precompiled.dx11;
                case ShaderCompilerPlatform.Metal: return ref precompiled.metal;
                case ShaderCompilerPlatform.GLES20: return ref precompiled.glsles;
                case ShaderCompilerPlatform.OpenGLCore: return ref precompiled.glsl;
                case ShaderCompilerPlatform.Vulkan: return ref precompiled.spirv;
                default:
                    throw new InvalidOperationException("No shader loaded for current platform.");
            }
        }
    }

    /// <summary>
    /// Export system for built-in shaders
    /// </summary>
    public abstract class ShaderExportSystem : ConfigurationSystemBase
    {
        ShaderCompilerClient m_Client;

        protected void InitShaderCompiler()
        {
            m_Client = new ShaderCompilerClient();
            m_Client.Open(OutputDir);
            if (!m_Client.IsOpen)
                UnityEngine.Debug.LogError("Unable to launch instance of Unity shader compiler.");
        }

        protected void ShutdownShaderCompiler()
        {
            m_Client.Close();
        }

        protected Entity CreateShaderDataEntity(Hash128 shaderGuid, string srcFile, ShaderCompilerPlatform[] platforms)
        {
            // Note: all shader stages are in a single source file
            var blobAsset = m_Client.CompileShaderForPlatforms(Path.GetFullPath(srcFile), platforms);
            if (!blobAsset.IsCreated)
                return Entity.Null;

            var e = EntityManager.CreateEntity(typeof(BuiltInShader), typeof(ShaderBinData));
            EntityManager.SetComponentData(e, new BuiltInShader { Guid = shaderGuid, Name = Path.GetFileNameWithoutExtension(srcFile) });
            EntityManager.SetComponentData(e, new ShaderBinData { shaders = blobAsset });
            return e;
        }
    }
}
