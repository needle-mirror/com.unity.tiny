using JetBrains.Annotations;
using Unity.Entities;

namespace Unity.Tiny.Animation
{
    [UsedImplicitly]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    class CompleteAnimationBindings : SystemBase
    {
        EndInitializationEntityCommandBufferSystem m_ECBSystem;

        protected override void OnCreate()
        {
            m_ECBSystem = World.GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var commandBuffer = m_ECBSystem.CreateCommandBuffer().AsParallelWriter();
            Dependency =
                Entities
                    .WithoutBurst() // Burst does not support: TypeManager.GetTypeIndexFromStableTypeHash
                    .ForEach(
                        (Entity entity, int entityInQueryIndex, ref DynamicBuffer<AnimationBinding> bindings, in DynamicBuffer<AnimationBindingRetarget> bindingRetargetBuffer) =>
                        {
                            for (int i = 0; i < bindingRetargetBuffer.Length; ++i)
                            {
                                var binding = bindings[i];
                                binding.TargetComponentTypeIndex = TypeManager.GetTypeIndexFromStableTypeHash(bindingRetargetBuffer[i].StableTypeHash);
                                bindings[i] = binding;
                            }

                            commandBuffer.RemoveComponent<AnimationBindingRetarget>(entityInQueryIndex, entity);
                        }).Schedule(Dependency);

            Dependency =
                Entities
                    .WithoutBurst() // Burst does not support: TypeManager.GetTypeIndexFromStableTypeHash
                    .ForEach(
                        (Entity entity, int entityInQueryIndex, ref DynamicBuffer<AnimationPPtrBinding> bindings, in DynamicBuffer<AnimationPPtrBindingRetarget> bindingRetargetBuffer) =>
                        {
                            for (int i = 0; i < bindingRetargetBuffer.Length; ++i)
                            {
                                var binding = bindings[i];
                                binding.TargetComponentTypeIndex = TypeManager.GetTypeIndexFromStableTypeHash(bindingRetargetBuffer[i].StableTypeHash);
                                bindings[i] = binding;
                            }

                            commandBuffer.RemoveComponent<AnimationPPtrBindingRetarget>(entityInQueryIndex, entity);
                        }).Schedule(Dependency);

            m_ECBSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
