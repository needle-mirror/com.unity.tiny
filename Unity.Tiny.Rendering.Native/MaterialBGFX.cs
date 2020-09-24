using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Bgfx;

namespace Unity.Tiny.Rendering
{
    public struct SimpleMaterialBGFX : ISystemStateComponentData
    {
        public bgfx.TextureHandle texAlbedoOpacity;

        // those colors are in srgb if srgb is disabled, otherwise linear
        public float4 constAlbedo_Opacity;
        public float4 mainTextureScaleTranslate;//scale: xy translate: zw

        public float4 billboarded; // yzw unused

        public ulong state; // includes blending and culling!
    }

    public struct LitMaterialBGFX : ISystemStateComponentData
    {
        public bgfx.TextureHandle texAlbedoOpacity;
        public bgfx.TextureHandle texMetal;
        public bgfx.TextureHandle texNormal;
        public bgfx.TextureHandle texEmissive;

        // those colors are in srgb if srgb is disabled, otherwise linear
        public float4 constAlbedo_Opacity;
        public float4 constMetal_Smoothness_Billboarded; // w unused
        public float4 constEmissive_normalMapZScale;
        public float4 mainTextureScaleTranslate; //scale: xy translate: zw
        public float4 smoothness; // x: 1 if the smoothness is albedo alpha, y: 1 if smoothness is metal alpha, z: opacity, w unused

        public ulong state; // includes blending and culling!

