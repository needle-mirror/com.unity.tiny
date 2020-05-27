using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Tiny.Particles
{
    internal static class ParticlesUtil
    {
        internal static float Random01(this ref Random rand)
        {
            return rand.NextFloat(0.0f, 1.0f);
        }

        internal static float RandomRange(this ref Random rand, Range range)
        {
            return rand.NextFloat(range.start, range.end);
        }

        internal static bool EmitterIsValid(EntityManager mgr, Entity eEmitter)
        {
            return eEmitter != Entity.Null && mgr.Exists(eEmitter) && mgr.HasComponent<ParticleEmitter>(eEmitter) && mgr.HasComponent<ParticleEmitterInternal>(eEmitter);
        }
    }
}
