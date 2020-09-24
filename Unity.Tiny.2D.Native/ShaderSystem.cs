using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Tiny.Rendering;
using Unity.Platforms;
using Unity.Collections.LowLevel.Unsafe;

using bgfx = Bgfx.bgfx;

namespace Unity.Tiny
{
    public interface IShader2D
    {
        Hash128 Guid { get; }
        bool IsInitialized { get; }
        void Init(bgfx.ProgramHandle programHandle);
        void Destroy();
    }

    internal struct SpriteDefaultShader : IShader2D
    {
        public bgfx.ProgramHandle ProgramHandle { get; private set; }
        public bgfx.UniformHandle TexColorSamplerHandle { get; private set; }
        public bgfx.UniformHandle TintColorHandle { get; private set; }
        public bgfx.VertexLayoutHandle LayoutHandle { get; private set; }
        public NativeArray<bgfx.VertexLayout> VertexLayout { get; private set; }

        Hash128 IShader2D.Guid => ShaderGuid.SpriteDefault;
        public bool IsInitialized => VertexLayout.IsCreated;

        public void Init(bgfx.ProgramHandle programHandle)
        {
            ProgramHandle = programHandle;
            TexColorSamplerHandle = bgfx.create_uniform("s_texColor", bgfx.UniformType.Sampler, 1);
            TintColorHandle = bgfx.create_uniform("u_tint0", bgfx.UniformType.Vec4, 1);

            unsafe // default vertex layout
            {
                var rendererType = bgfx.get_renderer_type();
                VertexLayout = new NativeArray<bgfx.VertexLayout>(8, Allocator.Persistent);

                var layoutPtr = (bgfx.VertexLayout*) VertexLayout.GetUnsafePtr();
                bgfx.vertex_layout_begin(layoutPtr, rendererType);
                bgfx.vertex_layout_add(layoutPtr, bgfx.Attrib.Position, 3, bgfx.AttribType.Float, false, false);
                bgfx.vertex_layout_add(layoutPtr, bgfx.Attrib.TexCoord0, 2, bgfx.AttribType.Float, false, false);
                bgfx.vertex_layout_end(layoutPtr);

                LayoutHandle = bgfx.create_vertex_layout(layoutPtr);
            }
        }

        public void Destroy()
        {
            if (!IsInitialized)
                return;

            VertexLayout.Dispose();
            bgfx.destroy_program(ProgramHandle);
            bgfx.destroy_uniform(TexColorSamplerHandle);
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(RendererBGFXSystem))]
    internal class ShaderSystem : ResumableSystemBase
    {
        private List<IShader2D> m_Shaders;
        public void RegisterShader(IShader2D shader)
        {
            m_Shaders.Add(shader);
        }

        private bool IsNativeRendererInitialized()
        {
            var system = World.GetExistingSystem<RendererBGFXSystem>();
            return system?.IsInitialized() ?? false;
        }

        protected override void OnCreate()
        {
            m_Shaders = new List<IShader2D>();
        }

        protected override void OnDestroy()
        {
            ClearShaders();
        }

        protected override void OnSuspendResume(object sender, SuspendResumeEvent evt)
        {
            if (!evt.Suspend)
                return;

            ClearShaders();
        }

        private void ClearShaders()
        {
            if (IsNativeRendererInitialized())
            {
                foreach (var shader in m_Shaders)
                    shader.Destroy();
            }

            m_Shaders.Clear();
        }

        protected override void OnUpdate()
        {
            if (!IsNativeRendererInitialized())
                return;

            var rendererType = bgfx.get_renderer_type();

            Entities
                .WithName("InitializeDefaultShader")
                .WithoutBurst()
                .ForEach((Entity e, ref BuiltInShader builtInShader, in ShaderBinData shaderBinData) =>
                {
                    for(var i = 0; i < m_Shaders.Count; i++)
                    {
                        if (m_Shaders[i].IsInitialized)
                            continue;

                        if (m_Shaders[i].Guid == builtInShader.Guid)
                            m_Shaders[i].Init(BGFXShaderHelper.GetPrecompiledShaderData(rendererType, shaderBinData, ref builtInShader.Name));
                    }
                }).Run();

        }
    }
}
