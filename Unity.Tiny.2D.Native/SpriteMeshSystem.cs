using Bgfx;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Platforms;
using Unity.Tiny.Rendering;

namespace Unity.Tiny
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    internal class SpriteMeshSystem : ResumableSystemBase
    {
        private EndInitializationEntityCommandBufferSystem m_Barrier;

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            m_Barrier = World.GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();
        }

        protected override void OnSuspendResume(object sender, SuspendResumeEvent evt)
        {
#if UNITY_ANDROID
            if (!evt.Suspend)
                return;

            Entities
                .WithStructuralChanges()
                .WithAll<SpriteMeshBuffers>()
                .ForEach((Entity e) =>
            {
                EntityManager.RemoveComponent<SpriteMeshBuffers>(e);
            }).Run();
#endif // UNITY_ANDROID
        }

        protected override void OnUpdate()
        {
            var shader = World.GetExistingSystem<SpriteRendererSubmitSystem>().DefaultShader;
            if (!shader.IsInitialized)
                return;

            var cmd = m_Barrier.CreateCommandBuffer();
            Dependency = Entities
                .WithName("CreateMesh2D")
                .WithoutBurst()
                .WithNone<SpriteMeshBuffers>()
                .ForEach((Entity entity, in Sprite sprite) =>
                {
                    var blob = sprite.Mesh;
                    var indexCount = blob.Value.Indices.Length;
                    var vertexCount = blob.Value.Vertices.Length;

                    unsafe
                    {
                        cmd.AddComponent(entity, new SpriteMeshBuffers
                        {
                            IndexCount = indexCount,
                            VertexCount = vertexCount,
                            VertexLayoutHandle = shader.LayoutHandle,
                            IndexBufferHandle = bgfx.create_index_buffer(RendererBGFXStatic.CreateMemoryBlock((byte*) blob.Value.Indices.GetUnsafePtr(), indexCount * 2), (ushort) bgfx.BufferFlags.None).idx,
                            VertexBufferHandle = bgfx.create_vertex_buffer(RendererBGFXStatic.CreateMemoryBlock((byte*) blob.Value.Vertices.GetUnsafePtr(), vertexCount * sizeof(SpriteVertex)), (bgfx.VertexLayout*) shader.VertexLayout.GetUnsafeReadOnlyPtr(), (ushort) bgfx.BufferFlags.None).idx
                        });
                    }
                }).Schedule(Dependency);

            Dependency = Entities
                .WithName("RemoveMesh2D")
                .WithoutBurst()
                .WithNone<Sprite>()
                .ForEach((Entity entity, in SpriteMeshBuffers mesh) =>
                {
                    bgfx.destroy_index_buffer(new bgfx.IndexBufferHandle {idx = mesh.IndexBufferHandle});
                    bgfx.destroy_vertex_buffer(new bgfx.VertexBufferHandle {idx = mesh.VertexBufferHandle});
                    cmd.RemoveComponent(entity, typeof(SpriteMeshBuffers));
                }).Schedule(Dependency);

            m_Barrier.AddJobHandleForProducer(Dependency);
        }
    }
}
