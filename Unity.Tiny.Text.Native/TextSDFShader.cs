using System;
using Bgfx;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Assertions;
using Unity.Tiny.Rendering;
using Unity.Transforms;

namespace Unity.Tiny.Text.Native
{
    internal struct TextSDFShader
    {
        public bgfx.ProgramHandle m_prog;

        public bgfx.UniformHandle u_FaceColor;
        public bgfx.UniformHandle u_MainTex;
        public bgfx.UniformHandle u_ClipRect;
        public bgfx.UniformHandle u_MiscP;
        #if false
        public bgfx.UniformHandle u_WorldSpaceCameraPos;

        public bgfx.UniformHandle u_TexDimScale;

        public bgfx.UniformHandle u_OutlineColor;
        public bgfx.UniformHandle u_OutlineP;

        public bgfx.UniformHandle u_UnderlayColor;
        public bgfx.UniformHandle u_UnderlayP;

        public bgfx.UniformHandle u_WeightAndMaskSoftness;

        public bgfx.UniformHandle u_ScaleRatio;

        public bgfx.UniformHandle u_ScreenParams;
        public bgfx.UniformHandle u_invModel0;
        #endif

        static bgfx.UniformHandle vec(string name, ushort count = 1)
        {
            return bgfx.create_uniform(name, bgfx.UniformType.Vec4, count);
        }

        static bgfx.UniformHandle sampler(string name, ushort count = 1)
        {
            return bgfx.create_uniform(name, bgfx.UniformType.Sampler, count);
        }

        public void Init(bgfx.ProgramHandle program)
        {
            m_prog = program;

            u_FaceColor = vec(nameof(u_FaceColor));
            u_MainTex = sampler(nameof(u_MainTex));
            u_ClipRect = vec(nameof(u_ClipRect));
            u_MiscP = vec(nameof(u_MiscP));
            #if false
            u_TexDimScale = vec(nameof(u_TexDimScale));
            u_WorldSpaceCameraPos = vec(nameof(u_WorldSpaceCameraPos));
            u_OutlineColor = vec(nameof(u_OutlineColor));
            u_OutlineP = vec(nameof(u_OutlineP));
            u_UnderlayColor = vec(nameof(u_UnderlayColor));
            u_UnderlayP = vec(nameof(u_UnderlayP));
            u_WeightAndMaskSoftness = vec(nameof(u_WeightAndMaskSoftness));
            u_ScaleRatio = vec(nameof(u_ScaleRatio));
            u_ScreenParams = vec(nameof(u_ScreenParams));
            u_invModel0 = vec(nameof(u_invModel0));
            #endif
        }

        public void Destroy()
        {
            bgfx.destroy_uniform(u_FaceColor);
            bgfx.destroy_uniform(u_MainTex);
            bgfx.destroy_uniform(u_ClipRect);
            bgfx.destroy_uniform(u_MiscP);
            #if false
            bgfx.destroy_uniform(u_TexDimScale);
            bgfx.destroy_uniform(u_WorldSpaceCameraPos);
            bgfx.destroy_uniform(u_OutlineColor);
            bgfx.destroy_uniform(u_OutlineP);
            bgfx.destroy_uniform(u_UnderlayColor);
            bgfx.destroy_uniform(u_UnderlayP);
            bgfx.destroy_uniform(u_WeightAndMaskSoftness);
            bgfx.destroy_uniform(u_ScaleRatio);
            bgfx.destroy_uniform(u_ScreenParams);
            bgfx.destroy_uniform(u_invModel0);
            bgfx.destroy_program(m_prog);
            #endif
            m_prog.idx = UInt16.MaxValue;
        }

        public bool Initialized => m_prog.idx != 0 && m_prog.idx != UInt16.MaxValue;
    }
}
