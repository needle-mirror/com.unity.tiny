using Unity.Entities;

using Bgfx;
using Unity.Jobs;

namespace Unity.Tiny
{
    internal unsafe struct EndSubmitJob : IJob
    {
        public bgfx.Encoder* Encoder;
        public void Execute()
        {
            Native2DUtils.EndSubmit(Encoder);
        }
    }

    internal struct SpriteMeshCacheData : IComponentData
    {
        public Hash128 Hash;
        public bgfx.TextureHandle TextureHandle;
        public ushort IndexBufferHandle;
        public ushort VertexBufferHandle;
        public int IndexCount;
        public int VertexCount;
        public bgfx.VertexLayoutHandle VertexLayoutHandle;
    }

    internal struct SpriteMeshBuffers : ISystemStateComponentData
    {
        public ushort IndexBufferHandle;
        public ushort VertexBufferHandle;
        public int IndexCount;
        public int VertexCount;
        public bgfx.VertexLayoutHandle VertexLayoutHandle;
    }
}
