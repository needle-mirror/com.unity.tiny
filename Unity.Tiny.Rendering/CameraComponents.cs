using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using Unity.Tiny;
using Unity.Tiny.Assertions;

namespace Unity.Tiny.Rendering
{
    public enum ProjectionMode
    {
        Perspective, Orthographic
    }

    public struct RenderOrder : IComponentData
    {
        public short Layer;
        public short Order;
    }

    // tag to only disable rendering this camera or renderer
    // this can be useful for still running transforms but not rendering it
    public struct DisableRendering : IComponentData
    {
    }

    /// <summary>
    ///  List of options for clearing a camera's viewport before rendering.
    ///  Used by the Camera2D component.
    /// </summary>
    public enum CameraClearFlags
    {
        /// <summary>
        ///  Do not clear. Use this when the camera renders to the entire screen,
        ///  and in situations where multiple cameras render to the same screen area.
        /// </summary>
        Nothing,

        /// <summary>
        ///  Only clear the depth and stencil buffers
        ///  This is useful for some multi layer effects, where one camera renders
        ///  with its own depth range, and the a second camera renders at a closer depth range
        /// </summary>
        DepthOnly,

        /// <summary>
        ///  Clears the viewport with a solid background color, specified by
        /// </summary>
        SolidColor
    }

    // LocalToWorld
    public struct Camera : IComponentData
    {
        public Color backgroundColor; // always linear clear color
        public CameraClearFlags clearFlags;
        public Rect viewportRect;
        public float clipZNear;
        public float clipZFar;
        public float fov; // in degrees for perspective, direct scale factor in orthographic
        public float aspect;
        public ProjectionMode mode;
        public float depth; // stacking depth of this camera, NOT clear depth
    }

    // Camera Settings 2D
    public struct CameraSettings2D : IComponentData
    {
        public float3 customSortAxis;    // For Custom Axis Sort.
    }

    // tag camera to auto set z range
    public struct CameraAutoZFarFromWorldBounds : IComponentData // next to camera
    {
        public float clipZFarMax;   // max far range
        public float clipZFarMin;   // minimum far range
    }

    // tag camera to auto update aspect to primary display
    public struct CameraAutoAspectFromNode : IComponentData // next to a camera
    {
        public Entity Node; // if this is Entity.Null, take aspect ratio from display, otherwise from RenderNode entity pointed by here 
    }

    public struct CameraMatrices : IComponentData
    {
        public float4x4 projection;
        public float4x4 view;
        public Frustum frustum;
    }

    public struct Frustum
    {
        private float4 p0;
        private float4 p1;
        private float4 p2;
        private float4 p3;
        private float4 p4;
        private float4 p5;
        public int PlanesCount;

        public float4 GetPlane(int idx)
        {
            Assert.IsTrue(idx < PlanesCount && idx >= 0);
            switch (idx)
            {
                case 0:
                    return p0;
                case 1:
                    return p1;
                case 2:
                    return p2;
                case 3:
                    return p3;
                case 4:
                    return p4;
                case 5:
                    return p5;
            }
            return new float4(0);
        }

        public void SetPlane(int idx, float4 p)
        {
            Assert.IsTrue(idx < PlanesCount && idx >= 0);
            switch (idx)
            {
                case 0:
                    p0 = p;
                    break;
                case 1:
                    p1 = p;
                    break;
                case 2:
                    p2 = p;
                    break;
                case 3:
                    p3 = p;
                    break;
                case 4:
                    p4 = p;
                    break;
                case 5:
                    p5 = p;
                    break;
            }
        }
    }

    public static class ProjectionHelper
    {
        public static float4x4 ProjectionMatrixPerspective(float n, float f, float fovDeg, float aspect)
        {
            var fov = math.radians(fovDeg);
            var t = n * math.tan(fov * .5f);
            var b = -t;
            var l = -t * aspect;
            var r = t * aspect;
            // homogeneous ndc [-1..1] z range, right handed
            return new float4x4(
                (2 * n) / (r - l), 0,                  -(r + l) / (r - l),   0,
                0,                (2 * n) / (t - b),   -(t + b) / (t - b),   0,
                0,                 0,                  (f + n) / (f - n),    (-2 * f * n) / (f - n),
                0,                 0,                  1,                    0
            );
        }

