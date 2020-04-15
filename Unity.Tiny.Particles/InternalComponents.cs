using Unity.Mathematics;
using Unity.Entities;

namespace Unity.Tiny.Particles
{
    // Modifies the position of the particle every frame.
    struct ParticleVelocity : IComponentData
    {
        public float3 velocity;
    }
#if false
    // Modifies the rotation around z axis.
    struct ParticleAngularVelocity : IComponentData
    {
        public float angularVelocity;
    }

    struct ParticleLifetimeColor : IComponentData
    {
       public Color initialColor;
    }

    struct ParticleLifetimeScale : IComponentData
    {
        public float3 initialScale;
    }
#endif
    struct BurstEmissionInternal : IComponentData
    {
        // If < 0.0, then the next burst should be emitted.
       public float cooldown;

        // Current cycle number
       public int cycle;
    }

    // Used to track initialization/cleanup of emitter
    struct ParticleEmitterInternal : ISystemStateComponentData
    {
        public Entity particleTemplate;
        public float particleSpawnCooldown;
        public uint numParticles;
    }

    // Reference to the emitter that emitted this particle.
    struct ParticleEmitterReference : ISharedComponentData
    {
        public Entity emitter;
    }
}
