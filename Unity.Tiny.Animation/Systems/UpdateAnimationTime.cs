using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Unity.Tiny.Animation
{
    [UpdateInGroup(typeof(TinyAnimationSystemGroup))]
    class UpdateAnimationTime : JobComponentSystem
    {
        [BurstCompile(FloatMode = FloatMode.Fast)]
        [RequireComponentTag(typeof(UpdateAnimationTimeTag))]
        struct AnimationTimeUpdateJob : IJobForEach<TinyAnimationClip>
        {
            public float dt;

            public void Execute(ref TinyAnimationClip tinyAnimationClip)
            {
                tinyAnimationClip.time = Repeat(tinyAnimationClip.time + dt, tinyAnimationClip.duration);
            }

            // Reimplementation of Mathf.Repeat()
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static float Repeat(float t, float length)
            {
                return math.clamp(t - math.floor(t / length) * length, 0.0f, length);
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var job = new AnimationTimeUpdateJob {dt = Time.DeltaTime};
            return job.Schedule(this, inputDeps);
        }
    }
}
