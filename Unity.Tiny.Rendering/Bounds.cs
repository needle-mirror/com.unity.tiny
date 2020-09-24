using System;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Tiny.Assertions;
using Unity.Transforms;
using Unity.Jobs;

namespace Unity.Tiny.Rendering
{
    public struct WorldBounds : IComponentData
    {
        public float3 c000, c001, c011, c010;
        public float3 c100, c101, c111, c110;

        public float3 GetVertex(int idx)
        {
            switch (idx)
            {
                case 0b_000: return c000;
                case 0b_001: return c001;
                case 0b_011: return c011;
                case 0b_010: return c010;
                case 0b_100: return c100;
                case 0b_101: return c101;
                case 0b_111: return c111;
                case 0b_110: return c110;
            }
            throw new IndexOutOfRangeException();
        }

        public void SetVertex(int idx, float3 value)
        {
            switch (idx)
            {
                case 0b_000: c000 = value; break;
                case 0b_001: c001 = value; break;
                case 0b_011: c011 = value; break;
                case 0b_010: c010 = value; break;
                case 0b_100: c100 = value; break;
                case 0b_101: c101 = value; break;
                case 0b_111: c111 = value; break;
                case 0b_110: c110 = value; break;
                default:  throw new IndexOutOfRangeException();
            }
        }
    }

    // optional component for integrations:
    // world bounds will be updated either from ObjectBounds (if present)
    // or MeshBounds (if there is a MeshRenderer)
    // ObjectBounds will take preference before mesh bounds, if both exist
    // ObjectBounds are not required for MeshRenderers
    public struct ObjectBounds : IComponentData
    {
        public AABB Bounds;
    }

    public struct WorldBoundingSphere : IComponentData
    {
        public float3 position;
        public float radius;
    }

    public struct ChunkWorldBoundingSphere : IComponentData
    {
        public WorldBoundingSphere Value;
    }

    public struct ChunkWorldBounds : IComponentData
    {
        public AABB Value;
    }

    public static class Culling
    {
        public static readonly int[] EdgeTable = { 0b_000_001, 0b_000_100, 0b001_101, 0b101_100, // top
                                                   0b_010_011, 0b_010_110, 0b011_111, 0b111_110, // bottom
                                                   0b_000_010, 0b_001_011, 0b_101_111, 0b_100_110 // sides
        };

        public static float3 SelectCoordsMinMax(float3 cMin, float3 cMax, int mask)
        {
            return new float3((mask & 1) == 1 ? cMax.x : cMin.x, (mask & 2) == 2 ? cMax.y : cMin.y, (mask & 4) == 4 ? cMax.z : cMin.z);
        }

        static bool IsCulled8(float3 p0, float3 p1, float3 p2, float3 p3, float3 p4, float3 p5, float3 p6, float3 p7, float4 plane)
        {
            float4 acc0;
            acc0  = plane.xxxx * new float4(p0.x, p1.x, p2.x, p3.x);
            acc0 += plane.yyyy * new float4(p0.y, p1.y, p2.y, p3.y);
            acc0 += plane.zzzz * new float4(p0.z, p1.z, p2.z, p3.z);
            bool4 c0 = acc0 >= -plane.wwww;
            if (math.any(c0))
                return false;
            float4 acc1;
            acc1  = plane.xxxx * new float4(p4.x, p5.x, p6.x, p7.x);
            acc1 += plane.yyyy * new float4(p4.y, p5.y, p6.y, p7.y);
            acc1 += plane.zzzz * new float4(p4.z, p5.z, p6.z, p7.z);
            bool4 c1 = acc1 >= -plane.wwww;
            if (math.any(c1))
                return false;
            return true;
        }

        static bool IsCulled(float3 p, float4 plane)
        {
            return math.dot(p, plane.xyz) < -plane.w; // important: (0,0,0,0) plane never culls, can be used to pad Frustum struct
        }

        public enum CullingResult
        {
            Outside = 0,
            Intersects = 1,
            Inside = 2
        }