        public bgfx.ProgramHandle shaderProgram;
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(SubmitSystemGroup))]
    [UpdateAfter(typeof(RendererBGFXSystem))]
    internal class UpdateMaterialsSystem : SystemBase
    {
        public unsafe void UpdateLitMaterialBGFX(RendererBGFXInstance *sys, Entity e, bool srgbColors)
        {
            var mat = EntityManager.GetComponentData<LitMaterial>(e);
            var matBGFX = EntityManager.GetComponentData<LitMaterialBGFX>(e);
            UpdateLitMaterialBGFX(sys, ref mat, ref matBGFX, srgbColors);
            EntityManager.SetComponentData(e, matBGFX);
        }

        // true if still loading
        private bool InitTexture(ref bgfx.TextureHandle dest, Entity src, bgfx.TextureHandle defValue)
        {
            dest = defValue;
            if (src == Entity.Null)
                return false;
            Image2D im = EntityManager.GetComponentData<Image2D>(src); // must have that one, no check?
            if (im.status == ImageStatus.Loaded && EntityManager.HasComponent<TextureBGFX>(src))
            {
                TextureBGFX tex = EntityManager.GetComponentData<TextureBGFX>(src);
                dest = tex.handle;
                return false;
            }
            return true;
        }

        private void InitShader(ref bgfx.ProgramHandle dest, Entity src, bgfx.ProgramHandle defValue)
        {
            dest = defValue;
            if (src == Entity.Null || !EntityManager.HasComponent<CustomShader>(src))
                return;
            var shader = EntityManager.GetComponentData<CustomShader>(src);
            if (shader.Status == ShaderStatus.Loaded && EntityManager.HasComponent<ShaderBGFX>(src))
            {
                ShaderBGFX shaderBGFX = EntityManager.GetComponentData<ShaderBGFX>(src);
                dest = shaderBGFX.handle;
            }
        }

        public unsafe bool UpdateLitMaterialBGFX(RendererBGFXInstance *sys, ref LitMaterial mat, ref LitMaterialBGFX matBGFX, bool srgbColors)
        {
            bool stillLoading = false;
            if (InitTexture(ref matBGFX.texAlbedoOpacity, mat.texAlbedoOpacity, sys->m_whiteTexture)) stillLoading = true;
            if (InitTexture(ref matBGFX.texNormal, mat.texNormal, sys->m_upTexture)) stillLoading = true;
            if (InitTexture(ref matBGFX.texMetal, mat.texMetal, sys->m_whiteTexture)) stillLoading = true;
            if (InitTexture(ref matBGFX.texEmissive, mat.texEmissive, sys->m_whiteTexture)) stillLoading = true;

            InitShader(ref matBGFX.shaderProgram, mat.shader, sys->m_litShader.m_prog);

            matBGFX.constAlbedo_Opacity = srgbColors ?
                new float4(Color.LinearToSRGB(mat.constAlbedo), mat.constOpacity) :
                new float4(mat.constAlbedo, mat.constOpacity);
            matBGFX.constMetal_Smoothness_Billboarded = new float4(mat.constMetal, mat.constSmoothness, mat.billboarded ? 1 : 0, 0);
            matBGFX.constEmissive_normalMapZScale = srgbColors ?
                new float4(Color.LinearToSRGB(mat.constEmissive), mat.normalMapZScale) :
                new float4(mat.constEmissive, mat.normalMapZScale);
            matBGFX.mainTextureScaleTranslate = new float4(mat.scale, mat.offset);
            matBGFX.smoothness = new float4(0.0f);
            matBGFX.smoothness.x = (!mat.transparent && mat.smoothnessAlbedoAlpha) ? 1 : 0;
            matBGFX.smoothness.y = (!mat.transparent && !mat.smoothnessAlbedoAlpha) ? 1 : 0;
            matBGFX.smoothness.z = !mat.transparent ? 1 : 0;

            // if twoSided, need to update state
            matBGFX.state = (ulong)(bgfx.StateFlags.WriteRgb | bgfx.StateFlags.WriteA | bgfx.StateFlags.DepthTestLess);
            if (!mat.twoSided && !mat.billboarded)
                matBGFX.state |= (ulong)bgfx.StateFlags.CullCcw;
            if (mat.transparent)
                matBGFX.state |= RendererBGFXStatic.MakeBGFXBlend(bgfx.StateFlags.BlendOne, bgfx.StateFlags.BlendInvSrcAlpha);
            else
                matBGFX.state |= (ulong)bgfx.StateFlags.WriteZ;
            return !stillLoading;
        }

        public unsafe void UpdateSimpleMaterialBGFX(RendererBGFXInstance *sys, Entity e, bool srgbColors)
        {
            var mat = EntityManager.GetComponentData<SimpleMaterial>(e);
            var matBGFX = EntityManager.GetComponentData<SimpleMaterialBGFX>(e);
            UpdateSimpleMaterialBGFX(sys, ref mat, ref matBGFX, srgbColors);
            EntityManager.SetComponentData(e, matBGFX);
        }

        public unsafe bool UpdateSimpleMaterialBGFX(RendererBGFXInstance *sys, ref SimpleMaterial mat, ref SimpleMaterialBGFX matBGFX, bool srgbColors)
        {
            // if constants changed, need to update packed value
            matBGFX.constAlbedo_Opacity = srgbColors ?
                new float4(Color.LinearToSRGB(mat.constAlbedo), mat.constOpacity) :
                new float4(mat.constAlbedo, mat.constOpacity);
            // if texture entity OR load state changed need to update texture handles
            // content of texture change should transparently update texture referenced by handle
            bool stillLoading = false;
            if (InitTexture(ref matBGFX.texAlbedoOpacity, mat.texAlbedoOpacity, sys->m_whiteTexture)) stillLoading = true;

            // if twoSided or hasalpha changed, need to update state
            matBGFX.state = (ulong)(bgfx.StateFlags.WriteRgb | bgfx.StateFlags.WriteA | bgfx.StateFlags.DepthTestLess);
            if (!mat.twoSided && !mat.billboarded)
                matBGFX.state |= (ulong)bgfx.StateFlags.CullCcw;
            if (mat.transparent)
                matBGFX.state |= RendererBGFXStatic.MakeBGFXBlend(bgfx.StateFlags.BlendOne, bgfx.StateFlags.BlendInvSrcAlpha);
            else
                matBGFX.state |= (ulong)bgfx.StateFlags.WriteZ;
            matBGFX.mainTextureScaleTranslate = new float4(mat.scale, mat.offset);

            matBGFX.billboarded = new float4(mat.billboarded ? 1 : 0, 0, 0, 0);
            return !stillLoading;
        }

        protected unsafe override void OnUpdate()
        {
            var sys = World.GetExistingSystem<RendererBGFXSystem>().InstancePointer();
            Dependency.Complete();

            var di = GetSingleton<DisplayInfo>();
            bool srgbColors = di.colorSpace == ColorSpace.Gamma;

            // add bgfx version of materials, system states
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
            Entities.WithoutBurst().WithNone<LitMaterialBGFX>().WithAll<LitMaterial>().ForEach((Entity e) =>
            {
                ecb.AddComponent<LitMaterialBGFX>(e);
            }).Run();
            Entities.WithoutBurst().WithNone<SimpleMaterialBGFX>().WithAll<SimpleMaterial>().ForEach((Entity e) =>
            {
                ecb.AddComponent<SimpleMaterialBGFX>(e);
            }).Run();
            ecb.Playback(EntityManager); // playback once here, so we do not have a one frame delay
            ecb.Dispose();

            // update materials
            ecb = new EntityCommandBuffer(Allocator.TempJob);
            Entities.WithoutBurst().ForEach((Entity e, ref LitMaterial mat, ref LitMaterialBGFX matBGFX) =>
            {
                UpdateLitMaterialBGFX(sys, ref mat, ref matBGFX, srgbColors); // true = done
            }).Run();

            Entities.WithoutBurst().ForEach((Entity e, ref SimpleMaterial mat, ref SimpleMaterialBGFX matBGFX) =>
            {
                UpdateSimpleMaterialBGFX(sys, ref mat, ref matBGFX, srgbColors); // true = done
            }).Run();

            // TODO: bring back some form of optimization here when materials do not change
            //       change tracking is not enough as we need to chase entity pointers
            //       For now we assume that the number of materials is relatively low (<100) and this avoids
            //       a lot of complications around change tracking

            // system state cleanup - can reuse the same ecb, it does not matter if there is a bit of delay
            Entities.WithAll<SimpleMaterialBGFX>().WithNone<SimpleMaterial>().ForEach((Entity e) =>
            {
                ecb.RemoveComponent<SimpleMaterialBGFX>(e);
            }).Run();

            Entities.WithAll<LitMaterialBGFX>().WithNone<LitMaterial>().ForEach((Entity e) =>
            {
                ecb.RemoveComponent<LitMaterialBGFX>(e);
            }).Run();

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
