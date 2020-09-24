using System;
using System.Collections.Generic;

namespace Unity.Tiny.ShaderCompiler
{
    static class BgfxHelper
    {
        internal const uint BGFX_SHADER_BIN_VERSION = 8;
        internal const uint BGFX_SHADERC_VERSION_MAJOR = 1;
        internal const uint BGFX_SHADERC_VERSION_MINOR = 16;
        internal static uint BGFX_CHUNK_MAGIC_CSH = BX_MAKEFOURCC('C', 'S', 'H', BGFX_SHADER_BIN_VERSION);
        internal static uint BGFX_CHUNK_MAGIC_FSH = BX_MAKEFOURCC('F', 'S', 'H', BGFX_SHADER_BIN_VERSION);
        internal static uint BGFX_CHUNK_MAGIC_VSH = BX_MAKEFOURCC('V', 'S', 'H', BGFX_SHADER_BIN_VERSION);

        internal const ushort BGFX_UNIFORM_FRAGMENTBIT = 0x10;
        internal const ushort BGFX_UNIFORM_SAMPLERBIT = 0x20;

        internal static uint BX_MAKEFOURCC(char _a, char _b, char _c, uint _d)
        {
            return _a | ((uint)_b << 8) | ((uint)_c << 16) | (_d << 24);
        }
        internal static uint BX_MAKEFOURCC(char _a, char _b, char _c, char _d)
        {
            return BX_MAKEFOURCC(_a, _b, _c, (uint)_d);
        }

        /// <remarks>
        /// See https://bkaradzic.github.io/bgfx/bgfx.html#_CPPv4N4bgfx6Attrib4EnumE
        /// </remarks>
        internal static string GetName(Attrib attrib)
        {
            switch (attrib)
            {
                case Attrib.Position:
                    return "a_position";
                case Attrib.Normal:
                    return "a_normal";
                case Attrib.Tangent:
                    return "a_tangent";
                case Attrib.Bitangent:
                    return "a_bitangent";
                case Attrib.Color0:
                    return "a_color0";
                case Attrib.Color1:
                    return "a_color1";
                case Attrib.Color2:
                    return "a_color2";
                case Attrib.Color3:
                    return "a_color3";
                case Attrib.Indices:
                    return "a_indices";
                case Attrib.Weight:
                    return "a_weight";
                case Attrib.TexCoord0:
                    return "a_texcoord0";
                case Attrib.TexCoord1:
                    return "a_texcoord1";
                case Attrib.TexCoord2:
                    return "a_texcoord2";
                case Attrib.TexCoord3:
                    return "a_texcoord3";
                case Attrib.TexCoord4:
                    return "a_texcoord4";
                case Attrib.TexCoord5:
                    return "a_texcoord5";
                case Attrib.TexCoord6:
                    return "a_texcoord6";
                case Attrib.TexCoord7:
                    return "a_texcoord7";
                default:
                    throw new ArgumentOutOfRangeException(nameof(attrib), attrib, null);
            }
        }

        internal enum UniformType
        {
            Sampler, //!< Sampler.
            End,     //!< Reserved, do not use.

            Vec4,    //!< 4 floats vector.
            Mat3,    //!< 3x3 matrix.
            Mat4,    //!< 4x4 matrix.

            Count
        }

        internal enum Attrib
        {
            Position,  //!< a_position
            Normal,    //!< a_normal
            Tangent,   //!< a_tangent
            Bitangent, //!< a_bitangent
            Color0,    //!< a_color0
            Color1,    //!< a_color1
            Color2,    //!< a_color2
            Color3,    //!< a_color3
            Indices,   //!< a_indices
            Weight,    //!< a_weight
            TexCoord0, //!< a_texcoord0
            TexCoord1, //!< a_texcoord1
            TexCoord2, //!< a_texcoord2
            TexCoord3, //!< a_texcoord3
            TexCoord4, //!< a_texcoord4
            TexCoord5, //!< a_texcoord5
            TexCoord6, //!< a_texcoord6
            TexCoord7, //!< a_texcoord7

            Count
        }

        internal static Dictionary<Attrib, ushort> s_AttribToId = new Dictionary<Attrib, ushort>
        {
            // NOTICE:
            // Attrib must be in order how it appears in Attrib::Enum! id is
            // unique and should not be changed if new Attribs are added.
            { Attrib.Position,  0x0001 },
            { Attrib.Normal,    0x0002 },
            { Attrib.Tangent,   0x0003 },
            { Attrib.Bitangent, 0x0004 },
            { Attrib.Color0,    0x0005 },
            { Attrib.Color1,    0x0006 },
            { Attrib.Color2,    0x0018 },
            { Attrib.Color3,    0x0019 },
            { Attrib.Indices,   0x000e },
            { Attrib.Weight,    0x000f },
            { Attrib.TexCoord0, 0x0010 },
            { Attrib.TexCoord1, 0x0011 },
            { Attrib.TexCoord2, 0x0012 },
            { Attrib.TexCoord3, 0x0013 },
            { Attrib.TexCoord4, 0x0014 },
            { Attrib.TexCoord5, 0x0015 },
            { Attrib.TexCoord6, 0x0016 },
            { Attrib.TexCoord7, 0x0017 }
        };
    }
}
