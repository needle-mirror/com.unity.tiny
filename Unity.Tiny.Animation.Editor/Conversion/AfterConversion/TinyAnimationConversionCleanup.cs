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
                }
            });

            var commandBuffer = new EntityCommandBuffer(Allocator.TempJob);
            var bakedAnimationEntities = bakedAnimationsQuery.ToEntityArray(Allocator.TempJob);

            for (int entityIndex = 0; entityIndex < bakedAnimationEntities.Length; ++entityIndex)
                commandBuffer.DestroyEntity(bakedAnimationEntities[entityIndex]);

            var animatedAssetGroupingsQuery = DstEntityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<AnimatedAssetGrouping>()
                }
            });

            var entityGroupingEntities = animatedAssetGroupingsQuery.ToEntityArray(Allocator.TempJob);
            for (int entityIndex = 0; entityIndex < entityGroupingEntities.Length; ++entityIndex)
                commandBuffer.DestroyEntity(entityGroupingEntities[entityIndex]);

            var animatedAssetGroupingRefsQuery = DstEntityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<AnimatedAssetGroupingRef>()
                }
            });

            commandBuffer.RemoveComponent(animatedAssetGroupingRefsQuery, new ComponentType(typeof(AnimatedAssetGroupingRef), ComponentType.AccessMode.ReadOnly));

            commandBuffer.Playback(DstEntityManager);
            commandBuffer.Dispose();

            bakedAnimationEntities.Dispose();
            entityGroupingEntities.Dispose();
        }
    }
}
