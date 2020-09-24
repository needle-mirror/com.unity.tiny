#define SAFARI_WEBGL_WORKAROUND

using System;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Tiny;
using Unity.Tiny.Assertions;
using Unity.Tiny.Rendering;
using Unity.Transforms;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;

namespace Unity.Tiny.Rendering
{
    // various tags that are used as hints to the builder

    public struct CameraMask : IComponentData
    {
        // add renderer to that camera if mask on camera AND mask on renderer != 0
        // note that if the mask component is missing it implies a mask with all bits set
        public ulong mask;
    }

    public struct ShadowMask : IComponentData
    {
        // cast shadows with regards to a light: if the light mask AND rendered mask !=0
        // note that if the mask component is missing it implies a mask with all bits set
        public ulong mask;
    }

    // component added to the main view texture node if it is auto scaling with the display window
    struct RenderNodeAutoScaleToDisplay : IComponentData
    {
        public int MaxSize;
        public bool Resized;
    }

    public struct MainViewNodeTag : IComponentData
    {
        // tag main view node for auto building
    }

    public struct CameraInRenderGraph : ISystemStateComponentData
    {
        // camera is added to graph
        public Entity Node; // render graph node the camera is on
    }

    // SpriteRenderer is a bit different, as it needs to batch and strict sort
    // need to buffer up entities once
    // TODO: this needs to move out and into 2D land
    public struct SortSpritesEntry : IBufferElementData, IComparable<SortSpritesEntry>
    {
        // do not put extra stuff in here, it's shuffled around during sorting
        public ulong key;
        public Entity e;

