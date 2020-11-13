using System;
using Unity.Entities;
using Bgfx;
using Unity.Collections;
using Unity.Tiny.Assertions;

namespace Unity.Tiny.Rendering
{
    public static class PrecompiledShaderExtention
    {
        public static ref BlobArray<byte> PrecompiledShaderForBackend(ref this PrecompiledShader shader, bgfx.RendererType type)
        {
            switch (type)
            {
                case bgfx.RendererType.Direct3D11:
                case bgfx.RendererType.Direct3D12:
                    return ref shader.dx11;
                case bgfx.RendererType.Metal: return ref shader.metal;
                case bgfx.RendererType.OpenGLES: return ref shader.glsles;
                case bgfx.RendererType.OpenGL: return ref shader.glsl;
                case bgfx.RendererType.Vulkan: return ref shader.spirv;
                default:
                    Debug.LogFormatAlways("No shader loaded for render type: {0}.", RendererBGFXStatic.GetBackendString());
                    throw new InvalidOperationException("No shader loaded for current backend.");
            }
        }
    }

    public static class BGFXShaderHelper
    {
        public static bgfx.TextureHandle MakeUnitTexture(uint value)
        {
            bgfx.TextureHandle ret;
            unsafe {
                ret = bgfx.create_texture_2d(1, 1, false, 1, bgfx.TextureFormat.RGBA8, (ulong)bgfx.TextureFlags.None,
                    RendererBGFXStatic.CreateMemoryBlock((byte*)&value, 4));
            }
            return ret;
        }

        public static bgfx.TextureHandle MakeNoShadowTexture(bgfx.RendererType backend, ushort value)
        {
            bgfx.TextureHandle ret;
            unsafe
            {
                if (backend == bgfx.RendererType.OpenGLES)
                {
                    ret = MakeUnitTexture(0xffffffff);
                }
                else
                {
                    bgfx.Memory* mem = (bgfx.Memory*) 0;
    #if !UNITY_MACOSX
                    // on Metal desktop we can't initialize depth textures, everywhere else we can
                    // TODO this is a temporary hack to avoid us failing Metal validation
                    mem = RendererBGFXStatic.CreateMemoryBlock((byte*)&value, 2);
    #endif
                    ret = bgfx.create_texture_2d(1, 1, false, 1, bgfx.TextureFormat.D16,
                        (ulong)bgfx.SamplerFlags.UClamp | (ulong)bgfx.SamplerFlags.VClamp |
                        (ulong)bgfx.SamplerFlags.CompareLess, mem);
                }
            }
            return ret;
        }

        public static unsafe bgfx.ProgramHandle MakeProgram(bgfx.RendererType backend, byte* fs, int fsLength, byte* vs, int vsLength, string debugName)
        {
            bgfx.ShaderHandle fshandle, vshandle;

            fshandle = bgfx.create_shader(RendererBGFXStatic.CreateMemoryBlock(fs, fsLength));
            vshandle = bgfx.create_shader(RendererBGFXStatic.CreateMemoryBlock(vs, vsLength));
            Assert.IsTrue(fshandle.idx != 0xffff && vshandle.idx != 0xffff);

            bgfx.set_shader_name(vshandle, debugName, debugName.Length);
            bgfx.set_shader_name(fshandle, debugName, debugName.Length);
            var phandle = bgfx.create_program(vshandle, fshandle, true);
            if (phandle.idx == UInt16.MaxValue)
                throw new InvalidOperationException($"Failed to link shader {debugName}!");
            return phandle;
        }

        public static unsafe bgfx.ProgramHandle GetPrecompiledShaderData(bgfx.RendererType backend, ShaderBinData data, ref FixedString32 shaderName)
        {
            var vsl = data.shaders.Value.vertex.PrecompiledShaderForBackend(backend).Length;
            var fsl = data.shaders.Value.fragment.PrecompiledShaderForBackend(backend).Length;
            Assert.IsTrue(fsl > 0 && vsl > 0, "Shader binary for this backend is not present. Try re-converting the scene for the correct target.");
            var vs_ptr = (byte*)data.shaders.Value.vertex.PrecompiledShaderForBackend(backend).GetUnsafePtr();
            var fs_ptr = (byte*)data.shaders.Value.fragment.PrecompiledShaderForBackend(backend).GetUnsafePtr();

            // TODO change bgfx api to take in char* instead of string
            return MakeProgram(backend, fs_ptr, fsl, vs_ptr, vsl, shaderName.ToString());
        }
    };

    public struct LitShader
    {
        public struct MappedLight
        {
            public bgfx.UniformHandle m_samplerShadow;
            public bgfx.UniformHandle m_uniformColorIVR;
            public bgfx.UniformHandle m_uniformViewPosOrDir;
            public bgfx.UniformHandle m_uniformLightMask;
            public bgfx.UniformHandle m_uniformMatrix;
        }

