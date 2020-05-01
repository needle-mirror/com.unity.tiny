using System;
using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Tiny
{
    /// <summary>
    /// RGBA floating-point color.
    /// </summary>
    public struct Color : IEquatable<Color>
    {
        public static Color Default { get; } = new Color(1f, 1f, 1f, 1f);

        public float4 Value;

        /// <summary> Red value, range is [0..1] </summary>
        public float r { get => Value.x; set => Value.x = value; }
        /// <summary> Green value, range is [0..1] </summary>
        public float g { get => Value.y; set => Value.y = value; }
        /// <summary> Blue value, range is [0..1] </summary>
        public float b { get => Value.z; set => Value.z = value; }
        /// <summary> Alpha value, range is [0..1] </summary>
        public float a { get => Value.w; set => Value.w = value; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Color(float red, float green, float blue, float alpha = 1f)
        {
            Value = new float4(red, green, blue, alpha);
        }

        public static bool operator==(Color cl, Color cr)
        {
            return cl.r == cr.r && cl.g == cr.g && cl.b == cr.b && cl.a == cr.a;
        }

        public static Color operator+(Color cl, Color cr)
        {
            return new Color(cl.r + cr.r, cl.g + cr.g, cl.b + cr.b, cl.a + cr.a);
        }

        public static Color operator*(Color cl, Color cr)
        {
            return new Color(cl.r * cr.r, cl.g * cr.g, cl.b * cr.b, cl.a * cr.a);
        }

        public static Color operator*(Color cl, float v)
        {
            return new Color(cl.r * v, cl.g * v, cl.b * v, cl.a * v);
        }

        public static bool operator!=(Color cl, Color cr)
        {
            return !(cl == cr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Color c)
        {
            return this == c;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            return Equals((Color)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = r.GetHashCode();
                hashCode = (hashCode * 397) ^ g.GetHashCode();
                hashCode = (hashCode * 397) ^ b.GetHashCode();
                hashCode = (hashCode * 397) ^ a.GetHashCode();
                return hashCode;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color Lerp(Color c1, Color c2, float time)
        {
            return c1 * (1.0f - time) + c2 * time;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float4 AsFloat4()
        {
            return new float4(r, g, b, a);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromFloat4(float4 c)
        {
            r = c.x;
            g = c.y;
            b = c.z;
            a = c.w;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LinearToSRGB(float x)
        {
            if (x <= 0.0031308f) return x * 12.92f;
            if (x >= 1.0f) return 1.0f;
            return math.pow(x, 1.0f / 2.4f) * 1.055f - 0.055f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SRGBToLinear(float x)
        {
            if (x < 0.04045f) return x * (1.0f / 12.92f);
            if (x >= 1.0f) return 1.0f;
            return math.pow((x + 0.055f) / 1.055f, 2.4f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float4 ToLinear()
        {
            return new float4(SRGBToLinear(r), SRGBToLinear(g), SRGBToLinear(b), a);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 LinearToSRGB(float3 rgb)
        {
            return new float3(
                LinearToSRGB(rgb.x),
                LinearToSRGB(rgb.y),
                LinearToSRGB(rgb.z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 LinearToSRGB(float4 rgba)
        {
            return new float4(
                LinearToSRGB(rgba.x),
                LinearToSRGB(rgba.y),
                LinearToSRGB(rgba.z),
                rgba.w);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 SRGBToLinear(float3 rgb)
        {
            return new float3(
                SRGBToLinear(rgb.x),
                SRGBToLinear(rgb.y),
                SRGBToLinear(rgb.z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 SRGBToLinear(float4 rgba)
        {
            return new float4(
                SRGBToLinear(rgba.x),
                SRGBToLinear(rgba.y),
                SRGBToLinear(rgba.z),
                rgba.w);
        }
    }

    /// <summary>
    /// Blending operation when drawing
    /// </summary>
    public enum BlendOp
    {
        /// <summary> Default. Normal alpha blending. </summary>
        Alpha,

        /// <summary> Additive blending. Only brightens colors. Black is neutral and has no effect.. </summary>
        Add,

        /// <summary> Multiplicative blending. Only darken colors. White is neutral and has no effect. </summary>
        Multiply,

        /// <summary>
        /// Multiplies the target by the source alpha.
        /// Only the source alpha channel is used.
        /// Drawing using this mode is useful when rendering to a textures to mask borders.
        /// </summary>
        MultiplyAlpha,

        /// <summary> Do not perform any blending. </summary>
        Disabled
    }

    /// <summary>
    /// Add this compoment next to a RectTransform component and a Text2DRenderer (for now)
    /// while adding a text in a rect transform
    /// </summary>
    //[HideInInspector]
    public struct RectTransformFinalSize : ISystemStateComponentData
    {
        /// <summary>
        /// Rect transform size of an entity.
        /// This value is updated by the SetRectTransformSizeSystem system
        /// </summary>
        public float2 size;
    }
}