        public int CompareTo(SortSpritesEntry other)
        {
            if (key != other.key)
                return key < other.key ? -1 : 1;
            else
                return e.Index - other.e.Index;
        }
    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public unsafe class RenderGraphBuilder : SystemBase
    {
        struct SortedCamera : IComparable<SortedCamera>
        {
            public float depth;
            public Entity e;
            public ulong mask;

            public int CompareTo(SortedCamera other)
            {
                if (depth == other.depth)
                    return e.Index - other.e.Index;
                return depth < other.depth ? -1 : 1;
            }
        }

        Entity CreateFrontBufferRenderNode(int w, int h)
        {
            Entity eNode = EntityManager.CreateEntity();
            EntityManager.AddComponentData(eNode, new RenderNode { });
            EntityManager.AddComponentData(eNode, new RenderNodePrimarySurface { });

            var passRect = new RenderPassRect { x = 0, y = 0, w = (ushort)w, h = (ushort)h };
            Entity ePassBlit = EntityManager.CreateEntity();
            EntityManager.AddComponentData(ePassBlit, new RenderPass {
                inNode = eNode,
                sorting = RenderPassSort.Unsorted,
                projectionTransform = float4x4.identity,
                viewTransform = float4x4.identity,
                passType = RenderPassType.FullscreenQuad,
                viewId = 0xffff,
                scissor = new RenderPassRect(),
                viewport = passRect,
                targetRect = passRect,
                clearFlags = RenderPassClear.Color,
                clearDepth = 1.0f,
                clearStencil = 0,
                clearRGBA = 0xff,
                shadowMask = ulong.MaxValue,
                cameraMask = ulong.MaxValue
            });
            EntityManager.AddComponent<RenderPassAutoSizeToNode>(ePassBlit);
            EntityManager.AddComponent<RenderPassClearColorFromBorder>(ePassBlit);

            Entity ePassDebug = EntityManager.CreateEntity();
            EntityManager.AddComponentData(ePassDebug, new RenderPass {
                inNode = eNode,
                sorting = RenderPassSort.Sorted,
                projectionTransform = float4x4.identity,
                viewTransform = float4x4.identity,
                passType = RenderPassType.DebugOverlay,
                viewId = 0xffff,
                scissor = new RenderPassRect(),
                viewport = passRect,
                targetRect = passRect,
                clearFlags = 0,
                clearDepth = 1.0f,
                clearStencil = 0,
                shadowMask = ulong.MaxValue,
                cameraMask = ulong.MaxValue
            });
            EntityManager.AddComponent<RenderPassAutoSizeToNode>(ePassDebug);

            DynamicBuffer<RenderNodeRef> nodeRefs = EntityManager.AddBuffer<RenderNodeRef>(eNode);
            DynamicBuffer<RenderPassRef> passRefs = EntityManager.AddBuffer<RenderPassRef>(eNode);
            passRefs.Add(new RenderPassRef { e = ePassBlit });
            passRefs.Add(new RenderPassRef { e = ePassDebug });

            return eNode;
        }

        void SetPassComponents(Entity ePass, Entity eCam, bool updateClear = false)
        {
            EntityManager.AddComponent<RenderPassAutoSizeToNode>(ePass);
            EntityManager.AddComponentData(ePass, new RenderPassUpdateFromCamera {
                camera = eCam,
                updateClear = updateClear
            });
        }

        void CreateAllCameraPasses(int w, int h, Entity eCam, Entity eNode)
        {
            Camera cam = EntityManager.GetComponentData<Camera>(eCam);
            ulong cameraMask;
            if (EntityManager.HasComponent<CameraMask>(eCam))
                cameraMask = EntityManager.GetComponentData<CameraMask>(eCam).mask;
            else
                cameraMask = ulong.MaxValue;

            RenderPassRect passRect = new RenderPassRect {
                x = (ushort)(cam.viewportRect.x * w),
                y = (ushort)(cam.viewportRect.y * h),
                w = (ushort)(cam.viewportRect.width * w),
                h = (ushort)(cam.viewportRect.height * h)
            };
            Entity ePassClear = EntityManager.CreateEntity();
            EntityManager.AddComponentData(ePassClear, new RenderPass {
                inNode = eNode,
                sorting = RenderPassSort.Unsorted,
                projectionTransform = float4x4.identity,
                viewTransform = float4x4.identity,
                passType = RenderPassType.Clear,
                viewId = 0xffff,
                scissor = new RenderPassRect(),
                viewport = passRect,
                targetRect = passRect,
                clearFlags = RenderPassClear.Depth | RenderPassClear.Color, // copied from camera
                clearRGBA = 0xff00ffff,
                clearDepth = 1.0f,
                clearStencil = 0,
                shadowMask = ulong.MaxValue,
                cameraMask = cameraMask
            });
            SetPassComponents(ePassClear, eCam, true);

            Entity ePassOpaque = EntityManager.CreateEntity();
            EntityManager.AddComponentData(ePassOpaque, new RenderPass {
                inNode = eNode,
                sorting = RenderPassSort.Unsorted,
                projectionTransform = float4x4.identity,
                viewTransform = float4x4.identity,
                passType = RenderPassType.Opaque,
                viewId = 0xffff,
                scissor = new RenderPassRect(),
                viewport = passRect,
                targetRect = passRect,
                clearFlags = 0,
                clearDepth = 1.0f,
                clearStencil = 0,
                shadowMask = ulong.MaxValue,
                cameraMask = cameraMask
            });
            SetPassComponents(ePassOpaque, eCam);

            Entity ePassSprites = EntityManager.CreateEntity();
            EntityManager.AddComponentData(ePassSprites, new RenderPass {
                inNode = eNode,
                sorting = RenderPassSort.SortZGreater,
                projectionTransform = float4x4.identity,
                viewTransform = float4x4.identity,
                passType = RenderPassType.Sprites,
                viewId = 0xffff,
                scissor = new RenderPassRect(),
                viewport = passRect,
                targetRect = passRect,
                clearFlags = 0,
                clearDepth = 1.0f,
                clearStencil = 0,
                shadowMask = ulong.MaxValue,
                cameraMask = cameraMask
            });
            SetPassComponents(ePassSprites, eCam);
            EntityManager.AddBuffer<SortSpritesEntry>(ePassSprites);

            Entity ePassUI = EntityManager.CreateEntity();
            EntityManager.AddComponentData(ePassUI, new RenderPass {
                inNode = eNode,
                sorting = RenderPassSort.Sorted,
                projectionTransform = float4x4.identity,
                viewTransform = float4x4.identity,
                passType = RenderPassType.UI,
                viewId = 0xffff,
                scissor = new RenderPassRect(),
                viewport = new RenderPassRect { x = 0, y = 0, w = (ushort)w, h = (ushort)h },
                targetRect = passRect,
                clearFlags = 0,
                clearDepth = 1.0f,
                clearStencil = 0,
                shadowMask = ulong.MaxValue,
                cameraMask = cameraMask
            });
            SetPassComponents(ePassUI, eCam);

            Entity ePassTransparent = EntityManager.CreateEntity();
            EntityManager.AddComponentData(ePassTransparent, new RenderPass {
                inNode = eNode,
                sorting = RenderPassSort.SortZLess,
                projectionTransform = float4x4.identity,
                viewTransform = float4x4.identity,
                passType = RenderPassType.Transparent,
                viewId = 0xffff,
                scissor = new RenderPassRect(),
                viewport = passRect,
                targetRect = passRect,
                clearFlags = 0,
                clearDepth = 1.0f,
                clearStencil = 0,
                shadowMask = ulong.MaxValue,
                cameraMask = cameraMask
            });
            SetPassComponents(ePassTransparent, eCam);

            // add passes to node, in order
            DynamicBuffer<RenderPassRef> passRefs = EntityManager.GetBuffer<RenderPassRef>(eNode);
            passRefs.Add(new RenderPassRef { e = ePassClear });
            passRefs.Add(new RenderPassRef { e = ePassOpaque });
            passRefs.Add(new RenderPassRef { e = ePassSprites });
            passRefs.Add(new RenderPassRef { e = ePassUI });
            passRefs.Add(new RenderPassRef { e = ePassTransparent });
        }

        void AddRenderToShadowMapForNode(Entity eNode, int size)
        {
            Entity eTexDepth = EntityManager.CreateEntity();
            EntityManager.AddComponentData(eTexDepth, new Image2DRenderToTexture { format = RenderToTextureFormat.ShadowMap });
            EntityManager.AddComponentData(eTexDepth, new Image2D {
                imagePixelWidth = size,
                imagePixelHeight = size,
                status = ImageStatus.Loaded,
                flags = TextureFlags.UVClamp | TextureFlags.Nearest
            });
            Entity eTexColor = Entity.Null;
#if SAFARI_WEBGL_WORKAROUND
            // Safari webgl can not render to depth only.
            // need to investigate more if this is caused by emscripten, bgfx, or Safari.
            // for now, this workaround does the job altough we are wasting a bunch of memory
            eTexColor = EntityManager.CreateEntity();
            EntityManager.AddComponentData(eTexColor, new Image2DRenderToTexture { format = RenderToTextureFormat.RGBA });
            EntityManager.AddComponentData(eTexColor, new Image2D {
                imagePixelWidth = size,
                imagePixelHeight = size,
                status = ImageStatus.Loaded,
                flags = TextureFlags.UVClamp | TextureFlags.Nearest
            });
#if false // enable to debug shadow maps
            EntityManager.AddComponentData(eTexColor, new GizmoDebugOverlayTexture
            {
                color = new float4(1),
                pos = new float2(-.7f, -.7f),
                size = new float2(.25f, .25f)
            });
#endif
#endif
            EntityManager.AddComponentData(eNode, new RenderNodeTexture {
                colorTexture = eTexColor,
                depthTexture = eTexDepth,
                rect = new RenderPassRect { x = 0, y = 0, w = (ushort)size, h = (ushort)size }
            });
        }

        void AddRenderToTextureForNode(Entity eNode, int w, int h, bool color, bool depth)
        {
            Assert.IsTrue(eNode != Entity.Null && w > 0 && h > 0);
            Entity eTex = Entity.Null;
#if UNITY_DOTSRUNTIME
            if (color) {
                var di = GetSingleton<DisplayInfo>();
                eTex = EntityManager.CreateEntity();
                EntityManager.AddComponentData(eTex, new Image2DRenderToTexture { format = RenderToTextureFormat.RGBA });
                TextureFlags tf = TextureFlags.Linear | TextureFlags.UVClamp;
                if (di.colorSpace == ColorSpace.Linear)
                    tf |= TextureFlags.Srgb;
                EntityManager.AddComponentData(eTex, new Image2D {
                    imagePixelWidth = w,
                    imagePixelHeight = h,
                    status = ImageStatus.Loaded,
                    flags = tf
                });
            }
#endif
            Entity eTexDepth = Entity.Null;
            if (depth) {
                eTexDepth = EntityManager.CreateEntity();
                EntityManager.AddComponentData(eTexDepth, new Image2DRenderToTexture { format = RenderToTextureFormat.DepthStencil });
                EntityManager.AddComponentData(eTexDepth, new Image2D {
                    imagePixelWidth = w,
                    imagePixelHeight = h,
                    status = ImageStatus.Loaded,
                    flags = TextureFlags.Linear | TextureFlags.UVClamp
                });
            }
            EntityManager.AddComponentData(eNode, new RenderNodeTexture {
                colorTexture = eTex,
                depthTexture = eTexDepth,
                rect = new RenderPassRect { x = 0, y = 0, w = (ushort)w, h = (ushort)h }
            });
        }

        void LinkNodes(Entity eThisNode, Entity eDependsOnThis)
        {
            if (!EntityManager.HasComponent<RenderNodeRef>(eThisNode))
                EntityManager.AddBuffer<RenderNodeRef>(eThisNode);
            DynamicBuffer<RenderNodeRef> nodeRefs = EntityManager.GetBuffer<RenderNodeRef>(eThisNode);
            nodeRefs.Add(new RenderNodeRef { e = eDependsOnThis });
        }

        Entity FindPassOnNode(Entity node, RenderPassType pt)
        {
            var passes = EntityManager.GetBuffer<RenderPassRef>(node);
            for (int i = 0; i < passes.Length; i++) {
                var p = EntityManager.GetComponentData<RenderPass>(passes[i].e);
                if (p.passType == pt)
                    return passes[i].e;
            }
            return Entity.Null;
        }

        Entity CreateNodeEntity()
        {
            var e = EntityManager.CreateEntity();
            EntityManager.AddComponent<RenderNode>(e);
            EntityManager.AddBuffer<RenderNodeRef>(e);
            EntityManager.AddBuffer<RenderPassRef>(e);
            return e;
        }

        public static void ComputeAutoScaleSize(int targetW, int targetH, int maxSize, out int bufferW, out int bufferH)
        {
            Assert.IsTrue(maxSize > 0);
            if (targetW <= maxSize && targetH <= maxSize) {
                bufferW = targetW;
                bufferH = targetH;
            } else {
                float scale;
                if (targetW >= targetH)
                    scale = (float)maxSize / (float)targetW;
                else
                    scale = (float)maxSize / (float)targetH;
                bufferW = (int)(targetW * scale);
                bufferH = (int)(targetH * scale);
                Assert.IsTrue(bufferW <= maxSize && bufferH <= maxSize);
            }
            if ( bufferW < 1 ) bufferW = 1;
            if ( bufferH < 1 ) bufferH = 1;
        }

        unsafe static bool IsRectFullyCovered(Rect* coveringList, int n, in Rect r)
        {
            if (n <= 0)
                return false;
            // partial cover - remove the partially covered part of r, and check again with the remaining rects
            unsafe {
                Rect* rects = stackalloc Rect[4];
                int nRects = coveringList[0].RemoveFrom(r, rects);
                for (int j = 0; j < nRects; j++)
                    if (!IsRectFullyCovered(coveringList + 1, n - 1, rects[j])) return false;
                return true;
            }
        }

#if DEBUG
        int WarningCheckCameras(NativeList<SortedCamera> cameras)
        {
            NativeList<Rect> coveredArea = new NativeList<Rect>(Allocator.TempJob);
            int n = 0;
            for (int i = cameras.Length - 1; i >= 0; i--) {
                Camera c = EntityManager.GetComponentData<Camera>(cameras[i].e);
                if ((c.clearFlags & CameraClearFlags.SolidColor) != 0) {
                    if (coveredArea.IsEmpty) {
                        coveredArea.Add(c.viewportRect);
                    } else if (IsRectFullyCovered((Rect*)coveredArea.GetUnsafeReadOnlyPtr(), coveredArea.Length, c.viewportRect)) {
                        RenderDebug.LogFormat("The camera entity {0} is fully overwritten by subsequent cameras. It will still render but this is wasting a lot of time for no visible effect.", cameras[i].e);
                        n++;
                    }
                }
            }
            coveredArea.Dispose();
            if (n != 0)
                return n;
            for (int i = 0; i < cameras.Length - 1; i++) {
                if (cameras[i].depth == cameras[i + 1].depth) {
                    RenderDebug.LogFormat("The camera entity {0} and {1} have the same depth value {3}. Sorting will not be consistent.",
                        cameras[i].e, cameras[i + 1].e, cameras[i].depth);
                    return -1;
                }
            }
            return 0;
        }
#endif

        bool DidCamerasChange(NativeList<SortedCamera> newCameras)
        {
            if (newCameras.Length != activeCameras.Length)
                return true;
            for (int i = 0; i < activeCameras.Length; i++) {
                if (newCameras[i].e != activeCameras[i].e)
                    return true;
                if (newCameras[i].mask != activeCameras[i].mask)
                    return true;
            }
            return false;
        }

        Entity BuildRenderGraph()
        {
            Assert.IsTrue(eMainViewNode == Entity.Null);
            Assert.IsTrue((int)currentConfig.Mode != 0);
            int w, h;
            if (currentConfig.Mode != RenderGraphMode.DirectToFrontBuffer) {
                eMainViewNode = CreateNodeEntity();
                EntityManager.AddComponent<MainViewNodeTag>(eMainViewNode);
                // add a target texture for main view
                if (currentConfig.Mode == RenderGraphMode.FixedRenderBuffer) {
                    w = currentConfig.RenderBufferWidth;
                    h = currentConfig.RenderBufferHeight;
                } else {
                    var di = GetSingleton<DisplayInfo>();
                    ComputeAutoScaleSize(di.framebufferWidth, di.framebufferHeight, currentConfig.RenderBufferMaxSize, out w, out h);
                    EntityManager.AddComponentData(eMainViewNode, new RenderNodeAutoScaleToDisplay {
                        MaxSize = currentConfig.RenderBufferMaxSize
                    });
                }
                AddRenderToTextureForNode(eMainViewNode, w, h, true, true);
                // blit the main view node
                eFrontBufferNode = CreateFrontBufferRenderNode(0, 0); // size does not matter, as it needs to auto update size from display 
                LinkNodes(eFrontBufferNode, eMainViewNode);
                // build a blit renderer
                AddBlitter(eMainViewNode, eFrontBufferNode);
            } else {
                // render direct to front buffer - this will break srgb rendering in browsers!
                var di = GetSingleton<DisplayInfo>();
                if (di.colorSpace != ColorSpace.Gamma)
                    Debug.LogAlways("Warning, using direct to frame buffer rendering with linear color space. This will not look the same on all platforms.");
                w = di.framebufferWidth;
                h = di.framebufferHeight;
                eMainViewNode = CreateFrontBufferRenderNode(w, h); // size does not matter, as it needs to auto update size from display 
                EntityManager.AddComponent<MainViewNodeTag>(eMainViewNode);
                eFrontBufferNode = eMainViewNode;
            }

            // init aspect and node for auto aspect cameras
            Entities.WithoutBurst().WithAll<CameraAutoAspectFromNode>().ForEach((Entity e, ref Camera cam) => {
                EntityManager.SetComponentData(e, new CameraAutoAspectFromNode {
                    Node = eMainViewNode
                });
                cam.aspect = (float)w / (float)h;
            }).Run();
            return eMainViewNode;
        }

        bool GetNewCameras()
        {
            // gather & sort camera
            NativeList<SortedCamera> cameras = new NativeList<SortedCamera>(Allocator.TempJob);
            Entities.WithoutBurst().WithNone<DisableRendering>().ForEach((Entity e, ref Camera c) => {
                ulong mask = ulong.MaxValue;
                if (EntityManager.HasComponent<CameraMask>(e)) {
                    mask = EntityManager.GetComponentData<CameraMask>(e).mask;
                    if (mask == 0)
                        return;
                }
                cameras.Add(new SortedCamera { depth = c.depth, e = e, mask = mask });
            }).Run();
            cameras.Sort();

            if (DidCamerasChange(cameras)) {
                #if DEBUG
                // check for cameras that are not visibly rendering
                WarningCheckCameras(cameras);
                #endif
                activeCameras.CopyFrom(cameras);
                cameras.Dispose();
                return true;
            }

            cameras.Dispose();
            return false;
        }


        Entity CreateScreenToWorldChain(RenderPassType rpt, ScreenToWorldId id)
        {
            Entity eBase = EntityManager.CreateEntity();
            if (eMainViewNode != eFrontBufferNode) {
                EntityManager.AddComponentData<ScreenToWorldRoot>(eBase, new ScreenToWorldRoot {
                    pass = FindPassOnNode(eFrontBufferNode, RenderPassType.FullscreenQuad),
                    id = id
                });
                var buf = EntityManager.AddBuffer<ScreenToWorldPassList>(eBase);
                buf.Add(new ScreenToWorldPassList {
                    pass = FindPassOnNode(eMainViewNode, rpt),
                });
            } else {
                EntityManager.AddComponentData<ScreenToWorldRoot>(eBase, new ScreenToWorldRoot {
                    pass = FindPassOnNode(eFrontBufferNode, rpt),
                    id = id
                });
            }
            return eBase;
        }

        Entity FindNodeColorOutput(Entity eNode)
        {
            if (!EntityManager.HasComponent<RenderNodeTexture>(eNode))
                return Entity.Null;
            var rnt = EntityManager.GetComponentData<RenderNodeTexture>(eNode);
            return rnt.colorTexture;
        }

        void AddBlitter(Entity eSourceNode, Entity eTargetNode)
        {
            // blitter
            Entity eBlitter = EntityManager.CreateEntity();
            EntityManager.AddComponentData(eBlitter, new BlitRenderer {
                texture = FindNodeColorOutput(eSourceNode),
                color = new float4(1)
            });

            Entity ePass = FindPassOnNode(eTargetNode, RenderPassType.FullscreenQuad);
            Entity eToBlitPasses = EntityManager.CreateEntity();
            var bToPasses = EntityManager.AddBuffer<RenderToPassesEntry>(eToBlitPasses);
            bToPasses.Add(new RenderToPassesEntry { e = ePass });
            EntityManager.AddSharedComponentData(eBlitter, new RenderToPasses { e = eToBlitPasses });
            EntityManager.AddComponentData(ePass, new RenderPassUpdateFromBlitterAutoAspect { blitRenderer = eBlitter });
        }

        void AddShadowMapPass(Entity eNode, Entity ePass, Entity eLight, int cascade, int res)
        {
            int dx = 0;
            int dy = 0;
            uint cc = 0x0000ffff; // blue
            var targetRect = new RenderPassRect { x = 0, y = 0, w = (ushort)res, h = (ushort)res };
            if (cascade >= 0) {
                Assert.IsTrue(EntityManager.HasComponent<CascadeShadowmappedLight>(eLight));
                res >>= 1;
                switch (cascade) {
                    // index must match cascade select in shader
                    case 0: cc = 0xff0000ff; break; // red
                    case 1: cc = 0xff7f00ff; dy = res; break; // orange
                    case 2: cc = 0xffff00ff; dx = res; break; // yellow
                    case 3: cc = 0x00ff00ff; dx = res; dy = res; break; // green
                }
            }
            var passRect =
            EntityManager.AddComponentData(ePass, new RenderPass {
                inNode = eNode,
                sorting = RenderPassSort.Unsorted,
                projectionTransform = float4x4.identity,
                viewTransform = float4x4.identity,
                passType = RenderPassType.ShadowMap,
                viewId = 0xffff,
                scissor = new RenderPassRect(),
                viewport = new RenderPassRect { x = (ushort)dx, y = (ushort)dy, w = (ushort)res, h = (ushort)res },
                targetRect = targetRect,
#if SAFARI_WEBGL_WORKAROUND
                clearFlags = RenderPassClear.Depth | RenderPassClear.Color,
#else
                clearFlags = RenderPassClear.Depth,
#endif
                clearRGBA = cc,
                clearDepth = 1,
                clearStencil = 0,
                shadowMask = ulong.MaxValue,
                cameraMask = ulong.MaxValue
            });
            if (cascade >= 0) {
                EntityManager.AddComponentData(ePass, new RenderPassCascade {
                    cascade = cascade
                });
                EntityManager.AddComponentData(ePass, new RenderPassUpdateFromCascade {
                    light = eLight,
                    cascade = cascade
                });
            } else {
                EntityManager.AddComponentData(ePass, new RenderPassUpdateFromLight {
                    light = eLight
                });
            }
            EntityManager.GetBuffer<RenderPassRef>(eNode).Add(new RenderPassRef { e = ePass });
        }

        void BuildAllLightNodes(Entity eNodeOutput)
        {
            Assert.IsTrue(EntityManager.HasComponent<RenderNode>(eNodeOutput));
            // go through all lights and create nodes
            Entities.WithoutBurst().WithStructuralChanges().ForEach((Entity eLight, ref Light l, ref ShadowmappedLight sl) => {
                if (sl.shadowMapRenderNode == Entity.Null) {
                    // need a node
                    Entity eNode = CreateNodeEntity();
                    sl.shadowMapRenderNode = eNode;
                    EntityManager.AddComponent<RenderNode>(eNode);

                    if (EntityManager.HasComponent<CascadeShadowmappedLight>(eLight)) {
                        // need four passes in node
                        for (int i = 0; i < 4; i++) {
                            Entity ePass = EntityManager.CreateEntity();
                            AddShadowMapPass(eNode, ePass, eLight, i, sl.shadowMapResolution);
                        }
                    } else {
                        Entity ePass = eNode; //why not stick everything on the same entity
                        AddShadowMapPass(eNode, ePass, eLight, -1, sl.shadowMapResolution);
                    }
                    EntityManager.AddComponentData(eNode, new RenderNodeShadowMap {
                        lightsource = eLight
                    });
                    LinkNodes(eNodeOutput, eNode);
                    RenderDebug.LogFormat("Build shadow map node {0}*{0}, input to {1}", sl.shadowMapResolution, eNodeOutput);
                }
                // allocate the texture if needed
                if (sl.shadowMap == Entity.Null) {
                    AddRenderToShadowMapForNode(sl.shadowMapRenderNode, sl.shadowMapResolution);
                    var rtt = EntityManager.GetComponentData<RenderNodeTexture>(sl.shadowMapRenderNode);
                    sl.shadowMap = rtt.depthTexture;
                    Assert.IsTrue(sl.shadowMap != Entity.Null);
                }
            }).Run();
        }

        void DestroyRenderGraph()
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
            // get rid of all the textures
            Entities.ForEach((ref RenderNodeTexture rnt) => {
                if (rnt.colorTexture != Entity.Null)
                    ecb.DestroyEntity(rnt.colorTexture);
                if (rnt.depthTexture != Entity.Null)
                    ecb.DestroyEntity(rnt.depthTexture);
            }).Run();
            // remove shadow map references 
            Entities.ForEach((ref ShadowmappedLight sml) => {
                sml.shadowMap = Entity.Null;
                sml.shadowMapRenderNode = Entity.Null;
            }).Run();
            // get rid of all the passes
            Entities.WithAll<RenderPass>().ForEach((Entity e) => {
                ecb.DestroyEntity(e);
            }).Run();
            // get rid of all the nodes
            Entities.WithAll<RenderNode>().ForEach((Entity e) => {
                ecb.DestroyEntity(e);
            }).Run();
            // remove picking 
            Entities.WithAll<ScreenToWorldRoot>().ForEach((Entity e) => {
                ecb.DestroyEntity(e);
            }).Run();
            ecb.Playback(EntityManager);
            ecb.Dispose();
            // invalidate render groups
            ecb = new EntityCommandBuffer(Allocator.TempJob);
            // remove all render group assignments
            Entities.WithoutBurst().WithAll<RenderToPasses>().ForEach((Entity e) => {
                ecb.RemoveComponent<RenderToPasses>(e);
            }).Run();
            // remove all groups
            Entities.WithAll<RenderGroup>().ForEach((Entity e) => {
                ecb.DestroyEntity(e);
            }).Run();
            ecb.Playback(EntityManager);
            ecb.Dispose();
            // need to tell the assignrendergroups system, as it caches entities outside ecs 
            World.GetExistingSystem<AssignRenderGroups>().InvalidateAllGroups();
            eFrontBufferNode = Entity.Null;
            eMainViewNode = Entity.Null;
        }

