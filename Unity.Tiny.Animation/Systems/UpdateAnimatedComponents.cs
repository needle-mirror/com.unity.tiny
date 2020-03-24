using JetBrains.Annotations;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Unity.Tiny.Animation
{
    [UsedImplicitly]
    [UpdateInGroup(typeof(TinyAnimationSystemGroup))]
    [UpdateAfter(typeof(UpdateAnimationTime))]
    unsafe class UpdateAnimatedComponents : SystemBase
    {
        protected override void OnUpdate()
        {
            var entityComponentStore = EntityManager.EntityComponentStore;
            var globalVersion = entityComponentStore->GlobalSystemVersion;

            Dependency =
                Entities
                   .WithNativeDisableUnsafePtrRestriction(entityComponentStore)
                   .WithBurst(FloatMode.Fast)
                   .WithAll<ApplyAnimationResultTag>()
                   .ForEach(
                        (in DynamicBuffer<AnimationBinding> bindings, in TinyAnimationTime animationTime) =>
                        {
                            var time = animationTime.Value;
                            for (int i = 0; i < bindings.Length; ++i)
                            {
                                var binding = bindings[i];
                                var result = KeyframeCurveEvaluator.Evaluate(time, binding.Curve);
                                var typeIndex = binding.TargetComponentTypeIndex;

                                var entity = binding.TargetEntity;

                                entityComponentStore->AssertEntityHasComponent(entity, typeIndex);

                                var targetComponentPtr = entityComponentStore->GetComponentDataWithTypeRW(entity, typeIndex, globalVersion);
                                var targetFieldPtr = targetComponentPtr + binding.FieldOffset;

                                UnsafeUtility.MemCpy(targetFieldPtr, UnsafeUtility.AddressOf(ref result), binding.FieldSize);
                            }
                        }).Schedule(Dependency);
        }
    }
}
