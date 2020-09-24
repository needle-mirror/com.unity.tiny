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
    /// An inclusive range of values. <see cref="Start"/> should be less than or equal to <see cref="End"/>.
    /// </summary>
    public struct Range
    {
        public float Start;
        public float End;
    }

    /// <summary>
    /// The core particle emitter component.  Adding this component to an entity
    /// turns the entity into an emitter with the specified characteristics.
    /// You can add other components (for example, EmitterInitialScale, EmitterConeSource,
    /// and so on) to the same entity after the initial emission to further control
    /// how particles are emitted.
    /// </summary>
    public struct ParticleEmitter : IComponentData
    {
        /// <summary>The length of time the system runs in seconds.</summary>
        public float Duration;

        /// <summary>Maximum number of particles to emit.</summary>
        public uint MaxParticles;

        /// <summary>Number of particles per second to emit.</summary>
        public Range EmitRate;

        /// <summary>Lifetime of each particle, in seconds.</summary>
        public Range Lifetime;

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
        public bool AttachToEmitter;
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
        public float Radius;

        /// <summary>The angle of the cone in degrees. The angle value will be clamped to range [0, 90].</summary>
        public float Angle;
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
        public float Radius;
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
        public float Radius;
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
        public float Radius;
    }

    /// <summary>
    /// Sets the initial speed of the source particle by a random value in the range
    /// specified by <see cref="Speed"/>
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct EmitterInitialSpeed : IComponentData
    {
        public Range Speed;
    }

    /// <summary>
    /// Multiplies the scale of the source particle by a random value in the range
    /// specified by <see cref="Scale"/>
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct EmitterInitialScale : IComponentData
    {
        public Range Scale;
    }

    /// <summary>
    /// Multiplies the X, Y, and Z scales of the source particle by random values in the ranges
    /// specified by <see cref="ScaleX"/>, <see cref="ScaleY"/>, and <see cref="ScaleZ"/> respectively
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct EmitterInitialNonUniformScale : IComponentData
    {
        public Range ScaleX;
        public Range ScaleY;
        public Range ScaleZ;
    }


    /// <summary>
    /// Sets the initial rotation for particles to a random value in the range specified by <see cref="Angle"/>.
    /// This axis of rotation is determined from the particle's direction of travel.
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct EmitterInitialRotation : IComponentData
    {
        /// <summary>Angle of rotation in radians.</summary>
        public Range Angle;
    }

    /// <summary>
    /// Sets the initial rotations on the X, Y, and Z axes for particles to a random values in the ranges
    /// specified by <see cref="AngleX"/>, <see cref="AngleX"/>, and <see cref="AngleX"/> respectively
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct EmitterInitialNonUniformRotation : IComponentData
    {
        /// <summary>Angle of rotation about the X axis in radians.</summary>
        public Range AngleX;

        /// <summary>Angle of rotation about the Y axis in radians.</summary>
        public Range AngleY;

        /// <summary>Angle of rotation about the Z axis in radians.</summary>
        public Range AngleZ;
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
    /// Delays the particle system from starting emission by a random value in the range specified by <see cref="Delay"/>.
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct StartDelay : IComponentData
    {
        /// <summary>Delay time in seconds.</summary>
        public Range Delay;
    }

    /// <summary>
    /// Sets the initial color of the particles by linearly interpolating between <see cref="ColorMin"/> and <see cref="ColorMax"/>
    /// by a random value between 0.0 and 1.0
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct InitialColor : IComponentData
    {
        public float4 ColorMin;
        public float4 ColorMax;
    }

    /// <summary>
    /// Allows for specifying the seed for all randomness used in the emitter's particle simulation so unique, repeatable effects can be created.
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct RandomSeed : IComponentData
    {
        public uint Value;
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
        public Range Count;

        /// <summary> The interval between cycles, in seconds. </summary>
        public float Interval;

        /// <summary> How many times to play the burst. </summary>
        public int Cycles;
    }

    /// <summary>
    /// Mesh used for each particle. An emitter entity that does not have this component
    /// will use billboards for particles that always face the camera.
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct ParticleMesh : IComponentData
    {
        public Entity Mesh;
    }

    /// <summary>
    /// Material used to render the particles.
    /// </summary>
    /// <remarks>
    /// Should be placed next to <see cref="ParticleEmitter"/>
    /// </remarks>
    public struct ParticleMaterial : IComponentData
    {
        public Entity Material;
    }

    /// <summary>
    /// System that updates all particle emitters
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
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
                    // Initialize new emitters
                    mgr.AddBuffer<DynamicParticle>(e);
                    var emitterInternal = new ParticleEmitterInternal();
                    emitterInternal.particleRenderer = CreateParticleRenderer(mgr, e);

                    // if duration is invalid then set to default
                    if (particleEmitter.Duration <= 0.0f)
                        particleEmitter.Duration = 5.0f;

                    uint seed = mgr.HasComponent<RandomSeed>(e) ? mgr.GetComponentData<RandomSeed>(e).Value : m_rand.NextUInt();
                    if (seed == 0) // Zero seed is invalid
                        seed = 1;
                    Random rand = new Random(seed);

                    if (mgr.HasComponent<StartDelay>(e))
                        emitterInternal.remainingDelay = rand.RandomRange(mgr.GetComponentData<StartDelay>(e).Delay);

                    mgr.AddComponentData(e, emitterInternal);
                    mgr.AddComponentData(e, new Rng { rand = rand });
                }).Run();

            CleanupBurstEmitters(mgr);
            InitBurstEmitters(mgr);
        }

        private static Entity CreateParticleRenderer(EntityManager mgr, Entity eEmitter)
        {
            Entity eParticleRenderer = mgr.CreateEntity(typeof(MeshRenderer), typeof(EmitterReferenceForRenderer), typeof(LocalToWorld), typeof(DynamicMeshData));
            var material = mgr.GetComponentData<ParticleMaterial>(eEmitter).Material;
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
                mgr.AddComponentData(eParticleRenderer, new LitParticleRenderer());
            else
            {
                Assert.IsTrue(mgr.HasComponent<SimpleMaterial>(material));
                mgr.AddComponentData(eParticleRenderer, new SimpleParticleRenderer());
            }

            // Particle mesh
            if (mgr.HasComponent<ParticleMesh>(eEmitter))
            {
                ParticleMesh particleMesh = mgr.GetComponentData<ParticleMesh>(eEmitter);
                Assert.IsTrue(particleMesh.Mesh != Entity.Null);
            }
            else // Billboarded
            {
                // Add mesh for 1x1 quad
                Entity mesh;
                float3 org = new float3(-0.5f, -0.5f, 0);
                float3 du = new float3(1, 0, 0);
                float3 dv = new float3(0, 1, 0);
                var builder = new BlobBuilder(Allocator.Temp);
                if (isLit)
                {
                    LitMeshRenderData lmrd;
                    MeshBounds mb;
                    ref var root = ref builder.ConstructRoot<LitMeshData>();
                    var vertices = builder.Allocate(ref root.Vertices, 4).AsNativeArray();
                    var indices = builder.Allocate(ref root.Indices, 6).AsNativeArray();
                    vertices[0] = new LitVertex { Position = org, TexCoord0 = new float2(0, 1) };
                    vertices[1] = new LitVertex { Position = org + du, TexCoord0 = new float2(1, 1) };
                    vertices[2] = new LitVertex { Position = org + du + dv, TexCoord0 = new float2(1, 0) };
                    vertices[3] = new LitVertex { Position = org + dv, TexCoord0 = new float2(0, 0) };
                    indices[0] = 0; indices[1] = 2; indices[2] = 1;
                    indices[3] = 2; indices[4] = 0; indices[5] = 3;
                    MeshHelper.ComputeNormals(vertices, indices);
                    MeshHelper.ComputeTangentAndBinormal(vertices, indices);
                    MeshHelper.SetAlbedoColor(vertices, new float4(1));
                    MeshHelper.SetMetalSmoothness(vertices, new float2(1));
                    mb.Bounds = MeshHelper.ComputeBounds(vertices);
                    lmrd.Mesh = builder.CreateBlobAssetReference<LitMeshData>(Allocator.Persistent);
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
                    ref var root = ref builder.ConstructRoot<SimpleMeshData>();
                    var vertices = builder.Allocate(ref root.Vertices, 4).AsNativeArray();
                    var indices = builder.Allocate(ref root.Indices, 6).AsNativeArray();
                    vertices[0] = new SimpleVertex { Position = org, Color = new float4(1), TexCoord0 = new float2(0, 1) };
                    vertices[1] = new SimpleVertex { Position = org + du, Color = new float4(1), TexCoord0 = new float2(1, 1) };
                    vertices[2] = new SimpleVertex { Position = org + du + dv, Color = new float4(1), TexCoord0 = new float2(1, 0) };
                    vertices[3] = new SimpleVertex { Position = org + dv, Color = new float4(1), TexCoord0 = new float2(0, 0) };
                    indices[0] = 0; indices[1] = 2; indices[2] = 1;
                    indices[3] = 2; indices[4] = 0; indices[5] = 3;
                    mb.Bounds = MeshHelper.ComputeBounds(vertices);
                    smrd.Mesh = builder.CreateBlobAssetReference<SimpleMeshData>(Allocator.Persistent);
                    mesh = mgr.CreateEntity(typeof(SimpleMeshRenderData), typeof(MeshBounds));
                    mgr.SetComponentData(mesh, smrd);
                    mgr.SetComponentData(mesh, mb);
                    var simpleMaterial = mgr.GetComponentData<SimpleMaterial>(material);
                    simpleMaterial.billboarded = true;
                    mgr.SetComponentData(material, simpleMaterial);
                }
                mgr.AddComponentData(eEmitter, new ParticleMesh { Mesh = mesh });
                builder.Dispose();
            }

            if (isLit)
            {
                mgr.AddBuffer<DynamicLitVertex>(eParticleRenderer);
            }
            else
            {
                mgr.AddBuffer<DynamicSimpleVertex>(eParticleRenderer);
            }
            mgr.AddBuffer<DynamicIndex>(eParticleRenderer);
            mgr.AddComponent<MeshBounds>(eParticleRenderer);

            return eParticleRenderer;
        }

        private void CleanupBurstEmitters(EntityManager mgr)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);

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
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);

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
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);

            Entities.WithoutBurst().WithNone<ParticleEmitter>().WithAll<ParticleEmitterInternal>().ForEach((Entity e) =>
            {
                ecb.DestroyEntity(mgr.GetComponentData<ParticleEmitterInternal>(e).particleRenderer);
                ecb.RemoveComponent<ParticleEmitterInternal>(e);
            }).Run();

            ecb.Playback(mgr);
            ecb.Dispose();
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
    [UpdateAfter(typeof(TransformSystemGroup))]
    public class ParticleSpawnSystem : SystemBase
    {
        private void SpawnParticles(EntityManager mgr, float deltaTime, Entity emitter)
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

                if (ParticlesUtil.ActiveBurstEmitter(burstEmission, burstEmissionInternal))
                {
                    burstEmissionInternal.cooldown -= deltaTime;
                    if (burstEmissionInternal.cooldown < 0.0f)
                    {
                        burstParticleCount = (uint)rand.NextInt((int)burstEmission.Count.Start, (int)burstEmission.Count.End);
                        if (burstEmission.Cycles != ParticlesUtil.kBurstCycleInfinitely)
                            burstEmissionInternal.cycle++;
                        burstEmissionInternal.cooldown = burstEmission.Interval;
                    }

                    mgr.SetComponentData(emitter, burstEmissionInternal);
                }
            }

            // Normal emission mode
            uint particlesToSpawn = 0;
            if (ParticlesUtil.ActiveEmitter(particleEmitter))
            {
                particleEmitterInternal.particleSpawnCooldown += deltaTime;
                float particleSpawnDelay = 1.0f / rand.RandomRange(particleEmitter.EmitRate);

                particlesToSpawn = (uint)(particleEmitterInternal.particleSpawnCooldown / particleSpawnDelay);

                if (particlesToSpawn > 0)
                    particleEmitterInternal.particleSpawnCooldown -= particleSpawnDelay * particlesToSpawn;

                mgr.SetComponentData(emitter, particleEmitterInternal);
            }

            particlesToSpawn += burstParticleCount;
            uint maxParticlesToSpawn = particleEmitter.MaxParticles - particleEmitterInternal.numParticles;
            if (particlesToSpawn > maxParticlesToSpawn)
                particlesToSpawn = maxParticlesToSpawn;

            if (particlesToSpawn == 0)
            {
                mgr.SetComponentData(emitter, new Rng { rand = rand });
                return;
            }

            var particles = EntityManager.GetBuffer<DynamicParticle>(emitter).Reinterpret<Particle>();
            int offset = particles.Length;
            particles.ResizeUninitialized(particles.Length + (int)particlesToSpawn);

            InitTime(deltaTime, particleEmitter.Lifetime, particles, offset, ref rand);
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
            InitColor(mgr, emitter, particles, offset, ref rand);
            InitScale(mgr, emitter, particles, offset, ref rand);

            // Init particle's position and the velocity based on the source
            Range speed = mgr.GetComponentData<EmitterInitialSpeed>(emitter).Speed;
            float randomizePos = mgr.HasComponent<RandomizePosition>(emitter) ? mgr.GetComponentData<RandomizePosition>(emitter).Value : 0.0f;
            float randomizeDir = mgr.HasComponent<RandomizeDirection>(emitter) ? mgr.GetComponentData<RandomizeDirection>(emitter).Value : 0.0f;
            float4x4 matrix = particleEmitter.AttachToEmitter ? float4x4.identity : mgr.GetComponentData<LocalToWorld>(emitter).Value;
            if (mgr.HasComponent<EmitterRectangleSource>(emitter))
            {
                ParticlesSource.InitEmitterRectangleSource(mgr, emitter, particles, offset, speed, randomizePos, randomizeDir, matrix, ref rand);
            }
            else if (mgr.HasComponent<EmitterCircleSource>(emitter))
            {
                ParticlesSource.InitEmitterCircleSource(mgr, emitter, particles, offset, speed, randomizePos, randomizeDir, matrix, ref rand);
            }
            else if (mgr.HasComponent<EmitterConeSource>(emitter))
            {
                ParticlesSource.InitEmitterConeSource(mgr, emitter, particles, offset, speed, randomizePos, randomizeDir, matrix, ref rand);
            }
            else if (mgr.HasComponent<EmitterSphereSource>(emitter))
            {
                var radius = mgr.GetComponentData<EmitterSphereSource>(emitter).Radius;
                ParticlesSource.InitEmitterSphereSource(mgr, emitter, particles, offset, radius, false, speed, randomizePos, randomizeDir, matrix, ref rand);
            }
            else if (mgr.HasComponent<EmitterHemisphereSource>(emitter))
            {
                var radius = mgr.GetComponentData<EmitterHemisphereSource>(emitter).Radius;
                ParticlesSource.InitEmitterSphereSource(mgr, emitter, particles, offset, radius, true, speed, randomizePos, randomizeDir, matrix, ref rand);
            }

            mgr.SetComponentData(emitter, new Rng { rand = rand });
        }

        static void InitTime(float deltaTime, Range lifetime, DynamicBuffer<Particle> particles, int offset, ref Random rand)
        {
            // The time is evenly distributted from 0.0 to deltaTime.

            // The time of each subsequent particle will be increased by this value.
            // particle.time[0..n] = (0 * timeStep, 1 * timeStep, 2 * timeStep, ..., n * timeStep).
            int newParticles = particles.Length - offset;
            float timeStep = newParticles > 1 ? deltaTime / (newParticles - 1) : 0.0f;

            // We need to subtract deltaTime from the particle's relative time, because later in
            // the same frame we are adding deltaTime to the particle's relative time when we process
            // them. This ensures that the first, newly created particle will start at a relative time 0.0.
            float time = -deltaTime;
            time += timeStep;

            for (var i = offset; i < particles.Length; i++)
            {
                var particle = particles[i];
                particle.lifetime = rand.RandomRange(lifetime);
                particle.time = time;
                particles[i] = particle;
            }
        }

        private static void InitScale(EntityManager mgr, Entity emitter, DynamicBuffer<Particle> particles, int offset, ref Random rand)
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
                    for (var i = offset; i < particles.Length; i++)
                    {
                        var particle = particles[i];
                        particle.scale = rand.RandomRange(initialScale.Scale);
                        particles[i] = particle;
                    }
                }
                else if (mgr.HasComponent<EmitterInitialNonUniformScale>(emitter))
                {
                    var initialScale = mgr.GetComponentData<EmitterInitialNonUniformScale>(emitter);
                    for (var i = offset; i < particles.Length; i++)
                    {
                        var particle = particles[i];
                        particle.scale = new float3(rand.RandomRange(initialScale.ScaleX), rand.RandomRange(initialScale.ScaleY), rand.RandomRange(initialScale.ScaleZ));
                        particles[i] = particle;
                    }
                }
            }
        }

        private void InitColor(EntityManager mgr, Entity emitter, DynamicBuffer<Particle> particles, int offset, ref Random rand)
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
                for (var i = offset; i < particles.Length; i++)
                {
                    var particle = particles[i];
                    particle.color = math.lerp(initialColor.ColorMin, initialColor.ColorMax, rand.Random01());
                    particles[i] = particle;
                }
            }
            else
            {
                float4 defaultColor = new float4(1);
                for (var i = offset; i < particles.Length; i++)
                {
                    var particle = particles[i];
                    particle.color = defaultColor;
                    particles[i] = particle;
                }
            }
        }

        protected override void OnUpdate()
        {
            Entities
                .WithoutBurst()
                .ForEach((Entity e, ref ParticleEmitterInternal emitterInternal, in ParticleEmitter emitter) =>
                {
                    float deltaTime = Time.DeltaTime;
                    bool looping = EntityManager.HasComponent<Looping>(e);
                    if (emitterInternal.remainingDelay < deltaTime)
                    {
                        // Update t
                        emitterInternal.t += deltaTime - emitterInternal.remainingDelay;
                        if (looping)
                        {
                            while (emitterInternal.t >= emitter.Duration)
                                emitterInternal.t -= emitter.Duration;
                        }
                        else
                            emitterInternal.t = math.min(emitterInternal.t, emitter.Duration);
                    }
                    else
                    {
                        emitterInternal.remainingDelay -= deltaTime;
                        deltaTime = math.max(-emitterInternal.remainingDelay, 0.0f);
                        emitterInternal.remainingDelay = math.max(emitterInternal.remainingDelay, 0.0f);
                    }

                    if (!looping && emitterInternal.t >= emitter.Duration
                        || !ParticlesUtil.ActiveEmitter(emitter) && !ParticlesUtil.ActiveBurstEmitter(EntityManager, e))
                    {
                        if (emitterInternal.active && emitterInternal.numParticles == 0)
                        {
                            // Heap space is limited on some platforms, so free up this memory if it seems like this emitter won't be spawning any particles for a while
                            if (EntityManager.HasComponent<LitParticleRenderer>(emitterInternal.particleRenderer))
                            {
                                var vBuffer = EntityManager.GetBuffer<DynamicLitVertex>(emitterInternal.particleRenderer);
                                vBuffer.Clear();
                                vBuffer.TrimExcess();
                            }
                            else
                            {
                                Assert.IsTrue(EntityManager.HasComponent<SimpleParticleRenderer>(emitterInternal.particleRenderer));
                                var vBuffer = EntityManager.GetBuffer<DynamicSimpleVertex>(emitterInternal.particleRenderer);
                                vBuffer.Clear();
                                vBuffer.TrimExcess();
                            }

                            var iBuffer = EntityManager.GetBuffer<DynamicIndex>(emitterInternal.particleRenderer);
                            iBuffer.Clear();
                            iBuffer.TrimExcess();
                            var particleBuffer = EntityManager.GetBuffer<DynamicParticle>(e);
                            particleBuffer.Clear();
                            particleBuffer.TrimExcess();
                            emitterInternal.active = false;
                        }
                    }
                    else
                    {
                        emitterInternal.active = true;
                        SpawnParticles(EntityManager, deltaTime, e);
                    }
                }).Run();
        }
    }

    /// <summary>
    ///  System that updates all particles
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EmitterSystem))]
    [UpdateAfter(typeof(ParticleSpawnSystem))]
    public class ParticleSystem : SystemBase
    {
        private static void UpdateParticleLife(DynamicBuffer<Particle> particles, float deltaTime, ref ParticleEmitterInternal emitterInternal)
        {
            // Reset particle counter for this emitter
            emitterInternal.numParticles = 0;
            uint numParticles = 0;
            for (var i = particles.Length - 1; i >= 0; i--)
            {
                var particle = particles[i];
                particle.time += deltaTime;
                if (particle.time >= particle.lifetime)
                {
                    particles.RemoveAt(i);
                }
                else
                {
                    numParticles++;
                    particles[i] = particle;
                }
            }

            emitterInternal.numParticles = numParticles;
        }

        private static void UpdateParticlePosition(DynamicBuffer<Particle> particles, float deltaTime)
        {
            for (var i = 0; i < particles.Length; i++)
            {
                var particle = particles[i];
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
                    particle.position += particle.velocity * deltaTime;
                }
                particles[i] = particle;
            }
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
            BufferFromEntity<DynamicParticle> BufferDynamicParticle = GetBufferFromEntity<DynamicParticle>();
            float deltaTime = Time.DeltaTime;
            Entities.ForEach((Entity e, ref ParticleEmitterInternal emitterInternal, in ParticleEmitter emitter) =>
            {
                var particles = BufferDynamicParticle[e].Reinterpret<Particle>();
                UpdateParticleLife(particles, deltaTime, ref emitterInternal);
                UpdateParticlePosition(particles, deltaTime);
#if false
                UpdateParticleRotation(EntityManager, emitterReference, deltaTime);
                UpdateParticleScale(EntityManager, emitterReference, deltaTime);
                UpdateParticleColor(EntityManager, emitterReference, deltaTime);
#endif
            }).Run();
        }
    }
}
