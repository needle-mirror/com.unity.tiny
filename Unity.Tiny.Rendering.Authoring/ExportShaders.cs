using System;
using Unity.Build.DotsRuntime;
using Unity.Entities;
using Unity.Tiny.Rendering;
using Unity.Tiny.Rendering.Settings;
using Unity.Tiny.ShaderCompiler;

namespace Unity.TinyConversion
{
    [DisableAutoCreation]
    class DefaultShaderExportSystem : ShaderExportSystem
    {
        public override Type[] UsedComponents { get; } =
        {
            typeof(DotsRuntimeBuildProfile),
            typeof(TinyShaderSettings)
        };

        protected override void OnUpdate()
        {
            if (BuildContext == null)
                return;
            if (!BuildContext.TryGetComponent<DotsRuntimeBuildProfile>(out var profile))
                return;
            if (!AssemblyCache.HasType<PrecompiledShader>())
                return;

            InitShaderCompiler();

            bool includeAllPlatform = false;
            if (BuildContext.TryGetComponent<TinyShaderSettings>(out var shaderSettings))
            {
                includeAllPlatform = shaderSettings.PackageShadersForAllPlatforms;
            }

            var platforms = ShaderCompilerClient.GetSupportedPlatforms(profile.Target, includeAllPlatform);

            CreateShaderDataEntity(BuiltInShaderType.simple, @"Packages/com.unity.tiny/Unity.Tiny.Rendering.Native/shadersrc~/simple.cg", platforms);
            CreateShaderDataEntity(BuiltInShaderType.simplegpuskinning, @"Packages/com.unity.tiny/Unity.Tiny.Rendering.Native/shadersrc~/simplegpuskinning.cg", platforms);
            CreateShaderDataEntity(BuiltInShaderType.simplelit, @"Packages/com.unity.tiny/Unity.Tiny.Rendering.Native/shadersrc~/simplelit.cg", platforms);
            CreateShaderDataEntity(BuiltInShaderType.simplelitgpuskinning, @"Packages/com.unity.tiny/Unity.Tiny.Rendering.Native/shadersrc~/simplelitgpuskinning.cg", platforms);
            CreateShaderDataEntity(BuiltInShaderType.line, @"Packages/com.unity.tiny/Unity.Tiny.Rendering.Native/shadersrc~/line.cg", platforms);
            CreateShaderDataEntity(BuiltInShaderType.blitsrgb, @"Packages/com.unity.tiny/Unity.Tiny.Rendering.Native/shadersrc~/blitsrgb.cg", platforms);
            CreateShaderDataEntity(BuiltInShaderType.shadowmap, @"Packages/com.unity.tiny/Unity.Tiny.Rendering.Native/shadersrc~/shadowmap.cg", platforms);
            CreateShaderDataEntity(BuiltInShaderType.shadowmapgpuskinning, @"Packages/com.unity.tiny/Unity.Tiny.Rendering.Native/shadersrc~/shadowmapgpuskinning.cg", platforms);

            ShutdownShaderCompiler();
        }
    }
}