        // Helper for other systems to force a full rebuild of the render graph
        // for whatever reason. This can be used after scene load/unload to reset the graph for now.
        // This function will go away eventually. 
        public void ForceRebuildRenderGraph()
        {
            UpdateRenderGraph(true);
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            activeCameras = new NativeList<SortedCamera>(Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            activeCameras.Dispose();
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
#if !UNITY_DOTSRUNTIME
            //Do not run this system in any other context than dots runtime
            return;
#else
            UpdateRenderGraph(false);
#endif
        }

        void UpdateRenderGraph(bool forceRebuild)
        {
            Dependency.Complete();
#if RENDERING_FORCE_DIRECT
            Assert.IsTrue(false, "Obsolete script define RENDERING_FORCE_DIRECT enabled. Use the RenderGraphConfig singleton to configure a render graph");
#endif
            if (!HasSingleton<RenderGraphConfig>()) {
                Entity eConfig = GetSingletonEntity<DisplayInfo>();
                EntityManager.AddComponentData<RenderGraphConfig>(eConfig, RenderGraphConfig.Default);
            }

            RenderGraphConfig config = GetSingleton<RenderGraphConfig>();
            bool needRebuild = false;

            if (forceRebuild) {
                RenderDebug.LogAlways("Forced render graph rebuild!");
                needRebuild = true;
            }

            if (!config.Equals(currentConfig) || eMainViewNode == Entity.Null) {
                RenderDebug.LogAlways("RenderGraphConfig changed, building a new render graph!");
                needRebuild = true;
            }

            // if new cameras were created, need to add them
            if (GetNewCameras()) {
                RenderDebug.LogAlways("Cameras list changed, building a new render graph!");
                needRebuild = true;
            }

            if (needRebuild) {
                DestroyRenderGraph();
                // we should run the bgfx system here once, so textures are cleaned up and ready for re-use
                currentConfig = config;
                // we only build a default graph if there are no existing nodes - otherwise assume they are already built
                eMainViewNode = BuildRenderGraph();
                // every camera needs passes
                for (int i = 0; i < activeCameras.Length; i++)
                    CreateAllCameraPasses(4, 4, activeCameras[i].e, eMainViewNode); // w,h ???
                // create/remove nodes and passes for light map rendering
                BuildAllLightNodes(eMainViewNode);

                CreateScreenToWorldChain(RenderPassType.Opaque, ScreenToWorldId.MainCamera);
                CreateScreenToWorldChain(RenderPassType.Sprites, ScreenToWorldId.Sprites);
                CreateScreenToWorldChain(RenderPassType.UI, ScreenToWorldId.UILayer);
            }
        }

        Entity eMainViewNode;
        Entity eFrontBufferNode;
        RenderGraphConfig currentConfig;
        NativeList<SortedCamera> activeCameras;
    }

