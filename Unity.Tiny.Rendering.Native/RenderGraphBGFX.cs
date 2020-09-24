using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Tiny;
using Unity.Tiny.Assertions;
using Unity.Tiny.Rendering;
using Bgfx;

namespace Unity.Tiny.Rendering
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(RendererBGFXSystem))]
    [UpdateBefore(typeof(SubmitSystemGroup))]
    [UpdateAfter(typeof(PreparePassesSystem))]
    public unsafe class PreparePassesFroBGFXSystem : SystemBase
    {
        protected override void OnCreate()
        {
        }

        protected override void OnUpdate()
        {
            var bgfxinst = World.GetExistingSystem<RendererBGFXSystem>().InstancePointer();
            if (!bgfxinst->m_initialized)
                return;

            // setup bgfx side 
            Entities.WithoutBurst().ForEach((Entity e, ref RenderPass pass) =>
            {
                if (pass.viewId == 0xffff)
                {
                    RenderDebug.LogFormat("Render pass entity {0} on render node entity {1} is not referenced by the render graph. It should be deleted.", e, pass.inNode);
                    Assert.IsTrue(false);
                    return;
                }
                bool rtt = EntityManager.HasComponent<FramebufferBGFX>(pass.inNode);
                if (rtt) pass.passFlags = RenderPassFlags.RenderToTexture;
                else pass.passFlags = 0;
                // those could be more shared ... (that is, do all passes really need a copy of view & projection?)
                unsafe { fixed(float4x4* viewp = &pass.viewTransform, projp = &pass.projectionTransform) {
                    if (bgfxinst->m_homogeneousDepth && bgfxinst->m_originBottomLeft) // gl style
                    {
                        bgfx.set_view_transform(pass.viewId, viewp, projp);
                        pass.passFlags &= ~RenderPassFlags.FlipCulling;
                    }
                    else // dx style
                    {
                        bool yflip = !bgfxinst->m_originBottomLeft && rtt;
                        float4x4 adjustedProjection = RendererBGFXStatic.AdjustProjection(ref pass.projectionTransform, !bgfxinst->m_homogeneousDepth, yflip);
                        bgfx.set_view_transform(pass.viewId, viewp, &adjustedProjection);
                        if (yflip) pass.passFlags |= RenderPassFlags.FlipCulling;
                        else  pass.passFlags &= ~RenderPassFlags.FlipCulling;
                    }
                    // make a viewProjection
                    pass.viewProjectionTransform = math.mul(pass.projectionTransform, pass.viewTransform);
                }}
                bgfx.set_view_mode(pass.viewId, (bgfx.ViewMode)pass.sorting);
                if ( bgfxinst->m_originBottomLeft ) {
                    bgfx.set_view_rect(pass.viewId, pass.viewport.x, (ushort)(pass.targetRect.h - pass.viewport.y - pass.viewport.h), pass.viewport.w, pass.viewport.h);
                    if ( pass.scissor.h == 0 )
                        bgfx.set_view_scissor(pass.viewId, 0, 0, 0, 0);
                    else
                        bgfx.set_view_scissor(pass.viewId, pass.scissor.x, (ushort)(pass.targetRect.h - pass.scissor.y - pass.scissor.h), pass.scissor.w, pass.scissor.h);
                } else { 
                    if ( rtt  ) { 
                        bgfx.set_view_rect(pass.viewId, pass.viewport.x, pass.viewport.y, pass.viewport.w, pass.viewport.h);
                        bgfx.set_view_scissor(pass.viewId, pass.scissor.x, pass.scissor.y, pass.scissor.w, pass.scissor.h);
                    } else {
                        bgfx.set_view_rect(pass.viewId, pass.viewport.x, (ushort)(pass.targetRect.h - pass.viewport.y - pass.viewport.h), pass.viewport.w, pass.viewport.h);
                        if ( pass.scissor.h == 0 )
                            bgfx.set_view_scissor(pass.viewId, 0, 0, 0, 0);
                        else
                            bgfx.set_view_scissor(pass.viewId, pass.scissor.x, (ushort)(pass.targetRect.h - pass.scissor.y - pass.scissor.h), pass.scissor.w, pass.scissor.h);
                    }
                }
                bgfx.set_view_clear(pass.viewId, (ushort)pass.clearFlags, pass.clearRGBA, pass.clearDepth, pass.clearStencil);
                if (rtt)
                {
                    var rttbgfx = EntityManager.GetComponentData<FramebufferBGFX>(pass.inNode);
                    bgfx.set_view_frame_buffer(pass.viewId, rttbgfx.handle);
                }
                else
                {
                    bgfx.set_view_frame_buffer(pass.viewId, new bgfx.FrameBufferHandle { idx = 0xffff });
                }
                // touch it? needed?
                bgfx.touch(pass.viewId);
            }).Run();
        }
    }
}
