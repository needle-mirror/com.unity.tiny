using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Tiny.Text
{
    /// <summary>
    /// Runtime data for a font.  Includes information about all glyphs present,
    /// their locations in the atlas, and their metrics.
    /// </summary>
    public struct FontData
    {
        /// <summary>
        /// Base information about the font face.  (Face metrics and such.)
        /// </summary>
        public FaceInfo Face;

        /// <summary>
        /// Information about the texture atlas used for this font.
        /// </summary>
        public AtlasInfo Atlas;

        /// <summary>
        /// An array of Glyphs (unicode code points and glyph metrics)
        /// </summary>
        public BlobArray<GlyphInfo> Glyphs;

        /// <summary>
        /// An array of Glyph atlas rectangles.
        /// </summary>
        public BlobArray<GlyphRect> GlyphRects;

        /// <summary>
        /// For a given Unicode code point, find the index of the glyph
        /// in this font, or -1 if this font doesn't have this glyph.
        /// </summary>
        /// <param name="unicode">unicode code point</param>
        /// <returns>glyph index or -1 if not found</returns>
        public int FindGlyphIndexForCodePoint(uint unicode)
        {
            // TODO optimize
            for (int i = 0; i < Glyphs.Length; ++i)
            {
                if (Glyphs[i].Unicode == unicode)
                    return i;
            }

            return -1;
        }
    }

    /// <summary>
    /// Information about the texture atlas for a font.
    /// </summary>
    public struct AtlasInfo
    {
        /// <summary>
        /// The texture atlas size (in pixels).
        /// </summary>
        public int2 AtlasSize;
    }

    /// <summary>
    /// Information about the font face metrics for a font.
    /// </summary>
    public struct FaceInfo
    {
        public float Scale;
        public float PointSize;
        public float LineHeight;
        public float Ascent;
        public float Descent;
        public float Baseline;
    }

    /// <summary>
    /// Per-glyph information.
    /// </summary>
    public struct GlyphInfo
    {
        public uint Unicode;
        public float2 Size;
        public float2 HorizontalBearing;
        public float HorizontalAdvance;
    }

    public struct GlyphRect
    {
        // TODO do we actually care about these at runtime? we can reconstruct from UV coords
        public int2 Position;
        public int2 Size;

        // UV texture rect positions
        public float2 TopLeftUV; // Position / AtlasSize
        public float2 BottomRightUV; // (Position + Size) / AtlasSize
    }
}
