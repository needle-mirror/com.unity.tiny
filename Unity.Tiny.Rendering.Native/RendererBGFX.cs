using System;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Tiny.Assertions;
#if UNITY_DOTSRUNTIME
#if !UNITY_WEBGL
using Unity.Tiny.STB;
#else
using Unity.Tiny.Web;
#endif
#endif
using Bgfx;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using System.Runtime.CompilerServices;
using Unity.Jobs;
#if UNITY_MACOSX
using Unity.Tiny.GLFW;
#endif
#if ENABLE_DOTSRUNTIME_PROFILER
using Unity.Development.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
#endif


namespace Unity.Tiny.Rendering
{
    internal class MonoPInvokeCallbackAttribute : Attribute
    {
    }

#if UNITY_WEBGL
    static class HTMLNativeCalls
    {
        [DllImport("lib_unity_tiny_web", EntryPoint = "js_html_validateWebGLContextFeatures")]
        public static extern void validateWebGLContextFeatures(bool requireSrgb);
    }
#endif

    // use this interface to make bgfx things public to other packages, like android that
    // wants re-init functionality
    // (except for tests)

    public abstract partial class RenderingGPUSystem : SystemBase
    {
        public abstract void Init();
        public abstract void Shutdown();
        public abstract void Resume();
        public abstract void ReloadAllImages();
    }

    internal struct TextureBGFX : ISystemStateComponentData
    {
        public bgfx.TextureHandle handle;
        public bool externalOwner;
    }

    internal struct TextureBGFXExternal : IComponentData
    {
        public UIntPtr value;
    }

    internal struct FramebufferBGFX : ISystemStateComponentData
    {
        public bgfx.FrameBufferHandle handle;
    }

    internal unsafe struct PerThreadDataBGFX
    {
        public LightingViewSpaceBGFX viewSpaceLightCache;
        public bgfx.Encoder *encoder;
    }

    internal struct ShaderBGFX : ISystemStateComponentData
    {
        public bgfx.ProgramHandle handle;
    }

    // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // static helper for both RendererBGFXInstance and RenderBGFXSystem
    internal static unsafe class RendererBGFXStatic
    {
        public static uint GetResetFlags(ref DisplayInfo di)
        {
            uint flags = 0;
            if (di.colorSpace == ColorSpace.Linear)
                flags |= (uint)bgfx.ResetFlags.SrgbBackbuffer;
            if (!di.disableVSync)
                flags |= (uint)bgfx.ResetFlags.Vsync;
            return flags;
        }

        public static unsafe bgfx.Memory* CreateMemoryBlock(int size)
        {
            return bgfx.alloc((uint)size);
        }

        public static unsafe bgfx.Memory* CreateMemoryBlock(byte* mem, int size)
        {
            return bgfx.copy(mem, (uint)size);
        }

        public static string GetBackendString()
        {
            var backend = bgfx.get_renderer_type();
            return Marshal.PtrToStringAnsi(bgfx.get_renderer_name(backend));
        }

        public static float4x4 AdjustShadowMapProjection(ref float4x4 m)
        {
            // adjust m so that xyz' = xyz * .5 + .5, to map ndc [-1..1] to [0..1] range
            return new float4x4(
                (m.c0 + m.c0.wwww) * 0.5f,
                (m.c1 + m.c1.wwww) * 0.5f,
                (m.c2 + m.c2.wwww) * 0.5f,
                (m.c3 + m.c3.wwww) * 0.5f
            );
        }

        public static float4x4 AdjustProjection(ref float4x4 m, bool zCompress, bool yFlip)
        {
            // adjust m so that z' = z * .5 + .5, to map ndc z from [-1..1] to [0..1] range
            float4x4 m2 = m;
            if (zCompress)
            {
                m2.c0.z = (m2.c0.z + m2.c0.w) * 0.5f;
                m2.c1.z = (m2.c1.z + m2.c1.w) * 0.5f;
                m2.c2.z = (m2.c2.z + m2.c2.w) * 0.5f;
                m2.c3.z = (m2.c3.z + m2.c3.w) * 0.5f;
            }
            if (yFlip)
            {
                m2.c0.y = -m2.c0.y;
                m2.c1.y = -m2.c1.y;
                m2.c2.y = -m2.c2.y;
                m2.c3.y = -m2.c3.y;
            }
            return m2;
        }

        static private int Clamp(int x, int iMin, int iMax)
        {
            if (x < iMin) return iMin;
            if (x > iMax) return iMax;
            return x;
        }

        public static ulong MakeBGFXBlend(bgfx.StateFlags srcRGB, bgfx.StateFlags dstRGB)
        {
            return (((ulong)(srcRGB) | ((ulong)(dstRGB) << 4)))
                | (((ulong)(srcRGB) | ((ulong)(dstRGB) << 4)) << 8);
        }

        public static bool IsPot(int x)
        {
            if (x <= 0) return false;
            return (x & x - 1) == 0;
        }

        public static void AdjustFlagsForPot(ref Image2D im2d)
        {
            if (!IsPot(im2d.imagePixelHeight) || !IsPot(im2d.imagePixelWidth))
            {
                if ((im2d.flags & TextureFlags.UVClamp) != TextureFlags.UVClamp)
                {
                    RenderDebug.LogFormat("Texture is not a power of tw  but is not set to UV clamp. Forcing clamp.");
                    im2d.flags &= ~(TextureFlags.UVMirror | TextureFlags.UVRepeat);
                    im2d.flags |= TextureFlags.UVClamp;
                }
                if ((im2d.flags & TextureFlags.MimapEnabled) == TextureFlags.MimapEnabled)
                {
                    RenderDebug.LogFormat("Texture is not a power of two but had mip maps enabled. Turning off mip maps.");
                    im2d.flags &= ~TextureFlags.MimapEnabled;
                }
            }
        }

        public static bgfx.Memory* InitMipMapChain32(int w, int h)
        {
            int countPixels = w * h;
            int wl = w, hl = h;
            for (;;)
            {
                wl = wl == 1 ? 1 : wl >> 1;
                hl = hl == 1 ? 1 : hl >> 1;
                countPixels += wl * hl;
                if (wl == 1 && hl == 1) break;
            }
            return bgfx.alloc((uint)countPixels * 4);
        }

        public static bgfx.Memory* CreateMipMapChain32(int w, int h, uint* src, bool srgb)
        {
            bgfx.Memory* r = InitMipMapChain32(w, h);
            uint* dest = (uint*)r->data;
            UnsafeUtility.MemCpy(dest, src, w * h * 4);
            MipMapHelper.FillMipMapChain32(w, h, dest, srgb);
            return r;
        }

#if RENDERING_ENABLE_TRACE
        public static string BGFXSamplerFlagsToString(ulong flags)
        {
            string s = "";
            if ((flags & (ulong)bgfx.SamplerFlags.UClamp) != 0)
                s += "[UClamp]";
            if ((flags & (ulong)bgfx.SamplerFlags.VClamp) != 0)
                s += "[VClamp]";
            if ((flags & (ulong)bgfx.SamplerFlags.UMirror) != 0)
                s += "[UMirror]";
            if ((flags & (ulong)bgfx.SamplerFlags.VMirror) != 0)
                s += "[VMirror]";
            if ((flags & (ulong)bgfx.TextureFlags.Srgb) != 0)
                s += "{Srgb}";
            if ((flags & (ulong)bgfx.SamplerFlags.MipPoint) != 0)
                s += "[MipPoint]";
            if ((flags & (ulong)bgfx.SamplerFlags.MinPoint) != 0)
                s += "[MinPoint]";
            if ((flags & (ulong)bgfx.SamplerFlags.MagPoint) != 0)
                s += "[MagPoint]";
            if ((flags & (ulong)bgfx.TextureFlags.Rt) != 0)
                s += "{Rt}";
            return s;
        }

#endif
    }

    // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    internal unsafe struct RendererBGFXInstance
    {
        public bgfx.VertexLayoutHandle m_simpleVertexBufferDeclHandle;
        public bgfx.VertexLayout m_simpleVertexBufferDecl;
        public bgfx.VertexLayoutHandle m_simpleSkinnedVertexBufferDeclHandle;
        public bgfx.VertexLayout m_simpleSkinnedVertexBufferDecl;
        public bgfx.VertexLayoutHandle m_litVertexBufferDeclHandle;
        public bgfx.VertexLayout m_litVertexBufferDecl;
        public bgfx.VertexLayoutHandle m_litSkinnedVertexBufferDeclHandle;
        public bgfx.VertexLayout m_litSkinnedVertexBufferDecl;
        public bgfx.VertexLayoutHandle m_posOnlyVertexBufferDeclHandle;
        public bgfx.VertexLayout m_posOnlyVertexBufferDecl;

        public bgfx.TextureHandle m_whiteTexture;
        public bgfx.TextureHandle m_greyTexture;
        public bgfx.TextureHandle m_blackTexture;
        public bgfx.TextureHandle m_upTexture;
        public bgfx.TextureHandle m_noShadow;

        public SimpleShader m_simpleShader;
        public SimpleSkinnedMeshShader m_simpleSkinnedMeshShader;
        public LineShader m_lineShader;
        public LitShader m_litShader;
        public LitSkinnedMeshShader m_litSkinnedMeshShader;
        public BlitShader m_blitShader;
        public ShadowMapShader m_shadowMapShader;
        public SkinnedMeshShadowMapShader m_skinnedMeshShadowMapShader;

        public MeshBGFX m_quadMesh;
        public AABB m_quadMeshBounds;

        public bool m_initialized;
        public bgfx.RendererType m_rendererType;

        public bool m_resume;
        public int m_fbWidth;
        public int m_fbHeight;
        public bool m_disableVSync;
        public ColorSpace m_colorSpace;

        public int m_maxPerThreadData;
        public PerThreadDataBGFX* m_perThreadData; // base pointer to all decls, needs to be allocated before init

        public uint m_persistentFlags;
        public uint m_frameFlags;
        public bool m_homogeneousDepth;
        public bool m_originBottomLeft;
        public float4 m_outputDebugSelect;

        public bool m_allowSRGBTextures;
        public bool m_blitPrimarySRGB;

        public bgfx.PlatformData m_platformData;
#if ENABLE_DOTSRUNTIME_PROFILER
        private IntPtr m_markerFrame;
        private IntPtr m_markerEncoders;

        // WebGL does not have a render thread to track
#if !UNITY_WEBGL
        private struct BgfxProfilerInit
        {
            internal bool m_renderThreadInitialized;
            internal IntPtr m_renderThreadId;
            internal Profiling.ProfilerMarker m_markerRenderThreadRoot;
        }
        private static readonly Burst.SharedStatic<BgfxProfilerInit> s_profilerInit = Burst.SharedStatic<BgfxProfilerInit>.GetOrCreate<BgfxProfilerInit>();
#endif

        [MonoPInvokeCallback]
        private static unsafe void ProfilerBeginCallbackFunc(byte* name, int bytes)
        {
            IntPtr marker = (IntPtr)Profiler.MarkerGetOrCreate(ProfilerUnsafeUtility.CategoryRender, name, bytes, (ushort)Profiling.LowLevel.MarkerFlags.Script);
            uint markerId = ((Profiler.MarkerBucketNode*)marker)->markerId;

#if !UNITY_WEBGL
            bool renderThreadSkipFirstRoot = false;  // We must hack around a hack in unity editor introduced to deal with an issue with big unity player builds

            // If the thread isn't setup, it must be the render thread, as encoder threads will be job workers and should
            // be setup already.
            if (!s_profilerInit.Data.m_renderThreadInitialized)
            {
                FixedString32 str = new FixedString32("bgfx::renderFrame");
                bool rootRender = (bytes >= str.Length) && (UnsafeUtility.MemCmp(name, ((byte*)&str) + 2, str.Length) == 0);
                if (s_profilerInit.Data.m_renderThreadId == IntPtr.Zero)
                {
                    if (rootRender)
                    {
                        s_profilerInit.Data.m_renderThreadId = Baselib.LowLevel.Binding.Baselib_Thread_GetCurrentThreadId();
                        Profiler.ThreadSetInfo((ulong)s_profilerInit.Data.m_renderThreadId, Profiler.GetProfilerTime(), false, "", "Render Thread");
                    }
                    else
                        return;
                }

                if (s_profilerInit.Data.m_renderThreadId == Baselib.LowLevel.Binding.Baselib_Thread_GetCurrentThreadId())
                {
                    // We can't enable the render thread to profile until the root marker begins. This happens MID-FRAME,
                    // we we have to detect it. Otherwise, if profiling is enabled in general and we just start sending
                    // render thread profiling data, hierarchy gets corrupted along with profiler data sent to the editor.
                    if (rootRender && PlayerConnectionProfiler.Enabled)
                    {
                        s_profilerInit.Data.m_renderThreadInitialized = true;
                        renderThreadSkipFirstRoot = true;
                    }
                    else
                        return;
                }

                // If not the render thread, must be an encoder thread, so we can just keep going
            }

            if (s_profilerInit.Data.m_renderThreadId == Baselib.LowLevel.Binding.Baselib_Thread_GetCurrentThreadId())
            {
                if (ProfilerProtocolThread.Stream.markerStack.stackPos == 0)
                {
                    ProfilerProtocolThread.SendNewFrame();
                    if (!renderThreadSkipFirstRoot)
                        s_profilerInit.Data.m_markerRenderThreadRoot.Begin();
                }
            }

            if (ProfilerProtocolThread.SendBeginSample(markerId, Profiler.GetProfilerTime()))
                ProfilerProtocolThread.Stream.markerStack.PushMarker(marker);
            else
                s_profilerInit.Data.m_renderThreadInitialized = false;  // Need to process next "first frame" in order to do the skip-first-root hack
#else
            ProfilerProtocolThread.SendBeginSample(markerId, Profiler.GetProfilerTime());
            ProfilerProtocolThread.Stream.markerStack.PushMarker(marker);
#endif
        }

        [MonoPInvokeCallback]
        private static unsafe void ProfilerEndCallbackFunc()
        {
#if !UNITY_WEBGL
            // Once profiling is turned on, this could be the end of a previous frame's render thread which goes beyond frame bounds.
            // (Frame bounds are when we determine if profiling should be turned on or off)
            int stackPos = ProfilerProtocolThread.Stream.markerStack.stackPos;
            if (stackPos == 0)
                return;
#endif
            IntPtr marker = ProfilerProtocolThread.Stream.markerStack.PopMarker();
            uint markerId = ((Profiler.MarkerBucketNode*)marker)->markerId;
            ProfilerProtocolThread.SendEndSample(markerId, Profiler.GetProfilerTime());

#if !UNITY_WEBGL
            if (stackPos == 1 && s_profilerInit.Data.m_renderThreadId == Baselib.LowLevel.Binding.Baselib_Thread_GetCurrentThreadId())
                s_profilerInit.Data.m_markerRenderThreadRoot.End();
#endif
        }

        static class Managed {
            public static bgfx.ProfilerBeginCallback m_profilerBeginCallback = ProfilerBeginCallbackFunc;

            public static bgfx.ProfilerEndCallback m_profilerEndCallback = ProfilerEndCallbackFunc;
        }
#endif

        public void SetFlagThisFrame(bgfx.DebugFlags flag)
        {
            m_frameFlags |= (uint)flag;
            if (m_initialized)
                bgfx.set_debug(m_persistentFlags | m_frameFlags);
        }

        public void SetFlagPersistent(bgfx.DebugFlags flag)
        {
            m_persistentFlags |= (uint)flag;
            if (m_initialized)
                bgfx.set_debug(m_persistentFlags | m_frameFlags);
        }

        public void ClearFlagPersistent(bgfx.DebugFlags flag)
        {
            m_persistentFlags &= ~(uint)flag;
            if (m_initialized)
                bgfx.set_debug(m_persistentFlags | m_frameFlags);
        }

        public void UpdateSRGBState(bgfx.RendererType backend)
        {
            if (m_colorSpace == ColorSpace.Linear)
            {
                m_allowSRGBTextures = true;
                m_blitPrimarySRGB = backend == bgfx.RendererType.OpenGLES;
            }
            else
            {
                RenderDebug.LogAlways("SRGB sampling and writing is disabled via DisplayInfo setting.");
                m_allowSRGBTextures = false;
                m_blitPrimarySRGB = false;
            }
        }

        public void ShutdownInstance()
        {
            bgfx.destroy_texture(m_whiteTexture);
            bgfx.destroy_texture(m_greyTexture);
            bgfx.destroy_texture(m_blackTexture);
            bgfx.destroy_texture(m_upTexture);
            bgfx.destroy_texture(m_noShadow);
            m_simpleShader.Destroy();
            m_litShader.Destroy();
            m_litSkinnedMeshShader.Destroy();
            m_lineShader.Destroy();
            m_blitShader.Destroy();
            m_shadowMapShader.Destroy();
            m_skinnedMeshShadowMapShader.Destroy();
            m_quadMesh.Destroy();
            bgfx.shutdown();
            MipMapHelper.Shutdown();
            m_initialized = false;
        }

        public void InitInstance(World world, DisplayInfo di)
        {
            Assert.IsTrue(m_perThreadData != null);

            var em = world.EntityManager;
            var windowSystem = world.GetExistingSystem<WindowSystem>();

            var rendererType = bgfx.RendererType.Count; // Auto

#if ENABLE_DOTSRUNTIME_PROFILER
            m_markerFrame = ProfilerUnsafeUtility.CreateMarker("Frame()", ProfilerUnsafeUtility.CategoryRender, Profiling.LowLevel.MarkerFlags.Default, 0);
            m_markerEncoders = ProfilerUnsafeUtility.CreateMarker("WaitForEncoders()", ProfilerUnsafeUtility.CategoryRender, Profiling.LowLevel.MarkerFlags.Default, 0);
#if !UNITY_WEBGL
            s_profilerInit.Data.m_markerRenderThreadRoot = new Profiling.ProfilerMarker("Hidden BGFX Render thread root");
            s_profilerInit.Data.m_renderThreadInitialized = false;
            s_profilerInit.Data.m_renderThreadId = IntPtr.Zero;
#endif
#endif

#if RENDERING_FORCE_OPENGL
            rendererType = bgfx.RendererType.OpenGL;
#endif
            m_platformData.nwh = windowSystem.GetPlatformWindowHandle().ToPointer();

#if UNITY_MACOSX
            // Mac takes a different path -- we need to create the actual CAMetalLayer
            // or NSOpenGLView + Context on the main thread instead of letting bgfx do it.
            if (rendererType == bgfx.RendererType.Metal || rendererType == bgfx.RendererType.Count)
            {
                var glfw = (GLFWWindowSystem)windowSystem;
                var layer = glfw.GetMacMetalLayerHandle();
                if (layer != IntPtr.Zero)
                {
                    m_platformData.nwh = layer.ToPointer();
                    rendererType = bgfx.RendererType.Metal;
                }
                else if (rendererType == bgfx.RendererType.Count)
                {
                    rendererType = bgfx.RendererType.OpenGL;
                }
                else
                {
                    throw new InvalidOperationException("Failed to create CAMetalLayer and Metal is forced");
                }
            }

            if (rendererType == bgfx.RendererType.OpenGL)
            {
                // force OpenGL, which on Mac also means forcing single threaded mode because the underlying
                // lib wants to do too many things from the render thread
                bgfx.render_frame(0);
            }
#endif
            // Must be called before bgfx::init
            fixed(bgfx.PlatformData*pd = &m_platformData)
            bgfx.set_platform_data(pd);

#if ENABLE_DOTSRUNTIME_PROFILER
            IntPtr mainThreadId = Baselib.LowLevel.Binding.Baselib_Thread_GetCurrentThreadId();
#endif

            bgfx.Init init = new bgfx.Init();
            init.allocator = bgfx.AllocatorInit();
#if ENABLE_DOTSRUNTIME_PROFILER
            init.profile = 1;
            init.callback = bgfx.CallbacksInit(Marshal.GetFunctionPointerForDelegate(Managed.m_profilerBeginCallback), Marshal.GetFunctionPointerForDelegate(Managed.m_profilerEndCallback));
#else
            init.profile = 0;
            init.callback = bgfx.CallbacksInit(IntPtr.Zero, IntPtr.Zero);
#endif

#if DEBUG
            init.debug = 1;
#else
            init.debug = 0;
#endif

            m_maxPerThreadData = JobsUtility.JobWorkerCount + 1;
            RenderDebug.LogFormat ( "Allocating {0} per thread data instances for rendering.", m_maxPerThreadData);
            init.platformData = m_platformData;
            init.type = rendererType;
            init.resolution.width = (uint)di.framebufferWidth;
            init.resolution.height = (uint)di.framebufferHeight;
            init.resolution.format = bgfx.TextureFormat.RGBA8;
            init.resolution.numBackBuffers = 1;
            init.resolution.reset = RendererBGFXStatic.GetResetFlags(ref di);
            init.limits.maxEncoders = (ushort)(m_maxPerThreadData + 1); // +1 for the default main thread encoder
            init.limits.transientVbSize = 6 << 20; // BGFX_CONFIG_TRANSIENT_VERTEX_BUFFER_SIZE;
            init.limits.transientIbSize = 1 << 20; // BGFX_CONFIG_TRANSIENT_INDEX_BUFFER_SIZE;

            FlushViewSpaceCache();

            m_fbHeight = di.framebufferHeight;
            m_fbWidth = di.framebufferWidth;
            if (!bgfx.init(&init))
                throw new InvalidOperationException("Failed BGFX init.");

            m_rendererType = rendererType = bgfx.get_renderer_type();

            RenderDebug.LogFormatAlways("BGFX init ok, backend is {0}.", RendererBGFXStatic.GetBackendString());

            var caps = bgfx.get_caps();
            m_homogeneousDepth = caps->homogeneousDepth != 0 ? true : false;
            m_originBottomLeft = caps->originBottomLeft != 0 ? true : false;
            RenderDebug.LogFormatAlways("  Depth: {0} Origin: {1}", m_homogeneousDepth ? "[-1..1]" : "[0..1]", m_originBottomLeft ? "bottom left" : "top left");
            if ((caps->supported & (ulong)bgfx.CapsFlags.TextureCompareLequal) == 0)
                RenderDebug.LogFormatAlways("  No direct shadow map support.");

            var backend = bgfx.get_renderer_type();

            UpdateSRGBState(backend);
            RenderDebug.LogFormatAlways("  SRGB request = {0}. SRGB textures actual = {1}. Shader SRGB blit = {2}", di.colorSpace == ColorSpace.Gamma ? "off" : "on", m_allowSRGBTextures ? "on" : "off", m_blitPrimarySRGB ? "on" : "off");

            m_persistentFlags = (uint)bgfx.DebugFlags.Text;
            bgfx.set_debug(m_persistentFlags);

            fixed(bgfx.VertexLayout *p = &m_simpleVertexBufferDecl)
            {
                bgfx.vertex_layout_begin(p, backend);
                bgfx.vertex_layout_add(p, bgfx.Attrib.Position, 3, bgfx.AttribType.Float, false, false);
                bgfx.vertex_layout_add(p, bgfx.Attrib.TexCoord0, 2, bgfx.AttribType.Float, false, false);
                bgfx.vertex_layout_add(p, bgfx.Attrib.Color0, 4, bgfx.AttribType.Float, false, false);
                bgfx.vertex_layout_add(p, bgfx.Attrib.TexCoord1, 3, bgfx.AttribType.Float, false, false); // billboard position
                bgfx.vertex_layout_end(p);
                m_simpleVertexBufferDeclHandle = bgfx.create_vertex_layout(p);
            }
            fixed (bgfx.VertexLayout* p = &m_simpleSkinnedVertexBufferDecl)
            {
                bgfx.vertex_layout_begin(p, backend);
                bgfx.vertex_layout_add(p, bgfx.Attrib.Position, 3, bgfx.AttribType.Float, false, false);
                bgfx.vertex_layout_add(p, bgfx.Attrib.TexCoord0, 2, bgfx.AttribType.Float, false, false);
                bgfx.vertex_layout_add(p, bgfx.Attrib.Color0, 4, bgfx.AttribType.Float, false, false);
                bgfx.vertex_layout_add(p, bgfx.Attrib.TexCoord1, 3, bgfx.AttribType.Float, false, false); // billboard position
                bgfx.vertex_layout_add(p, bgfx.Attrib.Weight, 4, bgfx.AttribType.Float, false, false);
                bgfx.vertex_layout_add(p, bgfx.Attrib.Indices, 4, bgfx.AttribType.Float, false, false);
                bgfx.vertex_layout_end(p);
                m_simpleSkinnedVertexBufferDeclHandle = bgfx.create_vertex_layout(p);
            }
            fixed(bgfx.VertexLayout *p = &m_litVertexBufferDecl)
            {
                bgfx.vertex_layout_begin(p, backend);
                bgfx.vertex_layout_add(p, bgfx.Attrib.Position, 3, bgfx.AttribType.Float, false, false);
                bgfx.vertex_layout_add(p, bgfx.Attrib.TexCoord0, 2, bgfx.AttribType.Float, false, false);
                bgfx.vertex_layout_add(p, bgfx.Attrib.Normal, 3, bgfx.AttribType.Float, false, false);
                bgfx.vertex_layout_add(p, bgfx.Attrib.Tangent, 3, bgfx.AttribType.Float, false, false);
                bgfx.vertex_layout_add(p, bgfx.Attrib.TexCoord1, 3, bgfx.AttribType.Float, false, false); // billboard position
                bgfx.vertex_layout_add(p, bgfx.Attrib.Color0, 4, bgfx.AttribType.Float, false, false); // albedo
                bgfx.vertex_layout_add(p, bgfx.Attrib.TexCoord2, 2, bgfx.AttribType.Float, false, false); // metal_smoothness
                bgfx.vertex_layout_end(p);
                m_litVertexBufferDeclHandle = bgfx.create_vertex_layout(p);
            }
            fixed(bgfx.VertexLayout *p = &m_litSkinnedVertexBufferDecl)
            {
                bgfx.vertex_layout_begin(p, backend);
                bgfx.vertex_layout_add(p, bgfx.Attrib.Position, 3, bgfx.AttribType.Float, false, false);
                bgfx.vertex_layout_add(p, bgfx.Attrib.TexCoord0, 2, bgfx.AttribType.Float, false, false);
                bgfx.vertex_layout_add(p, bgfx.Attrib.Normal, 3, bgfx.AttribType.Float, false, false);
                bgfx.vertex_layout_add(p, bgfx.Attrib.Tangent, 3, bgfx.AttribType.Float, false, false);
                bgfx.vertex_layout_add(p, bgfx.Attrib.TexCoord1, 3, bgfx.AttribType.Float, false, false); // billboard position
                bgfx.vertex_layout_add(p, bgfx.Attrib.Color0, 4, bgfx.AttribType.Float, false, false); // albedo
                bgfx.vertex_layout_add(p, bgfx.Attrib.TexCoord2, 2, bgfx.AttribType.Float, false, false); // metal_smoothness
                bgfx.vertex_layout_add(p, bgfx.Attrib.Weight, 4, bgfx.AttribType.Float, false, false);
                bgfx.vertex_layout_add(p, bgfx.Attrib.Indices, 4, bgfx.AttribType.Float, false, false);
                bgfx.vertex_layout_end(p);
                m_litSkinnedVertexBufferDeclHandle = bgfx.create_vertex_layout(p);
            }
            fixed(bgfx.VertexLayout *p = &m_posOnlyVertexBufferDecl)
            {
                bgfx.vertex_layout_begin(p, backend);
                bgfx.vertex_layout_add(p, bgfx.Attrib.Position, 3, bgfx.AttribType.Float, false, false);
                m_posOnlyVertexBufferDeclHandle = bgfx.create_vertex_layout(p);
            }

            int foundShaders = 0;
            using (var shaderQuery = em.CreateEntityQuery(typeof(BuiltInShader), typeof(ShaderBinData)))
            {
                using (var shaderEntities = shaderQuery.ToEntityArray(Allocator.TempJob))
                {
                    foreach (var shaderE in shaderEntities)
                    {
                        var builtInShader = em.GetComponentData<BuiltInShader>(shaderE);
                        var shaders = em.GetComponentData<ShaderBinData>(shaderE);
                        foundShaders++;
                        if (builtInShader.Guid == BuiltInShaderType.simple)
                            m_simpleShader.Init(BGFXShaderHelper.GetPrecompiledShaderData(backend, shaders, ref builtInShader.Name));
                        else if (builtInShader.Guid == BuiltInShaderType.simplegpuskinning)
                            m_simpleSkinnedMeshShader.Init(BGFXShaderHelper.GetPrecompiledShaderData(backend, shaders, ref builtInShader.Name));
                        else if (builtInShader.Guid == BuiltInShaderType.simplelit)
                            m_litShader.Init(BGFXShaderHelper.GetPrecompiledShaderData(backend, shaders, ref builtInShader.Name));
                        else if (builtInShader.Guid == BuiltInShaderType.simplelitgpuskinning)
                            m_litSkinnedMeshShader.Init(BGFXShaderHelper.GetPrecompiledShaderData(backend, shaders, ref builtInShader.Name));
                        else if (builtInShader.Guid == BuiltInShaderType.line)
                            m_lineShader.Init(BGFXShaderHelper.GetPrecompiledShaderData(backend, shaders, ref builtInShader.Name));
                        else if (builtInShader.Guid == BuiltInShaderType.blitsrgb)
                            m_blitShader.Init(BGFXShaderHelper.GetPrecompiledShaderData(backend, shaders, ref builtInShader.Name));
                        else if (builtInShader.Guid == BuiltInShaderType.shadowmap)
                            m_shadowMapShader.Init(BGFXShaderHelper.GetPrecompiledShaderData(backend, shaders, ref builtInShader.Name));
                        else if (builtInShader.Guid == BuiltInShaderType.shadowmapgpuskinning)
                            m_skinnedMeshShadowMapShader.Init(BGFXShaderHelper.GetPrecompiledShaderData(backend, shaders, ref builtInShader.Name));
                        else
                            foundShaders--;
                    }
                }
            }

            // must have all shaders
            int totalShadersCount = 8;
            if (foundShaders != totalShadersCount)
                throw new Exception("Couldn't find all needed core precompiled shaders, only found " + foundShaders + "/" + totalShadersCount);

            // default texture
            m_whiteTexture = BGFXShaderHelper.MakeUnitTexture(0xff_ff_ff_ff);
            m_blackTexture = BGFXShaderHelper.MakeUnitTexture(0x00_00_00_00);
            m_greyTexture = BGFXShaderHelper.MakeUnitTexture(0x7f_7f_7f_7f);
            m_upTexture = BGFXShaderHelper.MakeUnitTexture(0xff_ff_7f_7f);
            m_noShadow = BGFXShaderHelper.MakeNoShadowTexture(backend, 0xffff);

            // default mesh
            ushort[] indices = { 0, 1, 2, 2, 3, 0 };
            SimpleVertex[] vertices = { new SimpleVertex { Position = new float3(-1, -1, 0), TexCoord0 = new float2(0, 0), Color = new float4(1) },
                                        new SimpleVertex { Position = new float3(1, -1, 0), TexCoord0 = new float2(1, 0), Color = new float4(1) },
                                        new SimpleVertex { Position = new float3(1,  1, 0), TexCoord0 = new float2(1, 1), Color = new float4(1) },
                                        new SimpleVertex { Position = new float3(-1,  1, 0), TexCoord0 = new float2(0, 1), Color = new float4(1) } };
            fixed(ushort* indicesP = indices) fixed(SimpleVertex * verticesP = vertices) fixed(RendererBGFXInstance * instance = &this)
            m_quadMesh = MeshBGFX.CreateStaticMesh(instance, indicesP, 6, verticesP, 4);
            m_quadMeshBounds = new AABB { Center = new float3(0, 0, 0), Extents = new float3(1, 1, 0) };

            if (backend == bgfx.RendererType.OpenGLES)
                m_blitPrimarySRGB = true;

            if (di.colorSpace == ColorSpace.Linear)
            {
                m_allowSRGBTextures = true;
            }
            else
            {
                RenderDebug.LogAlways("SRGB sampling and writing is disabled via DisplayInfo setting.");
                m_allowSRGBTextures = false;
                m_blitPrimarySRGB = false;
            }

#if UNITY_WEBGL
            // Verify that the WebGL context that was created is appropriate to run this content.
            HTMLNativeCalls.validateWebGLContextFeatures(m_allowSRGBTextures);
#endif

            m_initialized = true;
        }

        public void WaitForEncoders()
        {
            if (!m_initialized)
                return;
#if ENABLE_DOTSRUNTIME_PROFILER
            ProfilerUnsafeUtility.BeginSample(m_markerEncoders);
#endif
            for (int i = 0; i < m_maxPerThreadData; i++)
            {
                if (m_perThreadData[i].encoder != null)
                {
                    bgfx.encoder_end(m_perThreadData[i].encoder);
                    m_perThreadData[i].encoder = null;
                }
            }
#if ENABLE_DOTSRUNTIME_PROFILER
            ProfilerUnsafeUtility.EndSample(m_markerEncoders);
#endif
        }

        public void Frame()
        {
            if (!m_initialized)
                return;
#if ENABLE_DOTSRUNTIME_PROFILER
            ProfilerUnsafeUtility.BeginSample(m_markerFrame);
#endif
            // go bgfx!
            bgfx.frame(false);

            m_frameFlags = 0;
            bgfx.set_debug(m_persistentFlags);
#if ENABLE_DOTSRUNTIME_PROFILER
            ProfilerUnsafeUtility.EndSample(m_markerFrame);
#endif
        }

        public void FlushViewSpaceCache()
        {
            for (int i = 0; i < m_maxPerThreadData; i++)
                m_perThreadData[i].viewSpaceLightCache.cacheTag = -1;
        }

        public void ResetIfNeeded(DisplayInfo di, IntPtr nativeWindowHandle)
        {
            bool needReset = di.framebufferWidth != m_fbWidth || di.framebufferHeight != m_fbHeight || di.disableVSync != m_disableVSync || di.colorSpace != m_colorSpace;
            if (m_platformData.nwh != nativeWindowHandle.ToPointer())
            {
                m_platformData.nwh = nativeWindowHandle.ToPointer();
                fixed(bgfx.PlatformData* platformData = &m_platformData)
                {
                    bgfx.set_platform_data(platformData);
                }
                needReset = true;
                RenderDebug.LogFormatAlways("BGFX native window handle updated");
            }
            if (needReset)
            {
                bgfx.reset((uint)di.framebufferWidth, (uint)di.framebufferHeight, RendererBGFXStatic.GetResetFlags(ref di), bgfx.TextureFormat.RGBA8);
                m_fbWidth = di.framebufferWidth;
                m_fbHeight = di.framebufferHeight;
                m_disableVSync = di.disableVSync;
                m_colorSpace = di.colorSpace;
                UpdateSRGBState(bgfx.get_renderer_type());
                RenderDebug.LogFormatAlways("Resize BGFX to {0}, {1}", m_fbWidth, m_fbHeight);
            }
        }

        public ulong TextureFlagsToBGFXSamplerFlags(Image2D im2d)
        {
            ulong samplerFlags = 0; //Default is repeat and trilinear

            if ((im2d.flags & TextureFlags.UClamp) == TextureFlags.UClamp)
                samplerFlags |= (ulong)bgfx.SamplerFlags.UClamp;
            if ((im2d.flags & TextureFlags.VClamp) == TextureFlags.VClamp)
                samplerFlags |= (ulong)bgfx.SamplerFlags.VClamp;
            if ((im2d.flags & TextureFlags.UMirror) == TextureFlags.UMirror)
                samplerFlags |= (ulong)bgfx.SamplerFlags.UMirror;
            if ((im2d.flags & TextureFlags.VMirror) == TextureFlags.VMirror)
                samplerFlags |= (ulong)bgfx.SamplerFlags.VMirror;
            if ((im2d.flags & TextureFlags.Point) == TextureFlags.Point)
                samplerFlags |= (ulong)bgfx.SamplerFlags.Point;
            if (m_allowSRGBTextures && (im2d.flags & TextureFlags.Srgb) == TextureFlags.Srgb)
                samplerFlags |= (ulong)bgfx.TextureFlags.Srgb;
            if ((im2d.flags & TextureFlags.MimapEnabled) == TextureFlags.MimapEnabled &&
                (im2d.flags & TextureFlags.Linear) == TextureFlags.Linear)
                samplerFlags |= (ulong)bgfx.SamplerFlags.MipPoint;

            return samplerFlags;
        }

        public float4x4 GetAdjustedProjection(ref RenderPass pass)
        {
            bool rtt = (pass.passFlags & RenderPassFlags.RenderToTexture) == RenderPassFlags.RenderToTexture;
            bool yflip = !m_originBottomLeft && rtt;
            return RendererBGFXStatic.AdjustProjection(ref pass.projectionTransform, !m_homogeneousDepth, yflip);
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(UpdateWorldBoundsSystem))]
    [UpdateBefore(typeof(SubmitSystemGroup))]
    [AlwaysUpdateSystem]
    internal unsafe class RendererBGFXSystem : RenderingGPUSystem
    {
        private RendererBGFXInstance *m_instancePtr;
        private NativeArray<PerThreadDataBGFX> m_allocPerThreadData;

#if ENABLE_DOTSRUNTIME_PROFILER
        private IntPtr m_markerUpdate = ProfilerUnsafeUtility.CreateMarker("RendererBGFXSystem.OnUpdate", ProfilerUnsafeUtility.CategoryRender, Profiling.LowLevel.MarkerFlags.Default, 0);
        private IntPtr m_markerUpdateUpload = ProfilerUnsafeUtility.CreateMarker("Upload data", ProfilerUnsafeUtility.CategoryRender, Profiling.LowLevel.MarkerFlags.Default, 0);
        private IntPtr m_markerUpdateCallbacks = ProfilerUnsafeUtility.CreateMarker("Process callbacks", ProfilerUnsafeUtility.CategoryRender, Profiling.LowLevel.MarkerFlags.Default, 0);
        private IntPtr m_markerUpdateReinit = ProfilerUnsafeUtility.CreateMarker("Re-initialize", ProfilerUnsafeUtility.CategoryRender, Profiling.LowLevel.MarkerFlags.Default, 0);
        private IntPtr m_markerUpdateJobs = ProfilerUnsafeUtility.CreateMarker("Complete job dependencies", ProfilerUnsafeUtility.CategoryRender, Profiling.LowLevel.MarkerFlags.Default, 0);
#endif

