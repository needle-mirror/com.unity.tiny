using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unity.Tiny.Particles
{
    static class ParticlesSource
    {
        internal static void InitEmitterCircleSource(EntityManager mgr, EntityCommandBuffer ecb, Entity emitter, NativeArray<Entity> particles, Range speed, float randomizePos, float randomizeDir, ref Random rand)
        {
            var radius = mgr.GetComponentData<EmitterCircleSource>(emitter).radius;
            foreach (var particle in particles)
            {
                float randomAngle = rand.NextFloat((float)-math.PI, (float)math.PI);
                float radiusNormalized = math.sqrt(rand.Random01());
                radius *= radiusNormalized;
                var positionNormalized = new float3(math.sin(randomAngle), math.cos(randomAngle), 0.0f);
                SetPosition(ecb, particle, positionNormalized * radius, randomizePos, ref rand);
                SetVelocity(ecb, particle, ref positionNormalized, speed, randomizeDir, ref rand);
                SetRotation(mgr, ecb, emitter, particle, positionNormalized, ref rand);
            }
        }

        internal static void InitEmitterConeSource(EntityManager mgr, EntityCommandBuffer ecb, Entity emitter, NativeArray<Entity> particles, Range speed, float randomizePos, float randomizeDir, ref Random rand)
        {
            var source = mgr.GetComponentData<EmitterConeSource>(emitter);
            source.angle = math.clamp(source.angle, 0.0f, 90.0f);
            float coneAngle = math.radians(source.angle);
            foreach (var particle in particles)
            {
                float angle = rand.Random01() * 2.0f * math.PI;
                float radiusNormalized = math.sqrt(rand.Random01());
                float3 localPositionOnConeBase;
                localPositionOnConeBase.x = math.cos(angle);
                localPositionOnConeBase.y = math.sin(angle);
                localPositionOnConeBase.z = 0.0f;
                localPositionOnConeBase *= radiusNormalized;
                SetPosition(ecb, particle, localPositionOnConeBase * source.radius, randomizePos, ref rand);
                float directionRadius = math.sin(coneAngle);
                float directionHeight = math.cos(coneAngle);
                float3 direction = new float3(localPositionOnConeBase.x * directionRadius, localPositionOnConeBase.y * directionRadius, directionHeight);
                SetVelocity(ecb, particle, ref direction, speed, randomizeDir, ref rand);
                SetRotation(mgr, ecb, emitter, particle, direction, ref rand);
            }
        }

        internal static void InitEmitterSphereSource(EntityManager mgr, EntityCommandBuffer ecb, Entity emitter, NativeArray<Entity> particles, float radius, bool hemisphere, Range speed, float randomizePos, float randomizeDir, ref Random rand)
        {
            foreach (var particle in particles)
            {
                float3 positionOnUnitSphere = rand.NextFloat3Direction();

                // For sphere, z ranges from [-1, 1]. For hemisphere, z ranges from [0, 1].
                if (hemisphere)
                    positionOnUnitSphere.z = math.abs(positionOnUnitSphere.z);

                // Create more points toward the outer part of the sphere
                float3 position = positionOnUnitSphere * math.pow(rand.Random01(), 1.0f / 3.0f) * radius;

                SetPosition(ecb, particle, position, randomizePos, ref rand);
                SetVelocity(ecb, particle, ref positionOnUnitSphere, speed, randomizeDir, ref rand);
                SetRotation(mgr, ecb, emitter, particle, positionOnUnitSphere, ref rand);
            }
        }

        internal static void InitEmitterRectangleSource(EntityManager mgr, EntityCommandBuffer ecb, Entity emitter, NativeArray<Entity> particles, Range speed, float randomizePos, float randomizeDir, ref Random rand)
        {
            // Unit rectangle centered at the origin
            float2 bottomLeft = new float2(-0.5f, -0.5f);
            float2 topRight = new float2(0.5f, 0.5f);
            float3 direction = new float3(0, 0, 1);
            foreach (var particle in particles)
            {
                var position = new float3(rand.NextFloat2(bottomLeft, topRight), 0.0f);
                SetPosition(ecb, particle, position, randomizePos, ref rand);
                SetVelocity(ecb, particle, ref direction, speed, randomizeDir, ref rand);
                SetRotation(mgr, ecb, emitter, particle, direction, ref rand);
            }
        }

        private static void SetPosition(EntityCommandBuffer ecb, Entity particle, float3 position, float randomizePos, ref Random rand)
        {
            if (randomizePos > 0.0f)
                position += rand.NextFloat3Direction() * randomizePos;

            ecb.AddComponent(particle, new Translation { Value = position });
        }

        private static void SetVelocity(EntityCommandBuffer ecb, Entity particle, ref float3 direction, Range speed, float randomizeDir,  ref Random rand)
        {
            if (speed.end != 0.0f)
            {
                if (randomizeDir > 0.0f)
                {
                    float3 randomDir = rand.NextFloat3Direction();
                    direction = math.lerp(direction, randomDir, randomizeDir);
                }

                float randomSpeed = rand.RandomRange(speed);
                var particleVelocity = new ParticleVelocity { velocity = direction * randomSpeed };
                ecb.AddComponent(particle, particleVelocity);
            }
        }

        private static void SetRotation(EntityManager mgr, EntityCommandBuffer ecb, Entity emitter, Entity particle, float3 direction, ref Random rand)
        {
            if (mgr.HasComponent<EmitterInitialRotation>(emitter))
            {
                // Set axis of rotation perpendicular to direction of travel
                var initialRotation = mgr.GetComponentData<EmitterInitialRotation>(emitter);
                float3 z = new float3(0, 0, 1);
                float3 axis = math.cross(z, direction);
                axis = math.dot(axis, axis) <= 0.01f ? new float3(0, 1, 0) : math.normalize(axis);

                ecb.AddComponent(particle, new Rotation
                {
                    Value = quaternion.AxisAngle(axis, rand.RandomRange(initialRotation.angle))
                });
            }
            else if (mgr.HasComponent<EmitterInitialNonUniformRotation>(emitter))
            {
                var initialRotation = mgr.GetComponentData<EmitterInitialNonUniformRotation>(emitter);
                ecb.AddComponent(particle, new Rotation
                {
                    Value = quaternion.Euler(
                        rand.RandomRange(initialRotation.angleX),
                        rand.RandomRange(initialRotation.angleY),
                        rand.RandomRange(initialRotation.angleZ))
                });
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
    }
} // namespace Particles
