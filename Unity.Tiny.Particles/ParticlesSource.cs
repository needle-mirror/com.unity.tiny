using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unity.Tiny.Particles
{
    static class ParticlesSource
    {
        private static Random m_rand = new Random(1);
        private static readonly float3 defaultDirection = new float3(0, 0, 1);

        public static void InitEmitterCircleSource(EntityManager mgr, EntityCommandBuffer ecb, Entity emitter, NativeArray<Entity> particles)
        {
            var source = mgr.GetComponentData<EmitterCircleSource>(emitter);
            foreach (var particle in particles)
            {
                float randomAngle = m_rand.NextFloat((float)-math.PI, (float)math.PI);
                float radiusNormalized = math.sqrt(m_rand.NextFloat(0.0f, 1.0f));
                float radius = source.radius * radiusNormalized;
                var positionNormalized = new float2(math.sin(randomAngle), math.cos(randomAngle));
                var position = new float3(positionNormalized.x * radius, positionNormalized.y * radius, 0.0f);
                ecb.AddComponent(particle, new Translation { Value = position });

                if (source.speed.start != 0.0f && source.speed.end != 0.0f)
                {
                    float randomSpeed = m_rand.NextFloat(source.speed.start, source.speed.end);

                    var particleVelocity = new ParticleVelocity
                    {
                        velocity = new float3(positionNormalized.x * randomSpeed, positionNormalized.y * randomSpeed, 0.0f)
                    };

                    ecb.AddComponent(particle, particleVelocity);
                }
            }
        }

        public static void InitEmitterConeSource(EntityManager mgr, EntityCommandBuffer ecb, Entity emitter, NativeArray<Entity> particles)
        {
            var source = mgr.GetComponentData<EmitterConeSource>(emitter);
            float coneAngle = math.radians(source.angle);

            foreach (var particle in particles)
            {
                float angle = m_rand.NextFloat(0.0f, 1.0f) * 2.0f * math.PI;
                float radiusNormalized = math.sqrt(m_rand.NextFloat(0.0f, 1.0f));

                float3 localPositionOnConeBase;
                localPositionOnConeBase.x = math.cos(angle);
                localPositionOnConeBase.y = math.sin(angle);
                localPositionOnConeBase.z = 0.0f;
                localPositionOnConeBase *= radiusNormalized;

                ecb.AddComponent(particle, new Translation { Value = localPositionOnConeBase * source.radius });

                ParticleVelocity particleVelocity = new ParticleVelocity();
                float directionRadius = math.sin(coneAngle);
                float directionHeight = math.cos(coneAngle);
                particleVelocity.velocity.x = localPositionOnConeBase.x * directionRadius;
                particleVelocity.velocity.y = localPositionOnConeBase.y * directionRadius;
                particleVelocity.velocity.z = directionHeight;
                float randomSpeed = m_rand.NextFloat(source.speed.start, source.speed.end);
                particleVelocity.velocity *= randomSpeed;

                ecb.AddComponent(particle, particleVelocity);
            }
        }

        public static void InitEmitterRectangleSource(EntityManager mgr, EntityCommandBuffer ecb, Entity emitter, NativeArray<Entity> particles)
        {
            var source = mgr.GetComponentData<EmitterRectangleSource>(emitter);

            foreach (var particle in particles)
            {
                var pos = ParticlesUtil.RandomPointInRect(source.rect);

                // center the box at the origin.
                // TODO: we could precompute the proper source rect (basically move the origin x/y by half) and
                // stash it somewhere to avoid division here
                pos.x -= source.rect.width / 2.0f;
                pos.y -= source.rect.height / 2.0f;

                ecb.AddComponent(particle, new Translation { Value = new float3(pos.x, pos.y, 0.0f) });

                if (source.speed.start != 0.0f && source.speed.end != 0.0f)
                {
                    float randomSpeed = m_rand.NextFloat(source.speed.start, source.speed.end);

                    var particleVelocity = new ParticleVelocity()
                    {
                        velocity = defaultDirection * randomSpeed
                    };

                    ecb.AddComponent(particle, particleVelocity);
                }
            }
        }
    }
} // namespace Particles