        public int m_screenShotWidth;
        public int m_screenShotHeight;
        public string m_screenShotPath;
        public NativeList<byte> m_screenShot;

        public RendererBGFXInstance* InstancePointer()
        {
            return m_instancePtr;
        }

        public bool IsInitialized()
        {
            return m_instancePtr != null && m_instancePtr->m_initialized;
        }

        // helper: useful for triggering images from disk reload
        // call DestroyAllTextures() followed by ReloadAllImages() to force reload all textures
        // or ReloadAllImages() after a deinit and reinit to re-create textures
        public override void ReloadAllImages()
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
            // with none Image2DLoadFromFile so we do not touch things where loading is in flight
            Entities.WithoutBurst().WithNone<Image2DLoadFromFile>().WithAny<Image2DLoadFromFileGuids, Image2DLoadFromFileImageFile, Image2DLoadFromFileMaskFile>().ForEach((Entity e) =>
            {
                if (EntityManager.HasComponent<Image2DLoadFromFileGuids>(e))
                {
                    var guid = EntityManager.GetComponentData<Image2DLoadFromFileGuids>(e);
                    Debug.LogFormatAlways("Trigger reload for image from guid: {0}, {1} at {2}", guid.imageAsset.ToString(), guid.maskAsset.ToString(), e);
                }
                else
                {
                    var fnImage = "";
                    if (EntityManager.HasComponent<Image2DLoadFromFileImageFile>(e))
                        fnImage = EntityManager.GetBufferAsString<Image2DLoadFromFileImageFile>(e);
                    var fnMask = "";
                    if (EntityManager.HasComponent<Image2DLoadFromFileMaskFile>(e))
                        fnMask = EntityManager.GetBufferAsString<Image2DLoadFromFileMaskFile>(e);
                    Debug.LogFormatAlways("Trigger reload for image from file: {0} {1} at {2}", fnImage, fnMask, e);
                }
                ecb.AddComponent<Image2DLoadFromFile>(e);
            }).Run();
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        internal void DestroyAllShaders()
        {
            if (!IsInitialized())
                return;
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
            Entities.ForEach((Entity e, ref ShaderBGFX s) =>
            {
                if (s.handle.Valid)
                    bgfx.destroy_program(s.handle);
                ecb.RemoveComponent<ShaderBGFX>(e);
            }).Run();
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        // possible to debug call to force evicting all textures at runtime
        // will not to trigger a image reload  to re-create them after (via
        internal void DestroyAllTextures()
        {
            if (!IsInitialized())
                return;
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
            Entities.ForEach((Entity e, ref TextureBGFX tex) =>
            {
                if (!tex.externalOwner)
                    bgfx.destroy_texture(tex.handle);
                ecb.RemoveComponent<TextureBGFX>(e);
            }).Run();

#if ENABLE_DOTSRUNTIME_PROFILER
            ProfilerStats.Stats.drawStats.renderTextureCount = 0;
#endif

            // remove material caches
            Entities.WithAll<LitMaterialBGFX>().ForEach((Entity e) =>
            {
                ecb.RemoveComponent<LitMaterialBGFX>(e);
            }).Run();
            Entities.WithAll<SimpleMaterialBGFX>().ForEach((Entity e) =>
            {
                ecb.RemoveComponent<SimpleMaterialBGFX>(e);
            }).Run();

            // need to also destroy framebuffers so rtt textures are re-created
            Entities.ForEach((Entity e, ref FramebufferBGFX fb) =>
            {
                bgfx.destroy_frame_buffer(fb.handle);
                ecb.RemoveComponent<FramebufferBGFX>(e);
            }).Run();

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        public override void Shutdown()
        {
            DestroyAllShaders();
            DestroyAllTextures();
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
            // marking DynamicMeshData components as changed to properly recreate MeshBGFX on resume
            Entities
                .WithAll<MeshBGFX>()
                .ForEach((ref DynamicMeshData dnm) =>
            {
            }).Run();
            Entities.ForEach((Entity e, ref MeshBGFX mesh) =>
            {
                mesh.Destroy();
                ecb.RemoveComponent<MeshBGFX>(e);
            }).Run();
            Entities.WithAll<LitMaterialBGFX>().ForEach((Entity e) =>
            {
                ecb.RemoveComponent<LitMaterialBGFX>(e);
            }).Run();
            Entities.WithAll<SimpleMaterialBGFX>().ForEach((Entity e) =>
            {
                ecb.RemoveComponent<SimpleMaterialBGFX>(e);
            }).Run();
            ecb.Playback(EntityManager);
            ecb.Dispose();
            m_instancePtr->ShutdownInstance();
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            m_allocPerThreadData = new NativeArray<PerThreadDataBGFX>(JobsUtility.MaxJobThreadCount, Allocator.Persistent);
            m_screenShot = new NativeList<byte>(Allocator.Persistent);
            m_instancePtr = (RendererBGFXInstance*)Memory.Unmanaged.Allocate(sizeof(RendererBGFXInstance), 32, Allocator.Persistent);
            UnsafeUtility.MemClear(m_instancePtr, sizeof(RendererBGFXInstance));
        }

        protected override void OnDestroy()
        {
            if (IsInitialized())
                Shutdown();
            m_screenShot.Dispose();
            m_allocPerThreadData.Dispose();
            Memory.Unmanaged.Free(m_instancePtr, Allocator.Persistent);
            base.OnDestroy();
        }

        public override void Init()
        {
            if (IsInitialized())
                return;
            m_instancePtr->m_perThreadData = (PerThreadDataBGFX*)m_allocPerThreadData.GetUnsafePtr();
            m_instancePtr->InitInstance(World, GetSingleton<DisplayInfo>());
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            if (IsInitialized())
                return;
            Init();
        }

        protected static unsafe string StringFromCString(byte *s)
        {
            if (s == null)
                return "";
            string rs = "";
            while (*s != 0)
            {
                rs += (char)*s;
                s++;
            }
            return rs;
        }

        protected void HandleCallbacks()
        {
            byte* callbackMem = null;
            bgfx.CallbackEntry* callbackLog = null;
            int n = bgfx.CallbacksLock(&callbackMem, &callbackLog);
            string s;

            for (int i = 0; i < n; i++)
            {
                bgfx.CallbackEntry e = callbackLog[i];
                switch (e.callbacktype)
                {
                    case bgfx.CallbackType.Fatal:
                        s = StringFromCString(callbackMem + e.additionalAllocatedDataStart);
                        RenderDebug.LogAlways(s);
                        bgfx.CallbacksUnlockAndClear();
                        throw new InvalidOperationException(s);
                    case bgfx.CallbackType.Trace:
                        s = StringFromCString(callbackMem + e.additionalAllocatedDataStart);
                        RenderDebug.LogAlways(s);
                        break;
                    case bgfx.CallbackType.ScreenShotDesc:
                        bgfx.ScreenShotDesc* desc = (bgfx.ScreenShotDesc*)(callbackMem + e.additionalAllocatedDataStart);
                        RenderDebug.LogFormatAlways("Screenshot captured: {0}*{1} {2} pitch={3}", desc->width, desc->height, desc->yflip != 0 ? "flipped" : "", desc->pitch);
                        Assert.IsTrue(desc->width * 4 == desc->pitch);
                        Assert.IsTrue(desc->width * desc->height * 4 == desc->size);
                        m_screenShotWidth = desc->width;
                        m_screenShotHeight = desc->height;
                        break;
                    case bgfx.CallbackType.ScreenShotFilename:
                        s = StringFromCString(callbackMem + e.additionalAllocatedDataStart);
                        RenderDebug.LogFormatAlways("  Filename is {0}", s);
                        m_screenShotPath = s;
                        break;
                    case bgfx.CallbackType.ScreenShot:
                        RenderDebug.LogFormatAlways("  Data available {0} bytes.", e.additionalAllocatedDataSize);
                        m_screenShot.ResizeUninitialized(e.additionalAllocatedDataSize);
                        UnsafeUtility.MemCpy(m_screenShot.GetUnsafePtr(), callbackMem + e.additionalAllocatedDataStart, e.additionalAllocatedDataSize);
                        break;
                    default:
                        RenderDebug.Log("Unknown BGFX callback type!");
                        break;
                }
            }

            bgfx.CallbacksUnlockAndClear();
        }

        protected override void OnUpdate()
        {
#if ENABLE_DOTSRUNTIME_PROFILER
            ProfilerUnsafeUtility.BeginSample(m_markerUpdate);
            ProfilerUnsafeUtility.BeginSample(m_markerUpdateJobs);
#endif
            Dependency.Complete();
#if ENABLE_DOTSRUNTIME_PROFILER
            ProfilerUnsafeUtility.EndSample(m_markerUpdateJobs);
#endif
            if (!IsInitialized())
            {
                if (m_instancePtr != null && m_instancePtr->m_resume)
                {
#if ENABLE_DOTSRUNTIME_PROFILER
                    ProfilerUnsafeUtility.BeginSample(m_markerUpdateReinit);
#endif
                    Init();
                    ReloadAllImages();
                    m_instancePtr->m_resume = false;
#if ENABLE_DOTSRUNTIME_PROFILER
                    ProfilerUnsafeUtility.EndSample(m_markerUpdateReinit);
#endif
                }
                else
                {
#if ENABLE_DOTSRUNTIME_PROFILER
                    ProfilerUnsafeUtility.EndSample(m_markerUpdate);
#endif
                    return;
                }
            }

            Assert.IsTrue(m_instancePtr->m_maxPerThreadData >= JobsUtility.JobWorkerCount + 1, "Tiny Rendering does not handle increasing JobWorkerCount at runtime yet.");
            var di = GetSingleton<DisplayInfo>();
            var nwh = World.GetExistingSystem<WindowSystem>().GetPlatformWindowHandle();
            m_instancePtr->ResetIfNeeded(di, nwh);
            m_instancePtr->FlushViewSpaceCache();
#if ENABLE_DOTSRUNTIME_PROFILER
            ProfilerUnsafeUtility.BeginSample(m_markerUpdateUpload);
#endif
            UploadShaders();
            UploadTextures();
            UploadMeshes();
            UpdateRTT();
            UpdateExternalTextures();
#if ENABLE_DOTSRUNTIME_PROFILER
            ProfilerUnsafeUtility.EndSample(m_markerUpdateUpload);
            ProfilerUnsafeUtility.BeginSample(m_markerUpdateCallbacks);
#endif
            HandleCallbacks();
#if ENABLE_DOTSRUNTIME_PROFILER
            ProfilerUnsafeUtility.EndSample(m_markerUpdateCallbacks);
            ProfilerUnsafeUtility.EndSample(m_markerUpdate);
#endif
        }

        private void UploadShaders()
        {
            Entities.WithStructuralChanges().WithoutBurst().WithNone<ShaderBGFX>().ForEach((Entity e, ref CustomShader shader, in ShaderBinData shaderData) => {
                var handle = BGFXShaderHelper.GetPrecompiledShaderData(bgfx.get_renderer_type(), shaderData, ref shader.Name);
                if (!handle.Valid)
                {
                    RenderDebug.LogFormatAlways("Failed to upload shader {0} to bgfx, from blob asset. Falling back to default shader.", shader.Name);
                    shader.Status = ShaderStatus.DeviceError;
                    return;
                }
                EntityManager.AddComponentData(e, new ShaderBGFX { handle = handle });
                RenderDebug.LogFormatAlways("Created shader {0}, from blob asset.", shader.Name);
                shader.Status = ShaderStatus.Loaded;
            }).Run();
        }

        private void UploadMeshes()
        {
            var inst = InstancePointer();
            EntityCommandBuffer ecb;
#if DEBUG
            // assert component combinations - all of those combinations are illegal
            int kBad = 0;
            Entities.WithoutBurst().WithAll<DynamicLitVertex, DynamicSimpleVertex>().ForEach((Entity e) => { // lit + simple vertex buffer
                kBad++;
                Debug.LogFormat("DynamicLitVertex and DynamicSimpleVertex on {0}", e);
            }).Run();
            Entities.WithoutBurst().ForEach((Entity e, ref SimpleMeshRenderData smrd, ref LitMeshRenderData lmrd) => { // lit + simple
                kBad++;
                Debug.LogFormat("SimpleMeshRenderData and LitMeshRenderData on {0}", e);
                Debug.LogFormat(" SimpleMeshRenderData: {0} Vertices, {1} Indices", smrd.Mesh.Value.Vertices.Length, smrd.Mesh.Value.Indices.Length);
                Debug.LogFormat(" LitMeshRenderData: {0} Vertices, {1} Indices", lmrd.Mesh.Value.Vertices.Length, lmrd.Mesh.Value.Indices.Length);
            }).Run();
            Entities.WithoutBurst().ForEach((Entity e, ref SimpleMeshRenderer sm, ref LitMeshRenderer lm) => { // lit + simple
                kBad++;
                Debug.LogFormat("SimpleMeshRenderer and LitMeshRenderer on {0}", e);
            }).Run();
            Entities.WithoutBurst().ForEach((Entity e, ref SimpleMaterial sm, ref LitMaterial lm) => { // lit + simple
                kBad++;
                Debug.LogFormat("SimpleMaterial and LitMaterial on {0}", e);
                Debug.LogFormat(" SimpleMaterial: {0} Texture", sm.texAlbedoOpacity);
                Debug.LogFormat(" LitMaterial: {0} Texture", lm.texAlbedoOpacity);
            }).Run();
            Entities.WithoutBurst().WithAll<SimpleMeshRenderData, DynamicMeshData>().ForEach((Entity e) => { // render data + dynamic
                kBad++;
                Debug.LogFormat("SimpleMeshRenderData and DynamicMeshData on {0}", e);
            }).Run();
            Entities.WithoutBurst().WithAll<LitMeshRenderData, DynamicMeshData>().ForEach((Entity e) => { // render data + dynamic
                kBad++;
                Debug.LogFormat("LitMeshRenderData and DynamicMeshData on {0}", e);
            }).Run();
            Entities.WithoutBurst().WithNone<DynamicLitVertex, DynamicSimpleVertex>().WithAll<DynamicMeshData>().ForEach((Entity e) => { // dynamic, but missing source vertex buffer
                kBad++;
                Debug.LogFormat("DynamicLitVertex or DynamicSimpleVertex missing next to DynamicMeshData {0}", e);
            }).Run();
            Entities.WithoutBurst().WithNone<DynamicIndex>().WithAll<DynamicMeshData>().ForEach((Entity e) => { // dynamic, but missing source index buffer
                kBad++;
                Debug.LogFormat("DynamicIndex missing next to DynamicMeshData on {0}", e);
            }).Run();
            Assert.IsTrue(kBad == 0);
#endif
            ecb = new EntityCommandBuffer(Allocator.TempJob);

            // create and upload new ones: from blob asset, always static
            ComponentDataFromEntity<SkinnedMeshRenderData> ComponentSkinnedRenderMeshData = GetComponentDataFromEntity<SkinnedMeshRenderData>();
            Entities.WithNone<MeshBGFX>().ForEach((Entity e, ref SimpleMeshRenderData meshData) =>
			{
                bool hasSkinnedData = ComponentSkinnedRenderMeshData.HasComponent(e);
                if (hasSkinnedData)
                    ecb.AddComponent(e, MeshBGFX.CreateStaticSkinnedMeshFromBlobAsset(inst, meshData, ComponentSkinnedRenderMeshData[e]));
				else
                	ecb.AddComponent(e, MeshBGFX.CreateStaticMeshFromBlobAsset(inst, meshData));
            }).Run();
            Entities.WithNone<MeshBGFX>().ForEach((Entity e, ref LitMeshRenderData meshData) =>
			{
 				bool hasSkinnedData = ComponentSkinnedRenderMeshData.HasComponent(e);
                if (hasSkinnedData)
                    ecb.AddComponent(e, MeshBGFX.CreateStaticSkinnedMeshFromBlobAsset(inst, meshData, ComponentSkinnedRenderMeshData[e]));
				else
					ecb.AddComponent(e, MeshBGFX.CreateStaticMeshFromBlobAsset(inst, meshData));
            }).Run();

            // Add a MeshBGFX component to dynamic meshes that don't have one

            // TODO change "Empty" MeshBGFX to be all zeros, then use a query with a single bulk AddComponent
            Entities.WithNone<MeshBGFX>().WithAny<DynamicMeshData>()
                .ForEach((Entity e) => ecb.AddComponent(e, MeshBGFX.CreateEmpty()))
                .Run();

            ecb.Playback(EntityManager);
            ecb.Dispose();

            ComponentSkinnedRenderMeshData = GetComponentDataFromEntity<SkinnedMeshRenderData>();
            // then actually create dynamic buffers, re-creating or re-sizing if needed
            Entities
                .WithAll<DynamicSimpleVertex, DynamicIndex>()
                .WithChangeFilter<DynamicMeshData>()
                .ForEach((Entity e, ref MeshBGFX mb, ref DynamicMeshData dnm) =>
            {
                if (mb.IsValidFor(dnm))
                    return;
                mb.Destroy();
                bool hasSkinningData = false;
                if (dnm.CopyFrom != Entity.Null)
                    hasSkinningData = ComponentSkinnedRenderMeshData.HasComponent(dnm.CopyFrom);
                if (dnm.UseDynamicGPUBuffer)
                    mb = MeshBGFX.CreateDynamicMeshSimple(inst, dnm.VertexCapacity, dnm.IndexCapacity, hasSkinningData);
            }).Run();

            Entities
                .WithAll<DynamicLitVertex, DynamicIndex>()
                .WithChangeFilter<DynamicMeshData>()
                .ForEach((Entity e, ref MeshBGFX mb, ref DynamicMeshData dnm) =>
            {
                if (mb.IsValidFor(dnm))
                    return;

                mb.Destroy();

                bool hasSkinningData = false;
                if (dnm.CopyFrom != Entity.Null)
                    hasSkinningData = ComponentSkinnedRenderMeshData.HasComponent(dnm.CopyFrom);
                if (dnm.UseDynamicGPUBuffer)
                    mb = MeshBGFX.CreateDynamicMeshLit(inst, dnm.VertexCapacity, dnm.IndexCapacity, hasSkinningData);
            }).Run();

            // dynamic meshes, dynamic buffer: re-upload if dirty
            // static meshes, dynamic buffer: re-create if dirty
            Entities.ForEach((Entity e, DynamicBuffer<DynamicSimpleVertex> vertexSrc, DynamicBuffer<DynamicIndex> indexSrc, ref MeshBGFX dest, ref DynamicMeshData src) => {
                if (!src.Dirty && src.NumIndices == dest.IndexCount && src.NumVertices == dest.VertexCount)
                    return;
                bool hasSkinningData = false;
                if (src.CopyFrom != Entity.Null)
                    hasSkinningData = ComponentSkinnedRenderMeshData.HasComponent(src.CopyFrom);
                if (src.UseDynamicGPUBuffer)
                {
                    Assert.IsTrue(dest.IsDynamic());
                    Assert.IsTrue(src.NumIndices <= indexSrc.Length);
                    Assert.IsTrue(src.NumVertices <= vertexSrc.Length);
                    Assert.IsTrue(src.NumVertices <= src.VertexCapacity);
                    Assert.IsTrue(src.NumIndices <= src.IndexCapacity);
                    if (hasSkinningData)
                    {
                        int vertexSize = sizeof(DynamicSimpleVertex);
                        int skinningVertexSize = sizeof(SkinnedMeshVertex);
                        int totalVertexSize = vertexSize + skinningVertexSize;
                        byte* tmpBlock = (byte*)UnsafeUtility.Malloc(totalVertexSize * src.NumVertices, 4, Allocator.Temp);
                        SkinnedMeshRenderData skinnedMeshRenderData = ComponentSkinnedRenderMeshData[src.CopyFrom];
                        SkinnedMeshVertex* skinningdata = (SkinnedMeshVertex*)skinnedMeshRenderData.SkinnedMeshDataRef.Value.Vertices.GetUnsafePtr();
                        UnsafeUtility.MemCpyStride(tmpBlock, totalVertexSize, vertexSrc.GetUnsafePtr(), vertexSize, vertexSize, src.NumVertices);
                        UnsafeUtility.MemCpyStride(tmpBlock + vertexSize, totalVertexSize, skinningdata, skinningVertexSize, skinningVertexSize, src.NumVertices);
                        dest.UpdateDynamic((ushort*)indexSrc.GetUnsafePtr(), src.NumIndices, (byte*)tmpBlock, src.NumVertices, totalVertexSize);
                        UnsafeUtility.Free(tmpBlock, Allocator.Temp);
                    }
                    else
                        dest.UpdateDynamic((ushort*)indexSrc.GetUnsafePtr(), src.NumIndices, (byte*)vertexSrc.GetUnsafePtr(), src.NumVertices, sizeof(SimpleVertex));
                }
                else
                {
                    Assert.IsTrue(!dest.IsDynamic());
                    dest.Destroy();
                    if (hasSkinningData)
                    {
                        SkinnedMeshRenderData skinnedMeshRenderData = ComponentSkinnedRenderMeshData[src.CopyFrom];
                        SkinnedMeshVertex* skinningdata = (SkinnedMeshVertex*)skinnedMeshRenderData.SkinnedMeshDataRef.Value.Vertices.GetUnsafePtr();
                        dest = MeshBGFX.CreateStaticMesh(inst, (ushort*)indexSrc.GetUnsafePtr(), src.NumIndices, (SimpleVertex*)vertexSrc.GetUnsafePtr(), src.NumVertices, skinningdata);
                    }
                    else
                        dest = MeshBGFX.CreateStaticMesh(inst, (ushort*)indexSrc.GetUnsafePtr(), src.NumIndices, (SimpleVertex*)vertexSrc.GetUnsafePtr(), src.NumVertices);
                }
                src.Dirty = false;
            }).Run();

            Entities.ForEach((Entity e, DynamicBuffer<DynamicLitVertex> vertexSrc, DynamicBuffer<DynamicIndex> indexSrc, ref MeshBGFX dest, ref DynamicMeshData src) => {
                if (!src.Dirty && src.NumIndices == dest.IndexCount && src.NumVertices == dest.VertexCount)
                    return;
                bool hasSkinningData = false;
                if (src.CopyFrom != Entity.Null)
                    hasSkinningData = ComponentSkinnedRenderMeshData.HasComponent(src.CopyFrom);
                if (src.UseDynamicGPUBuffer)
                {
                    Assert.IsTrue(dest.IsDynamic());
                    Assert.IsTrue(src.NumIndices <= indexSrc.Length);
                    Assert.IsTrue(src.NumVertices <= vertexSrc.Length);
                    Assert.IsTrue(src.NumVertices <= src.VertexCapacity);
                    Assert.IsTrue(src.NumIndices <= src.IndexCapacity);
                    if (hasSkinningData)
                    {
                        int vertexSize = sizeof(DynamicLitVertex);
                        int skinningVertexSize = sizeof(SkinnedMeshVertex);
                        int totalVertexSize = vertexSize + skinningVertexSize;
                        byte* tmpBlock = (byte*)UnsafeUtility.Malloc(totalVertexSize * src.NumVertices, 4, Allocator.Temp);
                        SkinnedMeshRenderData skinnedMeshRenderData = ComponentSkinnedRenderMeshData[src.CopyFrom];
                        SkinnedMeshVertex* skinningdata = (SkinnedMeshVertex*)skinnedMeshRenderData.SkinnedMeshDataRef.Value.Vertices.GetUnsafePtr();
                        UnsafeUtility.MemCpyStride(tmpBlock, totalVertexSize, vertexSrc.GetUnsafePtr(), vertexSize, vertexSize, src.NumVertices);
                        UnsafeUtility.MemCpyStride(tmpBlock + vertexSize, totalVertexSize, skinningdata, skinningVertexSize, skinningVertexSize, src.NumVertices);
                        dest.UpdateDynamic((ushort*)indexSrc.GetUnsafePtr(), src.NumIndices, (byte*)tmpBlock, src.NumVertices, totalVertexSize);
                        UnsafeUtility.Free(tmpBlock, Allocator.Temp);
                    }
                    else
                        dest.UpdateDynamic((ushort*)indexSrc.GetUnsafePtr(), src.NumIndices, (byte*)vertexSrc.GetUnsafePtr(), src.NumVertices, sizeof(LitVertex));
                }
                else
                {
                    Assert.IsTrue(!dest.IsDynamic());
                    dest.Destroy();
                    if (hasSkinningData)
                    {
                        SkinnedMeshRenderData skinnedMeshRenderData = ComponentSkinnedRenderMeshData[src.CopyFrom];
                        SkinnedMeshVertex* skinningdata = (SkinnedMeshVertex*)skinnedMeshRenderData.SkinnedMeshDataRef.Value.Vertices.GetUnsafePtr();
                        dest = MeshBGFX.CreateStaticMesh(inst, (ushort*)indexSrc.GetUnsafePtr(), src.NumIndices, (SimpleVertex*)vertexSrc.GetUnsafePtr(), src.NumVertices, skinningdata);
                    }
                    else
                        dest = MeshBGFX.CreateStaticMesh(inst, (ushort*)indexSrc.GetUnsafePtr(), src.NumIndices, (LitVertex*)vertexSrc.GetUnsafePtr(), src.NumVertices);
                }
                src.Dirty = false;
            }).Run();
        }

        private void UpdateExternalTextures()
        {
            Entities.ForEach((Entity e, ref TextureBGFX texbgfx, ref TextureBGFXExternal texext) =>
            {
                bgfx.override_internal_texture_ptr(texbgfx.handle, texext.value);
                texbgfx.externalOwner = true;
            }).Run();
        }

        private void UploadTextures()
        {
            // upload all texture that need uploading - we do not track changes to images here. need a different mechanic for that.
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
            var instPtr = InstancePointer();

            // memory image2d textures
            Entities.WithoutBurst().WithNone<TextureBGFX>().ForEach((Entity e, DynamicBuffer<Image2DMemorySource> srcPixels, ref Image2D im2d) =>
            {
                if (im2d.status != ImageStatus.Loaded)
                    return;
                RendererBGFXStatic.AdjustFlagsForPot(ref im2d);
                int w = im2d.imagePixelWidth;
                int h = im2d.imagePixelHeight;
                byte* pixels = (byte *)srcPixels.GetUnsafePtr();
                bool isSRGB = (im2d.flags & TextureFlags.Srgb) == TextureFlags.Srgb;
                bool makeMips = (im2d.flags & TextureFlags.MimapEnabled) == TextureFlags.MimapEnabled;
                ulong flags = instPtr->TextureFlagsToBGFXSamplerFlags(im2d);
                bgfx.Memory* bgfxblock = makeMips ? RendererBGFXStatic.CreateMipMapChain32(w, h, (uint*)pixels, isSRGB) : RendererBGFXStatic.CreateMemoryBlock(pixels, w * h * 4);
                bgfx.TextureHandle  texHandle = bgfx.create_texture_2d((ushort)w, (ushort)h, makeMips, 1, bgfx.TextureFormat.RGBA8, flags, bgfxblock);
                ecb.AddComponent(e, new TextureBGFX
                {
                    handle = texHandle,
                    externalOwner = false
                });
                RenderDebug.LogFormat("Uploaded BGFX texture {0},{1} from memory to bgfx index {2}", w, h, (int)texHandle.idx);
            }).Run();
#if UNITY_DOTSRUNTIME
#if !UNITY_WEBGL
            Entities.WithoutBurst().WithNone<TextureBGFX>().ForEach((Entity e, ref Image2D im2d, ref Image2DSTB imstb) =>
            {
                if (im2d.status != ImageStatus.Loaded)
                    return;
                RendererBGFXStatic.AdjustFlagsForPot(ref im2d);
                bgfx.TextureHandle texHandle;
                int w = 0;
                int h = 0;
                unsafe {
                    byte* pixels = ImageIOSTBNativeCalls.GetImageFromHandle(imstb.imageHandle, ref w, ref h);
                    bool isSRGB = (im2d.flags & TextureFlags.Srgb) == TextureFlags.Srgb;
                    bool makeMips = (im2d.flags & TextureFlags.MimapEnabled) == TextureFlags.MimapEnabled;
                    ulong flags = instPtr->TextureFlagsToBGFXSamplerFlags(im2d);
                    bgfx.Memory* bgfxblock = makeMips ? RendererBGFXStatic.CreateMipMapChain32(w, h, (uint*)pixels, isSRGB) : RendererBGFXStatic.CreateMemoryBlock(pixels, w * h * 4);
                    texHandle = bgfx.create_texture_2d((ushort)w, (ushort)h, makeMips, 1, bgfx.TextureFormat.RGBA8, flags, bgfxblock);
                    RenderDebug.LogFormat("Uploaded BGFX texture {0},{1} from image handle {2} to bgfx index {3}", w, h, imstb.imageHandle, (int)texHandle.idx);
                }
                ImageIOSTBNativeCalls.FreeBackingMemory(imstb.imageHandle);
                ecb.RemoveComponent<Image2DSTB>(e);
                ecb.AddComponent(e, new TextureBGFX
                {
                    handle = texHandle,
                    externalOwner = false
                });
            }).Run();
#else
            Entities.WithoutBurst().WithNone<TextureBGFX>().ForEach((Entity e, ref Image2D im2d, ref Image2DHTML imhtml) =>
            {
                if (im2d.status != ImageStatus.Loaded)
                    return;
                RendererBGFXStatic.AdjustFlagsForPot(ref im2d);
                int w = im2d.imagePixelWidth;
                int h = im2d.imagePixelHeight;
                bgfx.TextureHandle texHandle;
                ulong flags = instPtr->TextureFlagsToBGFXSamplerFlags(im2d);
                bool makeMips = (im2d.flags & TextureFlags.MimapEnabled) == TextureFlags.MimapEnabled;
                bool isSRGB = (im2d.flags & TextureFlags.Srgb) == TextureFlags.Srgb;
                isSRGB = false;
                unsafe {
                    bgfx.Memory* bgfxblock = makeMips ? RendererBGFXStatic.InitMipMapChain32(w, h) : RendererBGFXStatic.CreateMemoryBlock(w * h * 4);
                    ImageIOHTMLNativeCalls.ImageToMemory(imhtml.imageIndex, w, h, bgfxblock->data);
                    if (makeMips) MipMapHelper.FillMipMapChain32(w, h, (uint*)bgfxblock->data, isSRGB);
                    texHandle = bgfx.create_texture_2d((ushort)w, (ushort)h, makeMips, 1, bgfx.TextureFormat.RGBA8, flags, bgfxblock);
                }
                RenderDebug.LogFormat("Uploaded BGFX texture {0},{1} from image handle {2}", w, h, imhtml.imageIndex);
                ecb.AddComponent(e, new TextureBGFX
                {
                    handle = texHandle,
                    externalOwner = false
                });
            }).Run();
#endif
#endif
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        void RemoveTextureBGFX(Entity e)
        {
            if (e == Entity.Null ) return;
            if (!EntityManager.HasComponent<TextureBGFX>(e) ) return;
            var tex = EntityManager.GetComponentData<TextureBGFX>(e);
            bgfx.destroy_texture(tex.handle);
            EntityManager.RemoveComponent<TextureBGFX>(e);
#if ENABLE_DOTSPLAYER_PROFILER
            ProfilerStats.Stats.drawStats.renderTextureCount--;
#endif
        }

        void UpdateRTT()
        {
            var instPtr = InstancePointer();

            // create bgfx textures for rtt textures
            // remove framebuffers if RenderNode vanished
            Entities.WithNone<RenderNodeTexture>().WithStructuralChanges().ForEach((Entity e, ref FramebufferBGFX fb) =>
            {
                bgfx.destroy_frame_buffer(fb.handle);
                EntityManager.RemoveComponent<FramebufferBGFX>(e);
            }).Run();

            // if a render node texture needs resizing, get rid of all the associated bgfx things here
            Entities.WithoutBurst().WithStructuralChanges().WithChangeFilter<RenderNodeAutoScaleToDisplay>().ForEach((Entity e, ref RenderNodeAutoScaleToDisplay rnastd, ref RenderNodeTexture rtt) => {
                if ( !rnastd.Resized ) return;
                RemoveTextureBGFX(rtt.colorTexture);
                RemoveTextureBGFX(rtt.depthTexture);
                rnastd.Resized = false;
                if (EntityManager.HasComponent<FramebufferBGFX>(e) ) {
                    FramebufferBGFX fb = EntityManager.GetComponentData<FramebufferBGFX>(e);
                    bgfx.destroy_frame_buffer(fb.handle);
                    EntityManager.RemoveComponent<FramebufferBGFX>(e);
                }
            }).Run();

            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
            Entities.WithoutBurst().WithNone<TextureBGFX>().ForEach((Entity e, ref Image2D im2d, ref Image2DRenderToTexture rtt) => {
                ushort w = (ushort)im2d.imagePixelWidth;
                ushort h = (ushort)im2d.imagePixelHeight;
                bgfx.TextureFormat fmt = bgfx.TextureFormat.Unknown;
                ulong flags = instPtr->TextureFlagsToBGFXSamplerFlags(im2d) | (ulong)bgfx.TextureFlags.Rt;
                switch (rtt.format)
                {
                    case RenderToTextureFormat.ShadowMap:
                        fmt = bgfx.TextureFormat.D16;
                        flags |= (ulong)bgfx.SamplerFlags.CompareLess;
                        break;
                    case RenderToTextureFormat.Depth:
                        fmt = bgfx.TextureFormat.D16; // needed for webgl on safari, should be D32 or D24 (see SAFARI_WEBGL_WORKAROUND)
                        break;
                    case RenderToTextureFormat.DepthStencil:
                        fmt = bgfx.TextureFormat.D24S8;
                        break;
                    case RenderToTextureFormat.RGBA:
                        fmt = bgfx.TextureFormat.RGBA8;
                        break;
                    case RenderToTextureFormat.R:
                        fmt = bgfx.TextureFormat.R8;
                        break;
                    case RenderToTextureFormat.RGBA16f:
                        fmt = bgfx.TextureFormat.RGBA16F;
                        break;
                    case RenderToTextureFormat.R16f:
                        fmt = bgfx.TextureFormat.R16F;
                        break;
                    case RenderToTextureFormat.R32f:
                        fmt = bgfx.TextureFormat.R32F;
                        break;
                }
                var handle = bgfx.create_texture_2d(w, h, false, 1, fmt, flags, null);
                ecb.AddComponent(e, new TextureBGFX
                {
                    handle = handle,
                    externalOwner = false
                });
#if RENDERING_ENABLE_TRACE
                RenderDebug.LogFormat("Created BGFX render target texture {0},{1} to bgfx index {2}. Format {3}, Flags {4}",
                    (int)w, (int)h, (int)handle.idx, fmt, RendererBGFXStatic.BGFXSamplerFlagsToString(flags));
#endif
#if ENABLE_DOTSPLAYER_PROFILER
                ProfilerStats.Stats.drawStats.renderTextureCount++;
#endif
            }).Run();
            ecb.Playback(EntityManager);
            ecb.Dispose();

            // create bgfx framebuffers for rtt nodes
            ecb = new EntityCommandBuffer(Allocator.Temp);
            bgfx.Attachment* att = stackalloc bgfx.Attachment[4];
            Entities.WithoutBurst().WithNone<FramebufferBGFX>().ForEach((Entity e, ref RenderNodeTexture rtt) =>
            {
                unsafe {
                    byte n = 0;
                    if (rtt.colorTexture != Entity.Null)
                    {
                        var texBGFX = EntityManager.GetComponentData<TextureBGFX>(rtt.colorTexture);
                        att[n].access = bgfx.Access.Write; //?
                        att[n].handle = texBGFX.handle;
                        att[n].layer = 0;
                        att[n].mip = 0;
                        att[n].resolve = (byte)bgfx.ResolveFlags.None;
                        n++;
                    }
                    if (rtt.depthTexture != Entity.Null)
                    {
                        var texBGFX = EntityManager.GetComponentData<TextureBGFX>(rtt.depthTexture);
                        att[n].access = bgfx.Access.Write; // ?
                        att[n].handle = texBGFX.handle;
                        att[n].layer = 0;
                        att[n].mip = 0;
                        att[n].resolve = (byte)bgfx.ResolveFlags.None;
                        n++;
                    }
                    ecb.AddComponent(e, new FramebufferBGFX
                    {
                        handle = bgfx.create_frame_buffer_from_attachment(n, att, false)
                    });
                }
            }).Run();
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        public bool HasScreenShot()
        {
            return m_screenShot.IsCreated && m_screenShot.Length != 0;
        }

        public void ResetScreenShot()
        {
            m_screenShotWidth = 0;
            m_screenShotHeight = 0;
            m_screenShotPath = null;
            m_screenShot.ResizeUninitialized(0);
        }

        public void RequestScreenShot(string s)
        {
            // invalidate previous
            if (m_screenShot.Length != 0)
                RenderDebug.LogFormat("Warning, previous screen shot {0} still allocated. It will be overwritten.", m_screenShotPath);
            bgfx.request_screen_shot(new bgfx.FrameBufferHandle { idx = 0xffff }, s);
        }

        public override void Resume()
        {
            m_instancePtr->m_resume = true;
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // system that finalized renders
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(SubmitSystemGroup))]
    public class SubmitFrameSystem : SystemBase
    {
        private void CheckState()
        {
#if DEBUG
            bgfx.dbg_text_clear(0, false);
#endif
            int nprim = GetEntityQuery(ComponentType.ReadOnly<RenderNodePrimarySurface>()).CalculateEntityCount();
            int ncam = GetEntityQuery(ComponentType.ReadOnly<Camera>()).CalculateEntityCount();
            if (nprim == 0 || ncam == 0)
            {
                uint clearcolor = 0;
#if DEBUG
                unsafe {
                    var sys = World.GetExistingSystem<RendererBGFXSystem>().InstancePointer();
                    sys->SetFlagThisFrame(bgfx.DebugFlags.Text);
                }
                if (nprim == 0) bgfx.dbg_text_printf(0, 0, 0xf, "No primary surface render node.", null);
                if (ncam == 0) bgfx.dbg_text_printf(0, 1, 0xf, "No cameras in scene.", null);
                float t = (float)World.Time.ElapsedTime * .25f;
                float4 warncolor = new float4(math.abs(math.sin(t)), math.abs(math.cos(t * .23f)) * .8f, math.abs(math.sin(t * 7.0f)) * .3f, 1.0f);
                clearcolor = Color.PackFloatABGR(warncolor);
#endif
                var di = GetSingleton<DisplayInfo>();
                bgfx.set_view_rect(0, 0, 0, (ushort)di.framebufferWidth, (ushort)di.framebufferHeight);
                bgfx.set_view_frame_buffer(0, new bgfx.FrameBufferHandle { idx = 0xffff });
                bgfx.set_view_clear(0, (ushort)bgfx.ClearFlags.Color, clearcolor, 1, 0);
                bgfx.touch(0);
            }
        }

        private unsafe void CollectProfilerStats(RendererBGFXInstance* instance)
        {
#if ENABLE_DOTSRUNTIME_PROFILER
            var stats = bgfx.get_stats();

            // We'd like to fill out as many stats as possible for the profiler, but only some are either relevant or immediately available

            ProfilerStats.Stats.drawStats.setPassCalls = stats->numViews;  // views is the number of passes
            ProfilerStats.Stats.drawStats.drawCalls = (int)stats->numDraw;
            ProfilerStats.Stats.drawStats.triangles1024 = (int)stats->numPrims[0] / 1024;
            ProfilerStats.Stats.drawStats.vertices1024 = (int)(stats->numPrims[0] * 3 / 6) / 1024;  // 3 vertices per triangle, divided by average valence of 6 which is optimal for a good mesh
            ProfilerStats.Stats.drawStats.hasInstancing = 0;
            ProfilerStats.Stats.drawStats.shadowCasters = 0;
            ProfilerStats.Stats.drawStats.usedTextureCount = stats->numTextures;
            ProfilerStats.Stats.drawStats.usedTextureKB = (int)stats->textureMemoryUsed / 1024;
            //ProfilerStats.stats.drawStats.renderTextureCount;   // Calculated in RendererBGFXSystem.UpdateRTT()
            ProfilerStats.Stats.drawStats.renderTextureKB = (int)stats->rtMemoryUsed / 1024;
            ProfilerStats.Stats.drawStats.screenWidth = stats->width;
            ProfilerStats.Stats.drawStats.screenHeight = stats->height;
            ProfilerStats.Stats.drawStats.screenKB = stats->width * stats->height * 4 / 1024;
            ProfilerStats.Stats.drawStats.vboTotal = stats->numVertexBuffers;
            ProfilerStats.Stats.drawStats.totalAvailableVRamMB = (int)(stats->gpuMemoryMax / 1024 / 1024);

            //ProfilerStats.stats.drawStats.batches;
            //ProfilerStats.stats.drawStats.dynamicBatchedDrawCalls;
            //ProfilerStats.stats.drawStats.dynamicBatches;
            //ProfilerStats.stats.drawStats.dynamicBatchedTriangles;
            //ProfilerStats.stats.drawStats.dynamicBatchedVertices;
            //ProfilerStats.stats.drawStats.staticBatchedDrawCalls;
            //ProfilerStats.stats.drawStats.staticBatches;
            //ProfilerStats.stats.drawStats.staticBatchedTriangles;
            //ProfilerStats.stats.drawStats.staticBatchedVertices;
            //ProfilerStats.stats.drawStats.instancedBatchedDrawCalls;
            //ProfilerStats.stats.drawStats.instancedBatches;
            //ProfilerStats.stats.drawStats.instancedTriangles;
            //ProfilerStats.stats.drawStats.instancedVertices;
            //ProfilerStats.stats.drawStats.renderTextureStateChanges;
            //ProfilerStats.Stats.drawStats.shadowCasters;
            //ProfilerStats.stats.drawStats.vboTotalBytes;
            //ProfilerStats.stats.drawStats.vboUploads;
            //ProfilerStats.stats.drawStats.vboUploadBytes;
            //ProfilerStats.stats.drawStats.ibUploads;
            //ProfilerStats.stats.drawStats.ibUploadBytes;
            //ProfilerStats.stats.drawStats.visibleSkinnedMeshes;

            ProfilerStats.GatheredStats |= ProfilerModes.ProfileRendering;
#endif
        }

        protected unsafe override void OnUpdate()
        {
            CompleteDependency();
            var sys = World.GetExistingSystem<RendererBGFXSystem>();
            if (sys.IsInitialized())
            {
                CheckState();
                var instancePointer = sys.InstancePointer();
                instancePointer->WaitForEncoders();
                CollectProfilerStats(instancePointer);
                instancePointer->Frame();
            }
        }
    }
}
