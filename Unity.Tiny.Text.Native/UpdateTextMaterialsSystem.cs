using Bgfx;
using Unity.Entities;
using Unity.Tiny.Rendering;

namespace Unity.Tiny.Text.Native
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(SubmitSystemGroup))]
    [UpdateAfter(typeof(RendererBGFXSystem))]
    internal class UpdateTextMaterialsSystem : SystemBase
    {
        protected bgfx.TextureHandle m_WhiteTextureHandle;

        // Copied from MaterialBGFX; it needs access to EntityManager
        internal static bool InitTexture(EntityManager em, ref bgfx.TextureHandle dest, Entity src, bgfx.TextureHandle defValue)
        {
            dest = defValue;
            if (src == Entity.Null)
                return false;
            Image2D im = em.GetComponentData<Image2D>(src); // must have that one, no check?
            if (im.status == ImageStatus.Loaded && em.HasComponent<TextureBGFX>(src))
            {
                TextureBGFX tex = em.GetComponentData<TextureBGFX>(src);
                dest = tex.handle;
                return false;
            }

            return true;
        }

        protected override unsafe void OnUpdate()
        {
            CompleteDependency();

            // TODO -- most all of this should be rewritten with EntityQuery-based AddComponent/RemoveComponent instead of
            // through Entities.ForEach()

            // do this during initialization
            var sys = World.GetExistingSystem<RendererBGFXSystem>().InstancePointer();
            if (!sys->m_initialized)
                return;

            m_WhiteTextureHandle = sys->m_whiteTexture;

            // non-SDF

            // add bgfx version of materials, system states
            Entities.WithStructuralChanges().WithNone<TextMaterialBGFX>().WithAll<BitmapFontMaterial>().ForEach((Entity e) =>
            {
                EntityManager.AddComponent<TextMaterialBGFX>(e);
            }).Run();

            // upload materials
            Entities.WithoutBurst().ForEach((Entity e, ref BitmapFontMaterial mat, ref TextMaterialBGFX matBGFX) =>
            {
                matBGFX.Update(EntityManager, sys, ref mat);
            }).Run();

            // system state cleanup - can reuse the same ecb, it does not matter if there is a bit of delay
            Entities.WithStructuralChanges().WithAll<TextMaterialBGFX>().WithNone<BitmapFontMaterial>().ForEach((Entity e) =>
            {
                EntityManager.RemoveComponent<TextMaterialBGFX>(e);
            }).Run();

            // SDF

            // add bgfx version of materials, system states
            Entities.WithStructuralChanges().WithNone<TextSDFMaterialBGFX>().WithAll<SDFFontMaterial>().ForEach((Entity e) =>
            {
                EntityManager.AddComponent<TextSDFMaterialBGFX>(e);
            }).Run();

            // upload materials
            Entities.WithoutBurst().ForEach((Entity e, ref SDFFontMaterial mat, ref TextSDFMaterialBGFX matBGFX) =>
            {
                matBGFX.Update(EntityManager, sys, ref mat);
            }).Run();

            // system state cleanup - can reuse the same ecb, it does not matter if there is a bit of delay
            Entities.WithStructuralChanges().WithAll<TextSDFMaterialBGFX>().WithNone<SDFFontMaterial>().ForEach((Entity e) =>
            {
                EntityManager.RemoveComponent<TextSDFMaterialBGFX>(e);
            }).Run();


        }
    }
}
