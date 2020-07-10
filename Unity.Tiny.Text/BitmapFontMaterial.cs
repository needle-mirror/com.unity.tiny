using System;
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Tiny.Text
{
    /// <summary>
    /// A font material.
    /// </summary>
    public struct BitmapFontMaterial : IComponentData, IEquatable<BitmapFontMaterial>
    {
        public static readonly Guid ShaderGuid = new Guid("2A6FB286-E9B8-4557-801D-AEA0CF2E33B1");

        public Entity AtlasTexture;
        public BlobAssetReference<FontData> FontData;

        public float4 ConstClipRect;
        public float4 ConstMaskSoftness;

        public bool Equals(BitmapFontMaterial other)
        {
            return TypeManager.Equals(ref this, ref other);
        }
    }

    /// <summary>
    /// A font material.
    /// </summary>
    public struct SDFFontMaterial : IComponentData, IEquatable<SDFFontMaterial>
    {
        public static readonly Guid ShaderGuid = new Guid("7C8B4A25-9152-4B59-86D1-B37C040A6DB1");

        public Entity AtlasTexture;
        public BlobAssetReference<FontData> FontData;

        public float4 FaceColor;

        public float4 ClipRect;
        public float GradientScale;
        public float Sharpness;


        public bool Equals(SDFFontMaterial other)
        {
            return TypeManager.Equals(ref this, ref other);
        }
    }

    /// <summary>
    /// Component adjacent to a MeshRenderer that indicates that it is to be handled
    /// by the text renderer
    /// </summary>
    public struct TextMeshRenderer : IComponentData
    {
    }
}
