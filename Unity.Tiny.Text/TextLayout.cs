using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Assertions;
using Unity.Tiny.Rendering;

namespace Unity.Tiny.Text
{
    public static class TextLayout
    {
        public static void SetEntityTextRendererString(EntityManager em, Entity entity, string newText)
        {
            var buffer = em.AddBuffer<TextRendererString>(entity);
            SetEntityTextRendererString(buffer, newText);
            em.AddComponent<TextRendererNeedsUpdate>(entity);
        }

        public static void SetEntityTextRendererString(EntityCommandBuffer ecb, Entity entity, string newText)
        {
            var buffer = ecb.AddBuffer<TextRendererString>(entity);
            SetEntityTextRendererString(buffer, newText);
            ecb.AddComponent<TextRendererNeedsUpdate>(entity);
        }

        internal static void SetEntityTextRendererString(DynamicBuffer<TextRendererString> buffer,
            string newText)
        {
            buffer.Reinterpret<char>().FromString(newText);
        }

        // NB: vertexColor needs to be as srgb when in gamma space, and in linear otherwise
        public static unsafe void LayoutString(char* text, int textLength, float fontSize, HorizontalAlignment hAlign,
            VerticalAlignment vAlign, float4 vertexColor,
            ref FontData font, DynamicBuffer<DynamicSimpleVertex> dynMesh, DynamicBuffer<DynamicIndex> dynTriangles,
            out AABB aabb)
        {
            LayoutString(text, textLength, fontSize, hAlign, vAlign, vertexColor, ref font,
                dynMesh.Reinterpret<SimpleVertex>(),
                dynTriangles.Reinterpret<ushort>(),
                out aabb);
        }

