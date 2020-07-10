using Bgfx;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Tiny.Rendering;

namespace Unity.Tiny.Text.Native
{
    internal struct TextMaterialBGFX : ISystemStateComponentData
    {
        public bgfx.TextureHandle texAtlas;

        public float4 constClipRect;
        public float4 constMaskSoftness;

        public ulong state; // includes blending and culling!

        internal unsafe bool Update(EntityManager em, RendererBGFXInstance* sys, ref BitmapFontMaterial mat)
        {
            constClipRect = mat.ConstClipRect;
            constMaskSoftness = mat.ConstMaskSoftness;

            // if texture entity OR load state changed need to update texture handles
            // content of texture change should transparently update texture referenced by handle
            bool stillLoading = UpdateTextMaterialsSystem.InitTexture(em, ref texAtlas, mat.AtlasTexture, sys->m_whiteTexture);

            // text is always two-sided and transparent
            state = (ulong)(bgfx.StateFlags.WriteRgb | bgfx.StateFlags.WriteA | bgfx.StateFlags.DepthTestLess);
            state |= RendererBGFXStatic.MakeBGFXBlend(bgfx.StateFlags.BlendSrcAlpha, bgfx.StateFlags.BlendInvSrcAlpha);

            return !stillLoading;
        }
    }

    internal struct TextSDFMaterialBGFX : ISystemStateComponentData
    {
        public bgfx.TextureHandle texAtlas;

        public float4 faceColor;
        public float4 clipRect;
        public float4 miscP;
        public float faceDilate { get => miscP.x; set => miscP.x = value; }
        public float gradientScale { get => miscP.y; set => miscP.y = value; }
        public float perspectiveFilter { get => miscP.z; set => miscP.z = value; }
        public float sharpness { get => miscP.w; set => miscP.w = value; }

#if false
        public float4 texDimScale;
        public float2 textureSize { get => texDimScale.xy; set => texDimScale.xy = value; }
        public float2 scale { get => texDimScale.zw; set => texDimScale.zw = value; }
        public float4 worldSpaceCameraPos;
        public float4 outlineColor;
        public float4 outlineP;
        public float outlineWidth { get => outlineP.x; set => outlineP.x = value; }
        public float outlineSoftness { get => outlineP.y; set => outlineP.y = value; }
        public float4 underlayColor;
        public float4 underlayP;
        public float2 underlayOffset { get => underlayP.xy; set => underlayP.xy = value; }
        public float underlayDilate { get => underlayP.z; set => underlayP.z = value; }
        public float underlaySoftness { get => underlayP.w; set => underlayP.w = value; }
        public float4 weightAndMaskSoftness;
        public float weightNormal { get => weightAndMaskSoftness.x; set => weightAndMaskSoftness.x = value; }
        public float weightBold { get => weightAndMaskSoftness.y; set => weightAndMaskSoftness.y = value; }
        public float4 scaleRatio;

        public float2 maskSoftness { get => weightAndMaskSoftness.zw; set => weightAndMaskSoftness.zw = value; }
        public float4 screenParams;
#endif

        public ulong state; // includes blending and culling!

        internal unsafe bool Update(EntityManager em, RendererBGFXInstance* sys, ref SDFFontMaterial mat)
        {
            clipRect = mat.ClipRect;
            faceColor = mat.FaceColor;
            gradientScale = mat.GradientScale;
            sharpness = mat.Sharpness;
            //faceDilate = mat.FaceDilate;
            //perspectiveFilter = mat.PerspectiveFilter;


            // if texture entity OR load state changed need to update texture handles
            // content of texture change should transparently update texture referenced by handle
            bool stillLoading = UpdateTextMaterialsSystem.InitTexture(em, ref texAtlas, mat.AtlasTexture, sys->m_whiteTexture);

            // text is always two-sided and transparent
            state = (ulong)(bgfx.StateFlags.WriteRgb | bgfx.StateFlags.WriteA | bgfx.StateFlags.DepthTestLess);
            state |= RendererBGFXStatic.MakeBGFXBlend(bgfx.StateFlags.BlendSrcAlpha, bgfx.StateFlags.BlendInvSrcAlpha);

            return !stillLoading;
        }
    }
}
