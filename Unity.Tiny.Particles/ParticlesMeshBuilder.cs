using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Rendering;
using Unity.Transforms;

namespace Unity.Tiny.Particles
{
    /// <summary>
    /// A system that builds the vertex and index buffers for particles by aggregating all the particles from an emitter
    /// </summary>
    /// <remarks>
    /// Buffers will be written every frame unless the emitter does not have any living particles
    /// </remarks>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(UpdateWorldBoundsSystem))]
    [UpdateBefore(typeof(SubmitSystemGroup))]
    public class ParticlesMeshBuilderSystem : SystemBase
    {
        EntityQuery particleRenderersQuery;

        protected override void OnCreate()
        {
            base.OnCreate();
            var queryDesc = new EntityQueryDesc
            {
                All = new[] { typeof(MeshRenderer), ComponentType.ReadOnly<EmitterReferenceForRenderer>() },
                Options = EntityQueryOptions.IncludeDisabled
            };
            particleRenderersQuery = GetEntityQuery(queryDesc);
        }

        protected override void OnUpdate()
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
            ComponentDataFromEntity<LocalToWorld> localToWorldGetter = GetComponentDataFromEntity<LocalToWorld>(true);
            ComponentDataFromEntity<ParticleColor> colorGetter = GetComponentDataFromEntity<ParticleColor>(true);

