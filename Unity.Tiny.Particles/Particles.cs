using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Tiny.Assertions;
using Unity.Transforms;
using Unity.Tiny.Rendering;

/// <summary>
///  The Particles module allows you to create the particle effects such as smoke,
///  fire, explosions, and simple liquid effects.
///
/// The most important component is ParticleEmitter, which allows you to customize
///  emitter parameters such as the rate of emission, the number of particles emitted
///  the lifetime of emitted particles, and so on.
///
///  When setting up a particle system, you must also create an entity for the emitter
///  to use as a particle template. This is usually an entity with a SpriteRenderer
///  component.
///
///  Other components  work with the emitter to control particle parameters such as
///  velocity, rotation, tint, scale, and so on, both initially and over the lifetime
///  of the particles.
/// </summary>
namespace Unity.Tiny.Particles
{
    /// <summary>
    /// An inclusive range of values. <see cref="start"/> should be less than or equal to <see cref="end"/>.
    /// </summary>
    public struct Range
    {
        public float start;
        public float end;
    }

    /// <summary>
    ///  Each spawned particle has this component attached to it automatically.
    /// </summary>
    public struct Particle : IComponentData
    {
        /// <summary>How long this particle has existed, in seconds.  From 0.0 to lifetime.</summary>
        public float time;

        /// <summary>The maximum lifetime of this particle, in seconds.</summary>
        public float lifetime;
    };

    /// <summary>
    /// The core particle emitter component.  Adding this component to an entity
    /// turns the entity into an emitter with the specified characteristics.
    /// You can add other components (for example, EmitterInitialScale, EmitterConeSource,
    /// and so on) to the same entity after the initial emission to further control
    /// how particles are emitted.
    /// </summary>
    public struct ParticleEmitter : IComponentData
    {
        /// <summary>
        ///  The "proto-particle" entity that is used as a particle template.
        ///  This entity is instantiated for each emitted particle.
        /// </summary>
        public Entity particle;

        /// <summary>The length of time the system runs in seconds.</summary>
        public float duration;

        /// <summary>Maximum number of particles to emit.</summary>
        public uint maxParticles;

        /// <summary>Number of particles per second to emit.</summary>
        public Range emitRate;

        /// <summary>Lifetime of each particle, in seconds.</summary>
        public Range lifetime;

        /// <summary>
        ///  Specifies whether the Transform of the emitted particles is a child
        ///  of this emitter.
        ///
        ///  If true, the emission position is set to the entity's local position,
        ///  and the particle is added as a child transform.
        ///
        ///  If false, the emitter's world position is added to the emission position,
        ///  and that result set as the local position.
        /// </summary>
        public bool attachToEmitter;
    }

    /// <summary>
    ///  Spawns particles inside of a unit rectangle centered at the origin on the X/Y plane.
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct EmitterRectangleSource : IComponentData {}

    /// <summary>
    ///  Spawns particles in a cone. Particles are emitted from the base of the cone, which is a circle on the X/Z plane.
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct EmitterConeSource : IComponentData
    {
        /// <summary>The radius in which the particles are being spawned.</summary>
        public float radius;

        /// <summary>The angle of the cone in degrees. The angle value will be clamped to range [0, 90].</summary>
        public float angle;
    }

    /// <summary>
    ///  Spawns particles inside a circle on the X/Y plane.
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct EmitterCircleSource : IComponentData
    {
        /// <summary>The radius of the circle.</summary>
        public float radius;
    }

    /// <summary>
    /// Spawns particles inside a sphere volume.
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct EmitterSphereSource : IComponentData
    {
        /// <summary>The radius of the sphere.</summary>
        public float radius;
    }

    /// <summary>
    /// Spawns particles inside a hemisphere volume.
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct EmitterHemisphereSource : IComponentData
    {
        /// <summary>The radius of the hemisphere.</summary>
        public float radius;
    }

    /// <summary>
    /// Sets the initial speed of the source particle by a random value in the range
    /// specified by <see cref="speed"/>
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct EmitterInitialSpeed : IComponentData
    {
        public Range speed;
    }

    /// <summary>
    /// Multiplies the scale of the source particle by a random value in the range
    /// specified by <see cref="scale"/>
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct EmitterInitialScale : IComponentData
    {
        public Range scale;
    }

    /// <summary>
    /// Multiplies the X, Y, and Z scales of the source particle by random values in the ranges
    /// specified by <see cref="scaleX"/>, <see cref="scaleY"/>, and <see cref="scaleZ"/> respectively
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct EmitterInitialNonUniformScale : IComponentData
    {
        public Range scaleX;
        public Range scaleY;
        public Range scaleZ;
    }


    /// <summary>
    /// Sets the initial rotation for particles to a random value in the range specified by <see cref="angle"/>.
    /// This axis of rotation is determined from the particle's direction of travel.
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct EmitterInitialRotation : IComponentData
    {
        /// <summary>Angle of rotation in radians.</summary>
        public Range angle;
    }