        public static float4x4 ProjectionMatrixOrtho(float n, float f, float size, float aspect)
        {
            var t = size;
            var b = -t;
            var r = t * aspect;
            var l = -r;
            return new float4x4(
                2f / (r - l),   0,            0,            -(r + l) / (r - l),
                0,              2f / (t - b), 0,            -(t + b) / (t - b),
                0,              0,            2f / (f - n), -(f + n) / (f - n),
                0,              0,            0,            1
            );
        }

        public static float4x4 ProjectionMatrixUnitOrthoOffset(float2 offset, float invsize)
        {
            return new float4x4(
                invsize, 0,       0,    offset.x,
                0,       invsize, 0,    offset.y,
                0,       0,       2.0f, -1.0f,
                0,       0,       0,    1
            );
        }

        public static float4 InitPlane(float3 pos, float3 normal)
        {
            Assert.IsTrue(0.99f < math.lengthsq(normal) && math.lengthsq(normal) < 1.01f);
            return new float4(normal.x, normal.y, normal.z, math.dot(pos, normal));
        }

        public static float4 NormalizePlane(float4 p)
        {
            float l = math.length(p.xyz);
            return p * (1.0f / l);
        }

        // compute world space frustum
        public static void FrustumFromMatrices(float4x4 projection, float4x4 view, out Frustum dest)
        {
            // assumes opengl style projection (TODO, check with orthographic!)
            float4x4 vp = math.transpose(math.mul(projection, view));
            dest = default;
            dest.PlanesCount = 6;
            dest.SetPlane(0, NormalizePlane(vp.c3 + vp.c0));
            dest.SetPlane(1, NormalizePlane(vp.c3 - vp.c0));
            dest.SetPlane(2, NormalizePlane(vp.c3 + vp.c1));
            dest.SetPlane(3, NormalizePlane(vp.c3 - vp.c1));
            dest.SetPlane(4, NormalizePlane(vp.c3 + vp.c2));
            dest.SetPlane(5, NormalizePlane(vp.c3 - vp.c2));
        }

        public static void FrustumFromAABB(AABB b, out Frustum dest)
        {
            dest = default;
            dest.PlanesCount = 6;
            float3 bMin = b.Min;
            float3 bMax = b.Max;
            for (int i = 0; i < 6; i++)
            {
                float3 n = new float3(0.0f);
                float3 p;
                if ((i & 1) == 0)
                {
                    n[i >> 1] = 1.0f;
                    p = bMax;
                }
                else
                {
                    n[i >> 1] = -1.0f;
                    p = bMin;
                }
                dest.SetPlane(i, InitPlane(p, n));
            }
        }

        public static void FrustumFromCube(float3 pos, float size, out Frustum dest)
        {
            dest = default;
            dest.PlanesCount = 6;
            for (int i = 0; i < 6; i++)
            {
                float3 pp = pos;
                float s = (i & 1) == 0 ? 1.0f : -1.0f;
                pp[i >> 1] += size * s;
                float3 n = new float3(0.0f);
                n[i >> 1] = s;
                dest.SetPlane(i, InitPlane(pp, n));
            }
        }

        static public float3 FrustumVertexPerspective(int idx, float w, float h, float near, float far)
        {
            float3 r = new float3(w, h, 1.0f);
            if (idx < 4) { r *= near; } else { r *= far; }
            switch (idx & 3)
            {
                case 0: break;
                case 1: r.x = -r.x; break;
                case 3: r.x = -r.x; r.y = -r.y; break;
                case 2: r.y = -r.y; break;
            }
            return r;
        }

        static public float3 FrustumVertexOrtho(int idx, float size, float near, float far)
        {
            float3 r = new float3(size, size, 0.0f);
            if (idx < 4) { r.z = near; } else { r.z = far; }
            switch (idx & 3)
            {
                case 0: break;
                case 1: r.x = -r.x; break;
                case 3: r.x = -r.x; r.y = -r.y; break;
                case 2: r.y = -r.y; break;
            }
            return r;
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(UpdateCameraMatricesSystem))]
    [UpdateAfter(typeof(UpdateWorldBoundsSystem))]
    public class UpdateCameraZFarSystem : SystemBase
    {
        private AABB TransformBounds(in float4x4 tx, in AABB b)
        {
            WorldBounds wBounds;
            Culling.AxisAlignedToWorldBounds(in tx, in b, out wBounds);
            // now turn those bounds back to axis aligned..
            AABB aab;
            Culling.WorldBoundsToAxisAligned(in wBounds, out aab);
            return aab;
        }

