using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

namespace Unity.Tiny.Animation
{
    // TODO: jobify once the bogus IL generation is fixed in DOTS Player
    
    [UpdateInGroup(typeof(TinyAnimationSystemGroup))]
    [UpdateAfter(typeof(UpdateAnimationTime))]
    class UpdateAnimatedComponents : ComponentSystem
    {
        protected override unsafe void OnUpdate()
        {
            var entityComponentStore = EntityManager.EntityComponentStore;
            var globalVersion = entityComponentStore->GlobalSystemVersion;

            Entities
                .WithNone<AnimationBindingRetarget>()
                .WithAllReadOnly<ApplyAnimationResultTag>()
                .WithAllReadOnly<TinyAnimationClip>()
                .ForEach((DynamicBuffer<AnimationBinding> bindings, ref TinyAnimationClip animData) =>
                {
                    var bindingsArray = bindings.ToNativeArray(Allocator.TempJob);
                    var job = new UpdateJob
                    {
                        globalVersion = globalVersion,
                        entityComponentStore = entityComponentStore,
                        time = animData.time,
                        bindings = bindingsArray
                    };

                    job.Run();
                    bindingsArray.Dispose();
                });
        }

        [BurstCompile(FloatMode = FloatMode.Fast)]
        unsafe struct UpdateJob : IJob
        {
            [NativeDisableUnsafePtrRestriction]
            public EntityComponentStore* entityComponentStore;
            public uint globalVersion;

            public float time;
            public NativeArray<AnimationBinding> bindings;

            public void Execute()
            {
                for (int i = 0; i < bindings.Length; ++i)
                {
                    var binding = bindings[i];
                    var result = KeyframeCurveEvaluator.Evaluate(time, binding.curve);
                    var typeIndex = binding.targetComponentTypeIndex;

                    var entity = binding.targetEntity;

                    entityComponentStore->AssertEntityHasComponent(entity, typeIndex);

                    var targetComponentPtr = entityComponentStore->GetComponentDataWithTypeRW(entity, typeIndex, globalVersion);
                    var targetFieldPtr = targetComponentPtr + binding.fieldOffset;

                    UnsafeUtility.MemCpy(targetFieldPtr, UnsafeUtility.AddressOf(ref result), binding.fieldSize);
                }
            }
        }
    }
}
