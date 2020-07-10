using System;
using Bgfx;

namespace Unity.Tiny.Text.Native
{
    internal struct TextShader
    {
        public bgfx.ProgramHandle m_prog;

        public bgfx.UniformHandle m_clipRect;
        public bgfx.UniformHandle m_maskSoftness;
        public bgfx.UniformHandle m_mainTex;

        public void Init(bgfx.ProgramHandle program)
        {
            m_prog = program;
            m_clipRect = bgfx.create_uniform("u_ClipRect", bgfx.UniformType.Vec4, 1);
            m_maskSoftness = bgfx.create_uniform("u_MaskSoftness", bgfx.UniformType.Vec4, 1);
            m_mainTex = bgfx.create_uniform("u_MainTex", bgfx.UniformType.Sampler, 1);
        }

        public void Destroy()
        {
            bgfx.destroy_uniform(m_clipRect);
            bgfx.destroy_uniform(m_maskSoftness);
            bgfx.destroy_uniform(m_mainTex);
            bgfx.destroy_program(m_prog);
            m_prog.idx = UInt16.MaxValue;
        }

        public bool Initialized => m_prog.idx != 0 && m_prog.idx != UInt16.MaxValue;
    }
}
