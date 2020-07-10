using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace Unity.Tiny.Particles
{
    static class ParticlesSource
    {
        internal static void InitEmitterCircleSource(EntityManager mgr, Entity emitter, DynamicBuffer<Particle> particles, int offset, Range speed, float randomizePos, float randomizeDir, float4x4 matrix, ref Random rand)
        {
            var radius = mgr.GetComponentData<EmitterCircleSource>(emitter).Radius;
            for (var i = offset; i < particles.Length; i++)
            {
                var particle = particles[i];
                float randomAngle = rand.NextFloat((float)-math.PI, (float)math.PI);
                float radiusNormalized = math.sqrt(rand.Random01());
                radius *= radiusNormalized;
                var positionNormalized = new float3(math.sin(randomAngle), math.cos(randomAngle), 0.0f);
                particle.position = GetParticlePosition(positionNormalized * radius, randomizePos, matrix, ref rand);
                particle.velocity = GetParticleVelocity(ref positionNormalized, speed, randomizeDir, matrix, ref rand);
                particle.rotation = GetParticleRotation(mgr, emitter, positionNormalized, ref rand);
                particles[i] = particle;
            }
        }

        internal static void InitEmitterConeSource(EntityManager mgr, Entity emitter, DynamicBuffer<Particle> particles, int offset, Range speed, float randomizePos, float randomizeDir, float4x4 matrix, ref Random rand)
        {
            var source = mgr.GetComponentData<EmitterConeSource>(emitter);
            source.Angle = math.clamp(source.Angle, 0.0f, 90.0f);
            float coneAngle = math.radians(source.Angle);
            for (var i = offset; i < particles.Length; i++)
            {
                var particle = particles[i];
                float angle = rand.Random01() * 2.0f * math.PI;
                float radiusNormalized = math.sqrt(rand.Random01());
                float3 localPositionOnConeBase;
                localPositionOnConeBase.x = math.cos(angle);
                localPositionOnConeBase.y = math.sin(angle);
                localPositionOnConeBase.z = 0.0f;
                localPositionOnConeBase *= radiusNormalized;
                particle.position = GetParticlePosition(localPositionOnConeBase * source.Radius, randomizePos, matrix, ref rand);
                float directionRadius = math.sin(coneAngle);
                float directionHeight = math.cos(coneAngle);
                float3 direction = new float3(localPositionOnConeBase.x * directionRadius, localPositionOnConeBase.y * directionRadius, directionHeight);
                particle.velocity = GetParticleVelocity(ref direction, speed, randomizeDir, matrix, ref rand);
                particle.rotation = GetParticleRotation(mgr, emitter, direction, ref rand);
                particles[i] = particle;
            }
        }

        internal static void InitEmitterSphereSource(EntityManager mgr, Entity emitter, DynamicBuffer<Particle> particles, int offset, float radius, bool hemisphere, Range speed, float randomizePos, float randomizeDir, float4x4 matrix, ref Random rand)
        {
            for (var i = offset; i < particles.Length; i++)
            {
                var particle = particles[i];
                float3 positionOnUnitSphere = rand.NextFloat3Direction();

                // For sphere, z ranges from [-1, 1]. For hemisphere, z ranges from [0, 1].
                if (hemisphere)
                    positionOnUnitSphere.z = math.abs(positionOnUnitSphere.z);

                // Create more points toward the outer part of the sphere
                float3 position = positionOnUnitSphere * math.pow(rand.Random01(), 1.0f / 3.0f) * radius;

                particle.position = GetParticlePosition(position, randomizePos, matrix, ref rand);
                particle.velocity = GetParticleVelocity(ref positionOnUnitSphere, speed, randomizeDir, matrix, ref rand);
                particle.rotation = GetParticleRotation(mgr, emitter, positionOnUnitSphere, ref rand);
                particles[i] = particle;
            }
        }

        internal static void InitEmitterRectangleSource(EntityManager mgr, Entity emitter, DynamicBuffer<Particle> particles, int offset, Range speed, float randomizePos, float randomizeDir, float4x4 matrix, ref Random rand)
        {
            // Unit rectangle centered at the origin
            float2 bottomLeft = new float2(-0.5f, -0.5f);
            float2 topRight = new float2(0.5f, 0.5f);
            float3 direction = new float3(0, 0, 1);
            for (var i = offset; i < particles.Length; i++)
            {
                var particle = particles[i];
                var position = new float3(rand.NextFloat2(bottomLeft, topRight), 0.0f);
                particle.position = GetParticlePosition(position, randomizePos, matrix, ref rand);
                particle.velocity = GetParticleVelocity(ref direction, speed, randomizeDir, matrix, ref rand);
                particle.rotation = GetParticleRotation(mgr, emitter, direction, ref rand);
                particles[i] = particle;
            }
        }

        private static float3 GetParticlePosition(float3 position, float randomizePos, float4x4 matrix, ref Random rand)
        {
            if (randomizePos > 0.0f)
                position += rand.NextFloat3Direction() * randomizePos;

            return math.transform(matrix, position);
        }

        private static float3 GetParticleVelocity(ref float3 direction, Range speed, float randomizeDir, float4x4 matrix, ref Random rand)
        {
            if (speed.End == 0.0f)
                return float3.zero;

            if (randomizeDir > 0.0f)
            {
                float3 randomDir = rand.NextFloat3Direction();
                direction = math.lerp(direction, randomDir, randomizeDir);
            }

            var velocity = direction * rand.RandomRange(speed);
            matrix.c3 = new float4(0, 0, 0, 1); // remove translation
            return math.transform(matrix, velocity);

        }

        private static quaternion GetParticleRotation(EntityManager mgr, Entity emitter, float3 direction, ref Random rand)
        {
            if (mgr.HasComponent<EmitterInitialRotation>(emitter))
            {
                // Set axis of rotation perpendicular to direction of travel
                var initialRotation = mgr.GetComponentData<EmitterInitialRotation>(emitter);
                float3 z = new float3(0, 0, 1);
                float3 axis = math.cross(z, direction);
                axis = math.dot(axis, axis) <= 0.01f ? new float3(0, 1, 0) : math.normalize(axis);
                return quaternion.AxisAngle(axis, rand.RandomRange(initialRotation.Angle));
            }
            if (mgr.HasComponent<EmitterInitialNonUniformRotation>(emitter))
            {
                var initialRotation = mgr.GetComponentData<EmitterInitialNonUniformRotation>(emitter);
                return quaternion.Euler(
                    rand.RandomRange(initialRotation.AngleX),
                    rand.RandomRange(initialRotation.AngleY),
                    rand.RandomRange(initialRotation.AngleZ));
            }

            return quaternion.identity;
        }
    }
} // namespace Particles
