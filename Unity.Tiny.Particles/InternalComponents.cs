using Unity.Mathematics;
using Unity.Entities;

namespace Unity.Tiny.Particles
{
    struct Particle
    {
        /// <summary> How long this particle has existed, in seconds.  From 0.0 to lifetime. </summary>
        internal float time;

        /// <summary> The maximum lifetime of this particle, in seconds. </summary>
        internal float lifetime;

        internal float3 position;
        internal float3 scale;
        internal quaternion rotation;

        /// <summary> Modifies the position of the particle every frame. </summary>
        internal float3 velocity;

        internal float4 color;
    }

    struct DynamicParticle : IBufferElementData
    {
#pragma warning disable 0649
        internal Particle Value;
#pragma warning restore 0649
    }

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
        internal float particleSpawnCooldown;
        internal uint numParticles;
        internal Entity particleRenderer;

        /// <summary> Fractional value for current progress through duration </summary>
        internal float t;

        /// <summary> Current remainder of start delay in seconds </summary>
        internal float remainingDelay;

        /// <summary> True if this emitter is actively spawning new particles </summary>
        internal bool active;
    }

    /// <summary>
    /// For generating random values for an emitter
    /// </summary>
    /// <remarks>
    /// <see cref="Random"/> maintains internal state so this component must be set on the emitter entity any time it is used
    /// </remarks>
    struct Rng : IComponentData
    {
        internal Random rand;
    }

    /// <summary>
    /// Reference to particle emitter from particle renderer.
    /// </summary>
    struct EmitterReferenceForRenderer : IComponentData
    {
        internal Entity emitter;
    }
}
