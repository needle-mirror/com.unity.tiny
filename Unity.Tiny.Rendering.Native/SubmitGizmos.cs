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
    [AlwaysUpdateSystem]
    public unsafe class SubmitGizmos : SystemBase
    {
        static private void Circle(RendererBGFXInstance *sys, bgfx.Encoder* encoder, ushort viewId, float3 org, float3 du, float3 dv, float r, int n, float4 color, float2 normWidth,
            ref float4x4 tx, ref float4x4 txView, ref float4x4 txProj)
        {
            float3 pprev = org + dv * r;
            for (int i = 1; i <= n; i++)
            {
                float a = ((float)i / (float)n) * math.PI * 2.0f;
                float u = math.sin(a) * r;
                float v = math.cos(a) * r;
                float3 p = org + du * u + dv * v;
                SubmitHelper.EncodeLine(sys, encoder, viewId, pprev, p, color, normWidth, ref tx, ref txView, ref txProj);
                pprev = p;
            }
        }

        private void EncodeBox(RendererBGFXInstance *sys, bgfx.Encoder* encoder, Entity ePass, ref float4x4 tx, float3 cMin, float3 cMax, float width, float4 color)
        {
            var pass = EntityManager.GetComponentData<RenderPass>(ePass);
            float4x4 adjustedProjection = sys->GetAdjustedProjection(ref pass);
            float2 normWidth = new float2(width / pass.viewport.w, width / pass.viewport.h);
            for (int j = 0; j < Culling.EdgeTable.Length; j++)
            {
                float3 p0 = Culling.SelectCoordsMinMax(cMin, cMax, Culling.EdgeTable[j] & 7);
                float3 p1 = Culling.SelectCoordsMinMax(cMin, cMax, Culling.EdgeTable[j] >> 3);
                SubmitHelper.EncodeLine(sys, encoder, pass.viewId, p0, p1, color, normWidth,
                    ref tx, ref pass.viewTransform, ref adjustedProjection);
            }
        }

        // gizmos for debuging
        protected override void OnUpdate()
        {
            RendererBGFXInstance *sys = World.GetExistingSystem<RendererBGFXSystem>().InstancePointer();
            if (!sys->m_initialized)
                return;
            Dependency.Complete();

            bgfx.Encoder* encoder = bgfx.encoder_begin(false);

            // tangents & normals
            Entities.WithoutBurst().WithNone<DisableRendering>().WithAll<RenderToPasses>().ForEach((Entity e, ref MeshRenderer mr, ref LocalToWorld tx, ref GizmoNormalsAndTangents giz) =>
            {
                RenderToPasses toPassesRef = EntityManager.GetSharedComponentData<RenderToPasses>(e);
                DynamicBuffer<RenderToPassesEntry> toPasses = EntityManager.GetBufferRO<RenderToPassesEntry>(toPassesRef.e);
                for (int i = 0; i < toPasses.Length; i++)
                {
                    Entity ePass = toPasses[i].e;
                    var pass = EntityManager.GetComponentData<RenderPass>(ePass);
                    float4x4 adjustedProjection = sys->GetAdjustedProjection(ref pass);
                    if (EntityManager.HasComponent<LitMeshRenderData>(mr.mesh))
                    {
                        var meshBase = EntityManager.GetComponentData<LitMeshRenderData>(mr.mesh);
                        Assert.IsTrue(giz.length > 0);
                        float2 normWidth = new float2(giz.width / pass.viewport.w, giz.width / pass.viewport.h);
                        SubmitHelper.EncodeDebugTangents(sys, encoder, pass.viewId, normWidth, giz.length, ref meshBase, ref tx.Value, ref pass.viewTransform, ref adjustedProjection);
                    }
                }
            }).Run();

            // object bounding box
            Entities.WithoutBurst().WithNone<DisableRendering>().ForEach((Entity e, ref MeshRenderer mr, ref LocalToWorld tx, ref GizmoObjectBoundingBox giz) =>
            {
                if (!EntityManager.HasComponent<RenderToPasses>(e))
                    return;
                AABB b = EntityManager.GetComponentData<MeshBounds>(mr.mesh).Bounds;
                RenderToPasses toPassesRef = EntityManager.GetSharedComponentData<RenderToPasses>(e);
                DynamicBuffer<RenderToPassesEntry> toPasses = EntityManager.GetBufferRO<RenderToPassesEntry>(toPassesRef.e);
                for (int i = 0; i < toPasses.Length; i++)
                {
                    Entity ePass = toPasses[i].e;
                    EncodeBox(sys, encoder, ePass, ref tx.Value, b.Min, b.Max, giz.width, giz.color);
                }
            }).Run();

            // world bounds
            Entities.WithoutBurst().WithNone<DisableRendering>().ForEach((Entity e, ref WorldBounds b, ref LocalToWorld tx, ref GizmoWorldBoundingBox giz) =>
            {
                if (!EntityManager.HasComponent<RenderToPasses>(e))
                    return;
                RenderToPasses toPassesRef = EntityManager.GetSharedComponentData<RenderToPasses>(e);
                DynamicBuffer<RenderToPassesEntry> toPasses = EntityManager.GetBufferRO<RenderToPassesEntry>(toPassesRef.e);
                float4x4 idm = float4x4.identity;
                for (int i = 0; i < toPasses.Length; i++)
                {
                    Entity ePass = toPasses[i].e;
                    var pass = EntityManager.GetComponentData<RenderPass>(ePass);
                    float4x4 adjustedProjection = sys->GetAdjustedProjection(ref pass);
                    float2 normWidth = new float2(giz.width / pass.viewport.w, giz.width / pass.viewport.h);
                    for (int j = 0; j < Culling.EdgeTable.Length; j++)
                    {
                        float3 p0 = b.GetVertex(Culling.EdgeTable[j] & 7);
                        float3 p1 = b.GetVertex(Culling.EdgeTable[j] >> 3);
                        SubmitHelper.EncodeLine(sys, encoder, pass.viewId, p0, p1, giz.color, normWidth,
                            ref idm, ref pass.viewTransform, ref adjustedProjection);
                    }
                }
            }).Run();

            // transform
            Entities.WithoutBurst().WithNone<DisableRendering>().ForEach((Entity e, ref LocalToWorld tx, ref GizmoTransform giz) =>
            {
                if (!EntityManager.HasComponent<RenderToPasses>(e))
                    return;
                RenderToPasses toPassesRef = EntityManager.GetSharedComponentData<RenderToPasses>(e);
                DynamicBuffer<RenderToPassesEntry> toPasses = EntityManager.GetBufferRO<RenderToPassesEntry>(toPassesRef.e);
                for (int i = 0; i < toPasses.Length; i++)
                {
                    Entity ePass = toPasses[i].e;
                    var pass = EntityManager.GetComponentData<RenderPass>(ePass);
                    float4x4 adjustedProjection = sys->GetAdjustedProjection(ref pass);
                    float2 normWidth = new float2(giz.width / pass.viewport.w, giz.width / pass.viewport.h);
                    Assert.IsTrue(giz.length > 0);
                    SubmitHelper.EncodeLine(sys, encoder, pass.viewId, new float3(0), new float3(giz.length, 0, 0), new float4(1, 0, 0, 1), normWidth, ref tx.Value, ref pass.viewTransform, ref adjustedProjection);
                    SubmitHelper.EncodeLine(sys, encoder, pass.viewId, new float3(0), new float3(0, giz.length, 0), new float4(0, 1, 0, 1), normWidth, ref tx.Value, ref pass.viewTransform, ref adjustedProjection);
                    SubmitHelper.EncodeLine(sys, encoder, pass.viewId, new float3(0), new float3(0, 0, giz.length), new float4(0, 0, 1, 1), normWidth, ref tx.Value, ref pass.viewTransform, ref adjustedProjection);
                }
            }).Run();

            // sphere
            Entities.WithoutBurst().WithNone<DisableRendering>().ForEach((Entity e, ref LocalToWorld tx, ref WorldBoundingSphere bs, ref GizmoBoundingSphere giz) =>
            {
                if (!EntityManager.HasComponent<RenderToPasses>(e))
                    return;
                RenderToPasses toPassesRef = EntityManager.GetSharedComponentData<RenderToPasses>(e);
                DynamicBuffer<RenderToPassesEntry> toPasses = EntityManager.GetBufferRO<RenderToPassesEntry>(toPassesRef.e);
                float4x4 idm = float4x4.identity;
                for (int i = 0; i < toPasses.Length; i++)
                {
                    Entity ePass = toPasses[i].e;
                    var pass = EntityManager.GetComponentData<RenderPass>(ePass);
                    float4x4 adjustedProjection = sys->GetAdjustedProjection(ref pass);
                    float2 normWidth = new float2(giz.width / pass.viewport.w, giz.width / pass.viewport.h);
                    Assert.IsTrue(giz.subdiv >= 4);
                    Circle(sys, encoder, pass.viewId, bs.position, new float3(0, 0, 1), new float3(0, 1, 0), bs.radius, giz.subdiv, new float4(1, 0, 0, 1), normWidth, ref idm, ref pass.viewTransform, ref adjustedProjection); // z/y plane
                    Circle(sys, encoder, pass.viewId, bs.position, new float3(1, 0, 0), new float3(0, 0, 1), bs.radius, giz.subdiv, new float4(0, 1, 0, 1), normWidth, ref idm, ref pass.viewTransform, ref adjustedProjection); // x/z plane
                    Circle(sys, encoder, pass.viewId, bs.position, new float3(0, 1, 0), new float3(1, 0, 0), bs.radius, giz.subdiv, new float4(0, 0, 1, 1), normWidth, ref idm, ref pass.viewTransform, ref adjustedProjection); // y/x plane
                }
            }).Run();

            // spot lights
            Entities.WithoutBurst().WithNone<DisableRendering>().ForEach((Entity e, ref LocalToWorld tx, ref Light l, ref SpotLight sl, ref GizmoLight giz) =>
            {
                if (!EntityManager.HasComponent<RenderToPasses>(e))
                    return;
                RenderToPasses toPassesRef = EntityManager.GetSharedComponentData<RenderToPasses>(e);
                DynamicBuffer<RenderToPassesEntry> toPasses = EntityManager.GetBufferRO<RenderToPassesEntry>(toPassesRef.e);
                float4 color = giz.overrideColor ? giz.color : new float4(l.color, 1.0f);
                for (int i = 0; i < toPasses.Length; i++)
                {
                    Entity ePass = toPasses[i].e;
                    var pass = EntityManager.GetComponentData<RenderPass>(ePass);
                    float2 normWidth = new float2(giz.width / pass.viewport.w, giz.width / pass.viewport.h);
                    float4x4 adjustedProjection = sys->GetAdjustedProjection(ref pass);
                    // render frustum
                    float t = math.tan(math.radians(sl.fov) * .5f);
                    for (int j = 0; j < Culling.EdgeTable.Length; j++)
                    {
                        float3 pp0 = ProjectionHelper.FrustumVertexPerspective(Culling.EdgeTable[j] & 7, t, t, l.clipZNear, l.clipZFar);
                        float3 pp1 = ProjectionHelper.FrustumVertexPerspective(Culling.EdgeTable[j] >> 3, t, t, l.clipZNear, l.clipZFar);
                        SubmitHelper.EncodeLine(sys, encoder, pass.viewId, pp0, pp1, color, normWidth,
                            ref tx.Value, ref pass.viewTransform, ref adjustedProjection);
                    }
                }
            }).Run();

            // directional lights
            Entities.WithoutBurst().WithNone<DisableRendering>().ForEach((Entity e, ref LocalToWorld tx, ref Light l, ref DirectionalLight dl, ref GizmoLight giz) =>
            {
                if (!EntityManager.HasComponent<RenderToPasses>(e))
                    return;
                RenderToPasses toPassesRef = EntityManager.GetSharedComponentData<RenderToPasses>(e);
                DynamicBuffer<RenderToPassesEntry> toPasses = EntityManager.GetBufferRO<RenderToPassesEntry>(toPassesRef.e);
                float4 color = giz.overrideColor ? giz.color : new float4(l.color, 1.0f);
                float3 cMin = new float3(-1, -1, l.clipZNear);
                float3 cMax = new float3(1,  1, l.clipZFar);
                for (int i = 0; i < toPasses.Length; i++)
                {
                    Entity ePass = toPasses[i].e;
                    EncodeBox(sys, encoder, ePass, ref tx.Value, cMin, cMax, giz.width, color);
                }
            }).Run();

            // point lights

            // cameras
            Entities.WithoutBurst().WithNone<DisableRendering>().ForEach((Entity e, ref LocalToWorld tx, ref Camera cam, ref GizmoCamera giz) =>
            {
                if (!EntityManager.HasComponent<RenderToPasses>(e))
                    return;
                RenderToPasses toPassesRef = EntityManager.GetSharedComponentData<RenderToPasses>(e);
                DynamicBuffer<RenderToPassesEntry> toPasses = EntityManager.GetBufferRO<RenderToPassesEntry>(toPassesRef.e);
                for (int i = 0; i < toPasses.Length; i++)
                {
                    Entity ePass = toPasses[i].e;
                    var pass = EntityManager.GetComponentData<RenderPass>(ePass);
                    float2 normWidth = new float2(giz.width / pass.viewport.w, giz.width / pass.viewport.h);
                    float4x4 adjustedProjection = sys->GetAdjustedProjection(ref pass);
                    // render box
                    if (cam.mode == ProjectionMode.Orthographic)
                    {
                        float3 cMin = new float3(-cam.fov, -cam.fov, cam.clipZNear);
                        float3 cMax = new float3(cam.fov, cam.fov, cam.clipZFar);
                        EncodeBox(sys, encoder, ePass, ref tx.Value, cMin, cMax, giz.width, giz.color);
                    }
                    else if (cam.mode == ProjectionMode.Perspective)
                    {
                        float h = math.tan(math.radians(cam.fov) * .5f);
                        float w = h * cam.aspect;
                        for (int j = 0; j < Culling.EdgeTable.Length; j++)
                        {
                            float3 pp0 = ProjectionHelper.FrustumVertexPerspective(Culling.EdgeTable[j] & 7, w, h, cam.clipZNear, cam.clipZFar);
                            float3 pp1 = ProjectionHelper.FrustumVertexPerspective(Culling.EdgeTable[j] >> 3, w, h, cam.clipZNear, cam.clipZFar);
                            SubmitHelper.EncodeLine(sys, encoder, pass.viewId, pp0, pp1, giz.color, normWidth,
                                ref tx.Value, ref pass.viewTransform, ref adjustedProjection);
                        }
                    }
                }
            }).Run();

            // auto bounds
            Entities.WithoutBurst().WithNone<DisableRendering>().ForEach((Entity e, ref LocalToWorld tx, ref AutoMovingDirectionalLight amd, ref GizmoAutoMovingDirectionalLight giz) =>
            {
                if (!EntityManager.HasComponent<RenderToPasses>(e))
                    return;
                RenderToPasses toPassesRef = EntityManager.GetSharedComponentData<RenderToPasses>(e);
                DynamicBuffer<RenderToPassesEntry> toPasses = EntityManager.GetBufferRO<RenderToPassesEntry>(toPassesRef.e);
                float4x4 idm = float4x4.identity;
                for (int i = 0; i < toPasses.Length; i++)
                {
                    Entity ePass = toPasses[i].e;
                    EncodeBox(sys,  encoder, ePass, ref idm, amd.bounds.Min, amd.bounds.Max, giz.width, giz.colorCasters);
                    EncodeBox(sys, encoder, ePass, ref idm, amd.boundsClipped.Min, amd.boundsClipped.Max, giz.width, giz.colorClippedReceivers);
                }
            }).Run();

            // debug display textures as overlays
            Entities.WithoutBurst().WithNone<DisableRendering>().ForEach((Entity e, ref TextureBGFX tex, ref GizmoDebugOverlayTexture giz) => {
                if (!EntityManager.HasComponent<RenderToPasses>(e))
                    return;
                RenderToPasses toPassesRef = EntityManager.GetSharedComponentData<RenderToPasses>(e);
                DynamicBuffer<RenderToPassesEntry> toPasses = EntityManager.GetBufferRO<RenderToPassesEntry>(toPassesRef.e);
                float4x4 m = float4x4.identity;
                m.c3.xy = giz.pos;
                m.c0.x = giz.size.x;
                m.c1.y = giz.size.y;
                SimpleMaterialBGFX sm = new SimpleMaterialBGFX
                {
                    texAlbedoOpacity = tex.handle,
                    constAlbedo_Opacity = giz.color,
                    mainTextureScaleTranslate = new float4(1, 1, 0, 0),
                    state = (ulong)bgfx.StateFlags.WriteRgb
                };
                for (int i = 0; i < toPasses.Length; i++)
                {
                    Entity ePass = toPasses[i].e;
                    var pass = EntityManager.GetComponentData<RenderPass>(ePass);
                    SubmitHelper.EncodeSimpleMesh(sys, encoder, pass.viewId, ref sys->m_quadMesh, ref m, ref sm, 0, 6, pass.GetFlipCulling(), 0);
                }
            }).Run();
            bgfx.encoder_end(encoder);
        }
    }
}