    public struct BuildGroup : IComponentData, IEquatable<BuildGroup>
    {
        public RenderPassType passTypes;
        public CameraMask cameraMask;
        public ShadowMask shadowMask;

        override public int GetHashCode()
        {
            return (int)passTypes +
                (int)cameraMask.mask + (int)(cameraMask.mask >> 32) +
                (int)shadowMask.mask + (int)(shadowMask.mask >> 32);
        }

        public bool Equals(BuildGroup other)
        {
            return passTypes == other.passTypes &&
                cameraMask.mask == other.cameraMask.mask &&
                shadowMask.mask == other.shadowMask.mask;
        }
    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(RenderGraphBuilder))]
    public unsafe class AssignRenderGroups : SystemBase
    {
        NativeHashMap<BuildGroup, Entity> m_buildGroups;

        public void InvalidateAllGroups()
        {
            m_buildGroups.Clear();
        }

        internal Entity FindOrCreateRenderGroup(BuildGroup key)
        {
            Entity e = Entity.Null;
            if (m_buildGroups.TryGetValue(key, out e)) {
                return e;
            }

            // gather all passes
            // This whole thing is temporary; this perf is not great but only happens at startup
            var q = GetEntityQuery(ComponentType.ReadOnly<RenderPass>());
            using (var allPasses = q.ToEntityArray(Allocator.TempJob)) {
                e = EntityManager.CreateEntity();
                EntityManager.AddComponent<RenderGroup>(e);
                EntityManager.AddComponentData<BuildGroup>(e, key);
                var groupTargetPasses = EntityManager.AddBuffer<RenderToPassesEntry>(e);
                m_buildGroups.TryAdd(key, e);
                for (int i = 0; i < allPasses.Length; i++) {
                    var ePass = allPasses[i];
                    var pass = EntityManager.GetComponentData<RenderPass>(ePass);
                    if (((uint)pass.passType & (uint)key.passTypes) == 0)
                        continue;
                    if ( (pass.shadowMask & key.shadowMask.mask) == 0 ||
                         (pass.cameraMask & key.cameraMask.mask) == 0 )
                        continue;
                    groupTargetPasses.Add(new RenderToPassesEntry { e = ePass });
                }
            }

            return e;
        }

