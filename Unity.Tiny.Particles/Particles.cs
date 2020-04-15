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
    ///  Spawns particles inside of a rectangular area on the X/Y plane.
    /// </summary>
    public struct EmitterRectangleSource : IComponentData
    {
        /// <summary>
        ///  Particles are emitted from a random spot inside this rectangle, with
        ///  0,0 of the rect at the Emitter's position.
        /// </summary>
        public Rect rect;

        /// <summary>The initial speed of the particles.</summary>
        public Range speed;
    }

    /// <summary>
    ///  Spawns particles in a cone. Particles are emitted from the base of the cone,
    ///  which is a circle on the X/Z plane. The angle and speed parameters define
    ///  the initial particle velocity.
    /// </summary>
    public struct EmitterConeSource : IComponentData
    {
        /// <summary>The radius in which the particles are being spawned.</summary>
        public float radius;

        /// <summary>The angle of the cone in degrees.</summary>
        public float angle;

        /// <summary>The initial speed of the particles.</summary>
        public Range speed;
    }

    /// <summary>
    ///  Spawns particles inside a circle on the X/Y plane.
    /// </summary>
    public struct EmitterCircleSource : IComponentData
    {

        /// <summary>The radius of the circle.</summary>
        public float radius;

        /// <summary>The initial speed of the particles.</summary>
        public Range speed;
    }

    /// <summary>
    ///  Multiplies the scale of the source particle by a random value in the range
    ///  specified by <see cref="scale"/>
    /// </summary>
    public struct EmitterInitialScale : IComponentData
    {
        public Range scale;
    }

    /// <summary>
    ///  Multiplies the X, Y, and Z scales of the source particle by a random values in the ranges
    ///  specified by <see cref="scaleX"/>, <see cref="scaleY"/>, and <see cref="scaleZ"/> respectively
    /// </summary>
    public struct EmitterInitialNonUniformScale : IComponentData
    {
        public Range scaleX;
        public Range scaleY;
        public Range scaleZ;
    }


    /// <summary>
    ///  Sets the initial rotation on the Z axis for particles to a random value
    ///  in the range specified by <see cref="angle"/>
    /// </summary>
    public struct EmitterInitialRotation : IComponentData
    {
        public Range angle;
    }

    /// <summary>
    ///  Sets the initial rotations on the X, Y, and Z axes for particles to a random values in the ranges
    ///  specified by <see cref="angleX"/>, <see cref="angleX"/>, and <see cref="angleX"/> respectively
    /// </summary>
    public struct EmitterInitialNonUniformRotation: IComponentData
    {
        public Range angleX;
        public Range angleY;
        public Range angleZ;
    }