    /// <summary>
    /// Sets the initial rotations on the X, Y, and Z axes for particles to a random values in the ranges
    /// specified by <see cref="angleX"/>, <see cref="angleX"/>, and <see cref="angleX"/> respectively
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct EmitterInitialNonUniformRotation : IComponentData
    {
        /// <summary>Angle of rotation about the X axis in radians.</summary>
        public Range angleX;

        /// <summary>Angle of rotation about the Y axis in radians.</summary>
        public Range angleY;

        /// <summary>Angle of rotation about the Z axis in radians.</summary>
        public Range angleZ;
    }

    /// <summary>
    /// Moves particle spawn position by a random amount, up to <see cref="Value"/>.
    /// When <see cref="Value"/> is 0, this has no effect.
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct RandomizePosition : IComponentData
    {
        /// <summary>Must be in range [0.0, 1.0].</summary>
        public float Value;
    }

    /// <summary>
    /// Blends particle directions of travel towards a random direction using <see cref="Value"/>.
    /// When <see cref="Value"/> is 0, this has no effect.
    /// When <see cref="Value"/> is 1, the particle direction is completely random.
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct RandomizeDirection : IComponentData
    {
        /// <summary>Must be in range [0.0, 1.0].</summary>
        public float Value;
    }

    /// <summary>
    /// An emitter with this component repeats its particle simulation each time it reaches the end of its duration time.
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct Looping : IComponentData {}

    /// <summary>
    /// Delays the particle system from starting emission by a random value in the range specified by <see cref="delay"/>.
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct StartDelay : IComponentData
    {
        /// <summary>Delay time in seconds.</summary>
        public Range delay;
    }

    /// <summary>
    /// Sets the initial color of the particles by linearly interpolating between <see cref="colorMin"/> and <see cref="colorMax"/>
    /// by a random value between 0.0 and 1.0
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct InitialColor : IComponentData
    {
        public float4 colorMin;
        public float4 colorMax;
    }

    /// <summary>
    /// Allows for specifying the seed for all randomness used in the emitter's particle simulation so unique, repeatable effects can be created.
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct RandomSeed : IComponentData
    {
        public uint seed;
    }

#if false
    /// <summary>
    ///  Modifies the SpriteRenderer's color by multiplying it's initial color by
    ///  curve. The value of curve at time 0.0 defines the particle's color at the
    ///  beginning of its lifetime. The value at time 1.0 defines the particle's
    ///  color at the end of its lifetime.
    /// </summary>
    public struct LifetimeColor : IComponentData
    {
        /// <summary>Entity with the [Bezier|Linear|Step]CurveColor component.</summary>
        public Entity curve;
    }

    /// <summary>
    ///  Modifies the Transform's scale (uniform x/y/z scaling) by curve. The value
    ///  of curve at time 0.0 defines the particle's color at the beginning of its
    ///  lifetime. The value at time 1.0 defines the particle's color at the end
    ///  of its lifetime.
    /// </summary>
    public struct LifetimeScale : IComponentData
    {
        /// <summary>Entity with the [Bezier|Linear|Step]CurveFloat component.</summary>
        public Entity curve;
    }

    /// <summary>
    ///  The angular velocity over lifetime. The value of curve at time 0.0 defines
    ///  the particle's angular velocity at the beginning of its lifetime. The value
    ///  at time 1.0 defines the particle's angular velocity at the end of its lifetime.
    /// </summary>
    public struct LifetimeAngularVelocity : IComponentData
    {
        /// <summary>Entity with the [Bezier|Linear|Step]CurveFloat component.</summary>
        public Entity curve;
    }

    /// <summary>
    ///  The velocity over lifetime. The value of curve at time 0.0 defines
    ///  the particle's velocity at the beginning of its lifetime. The value
    ///  at time 1.0 defines the particle's velocity at the end of its lifetime.
    /// </summary>
    public struct LifetimeVelocity : IComponentData
    {
        /// <summary>Entity with the [Bezier|Linear|Step]CurveVector3 component.</summary>
        public Entity curve;
    }

    /// <summary>
    ///  Speed multiplier over lifetime. The value of curve at time 0.0 defines the
    ///  multiplier at the beginning of the particle's lifetime. The value at time
    ///  1.0 defines the multiplier at the end of the particle's lifetime.
    /// </summary>
    public struct LifetimeSpeedMultiplier : IComponentData
    {
        /// <summary>Entity with the [Bezier|Linear|Step]CurveFloat component.</summary>
        public Entity curve;
    }
#endif
    /// <summary>
    ///  An emitter with this component emits particles in bursts. A burst is a particle
    ///  event where a number of particles are all emitted at the same time. A cycle
    ///  is a single occurrence of a burst.
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct BurstEmission : IComponentData
    {
        /// <summary> How many particles in every cycle. </summary>
        public Range count;

        /// <summary> The interval between cycles, in seconds. </summary>
        public float interval;

        /// <summary> How many times to play the burst. </summary>
        public int cycles;
    }

    /// <summary>
    /// An emitter with this component has particles that always face the camera.
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct Billboarded : IComponentData {}

    /// <summary>
    /// Mesh used for each particle.
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct ParticleMesh : IComponentData
    {
        public Entity mesh;
    }

    /// <summary>
    /// Material used to render the particles.
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct ParticleMaterial : IComponentData
    {
        public Entity material;
    }

