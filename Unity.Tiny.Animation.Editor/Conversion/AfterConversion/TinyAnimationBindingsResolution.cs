using Unity.Collections;
using Unity.Entities;

namespace Unity.Tiny.Animation.Editor
{
    [UpdateInGroup(typeof(GameObjectAfterConversionGroup))]
    [UpdateBefore(typeof(TinyAnimationConversionCleanup))]
    [UpdateBefore(typeof(AfterTinyAnimationResolution))]
    class TinyAnimationBindingResolution : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            var dstWorldQuery = DstEntityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<AnimationBindingName>(),
                    ComponentType.ReadWrite<AnimationBinding>(),
                    ComponentType.ReadWrite<AnimationBindingRetarget>()
                }
            });

            var commandBuffer = new EntityCommandBuffer(Allocator.TempJob);
            var animatedEntities = dstWorldQuery.ToEntityArray(Allocator.TempJob);

            for (int entityIndex = 0; entityIndex < animatedEntities.Length; ++entityIndex)
            {
                var entity = animatedEntities[entityIndex];

                var bindingBuffer = DstEntityManager.GetBuffer<AnimationBinding>(entity);
                var bindingNameBuffer = DstEntityManager.GetBuffer<AnimationBindingName>(entity);
                var bindingRetargetBuffer = DstEntityManager.GetBuffer<AnimationBindingRetarget>(entity);

                for (int i = bindingBuffer.Length - 1; i >= 0; --i)
                {
                    var propertyPath = bindingNameBuffer[i].Value;
                    var discardEntry = true;

                    // A 0-length property path had no ECS equivalent at build time
                    if (propertyPath.LengthInBytes > 0)
                    {
                        var result = BindingUtils.GetBindingInfo(DstEntityManager, bindingBuffer[i].TargetEntity, propertyPath);
                        if (result.Success)
                        {
                            var retarget = bindingRetargetBuffer[i];
                            retarget.StableTypeHash = result.StableTypeHash;
                            bindingRetargetBuffer[i] = retarget;

                            var binding = bindingBuffer[i];
                            binding.FieldOffset = result.FieldOffset;
                            binding.FieldSize = result.FieldSize;
                            bindingBuffer[i] = binding;
                            discardEntry = false;
                        }
                    }

                    if (discardEntry)
                    {
                        bindingBuffer.RemoveAt(i);
                        bindingRetargetBuffer.RemoveAt(i);
                    }
                }

                // Cleanup
                bindingNameBuffer.Clear();
                commandBuffer.RemoveComponent<AnimationBindingName>(entity);

                if (bindingBuffer.Length == 0)
                {
                    // Nothing to animate
                    commandBuffer.RemoveComponent<AnimationBinding>(entity);
                    commandBuffer.RemoveComponent<AnimationBindingRetarget>(entity);
                    commandBuffer.RemoveComponent<TinyAnimationTime>(entity);
                    commandBuffer.RemoveComponent<TinyAnimationPlaybackInfo>(entity);
                    if (DstEntityManager.HasComponent<UpdateAnimationTimeTag>(entity))
                        commandBuffer.RemoveComponent<UpdateAnimationTimeTag>(entity);
                    if (DstEntityManager.HasComponent<ApplyAnimationResultTag>(entity))
                        commandBuffer.RemoveComponent<ApplyAnimationResultTag>(entity);
                }
                else
                {
                    bindingBuffer.TrimExcess();
                    bindingRetargetBuffer.TrimExcess();
                }
            }

            commandBuffer.Playback(DstEntityManager);
            commandBuffer.Dispose();

            animatedEntities.Dispose();
        }
    }
}
