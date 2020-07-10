using JetBrains.Annotations;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Tiny.Animation
{
    [UsedImplicitly]
    [UpdateInGroup(typeof(TinyAnimationSystemGroup))]
    class UpdateAnimationTime : SystemBase
    {
        EndSimulationEntityCommandBufferSystem m_ECBSystem;

        protected override void OnCreate()
        {
            m_ECBSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var dt = Time.DeltaTime;
            var commandBuffer = m_ECBSystem.CreateCommandBuffer().AsParallelWriter();
            Dependency =
                Entities
                    .WithBurst(FloatMode.Fast)
                    .WithAll<UpdateAnimationTimeTag>()
                    .ForEach(
                        (Entity entity, int entityInQueryIndex, ref TinyAnimationTime time, ref TinyAnimationPlaybackInfo info) =>
                        {
                            switch (info.WrapMode)
                            {
                                case WrapMode.Once:
                                    {
                                        time.Value += dt;
                                        if (time.Value >= info.Duration)
                                        {
                                            commandBuffer.RemoveComponent<UpdateAnimationTimeTag>(entityInQueryIndex, entity);
                                            commandBuffer.RemoveComponent<ApplyAnimationResultTag>(entityInQueryIndex, entity);
                                            time.Value = 0.0f;
                                            time.InternalWorkTime = 0.0f;
                                        }
                                        break;
                                    }
                                case WrapMode.ClampForever:
                                    {
                                        time.Value = math.min(time.Value + dt, info.Duration);
                                        break;
                                    }
                                case WrapMode.Loop:
                                    {
                                        time.Value = AnimationMath.Repeat(time.Value + dt, info.Duration);
                                        break;
                                    }
                                case WrapMode.PingPong:
                                    {
                                        time.InternalWorkTime = AnimationMath.Repeat(time.InternalWorkTime + dt, info.Duration * 2.0f);
                                        time.Value = AnimationMath.PingPong(time.InternalWorkTime, info.Duration);
                                        break;
                                    }
                            }
                        })
                    .Schedule(Dependency);

            m_ECBSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
