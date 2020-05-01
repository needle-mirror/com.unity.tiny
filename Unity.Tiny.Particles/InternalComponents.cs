using Unity.Mathematics;
using Unity.Entities;

namespace Unity.Tiny.Particles
{
    /// <summary>
    /// Modifies the position of the particle every frame.
    /// </summary>
    struct ParticleVelocity : IComponentData
    {
        internal float3 velocity;
    }
#if false
    // Modifies the rotation around z axis.
    struct ParticleAngularVelocity : IComponentData
    {
        internal float angularVelocity;
    }

    struct ParticleLifetimeColor : IComponentData
    {
        internal Color initialColor;
    }

    struct ParticleLifetimeScale : IComponentData
    {
        internal float3 initialScale;
    }
#endif
    struct BurstEmissionInternal : IComponentData
    {
        /// <summary> If 0.0, then the next burst should be emitted. </summary>
        internal float cooldown;

        /// <summary> Current cycle number </summary>
        internal int cycle;
    }

    /// <remarks>
    /// Used to track initialization/cleanup of emitter
    /// </remarks>
    struct ParticleEmitterInternal : ISystemStateComponentData
    {
        internal Entity particleTemplate;
        internal float particleSpawnCooldown;
        internal uint numParticles;
        internal Entity particleRenderer;
    }

    /// <summary>
    /// Reference to particle emitter. Shared across all particles that were emitted by the referenced emitter.
    /// </summary>
    struct EmitterReferenceForParticles : ISharedComponentData
    {
        internal Entity emitter;
    }

    /// <summary>
    /// Reference to particle emitter from particle renderer.
    /// </summary>
    struct EmitterReferenceForRenderer : IComponentData
    {
        internal Entity emitter;
    }
}