        // fragment
        public bgfx.UniformHandle m_numLights;

        public bgfx.UniformHandle m_simplelightPosOrDir;
        public bgfx.UniformHandle m_simplelightColorIVR;

        public MappedLight m_mappedLight0;
        public MappedLight m_mappedLight1;
        public bgfx.UniformHandle m_texShadow01sis;

        public bgfx.UniformHandle m_samplerShadowCSM;
        public bgfx.UniformHandle m_offsetScaleCSM;
        public bgfx.UniformHandle m_sisCSM;
        public bgfx.UniformHandle m_dirCSM;
        public bgfx.UniformHandle m_colorCSM;

        public bgfx.UniformHandle m_samplerAlbedoOpacity;
        public bgfx.UniformHandle m_samplerEmissive;
        public bgfx.UniformHandle m_samplerMetal;
        public bgfx.UniformHandle m_samplerNormal;

        public bgfx.UniformHandle m_uniformAmbientProbe;
        public bgfx.UniformHandle m_uniformEmissiveNormalZScale;
        public bgfx.UniformHandle m_uniformOutputDebugSelect;

        public bgfx.UniformHandle m_uniformSmoothness;

        public bgfx.UniformHandle m_uniformFogColor;
        public bgfx.UniformHandle m_uniformFogParams;

        // vertex
        public bgfx.UniformHandle m_matrixCSM;
        public bgfx.UniformHandle m_uniformAlbedoOpacity;
        public bgfx.UniformHandle m_uniformMetalSmoothnessBillboarded;
        public bgfx.UniformHandle m_uniformTexMad;
        public bgfx.UniformHandle m_uniformModelInverseTranspose;

        // program
        public bgfx.ProgramHandle m_prog;

        private void InitMappedLight(ref MappedLight dest, string namePostFix)
        {
            // samplers
            dest.m_samplerShadow = bgfx.create_uniform("s_texShadow" + namePostFix, bgfx.UniformType.Sampler, 1);
            // fs
            dest.m_uniformColorIVR = bgfx.create_uniform("u_light_color_ivr" + namePostFix, bgfx.UniformType.Vec4, 1);
            dest.m_uniformViewPosOrDir = bgfx.create_uniform("u_light_pos" + namePostFix, bgfx.UniformType.Vec4, 1);
            dest.m_uniformLightMask = bgfx.create_uniform("u_light_mask" + namePostFix, bgfx.UniformType.Vec4, 1);
            // vs
            dest.m_uniformMatrix = bgfx.create_uniform("u_wl_light" + namePostFix, bgfx.UniformType.Mat4, 1);
        }

        private void DestroyMappedLight(ref MappedLight dest)
        {
            bgfx.destroy_uniform(dest.m_samplerShadow);
            bgfx.destroy_uniform(dest.m_uniformLightMask);
            bgfx.destroy_uniform(dest.m_uniformColorIVR);
            bgfx.destroy_uniform(dest.m_uniformViewPosOrDir);
            bgfx.destroy_uniform(dest.m_uniformMatrix);
        }

