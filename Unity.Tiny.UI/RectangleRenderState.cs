using System;
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Tiny.UI
{
    public struct RectangleRenderState : IComponentData
    {
        /// <summary>
        /// If Simple, just stretched, and the values below aren't used.
        /// If Sliced, 9-slices are used, and we need all the extra metadata to render.
        /// </summary>
        public ImageRenderType ImageRenderType;

        public float PixelsPerUnit;
        public float PixelsPerUnitMultiplier;

        /// Pixel size of the original sprite. Modified by the PixelsPerUnit and
        /// PixelsPerUnitMultiplier to determine the final size of the slices.
        public float2 BaseSize;

        /// <summary>
        /// The splitting lines for the slicing. Measured from the outside
        /// and ordered Left, Bottom, Right, Top. Typical value is 0.25 for all 4.
        /// </summary>
        public float4 Border;

        // UV in sprite sheet. (0, 0, 1, 1 if not in a sprite sheet.)
        // Left, Bottom, Right, Top
        public float4 Outer;

    }

    public struct RectangleRendererNeedsUpdate : IComponentData { }

    /// <summary>
    /// Image fill type controls how to display the image.
    public enum ImageRenderType
    {
        Simple,
        Sliced,
        Tiled,
        Filled
    }
}
