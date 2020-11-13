using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Tiny;
using Unity.Tiny.Assertions;
using Unity.Tiny.Rendering;
using Unity.Transforms;

namespace Unity.Tiny.Rendering
{
    // a pickable root with an optional ScreenToWorldPassList entry next to it
    public struct ScreenToWorldRoot : IComponentData
    {
        public Entity camera;
        public Entity pass;         // first pass, used for grabbing the viewport transform from.
    }

    // a list of pickable passes, in order, next to pickable root entity
    // all the transforms in here are applied in order
    public struct ScreenToWorldPassList : IBufferElementData
    {
        public Entity pass;
    }

    [UpdateAfter(typeof(PreparePassesSystem))]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public class ScreenToWorld : SystemBase
    {
        // inverse transform for a render pass, camera and projection,
        static public float4 InversePassTransform(float4 pos, in RenderPass pass)
        {
            pos *= pos.w;
            float4 pp = math.mul(math.inverse(pass.projectionTransform), pos);
            return math.mul(math.inverse(pass.viewTransform), pp);
        }

        // forward transform for a render pass, camera and projection, then followed by a perspective divide
        static public float4 PassTransform(float4 pos, in RenderPass pass)
        {
            float4 pp = math.mul(pass.viewTransform, pos);
            pp = math.mul(pass.projectionTransform, pp);
            pp *= 1.0f / pp.w;
            return pp;
        }

        // the inverse viewport transform
        // go from pixel coordinates to [-1..1] normalized device coordinates
        static public float2 InverseViewPortTransform(float2 p, RenderPass pass)
        {
            p -= new float2(pass.viewport.x, pass.viewport.y);
            p /= new float2(pass.viewport.w, pass.viewport.h);
            p *= 2.0f;
            p -= 1.0f;
            return p;
        }

        // the forward viewport transform, the same that graphics cards apply
        // to go from [-1..1] normalized device coordinates to integer pixel coordinates
        static public float2 ViewPortTransform(float2 p, RenderPass pass)
        {
            p.xy += 1.0f;
            p.xy *= .5f;
            p.xy *= new float2(pass.viewport.w, pass.viewport.h);
            p.xy += new float2(pass.viewport.x, pass.viewport.y);
            return p;
        }

        // adjust position for devices where points are not equal to pixels, like some high dpi displays
        public float2 AdjustInputPositionToPixels(float2 inputPos)
        {
            var di = GetSingleton<DisplayInfo>();
            return inputPos * new float2(di.framebufferWidth / (float)di.width, di.framebufferHeight / (float)di.height);
        }

        protected void FindPickRoot(out Entity eOutPickRoot, out Entity eOutPass, Entity ecam)
        {
            Assert.IsTrue(ecam != Entity.Null);
            var ePickRoot = Entity.Null;
            var ePass = Entity.Null;
            Entities.ForEach((Entity e, ref ScreenToWorldRoot root) => {
                if (root.camera == ecam) {
                    Assert.IsTrue(ePickRoot == Entity.Null); // Multiple roots with same camera found
                    ePickRoot = e;
                    ePass = root.pass;
                    Assert.IsTrue(ePass != Entity.Null);
                }
            }).Run();
            Assert.IsTrue(ePickRoot != Entity.Null); // No root for picking with this camera found
            eOutPickRoot = ePickRoot;
            eOutPass = ePass;
        }

        // return value xy is in pixels, z is normalized -1..1 where -1 is near and 1 far
        public float3 WorldSpaceToScreenSpace(float3 worldPos, Entity ecam)
        {
            Assert.IsTrue(ecam != Entity.Null);
            Entity ePickRoot, ePass;
            FindPickRoot(out ePickRoot, out ePass, ecam);
            if (ePickRoot == Entity.Null)
                return new float3(0);
            // apply all the pass transforms back to front, if there are any
            var pp = new float4(worldPos, 1.0f);
            if (EntityManager.HasComponent<ScreenToWorldPassList>(ePickRoot)) {
                var l = EntityManager.GetBuffer<ScreenToWorldPassList>(ePickRoot);
                for (int i = l.Length - 1; i >= 0; i--) {
                    var lp = EntityManager.GetComponentData<RenderPass>(l[i].pass);
                    pp = PassTransform(pp, lp);
                }
            }
            // apply root and viewport transform
            var pass = EntityManager.GetComponentData<RenderPass>(ePass);
            pp = PassTransform(pp, pass);
            pp.xy = ViewPortTransform(pp.xy, pass);
            return pp.xyz;
        }

        // screenPos is in pixels. Note that this is pixels, not points. So for platforms where pixels != points, this need to be adjusted first.
        // depth is normalized -1..1, where -1 is near and 1 far
        public float3 ScreenSpaceToWorldSpace(float2 screenPos, float normalizedZ, Entity ecam)
        {
            Assert.IsTrue(ecam != Entity.Null);
            Entity ePickRoot, ePass;
            FindPickRoot(out ePickRoot, out ePass, ecam);
            if (ePickRoot == Entity.Null)
                return new float3(0);
            // root and viewport transform
            var pass = EntityManager.GetComponentData<RenderPass>(ePass);
            float2 pp = InverseViewPortTransform(screenPos, pass);
            float4 pp2 = InversePassTransform(new float4(pp, normalizedZ, 1), pass);
            // now apply transform for all passes in list as well, if there is one
            if (!EntityManager.HasComponent<ScreenToWorldPassList>(ePickRoot)) {
                pp2 *= 1.0f / pp2.w; // homogenize, as we drop w here, we need to turn it into w=1
                return pp2.xyz;
            }
            var l = EntityManager.GetBuffer<ScreenToWorldPassList>(ePickRoot);
            for (int i = 0; i < l.Length; i++) {
                var lp = EntityManager.GetComponentData<RenderPass>(l[i].pass);
                pp2 = InversePassTransform(pp2, lp);
            }
            pp2 *= 1.0f / pp2.w; // homogenize, as we drop w here, we need to turn it into w=1
            return pp2.xyz;
        }

        public Entity DefaultCamera()
        {
            // need some heuristic here, could require some tag...
            // for now, pick the one with the lowest depth
            Entity efound = Entity.Null;
            float bestdepth = float.MaxValue;
            Entities.WithAll<Camera>().ForEach((Entity e, in Camera cam) => {
                if (cam.depth < bestdepth) {
                    bestdepth = cam.depth;
                    efound = e;
                }
            }).Run();
            Assert.IsTrue(efound != Entity.Null);
            return efound;
        }

        // gets the transform and returns a plane at distance distanceToCamera in front of the camera.
        // if the camera entity is Null, try to find the camera for the ScreenToWorld Main Camera
        // the plane is centered on the camera view axis and up and left are normalized
        public void GetWorldSpaceCameraPlane(out float3 pos, out float3 up, out float3 left, float distanceToCamera, Entity eCam = default)
        {
            if (eCam == Entity.Null)
                eCam = DefaultCamera();
            if (eCam == Entity.Null) {
                pos = new float3(0);
                up = new float3(0, 1, 0);
                left = new float3(1, 0, 0);
                return;
            }
            var camMatrix = EntityManager.GetComponentData<LocalToWorld>(eCam).Value;
            pos = camMatrix.c3.xyz + camMatrix.c2.xyz * distanceToCamera;
            up = camMatrix.c1.xyz;
            left = camMatrix.c0.xyz;
        }

        protected float3 IntersectPlaneRay(float3 planePos, float3 planeNormal, float3 rayOrigin, float3 rayDirection)
        {
            float t = math.dot(planeNormal, planePos - rayOrigin) / math.dot(planeNormal, rayDirection);
            return rayOrigin + t * rayDirection;
        }

        // start with a screen space position (in pixels) and return a world space ray
        public void ScreenSpaceToWorldSpaceRay(float2 screenPos, out float3 origin, out float3 direction, Entity ecam = default)
        {
            if (ecam == Entity.Null)
                ecam = DefaultCamera();
            if (ecam == Entity.Null) {
                origin = new float3(0);
                direction = new float3(0, 0, 1);
                return;
            }
            origin = ScreenSpaceToWorldSpace(screenPos, 0, ecam);
            direction = ScreenSpaceToWorldSpace(screenPos, 1, ecam) - origin;
            direction = math.normalizesafe(direction);
        }

        // start with an input position (in pixels)and return a world space point, which is distanceToCamera world space units in front of the picking camera
        public float3 ScreenSpaceToWorldSpacePos(float2 screenPos, float distanceToCamera, Entity ecam = default)
        {
            if (ecam == Entity.Null)
                ecam = DefaultCamera();
            if (ecam == Entity.Null)
                return new float3(0);
            float3 origin, direction;
            ScreenSpaceToWorldSpaceRay(screenPos, out origin, out direction, ecam);
            float3 camPos, camUp, camLeft;
            GetWorldSpaceCameraPlane(out camPos, out camUp, out camLeft, distanceToCamera);
            return IntersectPlaneRay(camPos, math.cross(camUp, camLeft), origin, direction);
        }

        // start with an input position (in points) and return a world space ray
        public void InputPosToWorldSpaceRay(float2 inputPos, out float3 origin, out float3 direction, Entity ecam = default)
        {
            if (ecam == Entity.Null)
                ecam = DefaultCamera();
            if (ecam == Entity.Null) {
                origin = new float3(0);
                direction = new float3(0, 0, 1);
                return;
            }
            float2 screenPos = AdjustInputPositionToPixels(inputPos);
            ScreenSpaceToWorldSpaceRay(screenPos, out origin, out direction, ecam);
        }

        // start with an input position (in points) and return a world space point, which is distanceToCamera world space units in front of the picking camera
        public float3 InputPosToWorldSpacePos(float2 inputPos, float distanceToCamera, Entity ecam = default)
        {
            if (ecam == Entity.Null)
                ecam = DefaultCamera();
            if (ecam == Entity.Null)
                return new float3(0);
            float2 screenPos = AdjustInputPositionToPixels(inputPos);
            return ScreenSpaceToWorldSpacePos(screenPos, distanceToCamera, ecam);
        }

        protected override void OnUpdate()
        {
            // TODO: if the transforms ever become a speed issue, we can cache
            //       the whole pipeline as premultiplied matrices here, including default camera
            //       but then we have a toctou issue... 
        }
    }
}
