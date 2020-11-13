using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Tiny;
using Unity.Tiny.Assertions;
using Unity.Tiny.Rendering;

namespace Unity.Tiny.Rendering
{
    public enum RenderGraphMode
    {
        // The render buffer is fixed to a certain resolution and will blit to the front buffer, preserving aspect. 
        // If the aspect of the front and render buffer are different, the render buffer is scaled down.
        // Bars with the color in DisplayInfo.backgroundBorderColor are drawn where there is no rendering
        FixedRenderBuffer = 1,
        // The render buffer is fixed to a certain maximum resolution, but maintains the front buffer aspect ratio. 
        // If the front buffer resolution is larger than the maximum render buffer resolution in either width or height,
        // the render buffer is scaled up by a uniform factor. 
        ScaledRenderBuffer = 2,
        // There is no render buffer, all rendering goes directly to the front buffer. 
        // There is no blitting or scaling pass. This mode is only correct when Gamma color space is used.
        DirectToFrontBuffer = 3 
    }

    // Next to RenderGraphConfig singleton, shows state of render graph for inspection.
    // Treat as read-only 
    public struct RenderGraphState: IComponentData
    {
        public int RenderBufferCurrentWidth;    // read-only, set by render graph builder to the final render buffer size in pixels 
        public int RenderBufferCurrentHeight;   // read-only, set by render graph builder to the final render buffer size in pixels 
    }

    // Config singleton, usually found next to DisplayInfo on the config entity
    // Changing entries here will force a full render graph rebuild 
    public struct RenderGraphConfig : IComponentData, IEquatable<RenderGraphConfig>
    {
        public RenderGraphMode Mode;
        public int RenderBufferWidth;   // ignored if Mode is DirectToFrontBuffer
        public int RenderBufferHeight;  // ignored if Mode DirectToFrontBuffer
        public int RenderBufferMaxSize; // ignored if anything but ScaledRenderBuffer

        public static RenderGraphConfig Default { get; } = new RenderGraphConfig
        {
            // default to 1080p fixed
            RenderBufferWidth = 1920,
            RenderBufferHeight = 1080,
            RenderBufferMaxSize = 2048,
            Mode = RenderGraphMode.FixedRenderBuffer
        };

        public bool Equals(RenderGraphConfig other)
        {
            return Mode == other.Mode && RenderBufferWidth == other.RenderBufferWidth && RenderBufferHeight == other.RenderBufferHeight;
        }
    }

    public struct RenderNodeRef : IBufferElementData
    {
        public Entity e; // next to a RenderNode, this node depends on those other nodes
    }

    public struct RenderPassRef : IBufferElementData
    {
        public Entity e; // next to RenderNode, list of passes in this node
    }

    public struct RenderToPasses : ISharedComponentData
    {
        public Entity e; // shared on every renderer, points to an entity that has RenderToPassesEntry[] buffer
    }

    public struct RenderGroup : IComponentData
    {   // tag for a render group (optional)
        // next to it: optional object/world bounds
        // next to it: DynamicArray<RenderToPassesEntry>
    }

    public struct RenderToPassesEntry : IBufferElementData
    {
        public Entity e; // list of entities that have a RenderPass component, where the renderer will render to
    }

    public struct RenderNode : IComponentData
    {
        public bool alreadyAdded;
        // next to it, required: DynamicArray<RenderNodeRef>, dependencies
        // next to it, required: DynamicArray<RenderPassRef>, list of passes in node
    }

    public struct RenderNodePrimarySurface : IComponentData
    {
        // place next to a RenderNode, to mark it as a sink: recursively starts evaluating render graphs from here
    }

    public struct RenderNodeTexture : IComponentData
    {
        public Entity colorTexture;
        public Entity depthTexture;
        public RenderPassRect rect;
    }

    public struct RenderNodeCubemap : IComponentData
    {
        public Entity target;
        public int side;
    }

    public struct RenderNodeShadowMap : IComponentData
    {
        public Entity lightsource;
    }

    public struct RenderPassUpdateFromCamera : IComponentData
    {
        // frustum, clear color, mask, and transforms will auto update from a camera entity
        public Entity camera; // must have Camera component
        public bool updateClear; // update clear state and color from camera as well, if not set, clear state will be left alone
    }

    public struct RenderPassUpdateFromBlitterAutoAspect : IComponentData
    {
        public Entity blitRenderer;
    }

