using System;
using Unity.Mathematics;
using Unity.Entities;

namespace Unity.Tiny.Rendering
{
    // helper gizmos for debugging and visualization
    // those are not optimized and can be quite slow

    public struct GizmoNormalsAndTangents : IComponentData // next to a mesh render
    {
        public float length; // in object space
        public float width; // line width, in pixels
    }

    public struct GizmoObjectBoundingBox : IComponentData // next to something with object bounds
    {
        public float width; // line width, in pixels
        public float4 color;
    }

    public struct GizmoWorldBoundingBox : IComponentData // next to something with world bounds
    {
        public float width; // line width, in pixels
        public float4 color;
    }

    public struct GizmoBoundingSphere : IComponentData // next to something with object bounding sphere
    {
        public float width; // line width, in pixels
        public int subdiv; // number of circle subdivisions, must be > 4
    }

    public struct GizmoTransform : IComponentData // next to something with localToWorld transform
    {
        public float length; // in object space
        public float width; // line width, in pixels
    }

    public struct GizmoLight : IComponentData // next to a light (can detect spot or directional)
    {
        public float width; // line width, in pixels
        public bool overrideColor; // if true use color specified here, otherwise use color from light
        public float4 color;
    }

    public struct GizmoWireframe : IComponentData // next to a mesh render
    {
        // TODO
        public float width; // line width, in pixels
        public float4 color;
    }

    public struct GizmoCamera : IComponentData // next to a camera, shows clipping volume
    {
        public float width;
        public float4 color;
    }

    public struct GizmoAutoMovingDirectionalLight : IComponentData // next to a AutoMovingDirectionalLight
    {
        public float width;
        public float4 colorCasters;
        public float4 colorReceivers;
        public float4 colorClippedReceivers;
    }

    public struct GizmoDebugOverlayTexture : IComponentData // next to a TextureBGFX/Image2D
    {
        public float4 color;
        public float2 pos;   // normalized device -1..1, center position
        public float2 size;  // normalized device -1..1, scale of a unit -1..1 rectangle
    }
}
