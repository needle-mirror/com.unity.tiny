using System;
using Bgfx;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Assertions;
using Unity.Tiny.Rendering;
using Unity.Tiny.Scenes;
using Unity.Transforms;
using Unity.Platforms;

namespace Unity.Tiny.Text.Native
{
    [UpdateInGroup(typeof(SubmitSystemGroup))]
    internal unsafe class SubmitTextMesh : ComponentSystem
    {
        TextShader m_TextShader = default;
        TextSDFShader m_TextSDFShader = default;
        RendererBGFXSystem m_BGFXSystem;
        RendererBGFXInstance* m_BGFXInstance;

        unsafe void SubmitTextDirect(ushort viewId, ref MeshBGFX mesh, ref float4x4 tx, ref TextMaterialBGFX mat,
            int startIndex, int indexCount, uint depth, byte flipCulling)
        {
            bgfx.Encoder* encoder = bgfx.encoder_begin(false);
            EncodeText(encoder, viewId, ref mesh, ref tx, ref mat, startIndex, indexCount, depth, flipCulling);
            bgfx.encoder_end(encoder);
        }

        unsafe void EncodeText(bgfx.Encoder* encoder, ushort viewId, ref MeshBGFX mesh, ref float4x4 tx,
            ref TextMaterialBGFX mat, int startIndex, int indexCount, uint depth, byte flipCulling)
        {
            bgfx.set_state(mat.state, 0);
            fixed(float4x4* p = &tx)
            bgfx.encoder_set_transform(encoder, p, 1);
            mesh.SetForSubmit(encoder, startIndex, indexCount);
            // material uniforms setup
            fixed(TextMaterialBGFX* pmat = &mat)
            {
                bgfx.encoder_set_uniform(encoder, m_TextShader.m_clipRect, &pmat->constClipRect, 1);
                bgfx.encoder_set_uniform(encoder, m_TextShader.m_maskSoftness, &pmat->constMaskSoftness, 1);
            }

            bgfx.encoder_set_texture(encoder, 0, m_TextShader.m_mainTex, mat.texAtlas, UInt32.MaxValue);
            bgfx.encoder_submit(encoder, viewId, m_TextShader.m_prog, depth, (byte)bgfx.DiscardFlags.All);
        }

        void SubmitTextSDFDirect(ushort viewId, ref MeshBGFX mesh, ref float4x4 tx, ref TextSDFMaterialBGFX mat,
            int startIndex, int indexCount, uint depth, byte flipCulling)
        {
            bgfx.Encoder* encoder = bgfx.encoder_begin(false);
            EncodeTextSDF(encoder, viewId, ref mesh, ref tx, ref mat, startIndex, indexCount, depth, flipCulling);
            bgfx.encoder_end(encoder);
        }