#if false
    /// <summary>
    ///  Sets the initial velocity for particles.
    /// </summary>
    public struct EmitterInitialVelocity : IComponentData
    {
        public float3 velocity;
    }

    /// <summary>
    ///  Sets the initial color of the particles by multiplying the color of the
    ///  source particle by a random value obtained by sampling curve between time
    ///  0.0 and 1.0.
    /// </summary>
    public struct EmitterInitialColor : IComponentData
    {
        /// <summary>
        /// Entity with the [Bezier|Linear|Step]CurveColor component.
        /// The color is choosen randomly by sampling the curve between time 0.0 and 1.0.
        /// </summary>
        public Entity curve;
    }

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
    /// An emitter with this component has billboarded particles
    /// </summary>
    public struct Billboarded : IComponentData { }

    /// <summary>
    ///  A system that updates all particle emitters
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public class EmitterSystem : SystemBase
    {
        private void UpdateNewEmitters(EntityManager mgr)
        {
            Entities
                .WithStructuralChanges()
                .WithNone<ParticleEmitterInternal>()
                .ForEach((Entity e, ref ParticleEmitter particleEmitter) =>
                {
                    // Initialize new emitters and particle templates
                    MeshRenderer meshRenderer = mgr.GetComponentData<MeshRenderer>(particleEmitter.particle);

                    if (mgr.HasComponent<Billboarded>(e))
                    {
                        Assert.IsTrue(meshRenderer.mesh == Entity.Null);

                        // 1x1 quad
                        LitMeshRenderData lmrd;
                        MeshBounds mb;
                        MeshHelper.CreatePlaneLit(new float3(0, 0, 0), new float3(1, 0, 0), new float3(0, 1, 0), out mb, out lmrd);
                        meshRenderer.mesh = mgr.CreateEntity();
                        mgr.AddComponentData(meshRenderer.mesh, lmrd);
                        mgr.AddComponentData(meshRenderer.mesh, mb);
                        meshRenderer.startIndex = 0;
                        meshRenderer.indexCount = lmrd.Mesh.Value.Indices.Length;
                        mgr.SetComponentData(particleEmitter.particle, meshRenderer);
                    }

                    var emitterInternal = new ParticleEmitterInternal();

                    if (!mgr.HasComponent<Disabled>(particleEmitter.particle))
                        mgr.AddComponentData(particleEmitter.particle, new Disabled());
                    emitterInternal.particleTemplate = mgr.Instantiate(particleEmitter.particle);

                    mgr.AddComponentData(emitterInternal.particleTemplate, new Particle());

                    var position = new Translation { Value = float3.zero };
                    mgr.AddComponentData(emitterInternal.particleTemplate, position);

                    mgr.AddSharedComponentData(emitterInternal.particleTemplate, new ParticleEmitterReference { emitter = e });
                    mgr.AddComponentData(e, emitterInternal);
                }).Run();

            CleanupBurstEmitters(mgr);
            InitBurstEmitters(mgr);
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
                DestroyParticlesForEmitter(mgr, ecb, e);
                ecb.RemoveComponent<ParticleEmitterInternal>(e);
            }).Run();

            ecb.Playback(mgr);
            ecb.Dispose();
        }

        private void DestroyParticlesForEmitter(EntityManager mgr, EntityCommandBuffer ecb, Entity emitter)
        {
            Entities
                .WithoutBurst()
                .WithAll<Particle>()
                .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabled) // needed for proto particle
                .ForEach((Entity e, ParticleEmitterReference cEmitterRef) =>
            {
                if (cEmitterRef.emitter == emitter)
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
    ///  A system that handles spawning new particles for all emitters
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EmitterSystem))]
    public class ParticleSpawnSystem : SystemBase
    {
        private void SpawnParticles(EntityManager mgr, float deltaTime)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            Entities
                .WithoutBurst()
                .WithAll<ParticleEmitter, ParticleEmitterInternal>()
                .ForEach((Entity emitter) =>
                {
                    SpawnParticles(mgr, ecb, deltaTime, emitter);
                }).Run();

            ecb.Playback(mgr);
            ecb.Dispose();
        }

        const int kCycleInfinitely = 0;
        private void SpawnParticles(EntityManager mgr, EntityCommandBuffer ecb, float deltaTime, Entity emitter)
        {
            var particleEmitter = mgr.GetComponentData<ParticleEmitter>(emitter);
            var particleEmitterInternal = mgr.GetComponentData<ParticleEmitterInternal>(emitter);

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
                        burstParticleCount = (uint)m_rand.NextInt((int)burstEmission.count.start, (int)burstEmission.count.end);
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
                float particleSpawnDelay = 1.0f / ParticlesUtil.RandomRange(particleEmitter.emitRate);

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
                return;

            var newParticles = new NativeArray<Entity>((int)particlesToSpawn, Allocator.Persistent);

            // Before the new particles will spawn, Disabled component needs to be removed from the template particle
            ecb.RemoveComponent<Disabled>(particleEmitterInternal.particleTemplate);

            for (int i = 0; i < newParticles.Length; i++)
                newParticles[i] = ecb.Instantiate(particleEmitterInternal.particleTemplate);

            ecb.AddComponent(particleEmitterInternal.particleTemplate, new Disabled());

            InitTime(mgr, ecb, deltaTime, particleEmitter.lifetime, newParticles);

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

            InitColor(mgr, ecb, emitter, newParticles);
#endif
            InitScale(mgr, ecb, emitter, newParticles);
            InitRotation(mgr, ecb, emitter, newParticles);

            // Init particle's position and the velocity based on the source
            if (mgr.HasComponent<EmitterRectangleSource>(emitter))
            {
                ParticlesSource.InitEmitterRectangleSource(mgr, ecb, emitter, newParticles);
            }
            else if (mgr.HasComponent<EmitterCircleSource>(emitter))
            {
                ParticlesSource.InitEmitterCircleSource(mgr, ecb, emitter, newParticles);
            }
            else if (mgr.HasComponent<EmitterConeSource>(emitter))
            {
                ParticlesSource.InitEmitterConeSource(mgr, ecb, emitter, newParticles);
            }

            newParticles.Dispose();
        }

        static void InitTime(EntityManager mgr, EntityCommandBuffer ecb, float deltaTime, Range lifetime, NativeArray<Entity> newParticles)
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
                    lifetime = ParticlesUtil.RandomRange(lifetime),
                    time = time
                });
            }
        }

        private void InitScale(EntityManager mgr, EntityCommandBuffer ecb, Entity emitter, NativeArray<Entity> newParticles)
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
                        float scale = ParticlesUtil.RandomRange(initialScale.scale);
                        ecb.AddComponent(particle, new Scale { Value = scale });
                    }
                }
                else if (mgr.HasComponent<EmitterInitialNonUniformScale>(emitter))
                {
                    var initialScale = mgr.GetComponentData<EmitterInitialNonUniformScale>(emitter);
                    foreach (var particle in newParticles)
                    {
                        float3 scale = new float3(ParticlesUtil.RandomRange(initialScale.scaleX), ParticlesUtil.RandomRange(initialScale.scaleY), ParticlesUtil.RandomRange(initialScale.scaleZ));
                        ecb.AddComponent(particle, new NonUniformScale { Value = scale });
                    }
                }
            }
        }
