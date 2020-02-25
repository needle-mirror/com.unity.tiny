using Unity.Entities;

namespace Unity.Tiny.Animation
{
    // TODO: jobify once the bogus IL generation is fixed in DOTS Player

    [UpdateInGroup(typeof(TinyAnimationSystemGroup))]
    [UpdateAfter(typeof(UpdateAnimationTime))]
    class UpdatePPtrIndices : ComponentSystem
    {
        int m_PPtrIndexTypeIndex;

        protected override void OnCreate()
        {
            m_PPtrIndexTypeIndex = ComponentType.ReadWrite<PPtrIndex>().TypeIndex;
        }

        protected override unsafe void OnUpdate()
        {
            var entityComponentStore = EntityManager.EntityComponentStore;
            var globalVersion = entityComponentStore->GlobalSystemVersion;

            Entities
               .WithAllReadOnly<TinyAnimationClip>()
               .WithAllReadOnly<ApplyAnimationResultTag>()
               .WithAllReadOnly<AnimationPPtrBinding>()
               .ForEach((DynamicBuffer<AnimationPPtrBinding> bindings, ref TinyAnimationClip animData) =>
                {
                    var time = animData.time;
                    for (int i = 0; i < bindings.Length; ++i)
                    {
                        var binding = bindings[i];
                        var result = KeyframeCurveEvaluator.Evaluate(time, binding.curve);
                        var entity = binding.targetEntity;

                        entityComponentStore->AssertEntityHasComponent(entity, m_PPtrIndexTypeIndex);
                        var targetComponentPtr = (PPtrIndex*) entityComponentStore->GetComponentDataWithTypeRW(entity, m_PPtrIndexTypeIndex, globalVersion);
                        targetComponentPtr->value = (ushort) result;
                    }
                });
        }
    }
}
