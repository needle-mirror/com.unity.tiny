using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Unity.Tiny.Text.Authoring
{
    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    public class FontAssetConversion : GameObjectConversionSystem
    {
        internal static BlobAssetReference<FontData> ConvertFontData(TMPro.TMP_FontAsset font, Allocator allocatorType = Allocator.Persistent)
        {
            if (font.glyphLookupTable == null)
                throw new InvalidOperationException("Reading font resulted in null glyph lookup table?");

            using (var allocator = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref allocator.ConstructRoot<FontData>();

                root.Atlas.AtlasSize = new int2(font.atlasWidth, font.atlasHeight);

                root.Face.Ascent = font.faceInfo.ascentLine;
                root.Face.Descent = font.faceInfo.descentLine;
                root.Face.Baseline = font.faceInfo.baseline;
                root.Face.Scale = font.faceInfo.scale;
                root.Face.PointSize = font.faceInfo.pointSize;
                root.Face.LineHeight = font.faceInfo.lineHeight;

                // TODO we are smooshing chars and glyphs; multiple chars can map to
                // the same glyph.  This might not be the best thing.
                int charCount = font.characterTable.Count;

                var glyphs = allocator.Allocate(ref root.Glyphs, charCount);

                // TODO why did I want a separate glyphRects?
                var glyphRects = allocator.Allocate(ref root.GlyphRects, charCount);

                var atlasSize = new float2(font.atlasWidth, font.atlasHeight);

                for (int i = 0; i < charCount; ++i)
                {
                    var c = font.characterTable[i];

                    glyphs[i] = new GlyphInfo
                    {
                        Unicode = c.unicode,
                        Size = new float2(c.glyph.metrics.width, c.glyph.metrics.height),
                        HorizontalBearing = new float2(c.glyph.metrics.horizontalBearingX, c.glyph.metrics.horizontalBearingY),
                        HorizontalAdvance = c.glyph.metrics.horizontalAdvance
                    };

                    // UVs in Tiny rendering are flipped from Unity's orientation.
                    // glyphPos is the top-left coord.
                    int2 glyphPos = new int2(c.glyph.glyphRect.x, font.atlasHeight - (c.glyph.glyphRect.y + c.glyph.glyphRect.height));
                    int2 glyphSize = new int2(c.glyph.glyphRect.width, c.glyph.glyphRect.height);

                    glyphRects[i] = new GlyphRect
                    {
                        Position = glyphPos,
                        Size = glyphSize,
                        TopLeftUV = glyphPos / atlasSize,
                        BottomRightUV = (glyphPos + glyphSize) / atlasSize,
                    };
                }

                return allocator.CreateBlobAssetReference<FontData>(allocatorType);
            }
        }

        protected override void OnUpdate()
        {
            Entities.ForEach((TMPro.TMP_FontAsset font) =>
            {
                var fontEntity = GetPrimaryEntity(font);

                BlobAssetReference<FontData> fontDataBlobRef;

                var fontGuid = new Unity.Entities.Hash128(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(font)));

                if (!BlobAssetStore.TryGet(fontGuid, out fontDataBlobRef))
                {
                    fontDataBlobRef = ConvertFontData(font);
                    if (!BlobAssetStore.TryAdd(fontGuid, fontDataBlobRef))
                        throw new InvalidOperationException("Failed to add font data blob to BlobAssetStore");
                }

                var uMaterial = font.material;

                switch ((GlyphRenderMode) font.creationSettings.renderMode)
                {
                    case GlyphRenderMode.SDF:
                    case GlyphRenderMode.SDF8:
                    case GlyphRenderMode.SDF16:
                    case GlyphRenderMode.SDF32:
                    case GlyphRenderMode.SDFAA:
                    case GlyphRenderMode.SDFAA_HINTED:
                    {
                        var clipRect = uMaterial.GetVector("_ClipRect");
                        var gradientScale = uMaterial.GetFloat("_GradientScale");
                        var sharpness = uMaterial.GetFloat("_Sharpness") + 1.0f;
                        var atlasTexture = GetPrimaryEntity(uMaterial.GetTexture("_MainTex") as Texture2D);

                        DstEntityManager.AddComponentData(fontEntity, new SDFFontMaterial()
                        {
                            AtlasTexture = atlasTexture,
                            FontData = fontDataBlobRef,
                            ClipRect = clipRect,
                            FaceColor = new float4(1.0f, 1.0f, 1.0f, 1.0f),
                            GradientScale = gradientScale,
                            Sharpness = sharpness,
                        });
                    }
                        break;
                    case GlyphRenderMode.SMOOTH:
                    case GlyphRenderMode.SMOOTH_HINTED:
                    {
                        var clipRect = uMaterial.GetVector("_ClipRect");
                        var maskX = uMaterial.GetFloat("_MaskSoftnessX");
                        var maskY = uMaterial.GetFloat("_MaskSoftnessY");
                        var atlasTexture = GetPrimaryEntity(uMaterial.GetTexture("_MainTex") as Texture2D);

                        DstEntityManager.AddComponentData(fontEntity, new BitmapFontMaterial()
                        {
                            AtlasTexture = atlasTexture,
                            FontData = fontDataBlobRef,
                            ConstClipRect = clipRect,
                            ConstMaskSoftness = new float4(maskX, maskY, 0.0f, 0.0f)
                        });
                    }
                        break;

                    default:
                        Debug.LogError($"While converting font asset '{font.name}': Render mode {(GlyphRenderMode) font.creationSettings.renderMode} is not supported.");
                        break;
                }
            });
        }
    }
}