    /// <summary>
    /// System that updates all particle emitters
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public class EmitterSystem : SystemBase
    {
        static Random m_rand = new Random(1);

        private void UpdateNewEmitters(EntityManager mgr)
        {
            Entities
                .WithStructuralChanges()
                .WithNone<ParticleEmitterInternal>()
                .ForEach((Entity e, ref ParticleEmitter particleEmitter) =>
                {
                    // Initialize new emitters and particle templates
                    var emitterInternal = new ParticleEmitterInternal();

                    if (!mgr.HasComponent<Disabled>(particleEmitter.particle))
                        mgr.AddComponentData(particleEmitter.particle, new Disabled());
                    emitterInternal.particleTemplate = mgr.Instantiate(particleEmitter.particle);
                    mgr.AddComponentData(emitterInternal.particleTemplate, new Particle());
                    var position = new Translation { Value = float3.zero };
                    mgr.AddComponentData(emitterInternal.particleTemplate, position);
                    mgr.AddComponentData(emitterInternal.particleTemplate, new ParticleColor { color = new float4(1) });
                    mgr.AddSharedComponentData(emitterInternal.particleTemplate, new EmitterReferenceForParticles { emitter = e });

                    emitterInternal.particleRenderer = CreateParticleRenderer(mgr, e, particleEmitter);

                    // if duration is invalid then set to default
                    if (particleEmitter.duration <= 0.0f)
                        particleEmitter.duration = 5.0f;

                    uint seed = mgr.HasComponent<RandomSeed>(e) ? mgr.GetComponentData<RandomSeed>(e).seed : m_rand.NextUInt();
                    if (seed == 0) // Zero seed is invalid
                        seed = 1;
                    Random rand = new Random(seed);

                    if (mgr.HasComponent<StartDelay>(e))
                        emitterInternal.remainingDelay = rand.RandomRange(mgr.GetComponentData<StartDelay>(e).delay);

                    mgr.AddComponentData(e, emitterInternal);
                    mgr.AddComponentData(e, new Rng { rand = rand });
                }).Run();

            CleanupBurstEmitters(mgr);
            InitBurstEmitters(mgr);
        }

        private static Entity CreateParticleRenderer(EntityManager mgr, Entity eEmitter, ParticleEmitter particleEmitter)
        {
            Entity eParticleRenderer = mgr.CreateEntity(typeof(MeshRenderer), typeof(EmitterReferenceForRenderer), typeof(LocalToWorld), typeof(DynamicMeshData));
            var material = mgr.GetComponentData<ParticleMaterial>(eEmitter).material;
            Assert.IsTrue(material != Entity.Null);
            mgr.AddComponentData(eParticleRenderer, new MeshRenderer
            {
                mesh = eParticleRenderer,
                material = material,
                startIndex = 0,
                indexCount = 0
            });
            mgr.AddComponentData(eParticleRenderer, new EmitterReferenceForRenderer { emitter = eEmitter });
            mgr.AddComponentData(eParticleRenderer, new LocalToWorld { Value = float4x4.identity });

            bool isLit = mgr.HasComponent<LitMaterial>(material);
            if (isLit)
                mgr.AddComponentData(eParticleRenderer, new LitMeshRenderer());
            else
            {
                Assert.IsTrue(mgr.HasComponent<SimpleMaterial>(material));
                mgr.AddComponentData(eParticleRenderer, new SimpleMeshRenderer());
            }

            // Particle mesh
            ParticleMesh particleMesh;
            if (mgr.HasComponent<Billboarded>(eEmitter))
            {
                Assert.IsTrue(!mgr.HasComponent<ParticleMesh>(eEmitter));

                // Add mesh for 1x1 quad centered at the origin
                Entity mesh;
                if (isLit)
                {
                    LitMeshRenderData lmrd;
                    MeshBounds mb;
                    MeshHelper.CreatePlaneLit(new float3(-0.5f, -0.5f, 0), new float3(1, 0, 0), new float3(0, 1, 0), out mb, out lmrd);
                    mesh = mgr.CreateEntity(typeof(LitMeshRenderData), typeof(MeshBounds));
                    mgr.SetComponentData(mesh, lmrd);
                    mgr.SetComponentData(mesh, mb);
                    var litMaterial = mgr.GetComponentData<LitMaterial>(material);
                    litMaterial.billboarded = true;
                    mgr.SetComponentData(material, litMaterial);
                }
                else
                {
                    SimpleMeshRenderData smrd;
                    MeshBounds mb;
                    MeshHelper.CreatePlane(new float3(-0.5f, -0.5f, 0), new float3(1, 0, 0), new float3(0, 1, 0), out mb, out smrd);
                    mesh = mgr.CreateEntity(typeof(SimpleMeshRenderData), typeof(MeshBounds));
                    mgr.SetComponentData(mesh, smrd);
                    mgr.SetComponentData(mesh, mb);
                    var simpleMaterial = mgr.GetComponentData<SimpleMaterial>(material);
                    simpleMaterial.billboarded = true;
                    mgr.SetComponentData(material, simpleMaterial);
                }
                particleMesh = new ParticleMesh { mesh = mesh };
                mgr.AddComponentData(eEmitter, particleMesh);
            }
            else
            {
                Assert.IsTrue(mgr.HasComponent<ParticleMesh>(eEmitter));
                particleMesh = mgr.GetComponentData<ParticleMesh>(eEmitter);
                Assert.IsTrue(particleMesh.mesh != Entity.Null);
            }

            // Setup dynamic vertex/index buffers
            int vertexCapacity, indexCapacity;
            if (isLit)
            {
                ref LitMeshData lmd = ref mgr.GetComponentData<LitMeshRenderData>(particleMesh.mesh).Mesh.Value;
                vertexCapacity = (int)(lmd.Vertices.Length * particleEmitter.maxParticles);
                indexCapacity = (int)(lmd.Indices.Length * particleEmitter.maxParticles);
                mgr.AddBuffer<DynamicLitVertex>(eParticleRenderer);
                var vBuffer = mgr.GetBuffer<DynamicLitVertex>(eParticleRenderer);
                vBuffer.Capacity = vertexCapacity;
            }
            else
            {
                ref SimpleMeshData smd = ref mgr.GetComponentData<SimpleMeshRenderData>(particleMesh.mesh).Mesh.Value;
                vertexCapacity = (int)(smd.Vertices.Length * particleEmitter.maxParticles);
                indexCapacity = (int)(smd.Indices.Length * particleEmitter.maxParticles);
                mgr.AddBuffer<DynamicSimpleVertex>(eParticleRenderer);
                var vBuffer = mgr.GetBuffer<DynamicSimpleVertex>(eParticleRenderer);
                vBuffer.Capacity = vertexCapacity;
            }

            mgr.AddBuffer<DynamicIndex>(eParticleRenderer);
            var iBuffer = mgr.GetBuffer<DynamicIndex>(eParticleRenderer);
            iBuffer.Capacity = indexCapacity;

            DynamicMeshData dmd = new DynamicMeshData
            {
                VertexCapacity = vertexCapacity,
                IndexCapacity = indexCapacity,
                NumVertices = 0,
                NumIndices = 0,
                UseDynamicGPUBuffer = true
            };
            mgr.AddComponentData(eParticleRenderer, dmd);
            mgr.AddComponent<MeshBounds>(eParticleRenderer);

            return eParticleRenderer;
        }

        private void CleanupBurstEmitters(EntityManager mgr)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            // Remove BurstEmissionInternal component if the user removed BurstEmission
            Entities
                .WithNone<BurstEmission>()
                .WithAll<ParticleEmitter, BurstEmissionInternal>()
                .ForEach((Entity e) =>
                {
                    ecb.RemoveComponent<BurstEmissionInternal>(e);
                }).Run();

            ecb.Playback(mgr);
            ecb.Dispose();
        }

