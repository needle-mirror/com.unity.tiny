using System;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using Bgfx;

namespace Unity.Tiny.Rendering
{
    // submit helpers for bgfx
    internal static class SubmitHelper
    {
        // ---------------- shadow map ----------------------------------------------------------------------------------------------------------------------

        public static unsafe void SubmitShadowMapMeshDirect(RendererBGFXInstance* sys, ushort viewId, ref MeshBGFX mesh, ref float4x4 tx, int startIndex, int indexCount, byte flipCulling, float4 bias)
        {
            bgfx.Encoder* encoder = bgfx.encoder_begin(false);
            EncodeShadowMapMesh(sys, encoder, viewId, ref mesh, ref tx, startIndex, indexCount, flipCulling, bias);
            bgfx.encoder_end(encoder);
        }

        public static unsafe void SubmitSimpleShadowMapTransientDirect(RendererBGFXInstance* sys, bgfx.TransientIndexBuffer* tib, bgfx.TransientVertexBuffer* tvb, int nvertices, int nindices, ushort viewId, ref float4x4 tx, byte flipCulling, float4 bias)
        {
            bgfx.Encoder* encoder = bgfx.encoder_begin(false);
            EncodeSimpleShadowMapTransient(sys, encoder, tib, tvb, nvertices, nindices, viewId, ref tx, flipCulling, bias);
            bgfx.encoder_end(encoder);
        }


        public static unsafe void EncodeShadowMapMesh(RendererBGFXInstance* sys, bgfx.Encoder* encoder, ushort viewId, ref MeshBGFX mesh, ref float4x4 tx,
            int startIndex, int indexCount, byte flipCulling, float4 bias)
        {
            mesh.SetForSubmit(encoder, startIndex, indexCount);
            EncodeShadowMap(sys, encoder, ref sys->m_shadowMapShader, viewId, ref tx, flipCulling, bias);
        }

        public static unsafe void EncodeShadowMapSkinnedMesh(RendererBGFXInstance* sys, bgfx.Encoder* encoder, ushort viewId, ref MeshBGFX mesh, ref float4x4 tx,
            int startIndex, int indexCount, byte flipCulling, float4 bias, float4x4[] boneMatrices)
        {
            mesh.SetForSubmit(encoder, startIndex, indexCount);
            fixed (float4x4* p = boneMatrices) {
                bgfx.encoder_set_uniform(encoder, sys->m_skinnedMeshShadowMapShader.m_uniformBoneMatrices, p, (ushort)boneMatrices.Length);
            }
            EncodeShadowMap(sys, encoder, ref sys->m_skinnedMeshShadowMapShader.m_shadowMapShader, viewId, ref tx, flipCulling, bias);
        }

        public static unsafe void EncodeShadowMapTransient(RendererBGFXInstance* sys, bgfx.Encoder* encoder, bgfx.TransientIndexBuffer* tib, bgfx.TransientVertexBuffer* tvb, int nvertices, int nindices,
            ushort viewId, ref float4x4 tx, byte flipCulling, float4 bias)
        {
            EncodeLitTransientBuffers(sys, encoder, tib, tvb, nvertices, nindices);
            EncodeShadowMap(sys, encoder, ref sys->m_shadowMapShader, viewId, ref tx, flipCulling, bias);
        }

        public static unsafe void EncodeSimpleShadowMapTransient(RendererBGFXInstance* sys, bgfx.Encoder* encoder, bgfx.TransientIndexBuffer* tib, bgfx.TransientVertexBuffer* tvb, int nvertices, int nindices,
            ushort viewId, ref float4x4 tx, byte flipCulling, float4 bias)
        {
            EncodeSimpleTransientBuffers(sys, encoder, tib, tvb, nvertices, nindices);
            EncodeShadowMap(sys, encoder, ref sys->m_shadowMapShader, viewId, ref tx, flipCulling, bias);
        }