#if false
        private void InitColor(EntityManager mgr, EntityCommandBuffer ecb, Entity emitter, NativeArray<Entity> newParticles)
        {
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
            else if (hasInitialColor)
            {
                // Only EmitterInitialColor is present.

                var initialColor = mgr.GetComponentData<EmitterInitialColor>(emitter);

                foreach (var particle in newParticles)
                {
                    var renderer = mgr.GetComponentData<SpriteRenderer>(particle);
                    var randomColor = InterpolationService.EvaluateCurveColor(mgr, m_rand.NextFloat(), initialColor.curve);
                    renderer.Color = renderer.Color * randomColor;
                    mgr.SetComponentData(particle, renderer);
                }
            }
        }
#endif
        private void InitRotation(EntityManager mgr, EntityCommandBuffer ecb, Entity emitter, NativeArray<Entity> newParticles)
        {
            if (mgr.HasComponent<EmitterInitialRotation>(emitter))
            {
                var initialRotation = mgr.GetComponentData<EmitterInitialRotation>(emitter);
                foreach (var particle in newParticles)
                {
                    ecb.AddComponent(particle, new Rotation
                    {
                        Value = quaternion.RotateZ(ParticlesUtil.RandomRange(initialRotation.angle))
                    });
                }
            }
            else if (mgr.HasComponent<EmitterInitialNonUniformRotation>(emitter))
            {
                var initialRotation = mgr.GetComponentData<EmitterInitialNonUniformRotation>(emitter);
                foreach (var particle in newParticles)
                {
                    ecb.AddComponent(particle, new Rotation
                    {
                        Value = quaternion.Euler(
                            ParticlesUtil.RandomRange(initialRotation.angleX),
                            ParticlesUtil.RandomRange(initialRotation.angleY),
                            ParticlesUtil.RandomRange(initialRotation.angleZ))
                    });
                }
            }
#if false
            if (mgr.HasComponent<LifetimeAngularVelocity>(emitter))
            {
                foreach (var particle in newParticles)
                {
                    ecb.AddComponent(particle, new ParticleAngularVelocity());
                }
            }
#endif
        }

        // TODO: Random() throws exception, so we need to use seed for now.
        private Random m_rand = new Random(1);

        protected override void OnUpdate()
        {
            SpawnParticles(EntityManager, Time.DeltaTime);
        }
    }

    /// <summary>
    ///  A system that updates all particles
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EmitterSystem))]
    [UpdateAfter(typeof(ParticleSpawnSystem))]
    public class ParticleSystem : SystemBase
    {
        private void UpdateParticleLife(EntityManager mgr, ParticleEmitterReference emitterReference, float deltaTime)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            var emitterInternal = mgr.GetComponentData<ParticleEmitterInternal>(emitterReference.emitter);

            // Reset particle counter for this emitter
            emitterInternal.numParticles = 0;
            uint numParticles = 0;

            Entities.WithSharedComponentFilter(emitterReference).ForEach((Entity e, ref Particle particle) =>
            {
                particle.time += deltaTime;
                if (particle.time >= particle.lifetime)
                    ecb.DestroyEntity(e);
                else
                    numParticles++;

            }).Run();

            emitterInternal.numParticles = numParticles;
            mgr.SetComponentData(emitterReference.emitter, emitterInternal);

            ecb.Playback(mgr);
            ecb.Dispose();
        }

        private void UpdateParticlePosition(EntityManager mgr, ParticleEmitterReference emitterReference, float deltaTime)
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
        private static bool EmitterIsValid(EntityManager mgr, Entity eEmitter) => mgr.Exists(eEmitter) && mgr.HasComponent<ParticleEmitter>(eEmitter) && mgr.HasComponent<ParticleEmitterInternal>(eEmitter);

        protected override void OnUpdate()
        {
            float deltaTime = Time.DeltaTime;
            var emitterReferences = new List<ParticleEmitterReference>();
            EntityManager.GetAllUniqueSharedComponentData(emitterReferences);
            foreach (ParticleEmitterReference emitterReference in emitterReferences)
            {
                if (emitterReference.emitter == Entity.Null
                    // In some cases it takes an extra frame for the particle entities that reference a destroyed emitter to be destroyed, most likely because of system state components
                    || !EmitterIsValid(EntityManager, emitterReference.emitter))
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