        public void Init(bgfx.ProgramHandle program)
        {
            m_prog = program;

            m_samplerAlbedoOpacity = bgfx.create_uniform("s_texAlbedoOpacity", bgfx.UniformType.Sampler, 1);
            m_samplerMetal = bgfx.create_uniform("s_texMetal", bgfx.UniformType.Sampler, 1);
            m_samplerNormal = bgfx.create_uniform("s_texNormal", bgfx.UniformType.Sampler, 1);
            m_samplerEmissive = bgfx.create_uniform("s_texEmissive", bgfx.UniformType.Sampler, 1);

            m_uniformAlbedoOpacity = bgfx.create_uniform("u_albedo_opacity", bgfx.UniformType.Vec4, 1);
            m_uniformMetalSmoothnessBillboarded = bgfx.create_uniform("u_metal_smoothness_billboarded", bgfx.UniformType.Vec4, 1);

            m_uniformAmbientProbe = bgfx.create_uniform("u_ambientProbe", bgfx.UniformType.Vec4, 7);
            m_uniformTexMad = bgfx.create_uniform("u_texmad", bgfx.UniformType.Vec4, 1);
            m_uniformModelInverseTranspose = bgfx.create_uniform("u_modelInverseTranspose", bgfx.UniformType.Mat4, 1);
            m_uniformEmissiveNormalZScale = bgfx.create_uniform("u_emissive_normalz", bgfx.UniformType.Vec4, 1);

            InitMappedLight(ref m_mappedLight0, "0");
            InitMappedLight(ref m_mappedLight1, "1");
            m_texShadow01sis = bgfx.create_uniform("u_texShadow01sis", bgfx.UniformType.Vec4, 1);

            m_samplerShadowCSM = bgfx.create_uniform("s_texShadowCSM", bgfx.UniformType.Sampler, 1);
            m_offsetScaleCSM = bgfx.create_uniform("u_csm_offset_scale", bgfx.UniformType.Vec4, 4);
            m_sisCSM = bgfx.create_uniform("u_csm_texsis", bgfx.UniformType.Vec4, 1);
            m_dirCSM = bgfx.create_uniform("u_csm_light_dir", bgfx.UniformType.Vec4, 1);
            m_colorCSM = bgfx.create_uniform("u_csm_light_color", bgfx.UniformType.Vec4, 1);
            m_matrixCSM = bgfx.create_uniform("u_wl_csm", bgfx.UniformType.Mat4, 1);


            Assert.IsTrue(LightingSetup.maxPointOrDirLights == 8); // must match array size in shader
            m_simplelightPosOrDir = bgfx.create_uniform("u_simplelight_posordir", bgfx.UniformType.Vec4, 8);
            m_simplelightColorIVR = bgfx.create_uniform("u_simplelight_color_ivr", bgfx.UniformType.Vec4, 8);

            m_numLights = bgfx.create_uniform("u_numlights", bgfx.UniformType.Vec4, 1);

            m_uniformOutputDebugSelect = bgfx.create_uniform("u_outputdebugselect", bgfx.UniformType.Vec4, 1);

            m_uniformSmoothness = bgfx.create_uniform("u_smoothness_params", bgfx.UniformType.Vec4, 1);

            m_uniformFogColor = bgfx.create_uniform("u_fogcolor", bgfx.UniformType.Vec4, 1);
            m_uniformFogParams = bgfx.create_uniform("u_fogparams", bgfx.UniformType.Vec4, 1);
        }

        public void Destroy()
        {
            bgfx.destroy_program(m_prog);
            bgfx.destroy_uniform(m_samplerAlbedoOpacity);
            bgfx.destroy_uniform(m_samplerMetal);
            bgfx.destroy_uniform(m_samplerEmissive);
            bgfx.destroy_uniform(m_samplerNormal);

            bgfx.destroy_uniform(m_uniformAlbedoOpacity);
            bgfx.destroy_uniform(m_uniformMetalSmoothnessBillboarded);

            bgfx.destroy_uniform(m_uniformAmbientProbe);
            bgfx.destroy_uniform(m_uniformEmissiveNormalZScale);
            bgfx.destroy_uniform(m_uniformOutputDebugSelect);

            bgfx.destroy_uniform(m_uniformTexMad);
            bgfx.destroy_uniform(m_uniformModelInverseTranspose);

            bgfx.destroy_uniform(m_numLights);

            DestroyMappedLight(ref m_mappedLight0);
            DestroyMappedLight(ref m_mappedLight1);
            bgfx.destroy_uniform(m_texShadow01sis);

            bgfx.destroy_uniform(m_simplelightPosOrDir);
            bgfx.destroy_uniform(m_simplelightColorIVR);

            bgfx.destroy_uniform(m_samplerShadowCSM);
            bgfx.destroy_uniform(m_offsetScaleCSM);
            bgfx.destroy_uniform(m_sisCSM);
            bgfx.destroy_uniform(m_dirCSM);
            bgfx.destroy_uniform(m_colorCSM);
            bgfx.destroy_uniform(m_matrixCSM);

            bgfx.destroy_uniform(m_uniformSmoothness);

            bgfx.destroy_uniform(m_uniformFogColor);
            bgfx.destroy_uniform(m_uniformFogParams);
        }
    }

    public struct LitSkinnedMeshShader
    {
        public LitShader m_litShader;
        // mesh skinning
        public bgfx.UniformHandle m_uniformBoneMatrices;

        public void Init(bgfx.ProgramHandle program)
        {
            m_litShader.Init(program);
            m_uniformBoneMatrices = bgfx.create_uniform("u_bone_matrices", bgfx.UniformType.Mat4, 32);
        }

        public void Destroy()
        {
            bgfx.destroy_uniform(m_uniformBoneMatrices);
            m_litShader.Destroy();
        }
    }

    public struct SimpleShader
    {
        public bgfx.ProgramHandle m_prog;
        public bgfx.UniformHandle m_samplerTexColor0;
        public bgfx.UniformHandle m_uniformColor0;
        public bgfx.UniformHandle m_uniformTexMad;
        public bgfx.UniformHandle m_uniformBillboarded;