        void EncodeTextSDF(bgfx.Encoder* encoder, ushort viewId, ref MeshBGFX mesh, ref float4x4 tx,
            ref TextSDFMaterialBGFX mat, int startIndex, int indexCount, uint depth, byte flipCulling)
        {
            bgfx.set_state(mat.state, 0);
            fixed(float4x4* p = &tx)
            bgfx.encoder_set_transform(encoder, p, 1);
            mesh.SetForSubmit(encoder, startIndex, indexCount);
            // material uniforms setup
            fixed(TextSDFMaterialBGFX* pmat = &mat)
            {
                bgfx.encoder_set_uniform(encoder, m_TextSDFShader.u_FaceColor, &pmat->faceColor, 1);
                bgfx.encoder_set_uniform(encoder, m_TextSDFShader.u_ClipRect, &pmat->clipRect, 1);
                bgfx.encoder_set_uniform(encoder, m_TextSDFShader.u_MiscP, &pmat->miscP, 1);
#if false
                bgfx.encoder_set_uniform(encoder, m_TextSDFShader.u_TexDimScale, &pmat->texDimScale, 1);
                bgfx.encoder_set_uniform(encoder, m_TextSDFShader.u_OutlineColor, &pmat->outlineColor, 1);
                bgfx.encoder_set_uniform(encoder, m_TextSDFShader.u_OutlineP, &pmat->outlineP, 1);
                bgfx.encoder_set_uniform(encoder, m_TextSDFShader.u_UnderlayColor, &pmat->underlayColor, 1);
                bgfx.encoder_set_uniform(encoder, m_TextSDFShader.u_UnderlayP, &pmat->underlayP, 1);
                bgfx.encoder_set_uniform(encoder, m_TextSDFShader.u_WeightAndMaskSoftness, &pmat->weightAndMaskSoftness, 1);
                bgfx.encoder_set_uniform(encoder, m_TextSDFShader.u_ScaleRatio, &pmat->scaleRatio, 1);
                bgfx.encoder_set_uniform(encoder, m_TextSDFShader.u_ScreenParams, &pmat->screenParams, 1);

                var txinv = math.inverse(tx);
                bgfx.encoder_set_uniform(encoder, m_TextSDFShader.u_WorldSpaceCameraPos, UnsafeUtility.AddressOf(ref txinv), 1);
                bgfx.encoder_set_uniform(encoder, m_TextSDFShader.u_invModel0, UnsafeUtility.AddressOf(ref txinv), 1);
#endif
            }

            bgfx.encoder_set_texture(encoder, 0, m_TextSDFShader.u_MainTex, mat.texAtlas, UInt32.MaxValue);
            bgfx.encoder_submit(encoder, viewId, m_TextSDFShader.m_prog, depth, (byte)bgfx.DiscardFlags.All);
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();

            m_BGFXSystem = World.GetExistingSystem<RendererBGFXSystem>();

            // the work in EnsureInitialized should be done here, once we have
            // a singleton we can use to indicate that bgfx is initialized
#if UNITY_ANDROID
            PlatformEvents.OnSuspendResume += OnSuspendResume;
#endif
        }

        bool EnsureInitialized()
        {
            // early-out assumption that if we initialized the shader, we're good to go
            if (m_TextShader.Initialized)
                return true;

            if (!m_BGFXSystem.IsInitialized())
                return false;

            m_BGFXInstance = m_BGFXSystem.InstancePointer();

            // TODO -- need a better way to find a shader given a guid
            int foundShaders = 0;
            Entities.ForEach((ref PrecompiledShader shader, ref VertexShaderBinData vbin, ref FragmentShaderBinData fbin) =>
            {
                foundShaders++;
                if (shader.Guid == BitmapFontMaterial.ShaderGuid)
                    m_TextShader.Init(BGFXShaderHelper.GetPrecompiledShaderData(m_BGFXInstance->m_rendererType, vbin, fbin, ref shader.Name));
                else if (shader.Guid == SDFFontMaterial.ShaderGuid)
                    m_TextSDFShader.Init(BGFXShaderHelper.GetPrecompiledShaderData(m_BGFXInstance->m_rendererType, vbin, fbin, ref shader.Name));
                else
                    foundShaders--;
            });

            // must have the shader
            if (foundShaders != 2)
                throw new Exception("Couldn't find all needed Text precompiled shaders");

            return true;
        }

        protected override void OnStopRunning()
        {
            m_TextShader.Destroy();
            m_TextSDFShader.Destroy();
            m_BGFXSystem = null;
            m_BGFXInstance = null;
#if UNITY_ANDROID
            PlatformEvents.OnSuspendResume -= OnSuspendResume;
#endif

            base.OnStopRunning();
        }

#if UNITY_ANDROID
        // this is temporary fix, proper fix would be to make RendererBGFX to take care of all bgfx specific resources
        // we cannot call Destroy() methods for shaders here, because RendererBGFX system has already destroyed bgfx by this moment
        public void OnSuspendResume(object sender, SuspendResumeEvent evt)
        {
            if (evt.Suspend)
            {
                m_TextShader = default;
                m_TextSDFShader = default;
            }
        }
#endif