        // For uniforms and shaders setup. Does not handle vertex/index buffers
        private static unsafe void EncodeShadowMap(RendererBGFXInstance* sys, bgfx.Encoder* encoder, ref ShadowMapShader shadowMapShader, ushort viewId, ref float4x4 tx, byte flipCulling, float4 bias)
        {
            ulong state = (ulong)(bgfx.StateFlags.WriteZ | bgfx.StateFlags.DepthTestLess | bgfx.StateFlags.CullCcw);
            if (flipCulling != 0) state = FlipCulling(state);
#if DEBUG
            state |= (ulong)bgfx.StateFlags.WriteRgb | (ulong)bgfx.StateFlags.WriteA;
            float4 c = new float4(1);
            bgfx.encoder_set_uniform(encoder, shadowMapShader.m_uniformDebugColor, &c, 1);
#endif
            bgfx.encoder_set_state(encoder, state, 0);
            unsafe { fixed (float4x4* p = &tx) bgfx.encoder_set_transform(encoder, p, 1); }
            bgfx.encoder_set_uniform(encoder, shadowMapShader.m_uniformBias, &bias, 1);
            bgfx.encoder_submit(encoder, viewId, shadowMapShader.m_prog, 0, (byte)bgfx.DiscardFlags.All);
        }

        // ---------------- debug line rendering helper ----------------------------------------------------------------------------------------------------------------------
        private static bool ClipLinePositive(ref float4 p0, ref float4 p1, int coord)
        {
            bool isinside0 = p0[coord] < p0.w;
            bool isinside1 = p1[coord] < p1.w;
            if (isinside0 && isinside1) // no clipping
                return true;
            if (!isinside0 && !isinside1) // all out
                return false;
            float4 d = p1 - p0;
            float t = (p0[coord] - p0.w) / (d.w - d[coord]); // p = p0 + d * t && p.z = p.w
            if (!(t >= 0.0f && t <= 1.0f)) // can happen when d.w==d[coord]
                return false;
            float4 p = p0 + d * t;
            if (!isinside0) p0 = p;
            else p1 = p;
            return true;
        }

        private static bool ClipLineNegative(ref float4 p0, ref float4 p1, int coord)
        {
            bool isinside0 = p0[coord] >= -p0.w;
            bool isinside1 = p1[coord] >= -p1.w;
            if (isinside0 && isinside1) // no clipping
                return true;
            if (!isinside0 && !isinside1) // all out
                return false;
            float4 d = p1 - p0;
            float t = (p0[coord] + p0.w) / (-d.w - d[coord]); // p = p0 + d * t && p[coord] = -p.w
            if (!(t >= 0.0f && t <= 1.0f)) // can happen when d.w==d[coord]
                return false;
            float4 p = p0 + d * t;
            if (!isinside0) p0 = p;
            else p1 = p;
            return true;
        }

        public static unsafe void EncodeLine(RendererBGFXInstance* sys, bgfx.Encoder* encoder, ushort viewId, float3 p0, float3 p1, float4 color, float2 width, ref float4x4 objTx, ref float4x4 viewTx, ref float4x4 projTx)
        {
            float4 p0t = math.mul(projTx,
                math.mul(viewTx,
                    math.mul(objTx, new float4(p0, 1))));
            float4 p1t = math.mul(projTx,
                math.mul(viewTx,
                    math.mul(objTx, new float4(p1, 1))));
            for (int i = 0; i < 3; i++)   // really only need to clip z near, but clip all to make sure clipping works
            {
                if (!ClipLinePositive(ref p0t, ref p1t, i))
                    return;
                if (!ClipLineNegative(ref p0t, ref p1t, i))
                    return;
            }
            SimpleVertex* buf = stackalloc SimpleVertex[4];
            p0t.xyz *= 1.0f / p0t.w;
            p1t.xyz *= 1.0f / p1t.w;
            float2 dp = math.normalizesafe(p1t.xy - p0t.xy);
            float2 dprefl = new float2(-dp.y, dp.x);
            float3 dv = new float3(dprefl * width, 0);
            float3 du = new float3(dp * width * .5f, 0);
            buf[0].Position = p0t.xyz + dv - du; buf[0].Color = color; buf[0].TexCoord0 = new float2(0, 1);
            buf[1].Position = p0t.xyz - dv - du; buf[1].Color = color; buf[1].TexCoord0 = new float2(0, -1);
            buf[2].Position = p1t.xyz - dv + du; buf[2].Color = color; buf[2].TexCoord0 = new float2(1, -1);
            buf[3].Position = p1t.xyz + dv + du; buf[3].Color = color; buf[3].TexCoord0 = new float2(1, 1);
            EncodeLinePreTransformed(sys, encoder, viewId, buf, 4);
        }

