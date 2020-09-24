using System;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Tiny;
using Unity.Tiny.Assertions;
using Unity.Tiny.Rendering;
using Bgfx;
using System.Runtime.InteropServices;
using Unity.Transforms;
using Unity.Jobs;
using Unity.Burst;

namespace Unity.Tiny.Rendering
{
    [UpdateInGroup(typeof(SubmitSystemGroup))]
    public unsafe class SubmitBlitters : SystemBase
    {
        protected override void OnUpdate()
        {
            var sys = World.GetExistingSystem<RendererBGFXSystem>().InstancePointer();
            if (!sys->m_initialized)
                return;
            Dependency.Complete();

            Entities.WithoutBurst().ForEach((Entity e, ref BlitRenderer br) =>
            {
                if (!EntityManager.HasComponent<RenderToPasses>(e))
                    return;
                if (!EntityManager.HasComponent<TextureBGFX>(br.texture))
                    return;
                RenderToPasses toPassesRef = EntityManager.GetSharedComponentData<RenderToPasses>(e);
                DynamicBuffer<RenderToPassesEntry> toPasses = EntityManager.GetBufferRO<RenderToPassesEntry>(toPassesRef.e);
                var tex = EntityManager.GetComponentData<TextureBGFX>(br.texture);
                float4x4 idm = float4x4.identity;
                for (int i = 0; i < toPasses.Length; i++)
                {
                    Entity ePass = toPasses[i].e;
                    var pass = EntityManager.GetComponentData<RenderPass>(ePass);
                    if (sys->m_blitPrimarySRGB)
                    {
                        // need to convert linear to srgb if we are not rendering to a texture in linear workflow
                        bool toPrimaryWithSRGB = EntityManager.HasComponent<RenderNodePrimarySurface>(pass.inNode) && sys->m_allowSRGBTextures;
                        if (!toPrimaryWithSRGB)
                            SubmitHelper.SubmitBlitDirectFast(sys, pass.viewId, ref idm, br.color, tex.handle);
                        else
                            SubmitHelper.SubmitBlitDirectExtended(sys, pass.viewId,  ref idm, tex.handle,
                                false, true, 0.0f, new float4(1.0f), new float4(0.0f), false);
                    }
                    else
                    {
                        SubmitHelper.SubmitBlitDirectFast(sys, pass.viewId, ref idm, br.color, tex.handle);
                    }
                }
            }).Run();
        }
    }

    [UpdateInGroup(typeof(SubmitSystemGroup))]
    public unsafe class SubmitSimpleMesh : SystemBase
    {
        protected override void OnUpdate()
        {
            Dependency.Complete();
            var sys = World.GetExistingSystem<RendererBGFXSystem>().InstancePointer();
            if (!sys->m_initialized)
                return;
            // get all MeshRenderer, cull them, and add them to graph nodes that need them
            // any mesh renderer MUST have a shared component data that has a list of passes to render to
            // this list is usually very shared - all opaque meshes will render to all ZOnly and Opaque passes
            // this shared data is not dynamically updated - other systems are responsible to update them if needed
            // simple
            Entities.WithAll<SimpleMeshRenderer>().WithoutBurst().ForEach((Entity e, ref MeshRenderer mr, ref LocalToWorld tx, in WorldBounds wb, in WorldBoundingSphere wbs) =>
            {
                if (!EntityManager.HasComponent<RenderToPasses>(e))
                    return;

                RenderToPasses toPassesRef = EntityManager.GetSharedComponentData<RenderToPasses>(e);
                DynamicBuffer<RenderToPassesEntry> toPasses = EntityManager.GetBufferRO<RenderToPassesEntry>(toPassesRef.e);
                for (int i = 0; i < toPasses.Length; i++)
                {
                    Entity ePass = toPasses[i].e;
                    var pass = EntityManager.GetComponentData<RenderPass>(ePass);
                    if (Culling.Cull(in wbs, in pass.frustum) == Culling.CullingResult.Outside)
                        continue;
                    // double cull as example only
                    if (Culling.IsCulled(in wb, in pass.frustum))
                        continue;
                    var mesh = EntityManager.GetComponentData<MeshBGFX>(mr.mesh);
                    uint depth = 0;
                    switch (pass.passType)
                    {
                        case RenderPassType.ZOnly:
                            SubmitHelper.SubmitZOnlyMeshDirect(sys, pass.viewId, ref mesh, ref tx.Value, mr.startIndex, mr.indexCount, pass.GetFlipCulling());
                            break;
                        case RenderPassType.ShadowMap:
                            SubmitHelper.SubmitZOnlyMeshDirect(sys, pass.viewId, ref mesh, ref tx.Value, mr.startIndex, mr.indexCount, pass.GetFlipCullingInverse());
                            break;
                        case RenderPassType.Transparent:
                            depth = pass.ComputeSortDepth(tx.Value.c3);
                            goto case RenderPassType.Opaque;
                        case RenderPassType.Opaque:
                            var material = EntityManager.GetComponentData<SimpleMaterialBGFX>(mr.material);
                            SubmitHelper.SubmitSimpleMeshDirect(sys, pass.viewId, ref mesh, ref tx.Value, ref material, mr.startIndex, mr.indexCount, pass.GetFlipCulling(), depth);
                            break;
                        default:
                            Assert.IsTrue(false);
                            break;
                    }
                }
            }).Run();
        }
    }

