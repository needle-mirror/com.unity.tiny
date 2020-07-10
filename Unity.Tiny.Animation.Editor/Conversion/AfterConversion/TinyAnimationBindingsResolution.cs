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
            using (var commandBuffer = new EntityCommandBuffer(Allocator.Temp))
            {
                ResolveFloatBindings(commandBuffer);
                ResolvePPtrBindings(commandBuffer);

                commandBuffer.Playback(DstEntityManager);
            }
        }

        void ResolveFloatBindings(EntityCommandBuffer ecb)
        {
            var query = DstEntityManager.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadWrite<AnimationBindingRetarget>(),
                        ComponentType.ReadOnly<AnimationBindingName>(),
                        ComponentType.ReadWrite<AnimationBinding>()
                    }
                });

            using (var animatedEntities = query.ToEntityArray(Allocator.TempJob))
            {
                for (int entityIndex = 0; entityIndex < animatedEntities.Length; ++entityIndex)
                {
                    var entity = animatedEntities[entityIndex];
                    var bindingRetargetBuffer = DstEntityManager.GetBuffer<AnimationBindingRetarget>(entity);
                    var bindingNameBuffer = DstEntityManager.GetBuffer<AnimationBindingName>(entity);
                    var bindingBuffer = DstEntityManager.GetBuffer<AnimationBinding>(entity);

                    for (int i = bindingNameBuffer.Length - 1; i >= 0; --i)
                    {
                        var propertyPath = bindingNameBuffer[i].Value;
                        var discardEntry = true;

                        // A 0-length property path had no ECS equivalent at build time
                        if (!propertyPath.IsEmpty)
                        {
                            var targetEntity = bindingBuffer[i].TargetEntity;
                            var result = BindingUtils.GetBindingInfo(DstEntityManager, targetEntity, propertyPath);

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
                    ecb.RemoveComponent<AnimationBindingName>(entity);

                    if (bindingBuffer.Length == 0)
                    {
                        // Nothing to animate
                        ecb.RemoveComponent<AnimationBinding>(entity);
                        ecb.RemoveComponent<AnimationBindingRetarget>(entity);
                        ecb.RemoveComponent<TinyAnimationTime>(entity);
                        ecb.RemoveComponent<TinyAnimationPlaybackInfo>(entity);

                        if (DstEntityManager.HasComponent<UpdateAnimationTimeTag>(entity))
                            ecb.RemoveComponent<UpdateAnimationTimeTag>(entity);

                        if (DstEntityManager.HasComponent<ApplyAnimationResultTag>(entity))
                            ecb.RemoveComponent<ApplyAnimationResultTag>(entity);
                    }
                    else
                    {
                        bindingBuffer.TrimExcess();
                        bindingRetargetBuffer.TrimExcess();
                    }
                }
            }
        }

        void ResolvePPtrBindings(EntityCommandBuffer ecb)
        {
            var query = DstEntityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<AnimationPPtrBindingRetarget>(),
                    ComponentType.ReadOnly<AnimationPPtrBindingName>(),
                    ComponentType.ReadWrite<AnimationPPtrBinding>()
                }
            });

            using (var animatedEntities = query.ToEntityArray(Allocator.TempJob))
            {
                for (int entityIndex = 0; entityIndex < animatedEntities.Length; ++entityIndex)
                {
                    var entity = animatedEntities[entityIndex];
                    var bindingRetargetBuffer = DstEntityManager.GetBuffer<AnimationPPtrBindingRetarget>(entity);
                    var bindingNameBuffer = DstEntityManager.GetBuffer<AnimationPPtrBindingName>(entity);
                    var bindingBuffer = DstEntityManager.GetBuffer<AnimationPPtrBinding>(entity);

                    for (int i = bindingNameBuffer.Length - 1; i >= 0; --i)
                    {
                        var propertyPath = bindingNameBuffer[i].Value;
                        var discardEntry = true;

                        // A 0-length property path had no ECS equivalent at build time
                        if (!propertyPath.IsEmpty)
                        {
                            var targetEntity = bindingBuffer[i].TargetEntity;
                            var result = BindingUtils.GetBindingInfo(DstEntityManager, targetEntity, propertyPath);

                            if (result.Success)
                            {
                                var retarget = bindingRetargetBuffer[i];
                                retarget.StableTypeHash = result.StableTypeHash;
                                bindingRetargetBuffer[i] = retarget;

                                var binding = bindingBuffer[i];
                                binding.FieldOffset = result.FieldOffset;
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
                    ecb.RemoveComponent<AnimationPPtrBindingName>(entity);

                    if (bindingBuffer.Length == 0)
                    {
                        // Nothing to animate
                        ecb.RemoveComponent<AnimationPPtrBinding>(entity);
                        ecb.RemoveComponent<AnimationPPtrBindingRetarget>(entity);
                        ecb.RemoveComponent<TinyAnimationTime>(entity);
                        ecb.RemoveComponent<TinyAnimationPlaybackInfo>(entity);

                        if (DstEntityManager.HasComponent<UpdateAnimationTimeTag>(entity))
                            ecb.RemoveComponent<UpdateAnimationTimeTag>(entity);

                        if (DstEntityManager.HasComponent<ApplyAnimationResultTag>(entity))
                            ecb.RemoveComponent<ApplyAnimationResultTag>(entity);
                    }
                    else
                    {
                        bindingBuffer.TrimExcess();
                        bindingRetargetBuffer.TrimExcess();
                    }
                }
            }
        }
    }
}