            NativeArray<Entity> entities = particleRenderersQuery.ToEntityArray(Allocator.TempJob);
            foreach (var entity in entities)
            {
                var emitterReference = EntityManager.GetComponentData<EmitterReferenceForRenderer>(entity);
                var eEmitter = emitterReference.emitter;
                EmitterReferenceForParticles emitterReferenceForParticles = new EmitterReferenceForParticles { emitter = eEmitter };

                // In some cases it takes an extra frame for the particle entities that reference a destroyed emitter to be destroyed, most likely because of system state components
                if (!ParticlesUtil.EmitterIsValid(EntityManager, emitterReference.emitter))
                    continue;

                var particleMesh = EntityManager.GetComponentData<ParticleMesh>(eEmitter);
                var meshRenderer = EntityManager.GetComponentData<MeshRenderer>(entity);
                Entity eMesh = meshRenderer.mesh;
                bool billboarded = EntityManager.HasComponent<Billboarded>(eEmitter);

                if (EntityManager.HasComponent<LitMeshRenderer>(entity))
                {
                    ref LitMeshData lmd = ref EntityManager.GetComponentData<LitMeshRenderData>(particleMesh.mesh).Mesh.Value;
                    int numVertsPerParticle = lmd.Vertices.Length;
                    int numIndicesPerParticle = lmd.Indices.Length;
                    NativeArray<LitVertex> particleVertices = MeshHelper.AsNativeArray(ref lmd.Vertices);
                    NativeArray<ushort> particleIndices = MeshHelper.AsNativeArray(ref lmd.Indices);
                    var vBuffer = EntityManager.GetBuffer<DynamicLitVertex>(eMesh);
                    var iBuffer = EntityManager.GetBuffer<DynamicIndex>(eMesh);

                    // Note that buffers have their capacity initialized to support the max particles allowed for an emitter
                    vBuffer.ResizeUninitialized(vBuffer.Capacity);
                    iBuffer.ResizeUninitialized(iBuffer.Capacity);
                    NativeArray<LitVertex> vertices = vBuffer.AsNativeArray().Reinterpret<DynamicLitVertex, LitVertex>();
                    NativeArray<ushort> indices = iBuffer.AsNativeArray().Reinterpret<DynamicIndex, ushort>();

                    ushort particleIndex = 0;
                    MinMaxAABB minMaxAABB = MinMaxAABB.Empty;
                    Entities
                        .WithSharedComponentFilter(emitterReferenceForParticles)
                        .ForEach((Entity eParticle, Particle particle) =>
                        {
                            var color = colorGetter[eParticle].color;
                            var localToWorld = localToWorldGetter[eParticle].Value;
                            var localToWorldRS = new float3x3(localToWorld.c0.xyz, localToWorld.c1.xyz, localToWorld.c2.xyz);
                            float3x3 modelInverseTransposeRS = math.transpose(math.inverse(localToWorldRS));
                            float4x4 modelInverseTranspose = new float4x4(modelInverseTransposeRS, float3.zero);

                            // Postpone translate until after additional rotation for billboarding is applied in shader
                            float4x4 model = billboarded ? new float4x4(localToWorldRS, float3.zero) : localToWorld;

                            int vertexOffset = particleIndex * numVertsPerParticle;
                            for (int i = 0; i < numVertsPerParticle; i++)
                            {
                                LitVertex vertex = particleVertices[i];
                                vertex.Position = math.transform(model, vertex.Position);
                                vertex.Normal = math.transform(modelInverseTranspose, vertex.Normal);
                                vertex.Tangent = math.transform(modelInverseTranspose, vertex.Tangent);
                                vertex.BillboardPos = billboarded ? localToWorld.c3.xyz : float3.zero;
                                vertex.Albedo_Opacity = color;
                                vertices[vertexOffset + i] = vertex;
                                minMaxAABB.Encapsulate(vertex.Position + vertex.BillboardPos);
                            }
                            UpdateIndicesForParticle(indices, particleIndices, particleIndex, numIndicesPerParticle, vertexOffset);
                            particleIndex++;
                        }).Run();

                    int numVertices = particleIndex * numVertsPerParticle;
                    int numIndices = particleIndex * numIndicesPerParticle;
                    if (UpdateDynamicMeshData(EntityManager, ecb, entity, eMesh, meshRenderer, minMaxAABB, numVertices, numIndices))
                    {
                        vBuffer.ResizeUninitialized(numVertices);
                        iBuffer.ResizeUninitialized(numIndices);
                    }
                }
                else
                {
                    ref SimpleMeshData smd = ref EntityManager.GetComponentData<SimpleMeshRenderData>(particleMesh.mesh).Mesh.Value;
                    int numVertsPerParticle = smd.Vertices.Length;
                    int numIndicesPerParticle = smd.Indices.Length;
                    var particleVertices = MeshHelper.AsNativeArray(ref smd.Vertices);
                    var particleIndices = MeshHelper.AsNativeArray(ref smd.Indices);

                    var vBuffer = EntityManager.GetBuffer<DynamicSimpleVertex>(eMesh);
                    var iBuffer = EntityManager.GetBuffer<DynamicIndex>(eMesh);

                    // Note that buffers have their capacity initialized to support the max particles allowed for an emitter
                    vBuffer.ResizeUninitialized(vBuffer.Capacity);
                    iBuffer.ResizeUninitialized(iBuffer.Capacity);
                    var vertices = vBuffer.AsNativeArray().Reinterpret<DynamicSimpleVertex, SimpleVertex>();
                    var indices = iBuffer.AsNativeArray().Reinterpret<DynamicIndex, ushort>();

                    ushort particleIndex = 0;
                    MinMaxAABB minMaxAABB = MinMaxAABB.Empty;
                    Entities
                        .WithSharedComponentFilter(emitterReferenceForParticles)
                        .ForEach((Entity eParticle, Particle particle) =>
                        {
                            var color = colorGetter[eParticle].color;
                            var localToWorld = localToWorldGetter[eParticle].Value;
                            var localToWorldRS = new float3x3(localToWorld.c0.xyz, localToWorld.c1.xyz, localToWorld.c2.xyz);

                            // Postpone translate until after additional rotation for billboarding is applied in shader
                            float4x4 model = billboarded ? new float4x4(localToWorldRS, float3.zero) : localToWorld;

                            int vertexOffset = particleIndex * numVertsPerParticle;
                            for (int i = 0; i < numVertsPerParticle; i++)
                            {
                                SimpleVertex vertex = particleVertices[i];
                                vertex.Position = math.transform(model, vertex.Position);
                                vertex.Color = color;
                                vertex.BillboardPos = billboarded ? localToWorld.c3.xyz : float3.zero;
                                vertices[vertexOffset + i] = vertex;
                                minMaxAABB.Encapsulate(vertex.Position + vertex.BillboardPos);
                            }
                            UpdateIndicesForParticle(indices, particleIndices, particleIndex, numIndicesPerParticle, vertexOffset);
                            particleIndex++;
                        }).Run();

                    int numVertices = particleIndex * numVertsPerParticle;
                    int numIndices = particleIndex * numIndicesPerParticle;
                    if (UpdateDynamicMeshData(EntityManager, ecb, entity, eMesh, meshRenderer, minMaxAABB, numVertices, numIndices))
                    {
                        vBuffer.ResizeUninitialized(numVertices);
                        iBuffer.ResizeUninitialized(numIndices);
                    }
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
            entities.Dispose();
        }

        static bool UpdateDynamicMeshData(EntityManager mgr, EntityCommandBuffer ecb, Entity eRenderer, Entity eMesh, MeshRenderer meshRenderer, MinMaxAABB minMaxAABB, int numVertices, int numIndices)
        {
            if (numVertices > 0)
            {
                var dmd = mgr.GetComponentData<DynamicMeshData>(eMesh);
                dmd.Dirty = true;
                dmd.NumVertices = numVertices;
                dmd.NumIndices = numIndices;
                mgr.SetComponentData(eMesh, dmd);

                MeshBounds mb;
                mb.Bounds = minMaxAABB;
                mgr.SetComponentData(eMesh, mb);

                meshRenderer.indexCount = numIndices;
                mgr.SetComponentData(eRenderer, meshRenderer);

                if (mgr.HasComponent<Disabled>(eRenderer))
                    ecb.RemoveComponent<Disabled>(eRenderer);
                return true;
            }
            if (!mgr.HasComponent<Disabled>(eRenderer))
                ecb.AddComponent(eRenderer, new Disabled());
            return false;
        }

        static void UpdateIndicesForParticle(NativeArray<ushort> indices, NativeArray<ushort> particleIndices, int particleIndex, int numIndicesPerParticle, int vertexOffset)
        {
            int indexOffset = particleIndex * numIndicesPerParticle;
            for (int i = 0; i < numIndicesPerParticle; i++)
                indices[indexOffset + i] = (ushort)(particleIndices[i] + vertexOffset);
        }
    }
}