        private void InitBurstEmitters(EntityManager mgr)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            // Add BurstEmissionInternal component to newly created burst emitters
            Entities.WithNone<BurstEmissionInternal>().WithAll<ParticleEmitter, BurstEmission>().ForEach((Entity e) =>
            {
                ecb.AddComponent(e, new BurstEmissionInternal());
            }).Run();

            ecb.Playback(mgr);
            ecb.Dispose();
        }

        private void CleanupEmitters(EntityManager mgr)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            Entities.WithoutBurst().WithNone<ParticleEmitter>().WithAll<ParticleEmitterInternal>().ForEach((Entity e) =>
            {
                DestroyParticlesForEmitter(ecb, e);
                ecb.DestroyEntity(mgr.GetComponentData<ParticleEmitterInternal>(e).particleRenderer);
                ecb.RemoveComponent<ParticleEmitterInternal>(e);
            }).Run();

            ecb.Playback(mgr);
            ecb.Dispose();
        }

        private void DestroyParticlesForEmitter(EntityCommandBuffer ecb, Entity emitter)
        {
            EmitterReferenceForParticles emitterReference = new EmitterReferenceForParticles { emitter = emitter };
            Entities
                .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabled) // needed for proto particle
                .WithSharedComponentFilter(emitterReference).ForEach((Entity e) =>
                {
                    ecb.DestroyEntity(e);
                }).Run();
        }

        protected override void OnUpdate()
        {
            CleanupEmitters(EntityManager);
            UpdateNewEmitters(EntityManager);
        }
    }

    /// <summary>
    ///  System that handles spawning new particles for all emitters
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EmitterSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public class ParticleSpawnSystem : SystemBase
    {
        const int kCycleInfinitely = 0;
        private void SpawnParticles(EntityManager mgr, EntityCommandBuffer ecb, float deltaTime, Entity emitter)
        {
            var particleEmitter = mgr.GetComponentData<ParticleEmitter>(emitter);
            var particleEmitterInternal = mgr.GetComponentData<ParticleEmitterInternal>(emitter);
            Random rand = mgr.GetComponentData<Rng>(emitter).rand;

            // For debug
            //Debug.LogFormatAlways("Emitter: {0}, Particles: {1}, Max: {2}, Emit Rate: {3}, Lifetime: {4}", emitter.Index, (int)particleEmitterInternal.numParticles, (int)particleEmitter.maxParticles, particleEmitter.emitRate.end, particleEmitter.lifetime.end);

            // Burst emission mode
            uint burstParticleCount = 0;
            if (mgr.HasComponent<BurstEmissionInternal>(emitter) && mgr.HasComponent<BurstEmission>(emitter))
            {
                var burstEmission = mgr.GetComponentData<BurstEmission>(emitter);
                var burstEmissionInternal = mgr.GetComponentData<BurstEmissionInternal>(emitter);

                if (burstEmission.cycles == kCycleInfinitely || burstEmissionInternal.cycle < burstEmission.cycles)
                {
                    burstEmissionInternal.cooldown -= deltaTime;
                    if (burstEmissionInternal.cooldown < 0.0f)
                    {
                        burstParticleCount = (uint)rand.NextInt((int)burstEmission.count.start, (int)burstEmission.count.end);
                        if (burstEmission.cycles != kCycleInfinitely)
                            burstEmissionInternal.cycle++;
                        burstEmissionInternal.cooldown = burstEmission.interval;
                    }

                    mgr.SetComponentData(emitter, burstEmissionInternal);
                }
            }

            // Normal emission mode
            uint particlesToSpawn = 0;
            if (particleEmitter.emitRate.start >= 0.0f && particleEmitter.emitRate.end > 0.0f)
            {
                particleEmitterInternal.particleSpawnCooldown += deltaTime;
                float particleSpawnDelay = 1.0f / rand.RandomRange(particleEmitter.emitRate);

                particlesToSpawn = (uint)(particleEmitterInternal.particleSpawnCooldown / particleSpawnDelay);

                if (particlesToSpawn > 0)
                    particleEmitterInternal.particleSpawnCooldown -= particleSpawnDelay * particlesToSpawn;

                mgr.SetComponentData(emitter, particleEmitterInternal);
            }

            particlesToSpawn += burstParticleCount;
            uint maxParticlesToSpawn = particleEmitter.maxParticles - particleEmitterInternal.numParticles;
            if (particlesToSpawn > maxParticlesToSpawn)
                particlesToSpawn = maxParticlesToSpawn;

            if (particlesToSpawn == 0)
            {
                mgr.SetComponentData(emitter, new Rng { rand = rand });
                return;
            }

            var newParticles = new NativeArray<Entity>((int)particlesToSpawn, Allocator.Persistent);

            // Before the new particles will spawn, Disabled component needs to be removed from the template particle
            ecb.RemoveComponent<Disabled>(particleEmitterInternal.particleTemplate);

            for (int i = 0; i < newParticles.Length; i++)
                newParticles[i] = ecb.Instantiate(particleEmitterInternal.particleTemplate);

            ecb.AddComponent(particleEmitterInternal.particleTemplate, new Disabled());

            InitTime(ecb, deltaTime, particleEmitter.lifetime, newParticles, ref rand);

            Parent parent;
            if (particleEmitter.attachToEmitter)
            {
                parent = new Parent { Value = emitter };
            }
            else
            {
                Assert.IsTrue(EntityManager.HasComponent<LocalToWorld>(emitter));
                var localToWorld = mgr.GetComponentData<LocalToWorld>(emitter);
                Entity toWorldParent = ecb.CreateEntity();
                ecb.AddComponent(toWorldParent, localToWorld);
                parent = new Parent { Value = toWorldParent };
            }

            foreach (var particle in newParticles)
            {
                ecb.AddComponent(particle, parent);
                ecb.AddComponent(particle, new LocalToParent
                {
                    Value = float4x4.identity
                });

                ecb.AddComponent(particle, new Translation { Value = float3.zero });
            }

#if false
            if (mgr.HasComponent<EmitterInitialVelocity>(emitter))
            {
                var velocity = mgr.GetComponentData<EmitterInitialVelocity>(emitter).velocity;
                var particleVelocity = new ParticleVelocity { velocity = velocity };
                foreach (var particle in newParticles)
                    ecb.AddComponent(particle, particleVelocity);
            }
            else if (mgr.HasComponent<LifetimeVelocity>(emitter))
            {
                foreach (var particle in newParticles)
                    ecb.AddComponent(particle, new ParticleVelocity { velocity = float3.zero });
            }
#endif
            InitColor(mgr, ecb, emitter, newParticles, ref rand);
            InitScale(mgr, ecb, emitter, newParticles, ref rand);

            // Init particle's position and the velocity based on the source
            Range speed = mgr.GetComponentData<EmitterInitialSpeed>(emitter).speed;
            float randomizePos = mgr.HasComponent<RandomizePosition>(emitter) ? mgr.GetComponentData<RandomizePosition>(emitter).Value : 0.0f;
            float randomizeDir = mgr.HasComponent<RandomizeDirection>(emitter) ? mgr.GetComponentData<RandomizeDirection>(emitter).Value : 0.0f;
            if (mgr.HasComponent<EmitterRectangleSource>(emitter))
            {
                ParticlesSource.InitEmitterRectangleSource(mgr, ecb, emitter, newParticles, speed, randomizePos, randomizeDir, ref rand);
            }
            else if (mgr.HasComponent<EmitterCircleSource>(emitter))
            {
                ParticlesSource.InitEmitterCircleSource(mgr, ecb, emitter, newParticles, speed, randomizePos, randomizeDir, ref rand);
            }
            else if (mgr.HasComponent<EmitterConeSource>(emitter))
            {
                ParticlesSource.InitEmitterConeSource(mgr, ecb, emitter, newParticles, speed, randomizePos, randomizeDir, ref rand);
            }
            else if (mgr.HasComponent<EmitterSphereSource>(emitter))
            {
                var radius = mgr.GetComponentData<EmitterSphereSource>(emitter).radius;
                ParticlesSource.InitEmitterSphereSource(mgr, ecb, emitter, newParticles, radius, false, speed, randomizePos, randomizeDir, ref rand);
            }
            else if (mgr.HasComponent<EmitterHemisphereSource>(emitter))
            {
                var radius = mgr.GetComponentData<EmitterHemisphereSource>(emitter).radius;
                ParticlesSource.InitEmitterSphereSource(mgr, ecb, emitter, newParticles, radius, true, speed, randomizePos, randomizeDir, ref rand);
            }

            mgr.SetComponentData(emitter, new Rng { rand = rand });

            newParticles.Dispose();
        }

        static void InitTime(EntityCommandBuffer ecb, float deltaTime, Range lifetime, NativeArray<Entity> newParticles, ref Random rand)
        {
            // The time is evenly distributted from 0.0 to deltaTime.

            // The time of each subsequent particle will be increased by this value.
            // particle.time[0..n] = (0 * timeStep, 1 * timeStep, 2 * timeStep, ..., n * timeStep).
            float timeStep = newParticles.Length > 1 ? deltaTime / (newParticles.Length - 1) : 0.0f;

            // We need to subtract deltaTime from the particle's relative time, because later in
            // the same frame we are adding deltaTime to the particle's relative time when we process
            // them. This ensures that the first, newly created particle will start at a relative time 0.0.
            float time = -deltaTime;
            time += timeStep;

            foreach (var eParticle in newParticles)
            {
                ecb.AddComponent(eParticle, new Particle
                {
                    lifetime = rand.RandomRange(lifetime),
                    time = time
                });
            }
        }

        private static void InitScale(EntityManager mgr, EntityCommandBuffer ecb, Entity emitter, NativeArray<Entity> newParticles, ref Random rand)
        {
#if false
            bool hasInitialUniformScale = mgr.HasComponent<EmitterInitialScale>(emitter);
            bool hasInitialNonUniformScale = mgr.HasComponent<EmitterInitialNonUniformScale>(emitter);
            bool hasInitialScale = hasInitialUniformScale || hasInitialNonUniformScale;
            bool hasLifetimeScale = mgr.HasComponent<LifetimeScale>(emitter);

            if (!hasInitialScale && !hasLifetimeScale)
                return;

            if (hasLifetimeScale)
            {
                var lifetimeScale = mgr.GetComponentData<LifetimeScale>(emitter);

                if (hasInitialScale)
                {
                    var initialScale = mgr.GetComponentData<EmitterInitialScale>(emitter);

                    // LifetimeScale and EmitterInitialScale are present.
                    foreach (var particle in newParticles)
                    {
                        var localScale = mgr.GetComponentData<NonUniformScale>(particle).Value;
                        var scale = localScale * ParticlesUtil.RandomRange(initialScale.scale);
                        mgr.AddComponentData(particle, new ParticleLifetimeScale()
                        {
                            initialScale = localScale
                        });
                    }
                }
                else
                {
                    // Only LifetimeScale is present.
                    foreach (var particle in newParticles)
                    {
                        var localScale = mgr.GetComponentData<NonUniformScale>(particle).Value;
                        mgr.AddComponentData(particle, new ParticleLifetimeScale()
                        {
                            initialScale = localScale
                        });
                    }
                }
            }
            else if (hasInitialScale)
#endif
            {
                // Only EmitterInitialScale is present
                if (mgr.HasComponent<EmitterInitialScale>(emitter))
                {
                    var initialScale = mgr.GetComponentData<EmitterInitialScale>(emitter);
                    foreach (var particle in newParticles)
                    {
                        float scale = rand.RandomRange(initialScale.scale);
                        ecb.AddComponent(particle, new Scale { Value = scale });
                    }
                }
                else if (mgr.HasComponent<EmitterInitialNonUniformScale>(emitter))
                {
                    var initialScale = mgr.GetComponentData<EmitterInitialNonUniformScale>(emitter);
                    foreach (var particle in newParticles)
                    {
                        float3 scale = new float3(rand.RandomRange(initialScale.scaleX), rand.RandomRange(initialScale.scaleY), rand.RandomRange(initialScale.scaleZ));
                        ecb.AddComponent(particle, new NonUniformScale { Value = scale });
                    }
                }
            }
        }

        private void InitColor(EntityManager mgr, EntityCommandBuffer ecb, Entity emitter, NativeArray<Entity> newParticles, ref Random rand)
        {
#if false
            bool hasInitialColor = mgr.HasComponent<EmitterInitialColor>(emitter);
            bool hasLifetimeColor = mgr.HasComponent<LifetimeColor>(emitter);

            if (!hasInitialColor && !hasLifetimeColor)
                return;

            if (hasLifetimeColor)
            {
                var lifetimeColor = mgr.GetComponentData<LifetimeColor>(emitter);

                if (hasInitialColor)
                {
                    // LifetimeColor and EmitterInitialColor are present.

                    var initialColor = mgr.GetComponentData<EmitterInitialColor>(emitter);
                    foreach (var particle in newParticles)
                    {
                        var renderer = mgr.GetComponentData<SpriteRenderer>(particle);
                        var randomColor = InterpolationService.EvaluateCurveColor(
                            mgr, m_rand.NextFloat(), initialColor.curve);
                        var color = renderer.Color * randomColor;
                        mgr.AddComponentData(particle, new ParticleLifetimeColor()
                        {
                            initialColor = color
                        });
                    }
                }
                else
                {
                    // Only LifetimeColor is present.
                    foreach (var particle in newParticles)
                    {
                        var rendererColor = mgr.GetComponentData<SpriteRenderer>(particle).Color;
                        mgr.AddComponentData(particle, new ParticleLifetimeColor()
                        {
                            initialColor = rendererColor
                        });
                    }
                }
            }
#endif
            if (mgr.HasComponent<InitialColor>(emitter))
            {
                // Only InitialColor is present.
                var initialColor = mgr.GetComponentData<InitialColor>(emitter);
                foreach (var particle in newParticles)
                {
                    float4 color = math.lerp(initialColor.colorMin, initialColor.colorMax, rand.Random01());
                    ecb.SetComponent(particle, new ParticleColor { color = color });
                }
            }
        }

        protected override void OnUpdate()
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
            Entities
                .WithoutBurst()
                .ForEach((Entity e, ParticleEmitter emitter, ref ParticleEmitterInternal emitterInternal) =>
                {
                    float deltaTime = Time.DeltaTime;
                    bool looping = EntityManager.HasComponent<Looping>(e);
                    if (emitterInternal.remainingDelay < deltaTime)
                    {
                        // Update t
                        emitterInternal.t += deltaTime - emitterInternal.remainingDelay;
                        if (looping)
                        {
                            while (emitterInternal.t >= emitter.duration)
                                emitterInternal.t -= emitter.duration;
                        }
                        else
                            emitterInternal.t = math.min(emitterInternal.t, emitter.duration);
                    }
                    else
                    {
                        emitterInternal.remainingDelay -= deltaTime;
                        deltaTime = math.max(-emitterInternal.remainingDelay, 0.0f);
                        emitterInternal.remainingDelay = math.max(emitterInternal.remainingDelay, 0.0f);
                    }

                    if (!looping && emitterInternal.t >= emitter.duration)
                    {
                        // Destroy emitter once all existing particles have reached the end of their lifetime
                        if (emitterInternal.numParticles == 0)
                            ecb.DestroyEntity(e);
                    }
                    else
                    {
                        SpawnParticles(EntityManager, ecb, deltaTime, e);
                    }
                }).Run();

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    ///  System that updates all particles
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EmitterSystem))]
    [UpdateAfter(typeof(ParticleSpawnSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public class ParticleSystem : SystemBase
    {
        private void UpdateParticleLife(EntityManager mgr, EmitterReferenceForParticles emitterReference, float deltaTime)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            var emitterInternal = mgr.GetComponentData<ParticleEmitterInternal>(emitterReference.emitter);
            var emitter = mgr.GetComponentData<ParticleEmitter>(emitterReference.emitter);

            // Reset particle counter for this emitter
            emitterInternal.numParticles = 0;
            uint numParticles = 0;

            if (emitter.attachToEmitter)
            {
                Entities.WithSharedComponentFilter(emitterReference).ForEach((Entity e, ref Particle particle) =>
                {
                    particle.time += deltaTime;
                    if (particle.time >= particle.lifetime)
                        ecb.DestroyEntity(e);
                    else
                        numParticles++;
                }).Run();
            }
            else
            {
                Entities.WithSharedComponentFilter(emitterReference).ForEach((Entity e, ref Particle particle) =>
                {
                    particle.time += deltaTime;
                    if (particle.time >= particle.lifetime)
                    {
                        ecb.DestroyEntity(e);

                        // This assumes that we still own this parent, which is the dummy one created during particle spawning!
                        if (mgr.HasComponent<Parent>(e))
                            ecb.DestroyEntity(mgr.GetComponentData<Parent>(e).Value);
                    }
                    else
                    {
                        numParticles++;
                    }
                }).Run();
            }

            emitterInternal.numParticles = numParticles;
            mgr.SetComponentData(emitterReference.emitter, emitterInternal);

            ecb.Playback(mgr);
            ecb.Dispose();
        }

        private void UpdateParticlePosition(EntityManager mgr, EmitterReferenceForParticles emitterReference, float deltaTime)
        {
            Entities.WithSharedComponentFilter(emitterReference).ForEach((Entity e, ref Particle cParticle, ref ParticleVelocity cVelocity, ref Translation cLocalPos) =>
            {
#if false
                bool hasLifetimeVelocity = mgr.HasComponent<LifetimeVelocity>(emitterReference.emitter);
                bool hasLifetimeSpeed = mgr.HasComponent<LifetimeSpeedMultiplier>(emitterReference.emitter);
                float normalizedLife = cParticle.time / cParticle.lifetime;

                if (hasLifetimeVelocity && hasLifetimeSpeed)
                {
                    var lifetimeVel = mgr.GetComponentData<LifetimeVelocity>(emitterReference.emitter);
                    var lifetimeSpeed = mgr.GetComponentData<LifetimeSpeedMultiplier>(emitterReference.emitter);

                    var velocity = InterpolationService.EvaluateCurveFloat3(mgr, normalizedLife, lifetimeVel.curve);
                    var speed = InterpolationService.EvaluateCurveFloat(mgr, normalizedLife, lifetimeSpeed.curve);

                    cLocalPos.Value += (cVelocity.velocity + velocity) * speed * deltaTime;
                }
                else if (hasLifetimeVelocity)
                {
                    var lifetimeVel = mgr.GetComponentData<LifetimeVelocity>(emitterReference.emitter);

                    var velocity = InterpolationService.EvaluateCurveFloat3(mgr, normalizedLife, lifetimeVel.curve);
                    cLocalPos.Value += (cVelocity.velocity + velocity) * deltaTime;
                }
                else if (hasLifetimeSpeed)
                {
                    var lifetimeSpeed = mgr.GetComponentData<LifetimeSpeedMultiplier>(emitterReference.emitter);

                    var speed = InterpolationService.EvaluateCurveFloat(mgr, normalizedLife, lifetimeSpeed.curve);
                    cLocalPos.Value += cVelocity.velocity * speed * deltaTime;
                }
                else
#endif
                {
                    cLocalPos.Value += cVelocity.velocity * deltaTime;
                }
            }).Run();
        }

#if false
        private void UpdateParticleScale(EntityManager mgr, EntityCommandBuffer ecb, ParticleEmitterReference emitterReference, float deltaTime)
        {
            Entities.WithSharedComponentFilter(emitterReference).ForEach((ref Particle cParticle, ref ParticleLifetimeScale cLifetimeScale, ref NonUniformScale cLocalScale) =>
            {
                if (!mgr.HasComponent<LifetimeScale>(emitterReference.emitter))
                    return;

                var lifetimeScale = mgr.GetComponentData<LifetimeScale>(emitterReference.emitter);

                float normalizedLife = cParticle.time / cParticle.lifetime;

                var scale = InterpolationService.EvaluateCurveFloat(mgr, normalizedLife, lifetimeScale.curve);
                cLocalScale.Value = cLifetimeScale.initialScale * scale;
            }).Run();
        }

        private void UpdateParticleColor(EntityManager mgr, EntityCommandBuffer ecb, ParticleEmitterReference emitterReference, float deltaTime)
        {
            Entities.WithSharedComponentFilter(emitterReference).ForEach((ref Particle cParticle, ref SpriteRenderer cRenderer, ref ParticleLifetimeColor cLifetimeColor) =>
            {
                if (!mgr.HasComponent<LifetimeColor>(emitterReference.emitter))
                    return;

                var lifetimeColor = mgr.GetComponentData<LifetimeColor>(emitterReference.emitter);

                float normalizedLife = cParticle.time / cParticle.lifetime;

                var color = InterpolationService.EvaluateCurveColor(mgr, normalizedLife, lifetimeColor.curve);
                cRenderer.Color = cLifetimeColor.initialColor * color;
            }).Run();
        }

        private void UpdateParticleRotation(EntityManager mgr, EntityCommandBuffer ecb, ParticleEmitterReference emitterReference, float deltaTime)
        {
            Entities.WithSharedComponentFilter(emitterReference).ForEach(
                (ref Particle cParticle, ref Rotation cLocalRotation, ref ParticleAngularVelocity cAngularVelocity) =>
                {
                    if (mgr.HasComponent<LifetimeAngularVelocity>(emitterReference.emitter))
                    {
                        var lifetime = mgr.GetComponentData<LifetimeAngularVelocity>(emitterReference.emitter);

                        float normalizedLife = cParticle.time / cParticle.lifetime;

                        var angularVelocity = InterpolationService.EvaluateCurveFloat(mgr, normalizedLife, lifetime.curve);
                        float angle = (cAngularVelocity.angularVelocity + angularVelocity) * deltaTime;
                        cLocalRotation.Value = math.mul(cLocalRotation.Value, quaternion.Euler(0, 0, angle));
                    }
                    else
                    {
                        float angle = cAngularVelocity.angularVelocity * deltaTime;
                        cLocalRotation.Value = math.mul(cLocalRotation.Value, quaternion.Euler(0, 0, angle));
                    }
                }).Run();
        }

#endif
        protected override void OnUpdate()
        {
            float deltaTime = Time.DeltaTime;
            var emitterReferences = new List<EmitterReferenceForParticles>();
            EntityManager.GetAllUniqueSharedComponentData(emitterReferences);
            foreach (var emitterReference in emitterReferences)
            {
                // In some cases it takes an extra frame for the particle entities that reference a destroyed emitter to be destroyed, most likely because of system state components
                if (!ParticlesUtil.EmitterIsValid(EntityManager, emitterReference.emitter))
                    continue;

                UpdateParticleLife(EntityManager, emitterReference, deltaTime);
                UpdateParticlePosition(EntityManager, emitterReference, deltaTime);
#if false
                UpdateParticleRotation(EntityManager, emitterReference, deltaTime);
                UpdateParticleScale(EntityManager, emitterReference, deltaTime);
                UpdateParticleColor(EntityManager, emitterReference, deltaTime);
#endif
            }
        }
    }
}
