using Unity.Collections;
using Unity.Entities;

namespace Unity.Tiny.Animation.Editor
{
    [UpdateInGroup(typeof(GameObjectAfterConversionGroup))]
    class TinyAnimationConversionCleanup : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            var bakedAnimationsQuery = DstEntityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<BakedAnimationClip>()
                },
                Options = EntityQueryOptions.IncludeDisabled | EntityQueryOptions.IncludePrefab
            });

            var commandBuffer = new EntityCommandBuffer(Allocator.TempJob);
            var bakedAnimationEntities = bakedAnimationsQuery.ToEntityArray(Allocator.TempJob);

            for (int entityIndex = 0; entityIndex < bakedAnimationEntities.Length; ++entityIndex)
                commandBuffer.DestroyEntity(bakedAnimationEntities[entityIndex]);

            var animationBindingNameQuery = DstEntityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<AnimationBindingName>()
                },
                Options = EntityQueryOptions.IncludeDisabled | EntityQueryOptions.IncludePrefab
            });

            commandBuffer.RemoveComponent<AnimationBindingName>(animationBindingNameQuery);

            commandBuffer.Playback(DstEntityManager);
            commandBuffer.Dispose();

            bakedAnimationEntities.Dispose();

            TinyAnimationConversionState.Clear();
        }
    }
}