        protected override void OnUpdate()
        {
            Dependency.Complete();
            var wsBounds = World.GetExistingSystem<UpdateWorldBoundsSystem>().m_wholeWorldBounds;
            Entities.ForEach((Entity e, ref CameraAutoZFarFromWorldBounds ab, ref Camera cam, ref LocalToWorld tx) => {
                WorldBounds csBounds;
                float4x4 camTx = math.inverse(tx.Value);
                Culling.AxisAlignedToWorldBounds(in camTx, in wsBounds, out csBounds);
                float3 bbMin = csBounds.c000;
                float3 bbMax = bbMin;
                Culling.GrowBounds(ref bbMin, ref bbMax, in csBounds);
                if (bbMax.z < ab.clipZFarMin) bbMax.z = ab.clipZFarMin;
                if (bbMax.z > ab.clipZFarMax) bbMax.z = ab.clipZFarMax;
                cam.clipZFar = bbMax.z;
            }).Run();
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public class UpdateCameraMatricesSystem : SystemBase
    {
        public static float4x4 ProjectionMatrixFromCamera(ref Camera camera)
        {
            if (camera.mode == ProjectionMode.Orthographic)
                return ProjectionHelper.ProjectionMatrixOrtho(camera.clipZNear, camera.clipZFar, camera.fov, camera.aspect);
            else
                return ProjectionHelper.ProjectionMatrixPerspective(camera.clipZNear, camera.clipZFar, camera.fov, camera.aspect);
        }

        // bounds returned here are not a box for perspective cameras, but the vertices of the frustum
        public static WorldBounds BoundsFromCamera(in Camera camera)
        {
            WorldBounds r = default;
            if (camera.mode == ProjectionMode.Orthographic)
            {
                for (int i = 0; i < 8; i++)
                    r.SetVertex(i, ProjectionHelper.FrustumVertexOrtho(i, camera.fov, camera.clipZNear, camera.clipZFar));
            }
            else
            {
                float h = math.tan(math.radians(camera.fov) * .5f);
                float w = h * camera.aspect;
                for (int i = 0; i < 8; i++)
                    r.SetVertex(i, ProjectionHelper.FrustumVertexPerspective(i, w, h, camera.clipZNear, camera.clipZFar));
            }
            return r;
        }

        protected override void OnUpdate()
        {
            DisplayInfo di = GetSingleton<DisplayInfo>();
            float dispAspect = (float)di.width / (float)di.height;
            Entities.WithoutBurst().ForEach((ref Camera c, ref CameraAutoAspectFromNode from) =>
            {
                c.aspect = dispAspect;
                if ( from.Node != Entity.Null ) {
                    Assert.IsTrue(EntityManager.HasComponent<RenderNode>(from.Node));
                    if ( EntityManager.HasComponent<RenderNodePrimarySurface>(from.Node) ) { 
                        c.aspect = dispAspect;
                    } else if ( EntityManager.HasComponent<RenderNodeTexture>(from.Node) ) { 
                        var rtt = EntityManager.GetComponentData<RenderNodeTexture>(from.Node);
                        c.aspect = (float)rtt.rect.w / (float)rtt.rect.h;
                    }
                }
                if ( !c.viewportRect.IsEmpty() )
                    c.aspect *= c.viewportRect.width / c.viewportRect.height;
            }).Run();

            // add camera matrices if needed
            Entities.WithStructuralChanges().WithNone<CameraMatrices>().WithAll<Camera>().ForEach((Entity e) =>
            {
                EntityManager.AddComponent<CameraMatrices>(e);
            }).Run();

            // update
            Entities.ForEach((ref Camera c, ref LocalToWorld tx, ref CameraMatrices cm) =>
            {
                cm.projection = ProjectionMatrixFromCamera(ref c);
                cm.view = math.inverse(tx.Value);
                ProjectionHelper.FrustumFromMatrices(cm.projection, cm.view, out cm.frustum);
            }).Run();
        }
    }
}