        static CullingResult Cull8(float3 p0, float3 p1, float3 p2, float3 p3, float3 p4, float3 p5, float3 p6, float3 p7, float4 plane)
        {
            float4 acc0;
            acc0  = plane.xxxx * new float4(p0.x, p1.x, p2.x, p3.x);
            acc0 += plane.yyyy * new float4(p0.y, p1.y, p2.y, p3.y);
            acc0 += plane.zzzz * new float4(p0.z, p1.z, p2.z, p3.z);
            bool4 c0 = acc0 >= -plane.wwww;
            float4 acc1;
            acc1  = plane.xxxx * new float4(p4.x, p5.x, p6.x, p7.x);
            acc1 += plane.yyyy * new float4(p4.y, p5.y, p6.y, p7.y);
            acc1 += plane.zzzz * new float4(p4.z, p5.z, p6.z, p7.z);
            bool4 c1 = acc1 >= -plane.wwww;
            bool4 cor = c0 | c1;
            if (math.any(cor))
            {
                bool4 cand =  c0 & c1;
                if (math.all(cand))
                    return CullingResult.Inside;
                else
                    return CullingResult.Intersects;
            }
            return CullingResult.Outside;
        }

        static public CullingResult Cull(in WorldBoundingSphere bounds, float4 plane)
        {
            float dist = math.dot(bounds.position, plane.xyz) + plane.w;
            if (dist <= -bounds.radius) return CullingResult.Outside;
            if (dist >= bounds.radius) return CullingResult.Inside;
            return CullingResult.Intersects;
        }

        static public CullingResult Cull(in WorldBoundingSphere bounds, in Frustum f)
        {
            CullingResult rall = CullingResult.Inside;
            for (int i = 0; i < f.PlanesCount; i++)
            {
                float4 plane = f.GetPlane(i);
                CullingResult r = Cull(in bounds, plane);
                if (r == CullingResult.Outside)
                    return CullingResult.Outside;
                if (r == CullingResult.Intersects)
                    rall = CullingResult.Intersects;
            }
            return rall;
        }

        static public CullingResult Cull(in WorldBounds bounds, in Frustum f)
        {
            int mall = 0;
            for (int i = 0; i < f.PlanesCount; i++)
            {
                float4 plane = f.GetPlane(i);
                int m = IsCulled(bounds.c000, plane) ? 1 : 0;
                m |= IsCulled(bounds.c001, plane) ? 2 : 0;
                m |= IsCulled(bounds.c010, plane) ? 4 : 0;
                m |= IsCulled(bounds.c011, plane) ? 8 : 0;
                m |= IsCulled(bounds.c100, plane) ? 16 : 0;
                m |= IsCulled(bounds.c101, plane) ? 32 : 0;
                m |= IsCulled(bounds.c110, plane) ? 64 : 0;
                m |= IsCulled(bounds.c111, plane) ? 128 : 0;
                if (m == 255) return CullingResult.Outside; // all points outside one plane
                mall |= m;
            }
            if (mall == 0) return CullingResult.Inside; // all points inside all planes
            return CullingResult.Intersects;
        }

        static public bool IsCulled(in WorldBounds bounds, in Frustum f)
        {
            // if all vertices are completely outside of one culling plane, the object is culled
            for (int i = 0; i < f.PlanesCount; i++)
            {
                float4 plane = f.GetPlane(i);
                if (IsCulled8(bounds.c000, bounds.c001, bounds.c011, bounds.c010,
                    bounds.c100, bounds.c101, bounds.c111, bounds.c110, plane))
                    return true;
            }
            return false;
            /* reference
            bool r = false;
            bgfx.dbg_text_clear(0, false);
            for (int i = 0; i < 6; i++) {
                float4 plane = f.GetPlane(i);
                int m = IsCulled(bounds.c000, plane) ? 1 : 0;
                m |= IsCulled(bounds.c001, plane) ? 2 : 0;
                m |= IsCulled(bounds.c010, plane) ? 4 : 0;
                m |= IsCulled(bounds.c011, plane) ? 8 : 0;
                m |= IsCulled(bounds.c100, plane) ? 16 : 0;
                m |= IsCulled(bounds.c101, plane) ? 32 : 0;
                m |= IsCulled(bounds.c110, plane) ? 64 : 0;
                m |= IsCulled(bounds.c111, plane) ? 128 : 0;
                if (m == 255) r = true;

                string s = StringFormatter.Format("{0}: {1} {2}   ", i, m, plane);
                bgfx.dbg_text_printf(0, (ushort)i, 0xf0, s, null);

                //if (IsCulled8(bounds.c000, bounds.c001, bounds.c011, bounds.c010,
                //              bounds.c100, bounds.c101, bounds.c111, bounds.c110, plane))
                //    return true;
            }
            bgfx.set_debug((uint)bgfx.DebugFlags.Text);
            return r;
            */
        }