    [UpdateInGroup(typeof(SubmitSystemGroup))]
    public class SubmitStaticLitMeshChunked : SystemBase
    {
        unsafe struct SubmitStaticLitMeshJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<LocalToWorld> LocalToWorldType;
            [ReadOnly] public ComponentTypeHandle<MeshRenderer> MeshRendererType;
            [ReadOnly] public ComponentTypeHandle<WorldBounds> WorldBoundsType;
            [ReadOnly] public ComponentTypeHandle<WorldBoundingSphere> WorldBoundingSphereType;
            [ReadOnly] public ComponentTypeHandle<ChunkWorldBoundingSphere> ChunkWorldBoundingSphereType;
            [ReadOnly] public ComponentTypeHandle<ChunkWorldBounds> ChunkWorldBoundsType;
            [DeallocateOnJobCompletion][ReadOnly] public NativeArray<Entity> SharedRenderToPass;
            [DeallocateOnJobCompletion][ReadOnly] public NativeArray<Entity> SharedLightingRef;
            [ReadOnly] public BufferFromEntity<RenderToPassesEntry> BufferRenderToPassesEntry;
            [ReadOnly] public ComponentDataFromEntity<MeshBGFX> ComponentMeshBGFX;
            [ReadOnly] public ComponentDataFromEntity<RenderPass> ComponentRenderPass;
            [ReadOnly] public ComponentDataFromEntity<LitMaterialBGFX> ComponentLitMaterialBGFX;
            [ReadOnly] public ComponentDataFromEntity<LightingBGFX> ComponentLightingBGFX;
#pragma warning disable 0649
            [NativeSetThreadIndex] internal int ThreadIndex;
#pragma warning restore 0649
            [ReadOnly] public PerThreadDataBGFX* PerThreadData;
            [ReadOnly] public int MaxPerThreadData;
            [ReadOnly] public RendererBGFXInstance* BGFXInstancePtr;

            public unsafe void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var chunkLocalToWorld = chunk.GetNativeArray(LocalToWorldType);
                var chunkMeshRenderer = chunk.GetNativeArray(MeshRendererType);
                var worldBoundingSphere = chunk.GetNativeArray(WorldBoundingSphereType);
                var chunkWorldBoundingSphere = chunk.GetChunkComponentData(ChunkWorldBoundingSphereType).Value;
                var bounds = chunk.GetChunkComponentData(ChunkWorldBoundsType).Value;

                Assert.IsTrue(chunk.HasChunkComponent(ChunkWorldBoundingSphereType));

                Entity lighte = SharedLightingRef[chunkIndex];
                var lighting = ComponentLightingBGFX[lighte];
                Entity rtpe = SharedRenderToPass[chunkIndex];

                Assert.IsTrue(ThreadIndex >= 0 && ThreadIndex < MaxPerThreadData);
                bgfx.Encoder* encoder = PerThreadData[ThreadIndex].encoder;
                if (encoder == null)
                {
                    encoder = bgfx.encoder_begin(true);
                    Assert.IsTrue(encoder != null);
                    PerThreadData[ThreadIndex].encoder = encoder;
                }
                DynamicBuffer<RenderToPassesEntry> toPasses = BufferRenderToPassesEntry[rtpe];

