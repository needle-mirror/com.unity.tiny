using Unity.Entities;
using Unity.Mathematics;
using System;

namespace Unity.Tiny.Rendering
{
    public struct SimpleMaterial : IComponentData, IEquatable<SimpleMaterial>
    {
        public Entity texAlbedoOpacity;

        public float3 constAlbedo;
        public float constOpacity;

        public bool twoSided;
        public BlendOp blend;
        public bool transparent;
        public bool billboarded;

        public float2 scale;
        public float2 offset;

        public bool Equals(SimpleMaterial other)
        {
            return texAlbedoOpacity.Equals(other.texAlbedoOpacity) &&
                constAlbedo.Equals(other.constAlbedo) &&
                constOpacity.Equals(other.constOpacity) &&
                (twoSided == other.twoSided) &&
                (blend == other.blend) &&
                (transparent == other.transparent) &&
                scale.Equals(other.scale) &&
                offset.Equals(other.offset) &&
                (billboarded == other.billboarded);
        }
    }

    public struct LitMaterial : IComponentData, IEquatable<LitMaterial>
    {
        public Entity texAlbedoOpacity;
        public Entity texMetal;
        public Entity texNormal;
        public Entity texEmissive;

        public float3 constAlbedo;
        public float3 constEmissive;
        public float constOpacity;
        public float constMetal;
        public float constSmoothness;
        public float normalMapZScale;

        public bool twoSided;
        public bool transparent;
        public bool triangleSortTransparent;
        public bool smoothnessAlbedoAlpha;
        public bool billboarded;

        public float2 scale;
        public float2 offset;

        public Entity shader;

        public bool Equals(LitMaterial other)
        {
            return texAlbedoOpacity.Equals(other.texAlbedoOpacity) &&
                texMetal.Equals(other.texMetal) &&
                texNormal.Equals(other.texNormal) &&
                texEmissive.Equals(other.texEmissive) &&
                constAlbedo.Equals(other.constAlbedo) &&
                constEmissive.Equals(other.constEmissive) &&
                constOpacity.Equals(other.constOpacity) &&
                constMetal.Equals(other.constMetal) &&
                constSmoothness.Equals(other.constSmoothness) &&
                normalMapZScale.Equals(other.normalMapZScale) &&
                (twoSided == other.twoSided) &&
                (transparent == other.transparent) &&
                (triangleSortTransparent == other.triangleSortTransparent) &&
                scale.Equals(other.scale) &&
                offset.Equals(other.offset) &&
                (smoothnessAlbedoAlpha == other.smoothnessAlbedoAlpha) &&
                (billboarded == other.billboarded) &&
                (shader == other.shader);
        }
    }
}
