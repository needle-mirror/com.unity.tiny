using JetBrains.Annotations;
using Unity.Burst;
using Unity.Entities;

namespace Unity.Tiny.Animation
{
    [UsedImplicitly]
    [UpdateInGroup(typeof(TinyAnimationSystemGroup))]
    [UpdateAfter(typeof(UpdateAnimationTime))]
    unsafe class UpdatePPtrIndices : SystemBase
    {
        int m_PPtrIndexTypeIndex;

        protected override void OnCreate()
        {
            m_PPtrIndexTypeIndex = ComponentType.ReadWrite<PPtrIndex>().TypeIndex;
        }

        protected override void OnUpdate()
        {
            var entityComponentStore = EntityManager.EntityComponentStore;
            var globalVersion = entityComponentStore->GlobalSystemVersion;
            var pPtrTypeIndex = m_PPtrIndexTypeIndex;

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
                                var result = KeyframeCurveEvaluator.Evaluate(time, binding.Curve);
                                var entity = binding.TargetEntity;

                                entityComponentStore->AssertEntityHasComponent(entity, pPtrTypeIndex);
                                var targetComponentPtr = (PPtrIndex*) entityComponentStore->GetComponentDataWithTypeRW(entity, pPtrTypeIndex, globalVersion);
                                targetComponentPtr->Value = (ushort) result;
                            }
                        }).Schedule(Dependency);
        }
    }
}