    public struct RenderPassUpdateFromLight : IComponentData
    {
        // frustum and transforms will auto update from a light entity
        public Entity light; // must have Light component
    }

    public struct RenderPassUpdateFromCascade : IComponentData
    {
        // frustum and transforms will auto update from a camera entity
        public Entity light; // must have Light and CascadeShadowmappedLight component
        public int cascade;
    }

    public struct RenderPassCascade : IComponentData
    {
        public int cascade;
    }

    public struct RenderPassAutoSizeToNode : IComponentData
    {
        // convenience, place next to a RenderPass so it updates its size to match the node's size
        // the node must be either primary or have a target texture of some sort
    }

    public struct RenderPassClearColorFromBorder : IComponentData
    {
        // convenience, place next to a RenderPass so its clear color is updated from DisplayInfo.backgroundBorderColor
    }

    [Flags]
    public enum RenderPassClear : ushort
    {
        Color = 1, //bgfx.ClearFlags.Color,
        Depth = 2, //bgfx.ClearFlags.Depth,
        Stencil = 4//bgfx.ClearFlags.Stencil
    }

    public enum RenderPassSort: ushort
    {
        Unsorted, //bgfx.ViewMode.Default,
        Sorted, //bgfx.ViewMode.Sequential
        SortZGreater, //bgfx.ViewMode.DepthDescending,
        SortZLess //bgfx.ViewMode.DepthDescending,
    }

    [Flags]
    public enum RenderPassType : uint
    {
        Opaque = 2,
        Transparent = 4,
        UI = 8,
        FullscreenQuad = 16,
        ShadowMap = 32,
        Sprites = 64,
        DebugOverlay = 128,
        Clear = 256
    }

    public struct RenderPassRect
    {
        public ushort x, y, w, h;
    }

    public enum RenderPassFlags : uint
    {
        FlipCulling = 3,
        CullingMask = 3,
        RenderToTexture = 4
    }

    public struct RenderPass : IComponentData
    {
        public Entity inNode;
        public RenderPassSort sorting;
        public float4x4 projectionTransform;
        public float4x4 viewTransform;
        public float4x4 viewProjectionTransform;
        public RenderPassType passType;
        public ushort viewId;
        public RenderPassRect targetRect;   // size of the target texture or buffer area
        public RenderPassRect scissor;      // scissor rect, must be <= targetRect or empty to disable scissor testing
        public RenderPassRect viewport;     // viewport rect, must be <= targetRect. the viewport can be smaller than the targetRect when
                                            // for example rendering a texture in multiple passes, like cascades or split screen cameras
        public RenderPassClear clearFlags;  // matches bgfx
        public uint clearRGBA;              // clear color, packed in bgfx format
        public float clearDepth;            // matches bgfx
        public byte clearStencil;           // matches bgfx
        public RenderPassFlags passFlags;   // flags not used by bgfx, used internally
        public Frustum frustum;             // Frustum for late stage culling
        public ulong cameraMask;            // renderers have to match this camera mask
        public ulong shadowMask;            // renderers have to match this shadow mask

        // next to it, optional, Frustum for late stage culling
        public byte GetFlipCulling() { return (byte)(passFlags & RenderPassFlags.CullingMask); }
        public byte GetFlipCullingInverse() { return (byte)((passFlags & RenderPassFlags.CullingMask) ^ RenderPassFlags.CullingMask); }

        public uint ComputeSortDepth(in float4 txc3)
        {
            var screenPos = math.mul(viewProjectionTransform, txc3);
            float normZ = screenPos.z / screenPos.w; // [-1..1] range, in view frustrum
            normZ = normZ + 1024.0f; // give us generous extra room at near clip plane
            if (normZ < 0.0f) normZ = 0.0f; // we have to clamp here, as this is only the center of the object, and objects still can render even if their center is near clipped
            return math.asuint(normZ); // floats with the same sign sort as uints
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(UpdateCameraMatricesSystem))]
    [UpdateAfter(typeof(UpdateLightMatricesSystem))]
    [UpdateBefore(typeof(SubmitSystemGroup))]
    public unsafe class PreparePassesSystem : SystemBase
    {
        private void RecAddPasses(Entity eNode, ref ushort nextViewId)
        {
            // check already added
            RenderNode node = EntityManager.GetComponentData<RenderNode>(eNode);
            if (node.alreadyAdded)
                return;
            node.alreadyAdded = true;
            // recurse dependencies
            if (EntityManager.HasComponent<RenderNodeRef>(eNode))
            {
                DynamicBuffer<RenderNodeRef> deps = EntityManager.GetBuffer<RenderNodeRef>(eNode);
                for (int i = 0; i < deps.Length; i++)
                    RecAddPasses(deps[i].e, ref nextViewId);
            }
            // now add own passes
            if (EntityManager.HasComponent<RenderPassRef>(eNode))
            {
                DynamicBuffer<RenderPassRef> passes = EntityManager.GetBuffer<RenderPassRef>(eNode);
                //RenderDebug.LogFormat("Adding passes to graph for {0}: {1} passes.", eNode, passes.Length);
                for (int i = 0; i < passes.Length; i++)
                {
                    var p = EntityManager.GetComponentData<RenderPass>(passes[i].e);
                    p.viewId = nextViewId++;
                    EntityManager.SetComponentData<RenderPass>(passes[i].e, p);
                }
            }
        }