        public static unsafe void EncodeDebugTangents(RendererBGFXInstance* sys, bgfx.Encoder* encoder, ushort viewId, float2 width, float length, ref LitMeshRenderData mesh, ref float4x4 objTx, ref float4x4 viewTx, ref float4x4 projTx)
        {
            int nv = (int)mesh.Mesh.Value.Vertices.Length;
            LitVertex* vertices = (LitVertex*)mesh.Mesh.Value.Vertices.GetUnsafePtr();
            for (int i = 0; i < nv; i++) {
                EncodeLine(sys, encoder, viewId, vertices[i].Position, vertices[i].Position + vertices[i].Normal * length, new float4(0, 0, 1, 1), width, ref objTx, ref viewTx, ref projTx);
                EncodeLine(sys, encoder, viewId, vertices[i].Position, vertices[i].Position + vertices[i].Tangent * length, new float4(1, 0, 0, 1), width, ref objTx, ref viewTx, ref projTx);
            }
        }

        public static unsafe void EncodeLinePreTransformed(RendererBGFXInstance* sys, bgfx.Encoder* encoder, ushort viewId, SimpleVertex* vertices, int n)
        {
            bgfx.TransientIndexBuffer tib;
            bgfx.TransientVertexBuffer tvb;
            int ni = (n / 4) * 6;
            if (!bgfx.alloc_transient_buffers(&tvb, &sys->m_simpleVertexBufferDecl, (uint)n, &tib, (uint)ni))
                throw new InvalidOperationException("Out of transient bgfx memory!");
            UnsafeUtility.MemCpy((SimpleVertex*)tvb.data, vertices, sizeof(SimpleVertex) * n);
            ushort* indices = (ushort*)tib.data;
            for (int i = 0; i < n; i += 4) {
                indices[0] = (ushort)i; indices[1] = (ushort)(i + 1); indices[2] = (ushort)(i + 2);
                indices[3] = (ushort)(i + 2); indices[4] = (ushort)(i + 3); indices[5] = (ushort)i;
                indices += 6;
            }
            bgfx.encoder_set_transient_index_buffer(encoder, &tib, 0, (uint)ni);
            bgfx.encoder_set_transient_vertex_buffer(encoder, 0, &tvb, 0, (uint)n, sys->m_simpleVertexBufferDeclHandle);

            // material uniforms setup
            ulong state = (ulong)(bgfx.StateFlags.DepthTestLess | bgfx.StateFlags.WriteRgb) | RendererBGFXStatic.MakeBGFXBlend(bgfx.StateFlags.BlendOne, bgfx.StateFlags.BlendInvSrcAlpha);
            bgfx.encoder_set_state(encoder, state, 0);
            bgfx.encoder_submit(encoder, viewId, sys->m_lineShader.m_prog, 0, (byte)bgfx.DiscardFlags.All);
        }

        // ---------------- simple, lit, with mesh ----------------------------------------------------------------------------------------------------------------------
        public static ulong FlipCulling(ulong state)
        {
            ulong cull = state & (ulong)bgfx.StateFlags.CullMask;
            ulong docull = cull >> (int)bgfx.StateFlags.CullShift;
            docull = ((docull >> 1) ^ docull) & 1;
            docull = docull | (docull << 1);
            ulong r = state ^ (docull << (int)bgfx.StateFlags.CullShift);
            return r;
        }