        protected override void OnUpdate()
        {
            if (!EnsureInitialized())
                return;

            // get all MeshRenderer, cull them, and add them to graph nodes that need them
            // any mesh renderer MUST have a shared component data that has a list of passes to render to
            // this list is usually very shared - all opaque meshes will render to all ZOnly and Opaque passes
            // this shared data is not dynamically updated - other systems are responsible to update them if needed
            // simple
            Entities.WithAll<TextRenderer>().ForEach((Entity e, ref MeshRenderer mr, ref LocalToWorld tx, ref WorldBounds wb, ref WorldBoundingSphere wbs) =>
            {
                if (!EntityManager.HasComponent<RenderToPasses>(e))
                    return;

                RenderToPasses toPassesRef = EntityManager.GetSharedComponentData<RenderToPasses>(e);
                DynamicBuffer<RenderToPassesEntry> toPasses = EntityManager.GetBufferRO<RenderToPassesEntry>(toPassesRef.e);
                for (int i = 0; i < toPasses.Length; i++)
                {
                    Entity ePass = toPasses[i].e;
                    var pass = EntityManager.GetComponentData<RenderPass>(ePass);
                    if (Culling.Cull(in wbs, in pass.frustum) == Culling.CullingResult.Outside)
                        continue;
                    var mesh = EntityManager.GetComponentData<MeshBGFX>(mr.mesh);
                    uint depth = 0;
                    switch (pass.passType)
                    {
                        case RenderPassType.ZOnly:
                            // TODO -- need to do alpha kill to get the proper depth written here (and to support shadows)
                            SubmitHelper.SubmitZOnlyMeshDirect(m_BGFXInstance, pass.viewId, ref mesh, ref tx.Value, mr.startIndex, mr.indexCount, pass.GetFlipCulling());
                            break;
                        case RenderPassType.Transparent:
                            depth = pass.ComputeSortDepth(tx.Value.c3);
                            goto case RenderPassType.Opaque;
                        case RenderPassType.Opaque:
                            if (EntityManager.HasComponent<TextMaterialBGFX>(mr.material))
                            {
                                var material = EntityManager.GetComponentData<TextMaterialBGFX>(mr.material);
                                SubmitTextDirect(pass.viewId, ref mesh, ref tx.Value, ref material, mr.startIndex, mr.indexCount, depth, pass.GetFlipCulling());
                            } else if (EntityManager.HasComponent<TextSDFMaterialBGFX>(mr.material))
                            {
                                var material = EntityManager.GetComponentData<TextSDFMaterialBGFX>(mr.material);
                                SubmitTextSDFDirect(pass.viewId, ref mesh, ref tx.Value, ref material, mr.startIndex, mr.indexCount, depth, pass.GetFlipCulling());
                            }

                            break;
                        case RenderPassType.ShadowMap:
                        // TODO -- text doesn't cast shadows right now
                        default:
                            // Unknown pass, text doesn't render
                            break;
                    }
                }
            });
        }
    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(AssignRenderGroups))]
    internal class AssignTextRenderGroups : ComponentSystem
    {
        AssignRenderGroups m_AssignRenderGroups;
        protected override void OnCreate()
        {
            base.OnCreate();
            m_AssignRenderGroups = World.GetExistingSystem<AssignRenderGroups>();
        }

        protected override void OnUpdate()
        {
            // go through all known render types, and assign groups to them
            // assign groups to all renderers
            Entities
                .WithNone<RenderToPasses>()
                .WithAll<MeshRenderer, TextRenderer>()
                .ForEach((Entity e) => {
                    CameraMask cameraMask = new CameraMask { mask = ulong.MaxValue };
                    if (EntityManager.HasComponent<CameraMask>(e))
                        cameraMask = EntityManager.GetComponentData<CameraMask>(e);
                    ShadowMask shadowMask = new ShadowMask { mask = ulong.MaxValue };
                    if (EntityManager.HasComponent<ShadowMask>(e))
                        shadowMask = EntityManager.GetComponentData<ShadowMask>(e);
                    m_AssignRenderGroups.AddItemToRenderGroup(e,
                        new BuildGroup
                        {
                            passTypes = RenderPassType.Transparent,
                            cameraMask = cameraMask, shadowMask = shadowMask
                        });
                });
        }
    }
}