        protected override void OnCreate()
        {
        }

        void ResizeRenderTexture ( Entity eTex, int w, int h )
        {
            if ( eTex==Entity.Null || !EntityManager.HasComponent<Image2DRenderToTexture>(eTex) )
                return;
            Image2D im = EntityManager.GetComponentData<Image2D>(eTex);
            if ( im.imagePixelWidth==w && im.imagePixelHeight==h )
                return;
            // change tracked on rendering native side 
            im.imagePixelWidth = w;
            im.imagePixelHeight = h;
            EntityManager.SetComponentData<Image2D>(eTex,im);
        }

        protected override void OnUpdate()
        {
            // make sure passes have viewid, transform, scissor rect and view rect set

            // reset alreadyAdded state
            // we expect < 100 or so passes, so the below code does not need to be crazy great
            Entities.ForEach((ref RenderNode rnode) => { rnode.alreadyAdded = false; }).Run();
            Entities.ForEach((ref RenderPass pass) => { pass.viewId = 0xffff; }).Run(); // there SHOULD not be any passes around that are not referenced by the graph...

            // get all nodes, sort (bgfx issues in-order per view. a better api could use the render graph to issue without gpu
            // barriers where possible)
            // sort into eval order, assign pass viewId
            ushort nextViewId = 0;
            Entities.WithoutBurst().WithAll<RenderNodePrimarySurface>().ForEach((Entity eNode) => { RecAddPasses(eNode, ref nextViewId); }).Run();

            var di = GetSingleton<DisplayInfo>();
            
            Entities.WithoutBurst().WithAll<RenderNode>().ForEach((Entity e, ref RenderNodeAutoScaleToDisplay rnastd, ref RenderNodeTexture tex) => {
                Assert.IsTrue(EntityManager.HasComponent<MainViewNodeTag>(e));
                int w, h;
                RenderGraphBuilder.ComputeAutoScaleSize(di.framebufferWidth, di.framebufferHeight, rnastd.MaxSize, out w, out h);
                if ( tex.rect.w != w || tex.rect.h != h ) {
                    // have to resize the backing texture here
                    ResizeRenderTexture ( tex.colorTexture, w, h);
                    ResizeRenderTexture ( tex.depthTexture, w, h);
                    tex.rect.w = (ushort)w;
                    tex.rect.h = (ushort)h;
                    rnastd.Resized = true; // needed to signal renderer 
                    Debug.LogFormat ("Resize render target texture: tex = {0},{1} display = {2},{3}", w, h, di.framebufferWidth, di.framebufferHeight);
                }
            }).Run();

            Entities.WithoutBurst().WithAll<RenderPassAutoSizeToNode>().ForEach((Entity e, ref RenderPass pass) =>
            {
                if (EntityManager.HasComponent<RenderNodePrimarySurface>(pass.inNode))
                {
                    pass.viewport.x = 0;
                    pass.viewport.y = 0;
                    pass.viewport.w = (ushort)di.framebufferWidth;
                    pass.viewport.h = (ushort)di.framebufferHeight;
                    pass.targetRect = pass.viewport;
                    return;
                }
                if (EntityManager.HasComponent<RenderNodeTexture>(pass.inNode))
                {
                    var texRef = EntityManager.GetComponentData<RenderNodeTexture>(pass.inNode);
                    pass.viewport = texRef.rect;
                    pass.targetRect = pass.viewport;
                }
                // TODO: add others like cubemap
            }).Run();

            // auto update passes that are matched with a camera
            Entities.WithoutBurst().ForEach((Entity e, ref RenderPass pass, ref RenderPassUpdateFromCamera fromCam) =>
            {
                Entity eCam = fromCam.camera;
                Camera cam = EntityManager.GetComponentData<Camera>(eCam);
                CameraMatrices camData = EntityManager.GetComponentData<CameraMatrices>(eCam);
                pass.viewTransform = camData.view;
                pass.projectionTransform = camData.projection;
                pass.frustum = camData.frustum;
                pass.viewport = new RenderPassRect {
                    x = (ushort)(cam.viewportRect.x * pass.targetRect.w + pass.targetRect.x),
                    y = (ushort)(cam.viewportRect.y * pass.targetRect.h + pass.targetRect.y),
                    w = (ushort)(cam.viewportRect.width * pass.targetRect.w),
                    h = (ushort)(cam.viewportRect.height * pass.targetRect.h)
                };
                if ( EntityManager.HasComponent<CameraMask>(eCam) ) {
                    pass.cameraMask = EntityManager.GetComponentData<CameraMask>(eCam).mask;
                } else {
                    pass.cameraMask = ulong.MaxValue;
                }
                pass.shadowMask = ulong.MaxValue;

                if (fromCam.updateClear)
                {
                    switch (cam.clearFlags)
                    {
                        default:
                        case CameraClearFlags.SolidColor:
                            pass.clearFlags = RenderPassClear.Color | RenderPassClear.Depth | RenderPassClear.Stencil;
                            break;
                        case CameraClearFlags.DepthOnly:
                            pass.clearFlags = RenderPassClear.Depth | RenderPassClear.Stencil;
                            break;
                        case CameraClearFlags.Nothing:
                            pass.clearFlags = 0;
                            break;
                    }
                    float4 cc = cam.backgroundColor.AsFloat4();
                    if (di.colorSpace == ColorSpace.Gamma)
                        cc = Color.LinearToSRGB(cc);
                    pass.clearRGBA = Color.PackFloatABGR(cc);
                }
            }).Run();

            Entities.WithAll<RenderPassClearColorFromBorder>().ForEach((Entity e, ref RenderPass pass) => {
                float4 cc = di.backgroundBorderColor.AsFloat4();
                if (di.colorSpace == ColorSpace.Gamma)
                    cc = Color.LinearToSRGB(cc);
                pass.clearRGBA = Color.PackFloatABGR(cc);
            }).Run();

            // auto update passes that are matched with a cascade
            Entities.WithoutBurst().ForEach((Entity e, ref RenderPass pass, ref RenderPassUpdateFromCascade fromCascade) => {
                Entity eLight = fromCascade.light;
                CascadeShadowmappedLightCache csmData = EntityManager.GetComponentData<CascadeShadowmappedLightCache>(eLight);
                CascadeData cs = csmData.GetCascadeData(fromCascade.cascade);
                pass.viewTransform = cs.view;
                pass.projectionTransform = cs.proj;
                pass.frustum = cs.frustum;
            }).Run();

            // auto update passes that are matched with a light
            Entities.WithoutBurst().ForEach((Entity e, ref RenderPass pass, ref RenderPassUpdateFromLight fromLight) => {
                Entity eLight = fromLight.light;
                LightMatrices lightData = EntityManager.GetComponentData<LightMatrices>(eLight);
                pass.viewTransform = lightData.view;
                pass.projectionTransform = lightData.projection;
                pass.frustum = lightData.frustum;
            }).Run();

            // set model matrix for blitting to automatically match texture aspect
            Entities.WithoutBurst().ForEach((Entity e, ref RenderPass pass, ref RenderPassUpdateFromBlitterAutoAspect b) => {
                var br = EntityManager.GetComponentData<BlitRenderer>(b.blitRenderer);
                var im2d = EntityManager.GetComponentData<Image2D>(br.texture);
                float srcAspect = (float)im2d.imagePixelWidth / (float)im2d.imagePixelHeight;
                float4x4 m = float4x4.identity;
                float destAspect = (float)pass.viewport.w / (float)pass.viewport.h;
                if (destAspect <= srcAspect)   // flip comparison to zoom in instead of black bars
                {
                    m.c0.x = 1.0f; m.c1.y = destAspect / srcAspect;
                }
                else
                {
                    m.c0.x = srcAspect / destAspect; m.c1.y = 1.0f;
                }
                pass.viewTransform = m;
            }).Run();
        }
    }
}