        private unsafe static void EncodeMappedLight(bgfx.Encoder* encoder, ref MappedLightBGFX light, ref LitShader.MappedLight shader, byte samplerOffset, float4 viewPosOrDir)
        {
            fixed (float4x4* p = &light.projection)
                bgfx.encoder_set_uniform(encoder, shader.m_uniformMatrix, p, 1);
            fixed (float4* p = &light.color_invrangesqr)
                bgfx.encoder_set_uniform(encoder, shader.m_uniformColorIVR, p, 1);
            fixed (float4* p = &light.mask)
                bgfx.encoder_set_uniform(encoder, shader.m_uniformLightMask, p, 1);
            bgfx.encoder_set_uniform(encoder, shader.m_uniformViewPosOrDir, &viewPosOrDir, 1);
            bgfx.encoder_set_texture(encoder, (byte)(4 + samplerOffset), shader.m_samplerShadow, light.shadowMap, UInt32.MaxValue);
        }

        public static unsafe void EncodeLitMesh(RendererBGFXInstance* sys, bgfx.Encoder* encoder, ushort viewId, ref MeshBGFX mesh, ref float4x4 tx,
            ref LitMaterialBGFX mat, ref LightingBGFX lighting, ref float4x4 viewTx, int startIndex, int indexCount,
            byte flipCulling, ref LightingViewSpaceBGFX viewSpaceLightCache, uint depth)
        {
            mesh.SetForSubmit(encoder, startIndex, indexCount);
            EncodeLit(sys, encoder, ref sys->m_litShader, mat.shaderProgram, viewId, ref tx, ref mat, ref lighting, ref viewTx, flipCulling, ref viewSpaceLightCache, depth);
        }

        public static unsafe void EncodeLitSkinnedMesh(RendererBGFXInstance* sys, bgfx.Encoder* encoder, ushort viewId, ref MeshBGFX mesh, ref float4x4 tx,
            ref LitMaterialBGFX mat, ref LightingBGFX lighting, ref float4x4 viewTx, int startIndex, int indexCount,
            byte flipCulling, ref LightingViewSpaceBGFX viewSpaceLightCache, uint depth, float4x4[] boneMatrices)
        {
            mesh.SetForSubmit(encoder, startIndex, indexCount);
            fixed (float4x4* p = boneMatrices) {
                bgfx.encoder_set_uniform(encoder, sys->m_litSkinnedMeshShader.m_uniformBoneMatrices, p, (ushort)boneMatrices.Length);
            }
            EncodeLit(sys, encoder, ref sys->m_litSkinnedMeshShader.m_litShader, sys->m_litSkinnedMeshShader.m_litShader.m_prog, viewId, ref tx, ref mat, ref lighting, ref viewTx, flipCulling, ref viewSpaceLightCache, depth);
        }