                // we can do this loop either way, passes first or renderers first.
                // TODO: profile what is better!
                for (int i = 0; i < toPasses.Length; i++)   // for all passes this chunk renderer to
                {
                    Entity ePass = toPasses[i].e;
                    var pass = ComponentRenderPass[ePass];
                    Assert.IsTrue(encoder != null);
                    for (int j = 0; j < chunk.Count; j++)   // for every renderer in chunk
                    {
                        var wbs = worldBoundingSphere[j];
                        var tx = chunkLocalToWorld[j].Value;
                        if (wbs.radius > 0.0f && Culling.Cull(in wbs, in pass.frustum) == Culling.CullingResult.Outside) // TODO: fine cull only if rough culling was !Inside
                            continue;
                        var meshRenderer = chunkMeshRenderer[j];
                        if (meshRenderer.indexCount > 0 && ComponentMeshBGFX.HasComponent(meshRenderer.mesh))
                        {
                            var mesh = ComponentMeshBGFX[meshRenderer.mesh];
                            Assert.IsTrue(mesh.IsValid());
                            uint depth = 0;
                            switch (pass.passType)   // TODO: we can hoist this out of the loop
                            {
                                case RenderPassType.ZOnly:
                                    SubmitHelper.EncodeZOnlyMesh(BGFXInstancePtr, encoder, pass.viewId, ref mesh, ref tx, meshRenderer.startIndex, meshRenderer.indexCount, pass.GetFlipCulling());
                                    break;
                                case RenderPassType.ShadowMap:
                                    float4 bias = new float4(0);
                                    SubmitHelper.EncodeShadowMapMesh(BGFXInstancePtr, encoder, pass.viewId, ref mesh, ref tx, meshRenderer.startIndex, meshRenderer.indexCount, pass.GetFlipCullingInverse(), bias);
                                    break;
                                case RenderPassType.Transparent:
                                    depth = pass.ComputeSortDepth(tx.c3);
                                    goto case RenderPassType.Opaque;
                                case RenderPassType.Opaque:
                                    var material = ComponentLitMaterialBGFX[meshRenderer.material];
                                    SubmitHelper.EncodeLitMesh(BGFXInstancePtr, encoder, pass.viewId, ref mesh, ref tx, ref material, ref lighting, ref pass.viewTransform, meshRenderer.startIndex, meshRenderer.indexCount, pass.GetFlipCulling(), ref PerThreadData[ThreadIndex].viewSpaceLightCache, depth);
                                    break;
                                default:
                                    Assert.IsTrue(false);
                                    break;
                            }
                        }
                        else
                        {
                            //var mesh = ComponentDynamicMeshBGFX[meshRenderer.mesh];
                        }
                    }
                }
            }
        }

        EntityQuery m_query;

        protected override void OnCreate()
        {
            m_query = GetEntityQuery(
                ComponentType.ReadOnly<LitMeshRenderer>(),
                ComponentType.ReadOnly<MeshRenderer>(),
                ComponentType.ReadOnly<LocalToWorld>(),
                ComponentType.ReadOnly<WorldBounds>(),
                ComponentType.ReadOnly<WorldBoundingSphere>(),
                ComponentType.ChunkComponentReadOnly<ChunkWorldBoundingSphere>(),
                ComponentType.ChunkComponentReadOnly<ChunkWorldBounds>(),
                ComponentType.ReadOnly<RenderToPasses>()
            );
        }

        protected override void OnDestroy()
        {
        }

        protected unsafe override void OnUpdate()
        {
            var sys = World.GetExistingSystem<RendererBGFXSystem>().InstancePointer();
            if (!sys->m_initialized)
                return;

            var chunks = m_query.CreateArchetypeChunkArray(Allocator.TempJob);
            NativeArray<Entity> sharedRenderToPass = new NativeArray<Entity>(chunks.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<Entity> sharedLightingRef = new NativeArray<Entity>(chunks.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            SharedComponentTypeHandle<RenderToPasses> renderToPassesType = GetSharedComponentTypeHandle<RenderToPasses>();
            SharedComponentTypeHandle<LightingRef> lightingRefType = GetSharedComponentTypeHandle<LightingRef>();

            // it really sucks we can't get shared components in the job itself
            for (int i = 0; i < chunks.Length; i++)
            {
                sharedRenderToPass[i] = chunks[i].GetSharedComponentData<RenderToPasses>(renderToPassesType, EntityManager).e;
                sharedLightingRef[i] = chunks[i].GetSharedComponentData<LightingRef>(lightingRefType, EntityManager).e;
            }
            chunks.Dispose();

            var encodejob = new SubmitStaticLitMeshJob
            {
                LocalToWorldType = GetComponentTypeHandle<LocalToWorld>(true),
                MeshRendererType = GetComponentTypeHandle<MeshRenderer>(true),
                WorldBoundsType = GetComponentTypeHandle<WorldBounds>(true),
                WorldBoundingSphereType = GetComponentTypeHandle<WorldBoundingSphere>(true),
                ChunkWorldBoundingSphereType = GetComponentTypeHandle<ChunkWorldBoundingSphere>(true),
                ChunkWorldBoundsType = GetComponentTypeHandle<ChunkWorldBounds>(true),
                SharedRenderToPass = sharedRenderToPass,
                SharedLightingRef = sharedLightingRef,
                BufferRenderToPassesEntry = GetBufferFromEntity<RenderToPassesEntry>(true),
                ComponentMeshBGFX = GetComponentDataFromEntity<MeshBGFX>(true),
                ComponentRenderPass = GetComponentDataFromEntity<RenderPass>(true),
                ComponentLitMaterialBGFX = GetComponentDataFromEntity<LitMaterialBGFX>(true),
                ComponentLightingBGFX = GetComponentDataFromEntity<LightingBGFX>(true),
                PerThreadData = sys->m_perThreadData,
                MaxPerThreadData = sys->m_maxPerThreadData,
                BGFXInstancePtr = sys
            };
            Assert.IsTrue(sys->m_maxPerThreadData > 0 && encodejob.MaxPerThreadData > 0);

            Dependency = encodejob.ScheduleParallel(m_query, Dependency);
            // Temporary workaround until dependencies bugs are fixed.
            Dependency.Complete();
        }
    }

    [UpdateInGroup(typeof(SubmitSystemGroup))]
    public unsafe class SubmitSimpleParticles : SystemBase
    {
        protected override void OnUpdate()
        {
            Dependency.Complete();
            var sys = World.GetExistingSystem<RendererBGFXSystem>().InstancePointer();
            if (!sys->m_initialized)
                return;
            // get all MeshRenderer, cull them, and add them to graph nodes that need them
            // any mesh renderer MUST have a shared component data that has a list of passes to render to
            // this list is usually very shared - all opaque meshes will render to all ZOnly and Opaque passes
            // this shared data is not dynamically updated - other systems are responsible to update them if needed
            // simple
            Entities.WithAll<SimpleParticleRenderer>().WithoutBurst().ForEach((Entity e, ref MeshRenderer mr, ref LocalToWorld tx, in WorldBounds wb, in WorldBoundingSphere wbs) =>
            {
                if (!EntityManager.HasComponent<RenderToPasses>(e))
                    return;

                RenderToPasses toPassesRef = EntityManager.GetSharedComponentData<RenderToPasses>(e);
                DynamicBuffer<RenderToPassesEntry> toPasses = EntityManager.GetBufferRO<RenderToPassesEntry>(toPassesRef.e);
                DynamicBuffer<DynamicSimpleVertex> vBuffer = EntityManager.GetBufferRO<DynamicSimpleVertex>(mr.mesh);
                DynamicBuffer<DynamicIndex> iBuffer = EntityManager.GetBufferRO<DynamicIndex>(mr.mesh);
                int nindices = iBuffer.Length;
                int nvertices = vBuffer.Length;
                bgfx.TransientIndexBuffer tib;
                bgfx.TransientVertexBuffer tvb;
                if (!SubmitHelper.SubmitSimpleTransientAlloc(sys, &tib, &tvb, nvertices, nindices))
                    return;
                SimpleVertex* destVertices = (SimpleVertex*)tvb.data;
                ushort* destIndices = (ushort*)tib.data;
                UnsafeUtility.MemCpy(destIndices, iBuffer.GetUnsafeReadOnlyPtr(), nindices * 2);
                UnsafeUtility.MemCpy(destVertices, vBuffer.GetUnsafeReadOnlyPtr(), nvertices * sizeof(SimpleVertex));

                for (int i = 0; i < toPasses.Length; i++)
                {
                    Entity ePass = toPasses[i].e;
                    var pass = EntityManager.GetComponentData<RenderPass>(ePass);
                    if (Culling.Cull(in wbs, in pass.frustum) == Culling.CullingResult.Outside)
                        continue;
                    uint depth = 0;
                    switch (pass.passType)
                    {
                        case RenderPassType.ZOnly:
                            SubmitHelper.SubmitSimpleZOnlyTransientDirect(sys, &tib, &tvb, nvertices, nindices, pass.viewId, ref tx.Value, pass.GetFlipCulling());
                            break;
                        case RenderPassType.ShadowMap:
                            SubmitHelper.SubmitSimpleZOnlyTransientDirect(sys, &tib, &tvb, nvertices, nindices, pass.viewId, ref tx.Value, pass.GetFlipCullingInverse());
                            break;
                        case RenderPassType.Transparent:
                            depth = pass.ComputeSortDepth(new float4(wbs.position, 1.0f));
                            goto case RenderPassType.Opaque;
                        case RenderPassType.Opaque:
                            var material = EntityManager.GetComponentData<SimpleMaterialBGFX>(mr.material);
                            SubmitHelper.SubmitSimpleTransientDirect(sys, &tib, &tvb, nvertices, nindices, pass.viewId, ref tx.Value, ref material, pass.GetFlipCulling(), depth);
                            break;
                        default:
                            Assert.IsTrue(false);
                            break;
                    }
                }
            }).Run();
        }
    }

    [UpdateInGroup(typeof(SubmitSystemGroup))]
    public class SubmitStaticLitParticlesChunked : SystemBase
    {
        unsafe struct SubmitStaticLitParticleJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<LocalToWorld> LocalToWorldType;
            [ReadOnly] public ComponentTypeHandle<MeshRenderer> MeshRendererType;
            [ReadOnly] public ComponentTypeHandle<WorldBoundingSphere> WorldBoundingSphereType;
            [ReadOnly] public ComponentTypeHandle<ChunkWorldBoundingSphere> ChunkWorldBoundingSphereType;
            [ReadOnly] public BufferTypeHandle<DynamicLitVertex> DynamicLitVertexBufferType;
            [ReadOnly] public BufferTypeHandle<DynamicIndex> DynamicIndexBufferType;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Entity> SharedRenderToPass;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Entity> SharedLightingRef;
            [ReadOnly] public BufferFromEntity<RenderToPassesEntry> BufferRenderToPassesEntry;
            [ReadOnly] public ComponentDataFromEntity<RenderPass> ComponentRenderPass;
            [ReadOnly] public ComponentDataFromEntity<LitMaterialBGFX> ComponentLitMaterialBGFX;
            [ReadOnly] public ComponentDataFromEntity<LightingBGFX> ComponentLightingBGFX;
#pragma warning disable 0649
            [NativeSetThreadIndex] internal int ThreadIndex;
#pragma warning restore 0649
            [ReadOnly] public PerThreadDataBGFX* PerThreadData;
            [ReadOnly] public int MaxPerThreadData;
            [ReadOnly] public RendererBGFXInstance* BGFXInstancePtr;

            public unsafe void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var chunkLocalToWorld = chunk.GetNativeArray(LocalToWorldType);
                var chunkMeshRenderer = chunk.GetNativeArray(MeshRendererType);
                var worldBoundingSphere = chunk.GetNativeArray(WorldBoundingSphereType);

                Assert.IsTrue(chunk.HasChunkComponent(ChunkWorldBoundingSphereType));

                Entity lighte = SharedLightingRef[chunkIndex];
                var lighting = ComponentLightingBGFX[lighte];
                Entity rtpe = SharedRenderToPass[chunkIndex];

                Assert.IsTrue(ThreadIndex >= 0 && ThreadIndex < MaxPerThreadData);
                bgfx.Encoder* encoder = PerThreadData[ThreadIndex].encoder;
                if (encoder == null)
                {
                    encoder = bgfx.encoder_begin(true);
                    Assert.IsTrue(encoder != null);
                    PerThreadData[ThreadIndex].encoder = encoder;
                }
                DynamicBuffer<RenderToPassesEntry> toPasses = BufferRenderToPassesEntry[rtpe];

                for (int i = 0; i < chunk.Count; i++)   // for every renderer in chunk
                {
                    DynamicBuffer<DynamicLitVertex> vBuffer = chunk.GetBufferAccessor(DynamicLitVertexBufferType)[i];
                    DynamicBuffer<DynamicIndex> iBuffer = chunk.GetBufferAccessor(DynamicIndexBufferType)[i];
                    int nindices = iBuffer.Length;
                    int nvertices = vBuffer.Length;
                    bgfx.TransientIndexBuffer tib;
                    bgfx.TransientVertexBuffer tvb;
                    if (!SubmitHelper.SubmitLitTransientAlloc(BGFXInstancePtr, &tib, &tvb, nvertices, nindices))
                        return;
                    LitVertex* destVertices = (LitVertex*)tvb.data;
                    ushort* destIndices = (ushort*)tib.data;
                    UnsafeUtility.MemCpy(destIndices, iBuffer.GetUnsafeReadOnlyPtr(), nindices * 2);
                    UnsafeUtility.MemCpy(destVertices, vBuffer.GetUnsafeReadOnlyPtr(), nvertices * sizeof(LitVertex));

                    for (int j = 0; j < toPasses.Length; j++)   // for all passes this chunk renderer to
                    {
                        Entity ePass = toPasses[j].e;
                        var pass = ComponentRenderPass[ePass];
                        Assert.IsTrue(encoder != null);

                        var wbs = worldBoundingSphere[i];
                        var tx = chunkLocalToWorld[i].Value;
                        if (wbs.radius > 0.0f && Culling.Cull(in wbs, in pass.frustum) == Culling.CullingResult.Outside) // TODO: fine cull only if rough culling was !Inside
                            continue;
                        var meshRenderer = chunkMeshRenderer[i];
                        uint depth = 0;
                        switch (pass.passType)   // TODO: we can hoist this out of the loop
                        {
                            case RenderPassType.ZOnly:
                                SubmitHelper.EncodeLitZOnlyTransient(BGFXInstancePtr, encoder, &tib, &tvb, nvertices, nindices, pass.viewId, ref tx, pass.GetFlipCulling());
                                break;
                            case RenderPassType.ShadowMap:
                                float4 bias = new float4(0);
                                SubmitHelper.EncodeShadowMapTransient(BGFXInstancePtr, encoder, &tib, &tvb, nvertices, nindices, pass.viewId, ref tx, pass.GetFlipCullingInverse(), bias);
                                break;
                            case RenderPassType.Transparent:
                                depth = pass.ComputeSortDepth(new float4(wbs.position, 1.0f));
                                goto case RenderPassType.Opaque;
                            case RenderPassType.Opaque:
                                var material = ComponentLitMaterialBGFX[meshRenderer.material];
                                SubmitHelper.EncodeLitTransient(BGFXInstancePtr, encoder, &tib, &tvb, nvertices, nindices, pass.viewId, ref tx, ref material, ref lighting, ref pass.viewTransform, pass.GetFlipCulling(), ref PerThreadData[ThreadIndex].viewSpaceLightCache, depth);
                                break;
                            default:
                                Assert.IsTrue(false);
                                break;
                        }
                    }
                }
            }
        }

        EntityQuery m_query;

        protected override void OnCreate()
        {
            m_query = GetEntityQuery(
                ComponentType.ReadOnly<LitParticleRenderer>(),
                ComponentType.ReadOnly<MeshRenderer>(),
                ComponentType.ReadOnly<LocalToWorld>(),
                ComponentType.ReadOnly<WorldBounds>(),
                ComponentType.ReadOnly<WorldBoundingSphere>(),
                ComponentType.ChunkComponentReadOnly<ChunkWorldBoundingSphere>(),
                ComponentType.ChunkComponentReadOnly<ChunkWorldBounds>(),
                ComponentType.ReadOnly<RenderToPasses>()
            );
        }

        protected override void OnDestroy()
        {
        }

        protected override unsafe void OnUpdate()
        {
            var sys = World.GetExistingSystem<RendererBGFXSystem>().InstancePointer();
            if (!sys->m_initialized)
                return;

            var chunks = m_query.CreateArchetypeChunkArray(Allocator.TempJob);
            NativeArray<Entity> sharedRenderToPass = new NativeArray<Entity>(chunks.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<Entity> sharedLightingRef = new NativeArray<Entity>(chunks.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            SharedComponentTypeHandle<RenderToPasses> renderToPassesType = GetSharedComponentTypeHandle<RenderToPasses>();
            SharedComponentTypeHandle<LightingRef> lightingRefType = GetSharedComponentTypeHandle<LightingRef>();

            // it really sucks we can't get shared components in the job itself
            for (int i = 0; i < chunks.Length; i++)
            {
                sharedRenderToPass[i] = chunks[i].GetSharedComponentData<RenderToPasses>(renderToPassesType, EntityManager).e;
                sharedLightingRef[i] = chunks[i].GetSharedComponentData<LightingRef>(lightingRefType, EntityManager).e;
            }
            chunks.Dispose();

            var encodejob = new SubmitStaticLitParticleJob
            {
                LocalToWorldType = GetComponentTypeHandle<LocalToWorld>(true),
                MeshRendererType = GetComponentTypeHandle<MeshRenderer>(true),
                WorldBoundingSphereType = GetComponentTypeHandle<WorldBoundingSphere>(true),
                ChunkWorldBoundingSphereType = GetComponentTypeHandle<ChunkWorldBoundingSphere>(true),
                DynamicLitVertexBufferType = GetBufferTypeHandle<DynamicLitVertex>(true),
                DynamicIndexBufferType = GetBufferTypeHandle<DynamicIndex>(true),
                SharedRenderToPass = sharedRenderToPass,
                SharedLightingRef = sharedLightingRef,
                BufferRenderToPassesEntry = GetBufferFromEntity<RenderToPassesEntry>(true),
                ComponentRenderPass = GetComponentDataFromEntity<RenderPass>(true),
                ComponentLitMaterialBGFX = GetComponentDataFromEntity<LitMaterialBGFX>(true),
                ComponentLightingBGFX = GetComponentDataFromEntity<LightingBGFX>(true),
                PerThreadData = sys->m_perThreadData,
                MaxPerThreadData = sys->m_maxPerThreadData,
                BGFXInstancePtr = sys
            };
            Assert.IsTrue(sys->m_maxPerThreadData > 0 && encodejob.MaxPerThreadData > 0);

            Dependency = encodejob.ScheduleParallel(m_query, Dependency);
            // Temporary workaround until dependencies bugs are fixed.
            Dependency.Complete();
        }
    }

    [UpdateInGroup(typeof(SubmitSystemGroup))]
    public class SubmitStaticLitSkinnedMeshChunked : SystemBase
    {
        unsafe struct SubmitStaticLitSkinnedMeshJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<LocalToWorld> LocalToWorldType;
            [ReadOnly] public ComponentTypeHandle<SkinnedMeshRenderer> SkinnedMeshRendererType;
            [ReadOnly] public BufferTypeHandle<SkinnedMeshBoneRef> SkinnedMeshBoneRefType;
            [ReadOnly] public ComponentDataFromEntity<SkinnedMeshBoneInfo> ComponentSkinnedMeshBoneInfo;
            [ReadOnly] public ComponentTypeHandle<WorldBoundingSphere> WorldBoundingSphereType;
            [ReadOnly] public ComponentTypeHandle<ChunkWorldBoundingSphere> ChunkWorldBoundingSphereType;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Entity> SharedRenderToPass;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Entity> SharedLightingRef;
            [ReadOnly] public BufferFromEntity<RenderToPassesEntry> BufferRenderToPassesEntry;
            [ReadOnly] public ComponentDataFromEntity<MeshBGFX> ComponentMeshBGFX;
            [ReadOnly] public ComponentDataFromEntity<RenderPass> ComponentRenderPass;
            [ReadOnly] public ComponentDataFromEntity<LitMaterialBGFX> ComponentLitMaterialBGFX;
			[ReadOnly] public ComponentDataFromEntity<SimpleMaterialBGFX> ComponentSimpleMaterialBGFX;
            [ReadOnly] public ComponentDataFromEntity<LightingBGFX> ComponentLightingBGFX;
#pragma warning disable 0649
            [NativeSetThreadIndex] internal int ThreadIndex;
#pragma warning restore 0649
            [ReadOnly] public PerThreadDataBGFX* PerThreadData;
            [ReadOnly] public int MaxPerThreadData;
            [ReadOnly] public RendererBGFXInstance* BGFXInstancePtr;
            [ReadOnly] public bool UsingGPUSkinning;

            public unsafe void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var chunkLocalToWorld = chunk.GetNativeArray(LocalToWorldType);
                var chunkMeshRenderer = chunk.GetNativeArray(SkinnedMeshRendererType);
                BufferAccessor<SkinnedMeshBoneRef> smbrBufferAccessor = chunk.GetBufferAccessor(SkinnedMeshBoneRefType);
                var worldBoundingSphere = chunk.GetNativeArray(WorldBoundingSphereType);
                Assert.IsTrue (chunk.HasChunkComponent(ChunkWorldBoundingSphereType));

                Entity lighte = SharedLightingRef[chunkIndex];
                var lighting = ComponentLightingBGFX[lighte];
                Entity rtpe = SharedRenderToPass[chunkIndex];

                Assert.IsTrue(ThreadIndex >= 0 && ThreadIndex < MaxPerThreadData);
                bgfx.Encoder* encoder = PerThreadData[ThreadIndex].encoder;
                if (encoder == null) {
                    encoder = bgfx.encoder_begin(true);
                    Assert.IsTrue(encoder != null);
                    PerThreadData[ThreadIndex].encoder = encoder;
                }
                DynamicBuffer<RenderToPassesEntry> toPasses = BufferRenderToPassesEntry[rtpe];

                // we can do this loop either way, passes first or renderers first.
                // TODO: profile what is better!
                for (int i = 0; i < toPasses.Length; i++) { // for all passes this chunk renderer to
                    Entity ePass = toPasses[i].e;
                    var pass = ComponentRenderPass[ePass];
                    Assert.IsTrue(encoder != null);
                    for (int j = 0; j < chunk.Count; j++) { // for every renderer in chunk
                        var meshRenderer = chunkMeshRenderer[j];
                        if (UsingGPUSkinning && meshRenderer.canUseCPUSkinning && !meshRenderer.canUseGPUSkinning)
                            continue;
                        if (!UsingGPUSkinning && !meshRenderer.canUseCPUSkinning && meshRenderer.canUseGPUSkinning)
                            continue;

                        var wbs = worldBoundingSphere[j];
                        var tx = chunkLocalToWorld[j].Value;
                        if (wbs.radius > 0.0f && Culling.Cull(in wbs, in pass.frustum) == Culling.CullingResult.Outside) // TODO: fine cull only if rough culling was !Inside
                            continue;

                        MeshBGFX mesh = new MeshBGFX();
                        bool hasMeshBGFX = ComponentMeshBGFX.HasComponent(meshRenderer.dynamicMesh);
                        if (hasMeshBGFX)
                            mesh = ComponentMeshBGFX[meshRenderer.dynamicMesh];
                        else
                        {
                            hasMeshBGFX = ComponentMeshBGFX.HasComponent(meshRenderer.sharedMesh);
                            if (hasMeshBGFX)
                                mesh = ComponentMeshBGFX[meshRenderer.sharedMesh];
                        }

                        float4x4[] boneMatrices = null;
                        if (meshRenderer.indexCount > 0 && hasMeshBGFX)
                        {
                            Assert.IsTrue(mesh.IsValid());

                            DynamicBuffer<SkinnedMeshBoneRef> smbrBuffer = smbrBufferAccessor[j];
                            int boneCount = smbrBuffer.Length;
                            bool needGPUSkinning = UsingGPUSkinning && boneCount > 0;
                            if (needGPUSkinning)
                            {
                                boneMatrices = new float4x4[boneCount];
                                for (int k = 0; k < boneCount; k++)
                                {
                                    SkinnedMeshBoneRef smbr = smbrBuffer[k];
                                    SkinnedMeshBoneInfo smbi = ComponentSkinnedMeshBoneInfo[smbr.bone];
                                    boneMatrices[k] = smbi.bonematrix;
                                }
                            }

                            uint depth = 0;
                            switch (pass.passType) { // TODO: we can hoist this out of the loop
                                case RenderPassType.ZOnly:
                                    SubmitHelper.EncodeZOnlyMesh(BGFXInstancePtr, encoder, pass.viewId, ref mesh, ref tx, meshRenderer.startIndex, meshRenderer.indexCount, pass.GetFlipCulling());
                                    break;
                                case RenderPassType.ShadowMap:
                                    if (meshRenderer.shadowCastingMode != ShadowCastingMode.Off)
                                    {
                                        float4 bias = new float4(0);
                                        if (!needGPUSkinning)
                                            SubmitHelper.EncodeShadowMapMesh(BGFXInstancePtr, encoder, pass.viewId, ref mesh, ref tx, meshRenderer.startIndex, meshRenderer.indexCount, pass.GetFlipCullingInverse(), bias);
                                        else
                                            SubmitHelper.EncodeShadowMapSkinnedMesh(BGFXInstancePtr, encoder, pass.viewId, ref mesh, ref tx, meshRenderer.startIndex, meshRenderer.indexCount, pass.GetFlipCullingInverse(), bias, boneMatrices);
                                    }
                                    break;
                                case RenderPassType.Transparent:
                                    depth = pass.ComputeSortDepth(tx.c3);
                                    goto case RenderPassType.Opaque;
                                case RenderPassType.Opaque:
                                    if (meshRenderer.shadowCastingMode != ShadowCastingMode.ShadowsOnly)
                                    {
										if (ComponentLitMaterialBGFX.HasComponent(meshRenderer.material))
										{
                                    		var material = ComponentLitMaterialBGFX[meshRenderer.material];
                                    		if (!needGPUSkinning)
                                        		SubmitHelper.EncodeLitMesh(BGFXInstancePtr, encoder, pass.viewId, ref mesh, ref tx, ref material, ref lighting, ref pass.viewTransform, meshRenderer.startIndex, meshRenderer.indexCount, pass.GetFlipCulling(), ref PerThreadData[ThreadIndex].viewSpaceLightCache, depth);
                                    		else
                                        		SubmitHelper.EncodeLitSkinnedMesh(BGFXInstancePtr, encoder, pass.viewId, ref mesh, ref tx, ref material, ref lighting, ref pass.viewTransform, meshRenderer.startIndex, meshRenderer.indexCount, pass.GetFlipCulling(), ref PerThreadData[ThreadIndex].viewSpaceLightCache, depth, boneMatrices);
										}
										else if (ComponentSimpleMaterialBGFX.HasComponent(meshRenderer.material))
										{
											var material = ComponentSimpleMaterialBGFX[meshRenderer.material];
											if (!needGPUSkinning)
                                        		SubmitHelper.EncodeSimpleMesh(BGFXInstancePtr, encoder, pass.viewId, ref mesh, ref tx, ref material, meshRenderer.startIndex, meshRenderer.indexCount, pass.GetFlipCulling(), depth);
                                    		else
                                                SubmitHelper.EncodeSimpleSkinnedmesh(BGFXInstancePtr, encoder, pass.viewId, ref mesh, ref tx, ref material, meshRenderer.startIndex, meshRenderer.indexCount, pass.GetFlipCulling(), depth, boneMatrices);
										}
                                    }
                                    break;
                                default:
                                    Assert.IsTrue(false);
                                    break;
                            }
                        } else
                        {
                            //var mesh = ComponentDynamicMeshBGFX[meshRenderer.mesh];
                        }
                    }
                }
            }
        }

        EntityQuery m_query;

        protected override void OnCreate()
        {
            m_query = GetEntityQuery(
                ComponentType.ReadOnly<SkinnedMeshRenderer>(),
                ComponentType.ReadOnly<LocalToWorld>(),
                ComponentType.ReadOnly<WorldBounds>(),
                ComponentType.ReadOnly<WorldBoundingSphere>(),
                ComponentType.ChunkComponentReadOnly<ChunkWorldBoundingSphere>(),
                ComponentType.ChunkComponentReadOnly<ChunkWorldBounds>(),
                ComponentType.ReadOnly<RenderToPasses>()
            );
        }

        protected override void OnDestroy()
        {
        }

        protected unsafe override void OnUpdate()
        {
            var sys = World.GetExistingSystem<RendererBGFXSystem>().InstancePointer();
            if (!sys->m_initialized)
                return;

            var chunks = m_query.CreateArchetypeChunkArray(Allocator.TempJob);
            NativeArray<Entity> sharedRenderToPass = new NativeArray<Entity>(chunks.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<Entity> sharedLightingRef = new NativeArray<Entity>(chunks.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            SharedComponentTypeHandle<RenderToPasses> renderToPassesType = GetSharedComponentTypeHandle<RenderToPasses>();
            SharedComponentTypeHandle<LightingRef> lightingRefType = GetSharedComponentTypeHandle<LightingRef>();

            // it really sucks we can't get shared components in the job itself
            for (int i = 0; i < chunks.Length; i++)
            {
                sharedRenderToPass[i] = chunks[i].GetSharedComponentData<RenderToPasses>(renderToPassesType, EntityManager).e;
                sharedLightingRef[i] = chunks[i].GetSharedComponentData<LightingRef>(lightingRefType, EntityManager).e;
            }
            chunks.Dispose();

            var di = GetSingleton<DisplayInfo>();
            var encodejob = new SubmitStaticLitSkinnedMeshJob {
                LocalToWorldType = GetComponentTypeHandle<LocalToWorld>(true),
                SkinnedMeshRendererType = GetComponentTypeHandle<SkinnedMeshRenderer>(true),
                SkinnedMeshBoneRefType = GetBufferTypeHandle<SkinnedMeshBoneRef>(true),
                ComponentSkinnedMeshBoneInfo = GetComponentDataFromEntity<SkinnedMeshBoneInfo>(true),
                WorldBoundingSphereType = GetComponentTypeHandle<WorldBoundingSphere>(true),
                ChunkWorldBoundingSphereType = GetComponentTypeHandle<ChunkWorldBoundingSphere>(true),
                SharedRenderToPass = sharedRenderToPass,
                SharedLightingRef = sharedLightingRef,
                BufferRenderToPassesEntry = GetBufferFromEntity<RenderToPassesEntry>(true),
                ComponentMeshBGFX = GetComponentDataFromEntity<MeshBGFX>(true),
                ComponentRenderPass = GetComponentDataFromEntity<RenderPass>(true),
                ComponentLitMaterialBGFX = GetComponentDataFromEntity<LitMaterialBGFX>(true),
				ComponentSimpleMaterialBGFX = GetComponentDataFromEntity<SimpleMaterialBGFX>(true),
                ComponentLightingBGFX = GetComponentDataFromEntity<LightingBGFX>(true),
                PerThreadData = sys->m_perThreadData,
                MaxPerThreadData = sys->m_maxPerThreadData,
                BGFXInstancePtr = sys,
                UsingGPUSkinning = di.gpuSkinning
            };
            Assert.IsTrue(sys->m_maxPerThreadData>0 && encodejob.MaxPerThreadData>0);

            Dependency = encodejob.ScheduleParallel(m_query, Dependency);
            // Temporary workaround until dependencies bugs are fixed.
            Dependency.Complete();
        }
    }
}
