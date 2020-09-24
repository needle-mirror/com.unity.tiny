using System.IO;
using Unity.Entities;
using Unity.Build.DotsRuntime;
using Unity.Tiny.Rendering;
using Unity.Tiny.ShaderCompiler;
using Unity.TinyConversion;

namespace Unity.Tiny.Authoring
{
    [DisableAutoCreation]
    internal class ExportSpriteDefaultShader : ShaderExportSystem
    {
        static readonly string k_ShaderSourceFolderPath = @"Packages/com.unity.tiny/Unity.Tiny.2D.Native/shadersrc~/";

        public override System.Type[] UsedComponents { get; } =
        {
            typeof(DotsRuntimeBuildProfile)
        };

        protected override void OnUpdate()
        {
            if (BuildContext == null)
                return;
            if (!BuildContext.TryGetComponent<DotsRuntimeBuildProfile>(out var profile))
                return;
            if (!AssemblyCache.HasType<PrecompiledShader>())
                return;
            if (!AssemblyCache.HasType<SpriteRenderer>())
                return;

            InitShaderCompiler();

            var rendererTypes = ShaderCompilerClient.GetSupportedPlatforms(profile.Target);
            CreateShaderDataEntity(ShaderGuid.SpriteDefault, Path.Combine(k_ShaderSourceFolderPath, "SpriteDefault.cg"), rendererTypes);

            ShutdownShaderCompiler();
        }
    }
}
