using Bgfx;
using Unity.Build.DotsRuntime;
using Unity.Entities;
using Unity.Entities.Runtime.Build;
using Unity.Tiny.Rendering;
using Unity.TinyConversion;

namespace Unity.Tiny.Text.Authoring
{
    [DisableAutoCreation]
    internal class ExportTextShaders : ShaderExportSystem
    {
        static readonly string kBinaryShaderFolderPath = "Packages/com.unity.tiny/Unity.Tiny.Text.Native/shaderbin~/";

        protected override void OnUpdate()
        {
            if (BuildConfiguration == null)
                return;
            if (!BuildConfiguration.TryGetComponent<DotsRuntimeBuildProfile>(out var profile))
                return;
            if (!AssemblyCache.HasType<PrecompiledShaderData>())
                return;
            if (!AssemblyCache.HasType<Text.TextRenderer>())
                return;

            bgfx.RendererType[] types = GetShaderFormat(profile.Target);

            CreateShaderDataEntity(kBinaryShaderFolderPath, BitmapFontMaterial.ShaderGuid, "text", types);
            CreateShaderDataEntity(kBinaryShaderFolderPath, SDFFontMaterial.ShaderGuid, "textsdf", types);
        }
    }
}