        static public void WorldBoundsToAxisAligned(in WorldBounds wBounds, out AABB aab)
        {
            float3 bbMin = wBounds.c000;
            float3 bbMax = wBounds.c000;
            GrowBounds(ref bbMin, ref bbMax, in wBounds);
            aab.Center = (bbMax + bbMin) * .5f;
            aab.Extents = (bbMax - bbMin) * .5f;
        }

        static public void TransformWorldBounds(in float4x4 tx, ref WorldBounds b)
        {
            for (int i = 0; i < 8; i++)
                b.SetVertex(i, math.transform(tx, b.GetVertex(i)));
        }

        static public void AxisAlignedToWorldBounds(in float4x4 tx, in AABB aaBounds, out WorldBounds wBounds)
        {
            float3 s = aaBounds.Size;
            float3 o = math.mul(tx, new float4(aaBounds.Min, 1)).xyz;
            float3 dx = math.mul(tx, new float4(s.x, 0, 0, 0)).xyz;
            float3 dy = math.mul(tx, new float4(0, s.y, 0, 0)).xyz;
            float3 dz = math.mul(tx, new float4(0, 0, s.z, 0)).xyz;
            wBounds.c000 = o;
            wBounds.c001 = o + dx;
            wBounds.c011 = o + dx + dy;
            wBounds.c010 = o + dy;
            wBounds.c100 = o + dz;
            wBounds.c101 = o + dz + dx;
            wBounds.c111 = o + dz + dx + dy;
            wBounds.c110 = o + dz + dy;
        }

        static public int SphereInSphere(float4 sphere1, float4 sphere2)
        {
            float d = math.length(sphere1.xyz - sphere2.xyz);
            if (d + sphere2.w <= sphere1.w) return 1; // 2 inside 1
            if (d + sphere1.w <= sphere2.w) return 2; // 1 inside 2
            return 0; // intersecting
        }

        // modifies sphere1 with result
        static public void MergeSpheres(ref float4 sphere1, float4 sphere2)
        {
            int check = SphereInSphere(sphere1, sphere2);
            if (check == 0)
            {
                float3 resultPos = (sphere1.xyz + sphere2.xyz) * .5f;
                float rMaxTo1 = math.length(resultPos - sphere1.xyz) + sphere1.w;
                float rMaxTo2 = math.length(resultPos - sphere2.xyz) + sphere2.w;
                sphere1 = new float4(resultPos, math.max(rMaxTo1, rMaxTo2));
                return;
            }

            if (check == 2)
            {
                sphere1 = sphere2;
                return;
            }

            Assert.IsTrue(check == 1);
            // sphere1 unchanged
        }

        static public float4 PlaneFromTri(float3 p0, float3 p1, float3 p2)
        {
            float3 n = math.normalize(math.cross(p1 - p0, p2 - p0));
            return new float4(n, math.dot(n, p0));
        }

        static public bool PointInBounds(in WorldBounds bounds, float3 p)
        {
            float4 pfront = PlaneFromTri(bounds.c000, bounds.c001, bounds.c011);
            if (IsCulled(p, pfront))
                return false;
            float4 pback = PlaneFromTri(bounds.c100, bounds.c111, bounds.c101);
            if (IsCulled(p, pback))
                return false;
            // etc TODO
            Assert.IsTrue(false);

            return true;
        }

        // modifies bounds1 with results
        static public void MergeBounds(ref WorldBounds bounds1, ref WorldBounds bounds2)
        {
            // TODO
            Assert.IsTrue(false);
        }