        public void AddItemToRenderGroup(Entity item, BuildGroup key)
        {
            var group = FindOrCreateRenderGroup(key);
            OptionalSetSharedComponent(item, new RenderToPasses { e = group });
        }

        protected internal void OptionalSetSharedComponent<T>(Entity e, T value) where T : unmanaged, ISharedComponentData
        {
            if (EntityManager.HasComponent<T>(e)) {
                T oldValue = EntityManager.GetSharedComponentData<T>(e);
                if (UnsafeUtility.MemCmp(&oldValue, &value, sizeof(T)) != 0)
                    EntityManager.SetSharedComponentData<T>(e, value);
            } else {
                EntityManager.AddSharedComponentData<T>(e, value);
            }
        }

        private void FinalizePass(Entity e, bool isTransparent)
        {
            CameraMask cameraMask = new CameraMask { mask = ulong.MaxValue };
            if (EntityManager.HasComponent<CameraMask>(e))
                cameraMask = EntityManager.GetComponentData<CameraMask>(e);
            ShadowMask shadowMask = new ShadowMask { mask = ulong.MaxValue };
            if (EntityManager.HasComponent<ShadowMask>(e))
                shadowMask = EntityManager.GetComponentData<ShadowMask>(e);
            Entity eGroup;
            if (isTransparent)
                eGroup = FindOrCreateRenderGroup(new BuildGroup { passTypes = RenderPassType.Transparent, cameraMask = cameraMask, shadowMask = shadowMask });
            else
                eGroup = FindOrCreateRenderGroup(new BuildGroup { passTypes = RenderPassType.Opaque | RenderPassType.ZOnly | RenderPassType.ShadowMap, cameraMask = cameraMask, shadowMask = shadowMask });
            OptionalSetSharedComponent(e, new RenderToPasses { e = eGroup });
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            m_buildGroups = new NativeHashMap<BuildGroup, Entity>(16, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            m_buildGroups.Dispose();
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            // go through all known render types, and assign groups to them
            // assign groups to all renderers
            Entities.WithAny<SimpleMeshRenderer, SimpleParticleRenderer>().WithoutBurst().WithStructuralChanges().WithNone<RenderToPasses>().ForEach((Entity e, ref MeshRenderer rlmr) => {
                bool isTransparent = false;
                if (EntityManager.HasComponent<SimpleMaterial>(rlmr.material))
                    isTransparent = EntityManager.GetComponentData<SimpleMaterial>(rlmr.material).transparent;
                FinalizePass(e, isTransparent);
            }).Run();

            Entities.WithAny<LitMeshRenderer, LitParticleRenderer>().WithoutBurst().WithStructuralChanges()
                .WithNone<RenderToPasses>().ForEach((Entity e, ref MeshRenderer rlmr) => {
                    bool isTransparent = false;
                    if (EntityManager.HasComponent<LitMaterial>(rlmr.material))
                        isTransparent = EntityManager.GetComponentData<LitMaterial>(rlmr.material).transparent;
                    FinalizePass(e, isTransparent);
                }).Run();

            Entities.WithAny<SimpleMeshRenderer>().WithoutBurst().WithStructuralChanges().WithNone<RenderToPasses>().ForEach((Entity e, ref SkinnedMeshRenderer rlsmr) => {
                bool isTransparent = false;
                if (EntityManager.HasComponent<SimpleMaterial>(rlsmr.material))
                    isTransparent = EntityManager.GetComponentData<SimpleMaterial>(rlsmr.material).transparent;
                FinalizePass(e, isTransparent);
            }).Run();

            Entities.WithAny<LitMeshRenderer>().WithoutBurst().WithStructuralChanges().WithNone<RenderToPasses>().ForEach((Entity e, ref SkinnedMeshRenderer rlsmr) => {
                bool isTransparent = false;
                if (EntityManager.HasComponent<LitMaterial>(rlsmr.material))
                    isTransparent = EntityManager.GetComponentData<LitMaterial>(rlsmr.material).transparent;
                FinalizePass(e, isTransparent);
            }).Run();

            // those are things that do not render anywhere naturally, so add a to passes for gizmos
            // TODO add a GizmoRenderer tag for these
            Entities.WithoutBurst().WithStructuralChanges().WithNone<RenderToPasses>().WithAny<GizmoLight, GizmoCamera, GizmoAutoMovingDirectionalLight>().ForEach((Entity e) => {
                ShadowMask shadowMask = new ShadowMask { mask = ulong.MaxValue };
                CameraMask cameraMask = new CameraMask { mask = 0 };
                Entity eGroup = FindOrCreateRenderGroup(new BuildGroup { passTypes = RenderPassType.Transparent, cameraMask = cameraMask, shadowMask = shadowMask });
                OptionalSetSharedComponent(e, new RenderToPasses { e = eGroup });
            }).Run();

            Entities.WithoutBurst().WithStructuralChanges().WithNone<RenderToPasses>().WithAny<GizmoDebugOverlayTexture>().ForEach((Entity e) => {
                ShadowMask shadowMask = new ShadowMask { mask = ulong.MaxValue };
                CameraMask cameraMask = new CameraMask { mask = 0 };
                Entity eGroup = FindOrCreateRenderGroup(new BuildGroup { passTypes = RenderPassType.DebugOverlay, cameraMask = cameraMask, shadowMask = shadowMask });
                OptionalSetSharedComponent(e, new RenderToPasses { e = eGroup });
            }).Run();
        }
    }
}