        // NB: vertexColor needs to be as srgb when in gamma space, and in linear otherwise
        public static unsafe void LayoutString<T1, T2>(char* text, int textLength, float fontSize,
            HorizontalAlignment hAlign, VerticalAlignment vAlign, float4 vertexColor,
            ref FontData font, T1 mesh, T2 triangles,
            out AABB aabb)
            where T1 : INativeList<SimpleVertex>
            where T2 : INativeList<ushort>
        {
            // TODO -- unicode code points may map to 0..N glyphs.  We just assume 1:1 right now because we're not shaping
            int* glyphIndices = stackalloc int[textLength];

            // figure out the glyph indices
            int gi = 0;
            for (int ci = 0; ci < textLength; ci++)
            {
                var charUnicode = text[ci];
                int glyphIndex = font.FindGlyphIndexForCodePoint(charUnicode);

                // TODO handle tofu
                if (glyphIndex == -1)
                    continue;
                glyphIndices[gi++] = glyphIndex;
            }

            int numGlyphs = gi;

            // font  base calcs
            bool isOrtho = false; // ???
            float baseScale = fontSize / font.Face.PointSize * font.Face.Scale * (isOrtho ? 1 : 0.1f);
            float currentElementScale = baseScale;

            // allocate space in the mesh
            mesh.Length = numGlyphs * 4;

            int numLines = CountLines(text, textLength);

            float fontBaseLineOffset = font.Face.Baseline * currentElementScale;
            if (vAlign == VerticalAlignment.Top)
            {
                fontBaseLineOffset -= font.Face.Ascent * currentElementScale;
            }
            else if (vAlign == VerticalAlignment.Bottom)
            {
                fontBaseLineOffset += (-(font.Face.Descent) * currentElementScale);
                fontBaseLineOffset += (font.Face.Ascent + font.Face.Descent) * (numLines - 1);
            }
            else if (vAlign == VerticalAlignment.Center)
            {
                fontBaseLineOffset = (font.Face.Baseline - (font.Face.Ascent + font.Face.Descent) * 0.5f) *
                                     currentElementScale;
                fontBaseLineOffset += (font.Face.Ascent + font.Face.Descent) * (numLines - 1) * 0.5f;
            }

            // experimental multi line: wrap mesh generation into a while loop so we can change offset for each line
            bool keepGoing = true;
            int startIndex = 0;
            int meshIndex = 0;

            while (keepGoing)
            {
                int blockLength = StrTok('\n', text, textLength, out char* nextBlock, out int remainingTextLength);

                float xAdvance = 0.0f;
                float totalAdvance = 0.0f;
                if (hAlign != HorizontalAlignment.Left)
                {
                    for (int i = startIndex; i < startIndex + blockLength; ++i)
                    {
                        int glyphIndex = glyphIndices[i];
                        var glyph = font.Glyphs[glyphIndex];

                        totalAdvance += glyph.HorizontalAdvance * currentElementScale;
                    }

                    if (hAlign == HorizontalAlignment.Center)
                        xAdvance = -(totalAdvance / 2.0f);
                    else if (hAlign == HorizontalAlignment.Right)
                        xAdvance = -totalAdvance;
                }

                for (gi = startIndex; gi < startIndex + blockLength; ++gi)
                {
                    int glyphIndex = glyphIndices[gi];

                    var glyph = font.Glyphs[glyphIndex];
                    var glyphRect = font.GlyphRects[glyphIndex];

                    SimpleVertex topLeft = new SimpleVertex();
                    SimpleVertex bottomLeft = new SimpleVertex();
                    SimpleVertex topRight = new SimpleVertex();
                    SimpleVertex bottomRight = new SimpleVertex();

                    // TODO store this in the asset data per glyph
                    float2 tl = new float2(glyph.HorizontalBearing.x, glyph.HorizontalBearing.y);
                    float2 br = new float2(glyph.HorizontalBearing.x + glyph.Size.x,
                        glyph.HorizontalBearing.y - glyph.Size.y);

                    float2 advance = new float2(xAdvance, fontBaseLineOffset);

                    topLeft.Position = new float3(advance + tl * currentElementScale, 0.0f);
                    bottomLeft.Position = new float3(advance + new float2(tl.x, br.y) * currentElementScale, 0.0f);
                    topRight.Position = new float3(advance + new float2(br.x, tl.y) * currentElementScale, 0.0f);

                    bottomRight.Position.x = topRight.Position.x;
                    bottomRight.Position.y = bottomLeft.Position.y;
                    bottomRight.Position.z = 0.0f;

                    topLeft.TexCoord0 = glyphRect.TopLeftUV;
                    bottomLeft.TexCoord0 = new float2(glyphRect.TopLeftUV.x, glyphRect.BottomRightUV.y);
                    bottomRight.TexCoord0 = glyphRect.BottomRightUV;
                    topRight.TexCoord0 = new float2(glyphRect.BottomRightUV.x, glyphRect.TopLeftUV.y);

                    topLeft.Color = vertexColor;
                    bottomLeft.Color = vertexColor;
                    topRight.Color = vertexColor;
                    bottomRight.Color = vertexColor;

                    mesh[meshIndex + 0] = bottomLeft;
                    mesh[meshIndex + 1] = topLeft;
                    mesh[meshIndex + 2] = topRight;
                    mesh[meshIndex + 3] = bottomRight;
                    meshIndex += 4;

                    xAdvance += glyph.HorizontalAdvance * currentElementScale;
                }

                keepGoing = remainingTextLength > 0;
                fontBaseLineOffset -= (font.Face.Ascent + font.Face.Descent);

                textLength = remainingTextLength;
                text = nextBlock;
                startIndex += blockLength;
            }

            // mesh bounds
            MinMaxAABB bounds = MinMaxAABB.Empty;
            for (int i = 0; i < numGlyphs * 4; ++i)
            {
                bounds.Encapsulate(mesh[i].Position);
            }

            aabb = bounds;

            // Then make the index buffer
            triangles.Length = numGlyphs * 6;

            int index4 = 0;
            int index6 = 0;
            for (int i = 0; i < numGlyphs; i++)
            {
                triangles[index6 + 0] = (ushort) (index4 + 0);
                triangles[index6 + 1] = (ushort) (index4 + 1);
                triangles[index6 + 2] = (ushort) (index4 + 2);
                triangles[index6 + 3] = (ushort) (index4 + 2);
                triangles[index6 + 4] = (ushort) (index4 + 3);
                triangles[index6 + 5] = (ushort) (index4 + 0);

                index4 += 4;
                index6 += 6;
            }
        }

        // Returns the length of the string until the first token, tok, is found
        // Also returns information for further tokenizing a string
        static unsafe int StrTok(char tok, char* text, int textLength, out char* nextBlock, out int remainingLength)
        {
            nextBlock = null;
            remainingLength = 0;

            if (textLength == 0)
            {
                return 0;
            }

            for (int i = 0; i < textLength; i++)
            {
                Assert.IsTrue(text[i] != '\0');
                if (text[i] == tok)
                {
                    nextBlock = text + i + 1;
                    remainingLength = textLength - i - 1;
                    return i; // the length of the chunk before the token
                }
            }

            return textLength;
        }

        static unsafe int CountLines(char* text, int textLength)
        {
            // Ignores trailing newlines
            int count = 1;
            for (int i = 0; i < textLength - 1; i++)
            {
                if (text[i] == '\n')
                {
                    count++;
                }
            }

            return count;
        }
    }
}