        // For uniforms and shaders setup. Does not handle vertex/index buffers
        private unsafe static void EncodeLit(RendererBGFXInstance* sys, bgfx.Encoder* encoder, ref LitShader litShader, bgfx.ProgramHandle prog, ushort viewId, ref float4x4 tx, ref LitMaterialBGFX mat,
            ref LightingBGFX lighting, ref float4x4 viewTx, byte flipCulling, ref LightingViewSpaceBGFX viewSpaceLightCache, uint depth)
        {
            ulong state = mat.state;
            if (flipCulling != 0)
                state = FlipCulling(state);
            bgfx.encoder_set_state(encoder, state, 0);
            fixed (float4x4* p = &tx)
                bgfx.encoder_set_transform(encoder, p, 1);
            float3x3 minvt = math.transpose(math.inverse(new float3x3(tx.c0.xyz, tx.c1.xyz, tx.c2.xyz)));
            float4x4 minvtTemp = new float4x4(minvt, float3.zero);
            //float3x3 minvt = new float3x3(tx.c0.xyz, tx.c1.xyz, tx.c2.xyz);
            bgfx.encoder_set_uniform(encoder, litShader.m_uniformModelInverseTranspose, &minvtTemp, 1);
            // material uniforms setup
            fixed (float4* p = &mat.constAlbedo_Opacity)
                bgfx.encoder_set_uniform(encoder, litShader.m_uniformAlbedoOpacity, p, 1);
            fixed (float4* p = &mat.constMetal_Smoothness_Billboarded)
                bgfx.encoder_set_uniform(encoder, litShader.m_uniformMetalSmoothnessBillboarded, p, 1);
            fixed (float4* p = &mat.constEmissive_normalMapZScale)
                bgfx.encoder_set_uniform(encoder, litShader.m_uniformEmissiveNormalZScale, p, 1);
            float4 debugVect = sys->m_outputDebugSelect;
            bgfx.encoder_set_uniform(encoder, litShader.m_uniformOutputDebugSelect, &debugVect, 1);
            fixed (float4* p = &mat.smoothness)
                bgfx.encoder_set_uniform(encoder, litShader.m_uniformSmoothness, p, 1);

            // textures
            bgfx.encoder_set_texture(encoder, 0, litShader.m_samplerAlbedoOpacity, mat.texAlbedoOpacity, UInt32.MaxValue);
            bgfx.encoder_set_texture(encoder, 3, litShader.m_samplerMetal, mat.texMetal, UInt32.MaxValue);
            bgfx.encoder_set_texture(encoder, 1, litShader.m_samplerNormal, mat.texNormal, UInt32.MaxValue);

            bgfx.encoder_set_texture(encoder, 2, litShader.m_samplerEmissive, mat.texEmissive, UInt32.MaxValue);

            fixed (float4* p = &mat.mainTextureScaleTranslate)
                bgfx.encoder_set_uniform(encoder, litShader.m_uniformTexMad, p, 1);

            // ambient
            fixed (float4* p = &lighting.ambientProbe.SHAr)
                bgfx.encoder_set_uniform(encoder, litShader.m_uniformAmbientProbe, p, 7);

            // transform lighting to view space, if needed: this only needs to re-compute if the viewId changed
            // also the lighting view space is per-thread, hence it is passed in
            lighting.TransformToViewSpace(ref viewTx, ref viewSpaceLightCache, viewId);

            // dir or point lights
            fixed (float* p = viewSpaceLightCache.podl_positionOrDirViewSpace)
                bgfx.encoder_set_uniform(encoder, litShader.m_simplelightPosOrDir, p, (ushort)lighting.numPointOrDirLights);
            fixed (float* p = lighting.podl_colorIVR)
                bgfx.encoder_set_uniform(encoder, litShader.m_simplelightColorIVR, p, (ushort)lighting.numPointOrDirLights);

            // mapped lights (always have to set those or there are undefined samplers)
            EncodeMappedLight(encoder, ref lighting.mappedLight0, ref litShader.m_mappedLight0, 0, viewSpaceLightCache.mappedLight0_viewPosOrDir); // sampler 4
            EncodeMappedLight(encoder, ref lighting.mappedLight1, ref litShader.m_mappedLight1, 1, viewSpaceLightCache.mappedLight1_viewPosOrDir); // sampler 5
            fixed (float4* p = &lighting.mappedLight01sis)
                bgfx.encoder_set_uniform(encoder, litShader.m_texShadow01sis, p, 1);

            // csm
            fixed (float4* p = &viewSpaceLightCache.csmLight_viewPosOrDir)
                bgfx.encoder_set_uniform(encoder, litShader.m_dirCSM, p, 1);
            fixed (float* p = lighting.csmOffsetScale)
                bgfx.encoder_set_uniform(encoder, litShader.m_offsetScaleCSM, p, 4);
            fixed (float4* p = &lighting.csmLight.color_invrangesqr)
                bgfx.encoder_set_uniform(encoder, litShader.m_colorCSM, p, 1);
            fixed (float4x4* p = &lighting.csmLight.projection)
                bgfx.encoder_set_uniform(encoder, litShader.m_matrixCSM, p, 1);
            fixed (float4* p = &lighting.csmLightsis)
                bgfx.encoder_set_uniform(encoder, litShader.m_sisCSM, p, 1);

            bgfx.encoder_set_texture(encoder, 6, litShader.m_samplerShadowCSM, lighting.csmLight.shadowMap, UInt32.MaxValue);  // sampler 6

            float4 numlights = new float4(lighting.numPointOrDirLights, lighting.numMappedLights, lighting.numCsmLights, 0.0f);
            bgfx.encoder_set_uniform(encoder, litShader.m_numLights, &numlights, 1);

            // fog
            fixed (float4* p = &lighting.fogColor)
                bgfx.encoder_set_uniform(encoder, litShader.m_uniformFogColor, p, 1);
            fixed (float4* p = &lighting.fogParams)
                bgfx.encoder_set_uniform(encoder, litShader.m_uniformFogParams, p, 1);

            // submit
            bgfx.encoder_submit(encoder, viewId, prog, depth, (byte)bgfx.DiscardFlags.All);
        }

