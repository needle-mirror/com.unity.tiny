using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Rendering;

using bgfx = Bgfx.bgfx;

namespace Unity.Tiny
{
    internal static class Native2DUtils
    {
        private static readonly ulong k_RenderStates = (ulong) (bgfx.StateFlags.WriteRgb | bgfx.StateFlags.WriteA) |
                                                       RendererBGFXStatic.MakeBGFXBlend(
                                                           bgfx.StateFlags.BlendOne,
                                                           bgfx.StateFlags.BlendInvSrcAlpha);

        public static bool ExtractCacheData(EntityManager em, Entity spriteEntity, out SpriteMeshCacheData cacheData)
        {
            cacheData = default;

            if (!em.HasComponent<Sprite>(spriteEntity))
                return false;
            var spriteData = em.GetComponentData<Sprite>(spriteEntity);

            if (!em.HasComponent<TextureBGFX>(spriteData.Texture))
                return false;
            if (!em.HasComponent<SpriteMeshBuffers>(spriteEntity))
                return false;

            var spriteMesh = em.GetComponentData<SpriteMeshBuffers>(spriteEntity);
            var texture = em.GetComponentData<TextureBGFX>(spriteData.Texture);

            cacheData = new SpriteMeshCacheData
            {
                Hash = new Hash128((uint)spriteEntity.Index, (uint)spriteEntity.Version, 0 , 0),
                TextureHandle = texture.handle,
                IndexBufferHandle = spriteMesh.IndexBufferHandle,
                VertexBufferHandle = spriteMesh.VertexBufferHandle,
                IndexCount = spriteMesh.IndexCount,
                VertexCount = spriteMesh.VertexCount,
                VertexLayoutHandle = spriteMesh.VertexLayoutHandle,
            };

            return true;
        }

        public static unsafe bgfx.Encoder* BeginSubmit()
        {
            return bgfx.encoder_begin(false);
        }
        public static unsafe void EndSubmit(bgfx.Encoder* encoder)
        {
            bgfx.encoder_end(encoder);
        }

        public static unsafe void SubmitDrawInstruction(bgfx.Encoder* encoder, float4 color,
            SpriteDefaultShader defaultShader, ushort viewId,
            SpriteMeshCacheData spriteMesh, uint depth,
            ref float4x4 transform)
        {
            bgfx.encoder_set_state(encoder, k_RenderStates, 0);

            bgfx.encoder_set_index_buffer(encoder, new bgfx.IndexBufferHandle { idx = spriteMesh.IndexBufferHandle }, (uint)0, (uint)spriteMesh.IndexCount);
            bgfx.encoder_set_vertex_buffer(encoder, 0, new bgfx.VertexBufferHandle { idx = spriteMesh.VertexBufferHandle }, 0, (uint)spriteMesh.VertexCount, spriteMesh.VertexLayoutHandle);

            fixed (float4x4* p = &transform)
                bgfx.encoder_set_transform(encoder, p, 1);

            bgfx.encoder_set_uniform(encoder, defaultShader.TintColorHandle, &color, 1);

            bgfx.encoder_set_texture(encoder, 0, defaultShader.TexColorSamplerHandle, spriteMesh.TextureHandle, System.UInt32.MaxValue);
            bgfx.encoder_submit(encoder, viewId, defaultShader.ProgramHandle, depth, (byte)bgfx.DiscardFlags.All);
        }
    }
}
