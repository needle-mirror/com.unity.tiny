using System.IO;
using Unity.Build.Common;
using Unity.Build.DotsRuntime;
using Unity.Collections;
using Unity.Entities;
using Unity.Tiny.Rendering;
using Unity.Tiny.Rendering.Settings;
using Unity.Tiny.ShaderCompiler;
using UnityEditor;
using Hash128 = UnityEngine.Hash128;

namespace Unity.TinyConversion
{
    struct ShaderSettings { }

    /// <summary>
    /// Conversion system for custom shaders
    /// </summary>
    [UpdateInGroup(typeof(GameObjectConversionGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    public class ShaderConversion : GameObjectConversionSystem
    {
        ShaderCompilerClient m_Client;

        protected void InitShaderCompiler()
        {
            string outputDir = null;
            if (TryGetBuildConfigurationComponent<OutputBuildDirectory>(out var outputBuildDirectory))
            {
                outputDir = outputBuildDirectory.OutputDirectory;
            }

            m_Client = new ShaderCompilerClient();
            m_Client.Open(outputDir);
            if (!m_Client.IsOpen)
                UnityEngine.Debug.LogError("Unable to launch instance of Unity shader compiler.");
        }

        protected void ShutdownShaderCompiler()
        {
            m_Client?.Close();
        }

        protected override void OnUpdate()
        {
            if (GetEntityQuery(ComponentType.ReadOnly<UnityEngine.Shader>()).CalculateEntityCount() == 0)
            {
                return;
            }
            if (!TryGetBuildConfigurationComponent<DotsRuntimeBuildProfile>(out var profile))
            {
                return;
            }
            bool includeAllPlatform = false;
            if (TryGetBuildConfigurationComponent<TinyShaderSettings>(out var shaderSettings))
            {
                includeAllPlatform = shaderSettings.PackageShadersForAllPlatforms;
            }
            var platforms = ShaderCompilerClient.GetSupportedPlatforms(profile.Target, includeAllPlatform);
            var context = new BlobAssetComputationContext<ShaderSettings, PrecompiledShaderPipeline>(BlobAssetStore, 128, Allocator.Temp);

            Entities.ForEach((UnityEngine.Shader uShader) =>
            {
                if (MaterialConversion.GetMaterialType(uShader) != MaterialConversion.SupportedMaterialType.Custom)
                    return;

                var entity = GetPrimaryEntity(uShader);

                // Note: all shader stages are in a single source file
                string filepath = Path.GetFullPath(AssetDatabase.GetAssetPath(uShader));
                if (!File.Exists(filepath))
                {
                    throw new InvalidDataException($"Could not open shader file '{filepath}'");
                }
                string shaderSrc = File.ReadAllText(filepath);

                if (m_Client == null)
                {
                    InitShaderCompiler();
                }

                m_Client.Preprocess(shaderSrc, filepath, uShader.name, out string hlslSrc, out int startLine, out uint[] includeHash);
                Hash128 includeHash128 = new Hash128(includeHash[0], includeHash[1], includeHash[2], includeHash[3]);
                Hash128 hash = new Hash128();
                unsafe
                {
                    fixed (char* p = shaderSrc)
                    {
                       UnityEngine.HashUnsafeUtilities.ComputeHash128(p, (ulong)shaderSrc.Length, &hash);
                       UnityEngine.HashUtilities.AppendHash(ref includeHash128, ref hash);
                    }
                }

                context.AssociateBlobAssetWithUnityObject(hash, uShader);
                if (context.NeedToComputeBlobAsset(hash))
                {
                    context.AddBlobAssetToCompute(hash, default);
                    var blobAsset = m_Client.CompileShaderForPlatforms(hlslSrc, platforms, filepath, startLine, uShader.name);
                    if (!blobAsset.IsCreated)
                        return;

                    context.AddComputedBlobAsset(hash, blobAsset);
                }

                context.GetBlobAsset(hash, out var shaderBlob);
                DstEntityManager.AddComponentData(entity, new CustomShader { Status = ShaderStatus.Invalid, Name = Path.GetFileNameWithoutExtension(filepath) });
                DstEntityManager.AddComponentData(entity, new ShaderBinData { shaders = shaderBlob });
            });

            ShutdownShaderCompiler();

            context.Dispose();
        }
    }
}