        // ---------------- simple, unlit, with mesh ----------------------------------------------------------------------------------------------------------------------
        public static unsafe void SubmitSimpleMeshDirect(RendererBGFXInstance* sys, ushort viewId, ref MeshBGFX mesh, ref float4x4 tx, ref SimpleMaterialBGFX mat, int startIndex, int indexCount, byte flipCulling, uint depth)
        {
            bgfx.Encoder* encoder = bgfx.encoder_begin(false);
            EncodeSimpleMesh(sys, encoder, viewId, ref mesh, ref tx, ref mat, startIndex, indexCount, flipCulling, depth);
            bgfx.encoder_end(encoder);
        }

        public static unsafe void EncodeSimpleMesh(RendererBGFXInstance* sys, bgfx.Encoder* encoder, ushort viewId, ref MeshBGFX mesh, ref float4x4 tx, ref SimpleMaterialBGFX mat, int startIndex, int indexCount, byte flipCulling, uint depth)
        {
            mesh.SetForSubmit(encoder, startIndex, indexCount);
            EncodeSimple(sys, encoder, ref sys->m_simpleShader, viewId, ref tx, ref mat, flipCulling, depth);
        }

        public static unsafe void EncodeSimpleSkinnedmesh(RendererBGFXInstance* sys, bgfx.Encoder* encoder, ushort viewId, ref MeshBGFX mesh, ref float4x4 tx, ref SimpleMaterialBGFX mat,
            int startIndex, int indexCount, byte flipCulling, uint depth, float4x4[] boneMatrices)
        {
            mesh.SetForSubmit(encoder, startIndex, indexCount);
            fixed (float4x4* p = boneMatrices) {
                bgfx.encoder_set_uniform(encoder, sys->m_simpleSkinnedMeshShader.m_uniformBoneMatrices, p, (ushort)boneMatrices.Length);
            }
            EncodeSimple(sys, encoder, ref sys->m_simpleSkinnedMeshShader.m_simpleShader, viewId, ref tx, ref mat, flipCulling, depth);
        }

        // For uniforms and shaders setup. Does not handle vertex/index buffers
        private static unsafe void EncodeSimple(RendererBGFXInstance* sys, bgfx.Encoder* encoder, ref SimpleShader simpleShader, ushort viewId, ref float4x4 tx, ref SimpleMaterialBGFX mat, byte flipCulling, uint depth)
        {

            ulong state = mat.state;
            if (flipCulling != 0)
                state = FlipCulling(state);
            bgfx.set_state(state, 0);
            fixed (float4x4* p = &tx)
                bgfx.encoder_set_transform(encoder, p, 1);
            // material uniforms setup
            fixed (float4* p = &mat.constAlbedo_Opacity)
                bgfx.encoder_set_uniform(encoder, simpleShader.m_uniformColor0, p, 1);
            fixed (float4* p = &mat.mainTextureScaleTranslate)
                bgfx.encoder_set_uniform(encoder, simpleShader.m_uniformTexMad, p, 1);
            fixed (float4* p = &mat.billboarded)
                bgfx.encoder_set_uniform(encoder, simpleShader.m_uniformBillboarded, p, 1);
            bgfx.encoder_set_texture(encoder, 0, simpleShader.m_samplerTexColor0, mat.texAlbedoOpacity, UInt32.MaxValue);
            bgfx.encoder_submit(encoder, viewId, simpleShader.m_prog, depth, (byte)bgfx.DiscardFlags.All);
        }

