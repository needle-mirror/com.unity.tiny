using System;
using Unity.Build.DotsRuntime;
using Unity.Entities;
using Unity.Tiny.Rendering;
using Unity.Tiny.ShaderCompiler;
using Unity.TinyConversion;

namespace Unity.Tiny.Text.Authoring
{
    [DisableAutoCreation]
    internal class ExportTextShaders : ShaderExportSystem
    {
        public override Type[] UsedComponents { get; } =
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
            if (!AssemblyCache.HasType<Text.TextRenderer>())
                return;

            InitShaderCompiler();

            var platforms = ShaderCompilerClient.GetSupportedPlatforms(profile.Target);

            CreateShaderDataEntity(BitmapFontMaterial.ShaderGuid, @"Packages/com.unity.tiny/Unity.Tiny.Text.Native/shadersrc~/text.cg", platforms);
            CreateShaderDataEntity(SDFFontMaterial.ShaderGuid, @"Packages/com.unity.tiny/Unity.Tiny.Text.Native/shadersrc~/textsdf.cg", platforms);

            ShutdownShaderCompiler();
        }
    }
}
