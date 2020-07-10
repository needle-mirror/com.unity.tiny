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
            return rand.NextFloat(range.Start, range.End);
        }

        internal static bool EmitterIsValid(EntityManager mgr, Entity eEmitter)
        {
            return eEmitter != Entity.Null && mgr.Exists(eEmitter) && mgr.HasComponent<ParticleEmitter>(eEmitter) && mgr.HasComponent<ParticleEmitterInternal>(eEmitter);
        }

        internal static bool ActiveEmitter(ParticleEmitter particleEmitter)
        {
            return particleEmitter.EmitRate.Start >= 0.0f && particleEmitter.EmitRate.End > 0.0f;
        }

        internal const int kBurstCycleInfinitely = 0;
        internal static bool ActiveBurstEmitter(BurstEmission burstEmission, BurstEmissionInternal burstEmissionInternal)
        {
            return burstEmission.Cycles == kBurstCycleInfinitely || burstEmissionInternal.cycle < burstEmission.Cycles;
        }
        internal static bool ActiveBurstEmitter(EntityManager mgr, Entity emitter)
        {
            if (mgr.HasComponent<BurstEmissionInternal>(emitter) && mgr.HasComponent<BurstEmission>(emitter))
            {
                return ActiveBurstEmitter(mgr.GetComponentData<BurstEmission>(emitter), mgr.GetComponentData<BurstEmissionInternal>(emitter));
            }

            return false;
        }
    }
}