        static public void GrowBounds(ref float3 bbMin, ref float3 bbMax, in WorldBounds wb)
        {
            bbMin = math.min(wb.c000, bbMin);
            bbMin = math.min(wb.c001, bbMin);
            bbMin = math.min(wb.c010, bbMin);
            bbMin = math.min(wb.c011, bbMin);
            bbMin = math.min(wb.c100, bbMin);
            bbMin = math.min(wb.c101, bbMin);
            bbMin = math.min(wb.c110, bbMin);
            bbMin = math.min(wb.c111, bbMin);

            bbMax = math.max(wb.c000, bbMax);
            bbMax = math.max(wb.c001, bbMax);
            bbMax = math.max(wb.c010, bbMax);
            bbMax = math.max(wb.c011, bbMax);
            bbMax = math.max(wb.c100, bbMax);
            bbMax = math.max(wb.c101, bbMax);
            bbMax = math.max(wb.c110, bbMax);
            bbMax = math.max(wb.c111, bbMax);
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public class UpdateWorldBoundsSystem : SystemBase
    {
        public AABB m_wholeWorldBounds;

        private static void UpdateBoundsFromAABB(ref WorldBoundingSphere wbs, ref WorldBounds wb, in LocalToWorld tx, in AABB bounds)
        {
            // world obb
            Culling.AxisAlignedToWorldBounds(in tx.Value, in bounds, out wb);
            // object sphere (could be made a memeber of MeshBounds and always pre-computed
            float3 obsphereposition = bounds.Min + bounds.Extents;
            float obsradius = math.length(bounds.Extents);
            // world sphere
            wbs.position = math.transform(tx.Value, obsphereposition);
            // only really needed if there is scale
            float3 scale = new float3(math.lengthsq(tx.Value.c0), math.lengthsq(tx.Value.c1), math.lengthsq(tx.Value.c2));
            float s = math.sqrt(math.cmax(scale));
            wbs.radius = s * obsradius;
        }

        EntityQuery m_qAddChunkBounds;
        EntityQuery m_qAddChunkBoundinSphere;
        EntityQuery m_qUpdateChunkBounds;
        EntityQuery m_qMissingWorldBoundingSphere;
        EntityQuery m_qMissingWorldBounds;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_qAddChunkBoundinSphere = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] {ComponentType.ReadOnly<WorldBoundingSphere>()},
                None = new[] {ComponentType.ChunkComponent<ChunkWorldBoundingSphere>() }
            });