        // ---------------- blit ----------------------------------------------------------------------------------------------------------------------
        public static unsafe void SubmitBlitDirectFast(RendererBGFXInstance* sys, ushort viewId, ref float4x4 tx, float4 color, bgfx.TextureHandle tetxure)
        {
            unsafe {
                bgfx.Encoder* encoder = bgfx.encoder_begin(false);
                bgfx.set_state((uint)(bgfx.StateFlags.WriteRgb | bgfx.StateFlags.WriteA), 0);
                fixed (float4x4* p = &tx)
                    bgfx.encoder_set_transform(encoder, p, 1);
                sys->m_quadMesh.SetForSubmit(encoder, 0, 6);
                // material uniforms setup
                bgfx.encoder_set_uniform(encoder, sys->m_simpleShader.m_uniformColor0, &color, 1);
                float4 noTexMad = new float4(1, 1, 0, 0);
                bgfx.encoder_set_uniform(encoder, sys->m_simpleShader.m_uniformTexMad, &noTexMad, 1);
                bgfx.encoder_set_texture(encoder, 0, sys->m_simpleShader.m_samplerTexColor0, tetxure, UInt32.MaxValue);
                fixed (float4* p = &float4.zero)
                    bgfx.encoder_set_uniform(encoder, sys->m_simpleShader.m_uniformBillboarded, p, 1);
                // submit
                bgfx.encoder_submit(encoder, viewId, sys->m_simpleShader.m_prog, 0, (byte)bgfx.DiscardFlags.All);
                bgfx.encoder_end(encoder);
            }
        }

        public static unsafe void SubmitBlitDirectExtended(RendererBGFXInstance* sys, ushort viewId, ref float4x4 tx, bgfx.TextureHandle tetxure,
            bool fromSRGB, bool toSRGB, float reinhard, float4 mulColor, float4 addColor, bool premultiply)
        {
            unsafe {
                bgfx.Encoder* encoder = bgfx.encoder_begin(false);
                bgfx.encoder_set_state(encoder, (uint)(bgfx.StateFlags.WriteRgb | bgfx.StateFlags.WriteA), 0);
                fixed (float4x4* p = &tx)
                    bgfx.encoder_set_transform(encoder, p, 1);
                sys->m_quadMesh.SetForSubmit(encoder, 0, 6);
                // material uniforms setup
                bgfx.encoder_set_uniform(encoder, sys->m_blitShader.m_colormul, &mulColor, 1);
                bgfx.encoder_set_uniform(encoder, sys->m_blitShader.m_coloradd, &addColor, 1);
                float4 noTexMad = new float4(1, 1, 0, 0);
                bgfx.encoder_set_uniform(encoder, sys->m_blitShader.m_uniformTexMad, &noTexMad, 1);
                bgfx.encoder_set_texture(encoder, 0, sys->m_blitShader.m_samplerTexColor0, tetxure, UInt32.MaxValue);
                float4 s = new float4(fromSRGB ? 1.0f : 0.0f, toSRGB ? 1.0f : 0.0f, reinhard, premultiply ? 1.0f : 0.0f);
                bgfx.encoder_set_uniform(encoder, sys->m_blitShader.m_decodeSRGB_encodeSRGB_reinhard_premultiply, &s, 1);
                // submit
                bgfx.encoder_submit(encoder, viewId, sys->m_blitShader.m_prog, 0, (byte)bgfx.DiscardFlags.All);
                bgfx.encoder_end(encoder);
            }
        }

        // ---------------- simple, lit, transient, for ui/text/particles -------------------------------------------------------------------------------------------------
        public static unsafe bool SubmitSimpleTransientAlloc(RendererBGFXInstance* sys, bgfx.TransientIndexBuffer* tib, bgfx.TransientVertexBuffer* tvb, int nvertices, int nindices)
        {
            return SubmitTransientAlloc(sys, tib, tvb, nvertices, nindices, &sys->m_simpleVertexBufferDecl);
        }

        public static unsafe bool SubmitLitTransientAlloc(RendererBGFXInstance* sys, bgfx.TransientIndexBuffer* tib, bgfx.TransientVertexBuffer* tvb, int nvertices, int nindices)
        {
            return SubmitTransientAlloc(sys, tib, tvb, nvertices, nindices, &sys->m_litVertexBufferDecl);
        }

