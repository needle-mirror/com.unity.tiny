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
        EntityQuery m_ParticleRenderersQuery;

        protected override void OnCreate()
        {
            base.OnCreate();
            var rendererQueryDesc = new EntityQueryDesc
            {
                All = new[] { typeof(MeshRenderer), ComponentType.ReadOnly<EmitterReferenceForRenderer>() },
                Options = EntityQueryOptions.IncludeDisabled
            };
            m_ParticleRenderersQuery = GetEntityQuery(rendererQueryDesc);
        }

        protected override void OnUpdate()
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
            NativeArray<Entity> renderers = m_ParticleRenderersQuery.ToEntityArray(Allocator.TempJob);
            foreach (var renderer in renderers)
            {
                var emitterReference = EntityManager.GetComponentData<EmitterReferenceForRenderer>(renderer);
                var eEmitter = emitterReference.emitter;

                // In some cases it takes an extra frame for the particle entities that reference a destroyed emitter to be destroyed, most likely because of system state components
                if (!ParticlesUtil.EmitterIsValid(EntityManager, eEmitter))
                    continue;

                DynamicBuffer<Particle> particles = EntityManager.GetBuffer<DynamicParticle>(eEmitter).Reinterpret<Particle>();

                if (!EntityManager.GetEnabled(eEmitter) || particles.Length == 0)
                {
                    if (!EntityManager.HasComponent<Disabled>(renderer))
                        ecb.AddComponent(renderer, new Disabled());

                    continue;
                }

                if (EntityManager.HasComponent<Disabled>(renderer))
                    ecb.RemoveComponent<Disabled>(renderer);

                var particleMesh = EntityManager.GetComponentData<ParticleMesh>(eEmitter);
                var meshRenderer = EntityManager.GetComponentData<MeshRenderer>(renderer);
                Entity eMesh = meshRenderer.mesh;
                var particleEmitter = EntityManager.GetComponentData<ParticleEmitter>(eEmitter);
                var localToWorldEmitter = particleEmitter.AttachToEmitter ? EntityManager.GetComponentData<LocalToWorld>(eEmitter).Value : float4x4.identity;
                MinMaxAABB minMaxAABB = MinMaxAABB.Empty;
                int numVertices, numIndices;
                if (EntityManager.HasComponent<LitParticleRenderer>(renderer))
                {
                    ref LitMeshData lmd = ref EntityManager.GetComponentData<LitMeshRenderData>(particleMesh.Mesh).Mesh.Value;
                    int numVertsPerParticle = lmd.Vertices.Length;
                    int numIndicesPerParticle = lmd.Indices.Length;
                    NativeArray<LitVertex> particleVertices = MeshHelper.AsNativeArray(ref lmd.Vertices);
                    NativeArray<ushort> particleIndices = MeshHelper.AsNativeArray(ref lmd.Indices);
                    var vBuffer = EntityManager.GetBuffer<DynamicLitVertex>(eMesh);
                    var iBuffer = EntityManager.GetBuffer<DynamicIndex>(eMesh);
                    numVertices = particles.Length * numVertsPerParticle;
                    numIndices = particles.Length * numIndicesPerParticle;
                    vBuffer.ResizeUninitialized(numVertices); // Grow buffers as needed
                    iBuffer.ResizeUninitialized(numIndices);
                    NativeArray<LitVertex> vertices = vBuffer.AsNativeArray().Reinterpret<DynamicLitVertex, LitVertex>();
                    NativeArray<ushort> indices = iBuffer.AsNativeArray().Reinterpret<DynamicIndex, ushort>();
                    bool billboarded = EntityManager.GetComponentData<LitMaterial>(meshRenderer.material).billboarded;

                    for (int particleIndex = 0; particleIndex < particles.Length; particleIndex++)
                    {
                        float4x4 localToWorld = GetParticleLocalToWorld(particles[particleIndex], localToWorldEmitter);
                        float3x3 localToWorldRS = new float3x3(localToWorld.c0.xyz, localToWorld.c1.xyz, localToWorld.c2.xyz);
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
                            vertex.Albedo_Opacity = particles[particleIndex].color;
                            vertices[vertexOffset + i] = vertex;
                            minMaxAABB.Encapsulate(vertex.Position + vertex.BillboardPos);
                        }
                        UpdateIndicesForParticle(indices, particleIndices, particleIndex, numIndicesPerParticle, vertexOffset);
                    }
                }
                else
                {
                    ref SimpleMeshData smd = ref EntityManager.GetComponentData<SimpleMeshRenderData>(particleMesh.Mesh).Mesh.Value;
                    int numVertsPerParticle = smd.Vertices.Length;
                    int numIndicesPerParticle = smd.Indices.Length;
                    var particleVertices = MeshHelper.AsNativeArray(ref smd.Vertices);
                    var particleIndices = MeshHelper.AsNativeArray(ref smd.Indices);
                    var vBuffer = EntityManager.GetBuffer<DynamicSimpleVertex>(eMesh);
                    var iBuffer = EntityManager.GetBuffer<DynamicIndex>(eMesh);
                    numVertices = particles.Length * numVertsPerParticle;
                    numIndices = particles.Length * numIndicesPerParticle;
                    vBuffer.ResizeUninitialized(numVertices); // Grow buffers as needed
                    iBuffer.ResizeUninitialized(numIndices);
                    var vertices = vBuffer.AsNativeArray().Reinterpret<DynamicSimpleVertex, SimpleVertex>();
                    var indices = iBuffer.AsNativeArray().Reinterpret<DynamicIndex, ushort>();
                    bool billboarded = EntityManager.GetComponentData<SimpleMaterial>(meshRenderer.material).billboarded;

                    for (int particleIndex = 0; particleIndex < particles.Length; particleIndex++)
                    {
                        float4x4 localToWorld = GetParticleLocalToWorld(particles[particleIndex], localToWorldEmitter);
                        float3x3 localToWorldRS = new float3x3(localToWorld.c0.xyz, localToWorld.c1.xyz, localToWorld.c2.xyz);

                        // Postpone translate until after additional rotation for billboarding is applied in shader
                        float4x4 model = billboarded ? new float4x4(localToWorldRS, float3.zero) : localToWorld;

                        int vertexOffset = particleIndex * numVertsPerParticle;
                        for (int i = 0; i < numVertsPerParticle; i++)
                        {
                            SimpleVertex vertex = particleVertices[i];
                            vertex.Position = math.transform(model, vertex.Position);
                            vertex.Color = particles[particleIndex].color;
                            vertex.BillboardPos = billboarded ? localToWorld.c3.xyz : float3.zero;
                            vertices[vertexOffset + i] = vertex;
                            minMaxAABB.Encapsulate(vertex.Position + vertex.BillboardPos);
                        }
                        UpdateIndicesForParticle(indices, particleIndices, particleIndex, numIndicesPerParticle, vertexOffset);
                    }
                }

                MeshBounds mb;
                mb.Bounds = minMaxAABB;
                EntityManager.SetComponentData(eMesh, mb);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
            renderers.Dispose();
        }

        static float4x4  GetParticleLocalToWorld(Particle particle, float4x4 localToWorldEmitter)
        {
            particle.position = math.transform(localToWorldEmitter, particle.position);
            return float4x4.TRS(particle.position, particle.rotation, particle.scale);
        }

        static void UpdateIndicesForParticle(NativeArray<ushort> indices, NativeArray<ushort> particleIndices, int particleIndex, int numIndicesPerParticle, int vertexOffset)
        {
            int indexOffset = particleIndex * numIndicesPerParticle;
            for (int i = 0; i < numIndicesPerParticle; i++)
                indices[indexOffset + i] = (ushort)(particleIndices[i] + vertexOffset);
        }
    }
}