            m_qAddChunkBounds = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] {ComponentType.ReadOnly<WorldBounds>()},
                None = new[] {ComponentType.ChunkComponent<ChunkWorldBounds>() }
            });

            m_qUpdateChunkBounds = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] {ComponentType.ReadOnly<WorldBounds>(), ComponentType.ReadOnly<WorldBoundingSphere>(),
                             ComponentType.ChunkComponent<ChunkWorldBoundingSphere>(), ComponentType.ChunkComponent<ChunkWorldBounds>()},
            });

            m_qMissingWorldBoundingSphere = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] {ComponentType.ReadOnly<LocalToWorld>()},
                Any = new ComponentType[] {ComponentType.ReadOnly<MeshRenderer>(), ComponentType.ReadOnly<SkinnedMeshRenderer>(), ComponentType.ReadOnly<ObjectBounds>()},
                None = new ComponentType[] {typeof(WorldBoundingSphere)}
            });

            m_qMissingWorldBounds = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] {ComponentType.ReadOnly<LocalToWorld>()},
                Any = new ComponentType[] {ComponentType.ReadOnly<MeshRenderer>(), ComponentType.ReadOnly<SkinnedMeshRenderer>(), ComponentType.ReadOnly<ObjectBounds>()},
                None = new ComponentType[] {typeof(WorldBounds)}
            });
        }

        protected override void OnUpdate()
        {
            Dependency.Complete();

            // add WorldBounds and WorldBoundingSphere if not present
            EntityManager.AddComponent<WorldBoundingSphere>(m_qMissingWorldBoundingSphere);
            EntityManager.AddComponent<WorldBounds>(m_qMissingWorldBounds);

            // update world bounds, sphere and box (TODO: this should really change track!)
            float3 bbMinWhole = new float3(float.MaxValue);
            float3 bbMaxWhole = new float3(-float.MaxValue);

            // option one: bounds straight from mesh
            ComponentDataFromEntity<MeshBounds> cmd = GetComponentDataFromEntity<MeshBounds>(true);
            Entities.WithNone<ObjectBounds>().ForEach((Entity e, ref MeshRenderer mr, ref WorldBoundingSphere wbs, ref WorldBounds wb, in LocalToWorld tx) => {
                var mb = cmd[mr.mesh];
                UpdateBoundsFromAABB(ref wbs, ref wb, in tx, in mb.Bounds);
                Culling.GrowBounds(ref bbMinWhole, ref bbMaxWhole, wb);
            }).Run();
            Entities.WithNone<ObjectBounds>().ForEach((Entity e, ref SkinnedMeshRenderer smr, ref WorldBoundingSphere wbs, ref WorldBounds wb, ref LocalToWorld tx) => {
                var mb = cmd[smr.sharedMesh];
                UpdateBoundsFromAABB(ref wbs, ref wb, in tx, in mb.Bounds);
                Culling.GrowBounds(ref bbMinWhole, ref bbMaxWhole, wb);
            }).Run();
            // option two: bounds from ObjectBounds
            Entities.ForEach((ref ObjectBounds ob, ref WorldBoundingSphere wbs, ref WorldBounds wb, in LocalToWorld tx) => {
                UpdateBoundsFromAABB(ref wbs, ref wb, in tx, in ob.Bounds);
                Culling.GrowBounds(ref bbMinWhole, ref bbMaxWhole, wb);
            }).Run();

            m_wholeWorldBounds.Center = (bbMaxWhole + bbMinWhole) * .5f;
            m_wholeWorldBounds.Extents = (bbMaxWhole - bbMinWhole) * .5f;

            // ----------------------------------------------------------------------------------------------------------------------------------
            // experimental: chunk bounds, if we keep this it could be done in one loop, updating bounds and chunk bounds at the same time

            // add chunk bounds
            EntityManager.AddChunkComponentData(m_qAddChunkBoundinSphere, new ChunkWorldBoundingSphere
            {
                Value = new WorldBoundingSphere {  position = new float3(0), radius = -100000.0f }
            });
            EntityManager.AddChunkComponentData(m_qAddChunkBounds, new ChunkWorldBounds());

            // update all chunk bounds
            var chunks = m_qUpdateChunkBounds.CreateArchetypeChunkArray(Allocator.TempJob);
            var chunkBoundsType = EntityManager.GetComponentTypeHandle<ChunkWorldBounds>(false);
            var worldBoundsType = EntityManager.GetComponentTypeHandle<WorldBounds>(true);
            var chunkBoundingSphereType = EntityManager.GetComponentTypeHandle<ChunkWorldBoundingSphere>(false);
            var worldBoundingSphereType = EntityManager.GetComponentTypeHandle<WorldBoundingSphere>(true);
            for (int i = 0; i < chunks.Length; i++)
            {
                var chunk = chunks[i];
                var worldBounds = chunk.GetNativeArray<WorldBounds>(worldBoundsType);
                var worldBoundingSpheres = chunk.GetNativeArray<WorldBoundingSphere>(worldBoundingSphereType);
                float3 bbMin, bbMax;
                float4 sphere;
                unsafe {
                    WorldBounds *wbPtr = (WorldBounds*)worldBounds.GetUnsafeReadOnlyPtr();
                    WorldBoundingSphere *wbsPtr = (WorldBoundingSphere*)worldBoundingSpheres.GetUnsafeReadOnlyPtr();
                    int k = worldBounds.Length;
                    Assert.IsTrue(k > 0 && k == worldBoundingSpheres.Length);
                    bbMin = wbPtr[0].c000;
                    bbMax = bbMin;
                    sphere = new float4(wbsPtr[0].position, wbsPtr[0].radius);
                    for (int j = 1; j < k; j++)
                    {
                        Culling.GrowBounds(ref bbMin, ref bbMax, in wbPtr[j]);
                        Culling.MergeSpheres(ref sphere, new float4(wbsPtr[j].position, wbsPtr[j].radius));
                    }
                }
                chunks[i].SetChunkComponentData<ChunkWorldBounds>(chunkBoundsType, new ChunkWorldBounds
                {
                    Value = new AABB
                    {
                        Center = (bbMin + bbMax) * 0.5f,
                        Extents = (bbMax - bbMin) * 0.5f
                    }
                });
                chunks[i].SetChunkComponentData<ChunkWorldBoundingSphere>(chunkBoundingSphereType, new ChunkWorldBoundingSphere
                {
                    Value = new WorldBoundingSphere
                    {
                        position = sphere.xyz,
                        radius = sphere.w
                    }
                });
            }
            chunks.Dispose();
        }
    }
}
