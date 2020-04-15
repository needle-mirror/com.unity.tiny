using JetBrains.Annotations;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Unity.Tiny.Animation
{
    [UsedImplicitly]
    [UpdateInGroup(typeof(TinyAnimationSystemGroup))]
    [UpdateAfter(typeof(UpdateAnimationTime))]
    unsafe class UpdateAnimatedPPtrValues : SystemBase
    {
        int m_AnimatedAssetReferenceTypeIndex;

        protected override void OnCreate()
        {
            m_AnimatedAssetReferenceTypeIndex = ComponentType.ReadWrite<AnimationPPtrBindingSources>().TypeIndex;
        }

        protected override void OnUpdate()
        {
            var entityComponentStore = EntityManager.EntityComponentStore;
            var globalVersion = entityComponentStore->GlobalSystemVersion;
            var animatedAssetReferenceTypeIndex = m_AnimatedAssetReferenceTypeIndex;

            Dependency =
                Entities
                   .WithNativeDisableUnsafePtrRestriction(entityComponentStore)
                   .WithBurst(FloatMode.Fast)
                   .WithAll<ApplyAnimationResultTag>()
                   .ForEach(
                        (in DynamicBuffer<AnimationPPtrBinding> bindings, in TinyAnimationTime animationTime) =>
                        {
                            var time = animationTime.Value;
                            for (int i = 0; i < bindings.Length; ++i)
                            {
                                var binding = bindings[i];
                                var result = (int) KeyframeCurveEvaluator.Evaluate(time, binding.Curve);

                                var source = binding.SourceEntity;
                                entityComponentStore->AssertEntityHasComponent(source, animatedAssetReferenceTypeIndex);

                                var pPtrBindingSourcesBuffer = (BufferHeader*) entityComponentStore->GetComponentDataWithTypeRO(source, animatedAssetReferenceTypeIndex);
                                var pPtrBindingSource = ((AnimationPPtrBindingSources*)BufferHeader.GetElementPointer(pPtrBindingSourcesBuffer))[result];

                                var typeIndex = binding.TargetComponentTypeIndex;

                                var entity = binding.TargetEntity;

                                entityComponentStore->AssertEntityHasComponent(entity, typeIndex);

                                var targetComponentPtr = entityComponentStore->GetComponentDataWithTypeRW(entity, typeIndex, globalVersion);
                                var targetFieldPtr = targetComponentPtr + binding.FieldOffset;

                                UnsafeUtility.MemCpy(targetFieldPtr, UnsafeUtility.AddressOf(ref pPtrBindingSource.Value), UnsafeUtility.SizeOf<Entity>());
                            }
                        }).Schedule(Dependency);
        }
    }
}
