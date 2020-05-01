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
                    ComponentType.ReadWrite<AnimationBindingRetarget>()
                },
                Any = new[]
                {
                    ComponentType.ReadWrite<AnimationBinding>(),
                    ComponentType.ReadWrite<AnimationPPtrBinding>()
                }
            });

            using (var commandBuffer = new EntityCommandBuffer(Allocator.Temp))
            using (var animatedEntities = dstWorldQuery.ToEntityArray(Allocator.TempJob))
            {
                for (int entityIndex = 0; entityIndex < animatedEntities.Length; ++entityIndex)
                {
                    var entity = animatedEntities[entityIndex];

                    var bindingNameBuffer = DstEntityManager.GetBuffer<AnimationBindingName>(entity);
                    var bindingRetargetBuffer = DstEntityManager.GetBuffer<AnimationBindingRetarget>(entity);

                    var isFloatAnimation = DstEntityManager.HasComponent<AnimationBinding>(entity);
                    var floatBindingBuffer = isFloatAnimation ? DstEntityManager.GetBuffer<AnimationBinding>(entity)     : default;
                    var pPtrBindingBuffer = !isFloatAnimation? DstEntityManager.GetBuffer<AnimationPPtrBinding>(entity) : default;

                    for (int i = bindingNameBuffer.Length - 1; i >= 0; --i)
                    {
                        var propertyPath = bindingNameBuffer[i].Value;
                        var discardEntry = true;

                        // A 0-length property path had no ECS equivalent at build time
                        if (propertyPath.LengthInBytes > 0)
                        {
                            var targetEntity = isFloatAnimation ? floatBindingBuffer[i].TargetEntity : pPtrBindingBuffer[i].TargetEntity;
                            var result = BindingUtils.GetBindingInfo(DstEntityManager, targetEntity, propertyPath);

                            if (result.Success)
                            {
                                var retarget = bindingRetargetBuffer[i];
                                retarget.StableTypeHash = result.StableTypeHash;
                                bindingRetargetBuffer[i] = retarget;

                                if (isFloatAnimation)
                                {
                                    var binding = floatBindingBuffer[i];
                                    binding.FieldOffset = result.FieldOffset;
                                    binding.FieldSize = result.FieldSize;
                                    floatBindingBuffer[i] = binding;
                                }
                                else
                                {
                                    var binding = pPtrBindingBuffer[i];
                                    binding.FieldOffset = result.FieldOffset;
                                    pPtrBindingBuffer[i] = binding;
                                }

                                discardEntry = false;
                            }
                        }

                        if (discardEntry)
                        {
                            if (isFloatAnimation)
                            {
                                floatBindingBuffer.RemoveAt(i);
                            }
                            else
                            {
                                pPtrBindingBuffer.RemoveAt(i);
                            }

                            bindingRetargetBuffer.RemoveAt(i);
                        }
                    }

                    // Cleanup
                    bindingNameBuffer.Clear();
                    commandBuffer.RemoveComponent<AnimationBindingName>(entity);

                    if ((isFloatAnimation && floatBindingBuffer.Length == 0) || (!isFloatAnimation && pPtrBindingBuffer.Length == 0))
                    {
                        // Nothing to animate
                        if (isFloatAnimation)
                            commandBuffer.RemoveComponent<AnimationBinding>(entity);
                        else
                            commandBuffer.RemoveComponent<AnimationPPtrBinding>(entity);

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
                        if (isFloatAnimation)
                            floatBindingBuffer.TrimExcess();
                        else
                            pPtrBindingBuffer.TrimExcess();

                        bindingRetargetBuffer.TrimExcess();
                    }
                }

                commandBuffer.Playback(DstEntityManager);
            }
        }
    }
}
