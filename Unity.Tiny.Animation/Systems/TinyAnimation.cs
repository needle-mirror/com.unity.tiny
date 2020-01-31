using System.Diagnostics;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Tiny.Animation
{
    public static unsafe class TinyAnimation
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Configure(World world,
            out EntityCommandBuffer buffer, out ComponentType updateAnimationTimeTagType, out ComponentType applyAnimationResultsTagType)
        {
            buffer = world.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>().CreateCommandBuffer();
            updateAnimationTimeTagType = ComponentType.ReadWrite<UpdateAnimationTimeTag>();
            applyAnimationResultsTagType = ComponentType.ReadWrite<ApplyAnimationResultTag>();
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void ValidateEntity(World world, Entity entity)
        {
            var tinyAnimationPlaybackType = ComponentType.ReadWrite<TinyAnimationPlayback>();
            world.EntityManager.EntityComponentStore->AssertEntityHasComponent(entity, tinyAnimationPlaybackType);
        }

        [PublicAPI]
        public static void Play(World world, Entity entity)
        {
            ValidateEntity(world, entity);
            Configure(world, out EntityCommandBuffer buffer, out ComponentType _, out ComponentType _);

            buffer.AddComponent<UpdateAnimationTimeTag>(entity);
            buffer.AddComponent<ApplyAnimationResultTag>(entity);
        }

        [PublicAPI]
        public static void Pause(World world, Entity entity)
        {
            ValidateEntity(world, entity);
            Configure(world, out EntityCommandBuffer buffer, out ComponentType updateAnimationTimeTagType, out ComponentType _);

            // Note: we're not removing ApplyAnimationResultTag during pause

            if (world.EntityManager.HasComponent(entity, updateAnimationTimeTagType))
                buffer.RemoveComponent<UpdateAnimationTimeTag>(entity);
        }

        [PublicAPI]
        public static void Stop(World world, Entity entity)
        {
            ValidateEntity(world, entity);
            Configure(world, out EntityCommandBuffer buffer, out ComponentType updateAnimationTimeTagType, out ComponentType applyAnimationResultsTagType);

            if (world.EntityManager.HasComponent(entity, updateAnimationTimeTagType))
                buffer.RemoveComponent<UpdateAnimationTimeTag>(entity);

            if (world.EntityManager.HasComponent(entity, applyAnimationResultsTagType))
                buffer.RemoveComponent<ApplyAnimationResultTag>(entity);

            var entityComponentStore = world.EntityManager.EntityComponentStore;
            var tinyAnimationPlayback = (TinyAnimationPlayback*) entityComponentStore->GetComponentDataWithTypeRW(entity, TypeManager.GetTypeIndex(typeof(TinyAnimationPlayback)), entityComponentStore->GlobalSystemVersion);
            tinyAnimationPlayback->time = 0;
        }

        [PublicAPI]
        public static bool IsPlaying(World world, Entity entity)
        {
            ValidateEntity(world, entity);
            return world.EntityManager.HasComponent(entity, ComponentType.ReadWrite<UpdateAnimationTimeTag>());
        }

        [PublicAPI]
        public static float GetDuration(World world, Entity entity)
        {
            ValidateEntity(world, entity);
            return world.EntityManager.GetComponentData<TinyAnimationPlayback>(entity).duration;
        }

        [PublicAPI]
        public static float GetTime(World world, Entity entity)
        {
            ValidateEntity(world, entity);
            return world.EntityManager.GetComponentData<TinyAnimationPlayback>(entity).time;
        }

        [PublicAPI]
        public static void SetTime(World world, Entity entity, float newTime)
        {
            ValidateEntity(world, entity);

            var entityComponentStore = world.EntityManager.EntityComponentStore;
            var tinyAnimationPlayback = (TinyAnimationPlayback*) entityComponentStore->GetComponentDataWithTypeRW(entity, TypeManager.GetTypeIndex(typeof(TinyAnimationPlayback)), entityComponentStore->GlobalSystemVersion);

            if (newTime <= 0.0f)
            {
                tinyAnimationPlayback->time = 0.0f;
            }
            else
            {
                var length = tinyAnimationPlayback->duration;
                var remainder = math.floor(newTime / length) * length;
                if (math.abs(remainder - newTime) < 0.0001f)
                {
                    tinyAnimationPlayback->time = length;
                }
                else
                {
                    tinyAnimationPlayback->time = math.clamp(newTime - remainder, 0.0f, length);
                }
            }
        }
    }
}