        public static unsafe bool SubmitTransientAlloc(RendererBGFXInstance* sys, bgfx.TransientIndexBuffer* tib, bgfx.TransientVertexBuffer* tvb, int nvertices, int nindices, bgfx.VertexLayout* layout)
        {
            if (!bgfx.alloc_transient_buffers(tvb, layout, (uint)nvertices, tib, (uint)nindices)) {
#if DEBUG
                // TODO: throw or ignore draw?
                throw new InvalidOperationException("Out of transient bgfx memory!");
#else
                RenderDebug.LogFormat("Warning: Out of transient bgfx memory! Skipping draw call.");
                return false;
#endif
            }
            return true;
        }

        public static unsafe void EncodeSimpleTransientBuffers(RendererBGFXInstance* sys, bgfx.Encoder* encoder, bgfx.TransientIndexBuffer* tib, bgfx.TransientVertexBuffer* tvb, int nvertices, int nindices)
        {
            EncodeTransientBuffers(sys, encoder, tib, tvb, nvertices, nindices, sys->m_simpleVertexBufferDeclHandle);
        }

        public static unsafe void EncodeLitTransientBuffers(RendererBGFXInstance* sys, bgfx.Encoder* encoder, bgfx.TransientIndexBuffer* tib, bgfx.TransientVertexBuffer* tvb, int nvertices, int nindices)
        {
            EncodeTransientBuffers(sys, encoder, tib, tvb, nvertices, nindices, sys->m_litVertexBufferDeclHandle);
        }

        public static unsafe void EncodeTransientBuffers(RendererBGFXInstance* sys, bgfx.Encoder* encoder, bgfx.TransientIndexBuffer* tib, bgfx.TransientVertexBuffer* tvb, int nvertices, int nindices, bgfx.VertexLayoutHandle layoutHandle)
        {
            bgfx.encoder_set_transient_index_buffer(encoder, tib, 0, (uint)nindices);
            bgfx.encoder_set_transient_vertex_buffer(encoder, 0, tvb, 0, (uint)nvertices, layoutHandle);
        }

        public static unsafe void SubmitSimpleTransientDirect(RendererBGFXInstance* sys, bgfx.TransientIndexBuffer* tib, bgfx.TransientVertexBuffer* tvb, int nvertices, int nindices, ushort viewId, ref float4x4 tx, ref SimpleMaterialBGFX mat, byte flipCulling, uint depth)
        {
            bgfx.Encoder* encoder = bgfx.encoder_begin(false);
            EncodeSimpleTransient(sys, encoder, tib, tvb, nvertices, nindices, viewId, ref tx, ref mat, flipCulling, depth);
            bgfx.encoder_end(encoder);
        }

        public static unsafe void EncodeSimpleTransient(RendererBGFXInstance* sys, bgfx.Encoder* encoder, bgfx.TransientIndexBuffer* tib, bgfx.TransientVertexBuffer* tvb, int nvertices, int nindices, ushort viewId, ref float4x4 tx, ref SimpleMaterialBGFX mat, byte flipCulling, uint depth)
        {
            EncodeSimpleTransientBuffers(sys, encoder, tib, tvb, nvertices, nindices);
            EncodeSimple(sys, encoder, ref sys->m_simpleShader, viewId, ref tx, ref mat, flipCulling, depth);
        }

        public static unsafe void EncodeLitTransient(RendererBGFXInstance* sys, bgfx.Encoder* encoder, bgfx.TransientIndexBuffer* tib, bgfx.TransientVertexBuffer* tvb, int nvertices, int nindices, ushort viewId, ref float4x4 tx, ref LitMaterialBGFX mat, ref LightingBGFX lighting, ref float4x4 viewTx, byte flipCulling, ref LightingViewSpaceBGFX viewSpaceLightCache, uint depth)
        {
            EncodeLitTransientBuffers(sys, encoder, tib, tvb, nvertices, nindices);
            EncodeLit(sys, encoder, ref sys->m_litShader, mat.shaderProgram, viewId, ref tx, ref mat, ref lighting, ref viewTx, flipCulling, ref viewSpaceLightCache, depth);
        }
    }
}