        public void Init(bgfx.ProgramHandle program)
        {
            m_prog = program;
            m_samplerTexColor0 = bgfx.create_uniform("s_texColor", bgfx.UniformType.Sampler, 1);
            m_uniformColor0 = bgfx.create_uniform("u_color0", bgfx.UniformType.Vec4, 1);
            m_uniformTexMad = bgfx.create_uniform("u_texmad", bgfx.UniformType.Vec4, 1);
            m_uniformBillboarded = bgfx.create_uniform("u_billboarded", bgfx.UniformType.Vec4, 1);
        }

        public void Destroy()
        {
            bgfx.destroy_program(m_prog);
            bgfx.destroy_uniform(m_samplerTexColor0);
            bgfx.destroy_uniform(m_uniformColor0);
            bgfx.destroy_uniform(m_uniformTexMad);
            bgfx.destroy_uniform(m_uniformBillboarded);
        }
    }

    public struct SimpleSkinnedMeshShader
    {
        public SimpleShader m_simpleShader;
        // mesh skinning
        public bgfx.UniformHandle m_uniformBoneMatrices;

        public void Init(bgfx.ProgramHandle program)
        {
            m_simpleShader.Init(program);
            m_uniformBoneMatrices = bgfx.create_uniform("u_bone_matrices", bgfx.UniformType.Mat4, 32);
        }

        public void Destroy()
        {
            bgfx.destroy_uniform(m_uniformBoneMatrices);
            m_simpleShader.Destroy();
        }
    }

    public struct BlitShader
    {
        public bgfx.ProgramHandle m_prog;
        public bgfx.UniformHandle m_uniformTexMad;
        public bgfx.UniformHandle m_samplerTexColor0;
        public bgfx.UniformHandle m_colormul;
        public bgfx.UniformHandle m_coloradd;
        public bgfx.UniformHandle m_decodeSRGB_encodeSRGB_reinhard_premultiply;

        public void Init(bgfx.ProgramHandle program)
        {
            m_prog = program;

            m_uniformTexMad = bgfx.create_uniform("u_texmad", bgfx.UniformType.Vec4, 1);
            m_samplerTexColor0 = bgfx.create_uniform("s_texColor", bgfx.UniformType.Sampler, 1);
            m_colormul = bgfx.create_uniform("u_colormul", bgfx.UniformType.Vec4, 1);
            m_coloradd = bgfx.create_uniform("u_coloradd", bgfx.UniformType.Vec4, 1);
            m_decodeSRGB_encodeSRGB_reinhard_premultiply = bgfx.create_uniform("u_decodeSRGB_encodeSRGB_reinhard_premultiply", bgfx.UniformType.Vec4, 1);
        }

        public void Destroy()
        {
            bgfx.destroy_program(m_prog);
            bgfx.destroy_uniform(m_uniformTexMad);
            bgfx.destroy_uniform(m_samplerTexColor0);
            bgfx.destroy_uniform(m_colormul);
            bgfx.destroy_uniform(m_coloradd);
            bgfx.destroy_uniform(m_decodeSRGB_encodeSRGB_reinhard_premultiply);
        }
    }

    public struct LineShader
    {
        public bgfx.ProgramHandle m_prog;

        public void Init(bgfx.ProgramHandle program)
        {
            m_prog = program;
        }

        public void Destroy()
        {
            bgfx.destroy_program(m_prog);
        }
    }

    public struct ShadowMapShader
    {
        public bgfx.ProgramHandle m_prog;

        public bgfx.UniformHandle m_uniformBias;
        public bgfx.UniformHandle m_uniformDebugColor;

        public void Init(bgfx.ProgramHandle program)
        {
            m_prog = program;
            m_uniformBias = bgfx.create_uniform("u_bias", bgfx.UniformType.Vec4, 1);
            m_uniformDebugColor = bgfx.create_uniform("u_colorDebug", bgfx.UniformType.Vec4, 1);
        }

        public void Destroy()
        {
            bgfx.destroy_program(m_prog);
            bgfx.destroy_uniform(m_uniformBias);
            bgfx.destroy_uniform(m_uniformDebugColor);
        }
    }

    public struct SkinnedMeshShadowMapShader
    {
        public ShadowMapShader m_shadowMapShader;

        // mesh skinning
        public bgfx.UniformHandle m_uniformBoneMatrices;

        public void Init(bgfx.ProgramHandle program)
        {
            m_shadowMapShader.Init(program);
            m_uniformBoneMatrices = bgfx.create_uniform("u_bone_matrices", bgfx.UniformType.Mat4, 32);
        }

        public void Destroy()
        {
            bgfx.destroy_uniform(m_uniformBoneMatrices);
            m_shadowMapShader.Destroy();
        }
    }
}
