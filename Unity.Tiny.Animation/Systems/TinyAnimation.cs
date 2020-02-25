using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Tiny.Animation
{
    public static unsafe class TinyAnimation
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Configure(World world, Entity entity,
            out Entity currentClip, out EntityCommandBuffer buffer,
            out ComponentType updateAnimationTimeTagType, out ComponentType applyAnimationResultsTagType)
        {
            var clipPlayer = world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var tinyAnimPlayback = ComponentType.ReadWrite<TinyAnimationClip>();
            world.EntityManager.EntityComponentStore->AssertEntityHasComponent(clipPlayer.currentClip, tinyAnimPlayback);
#endif
            currentClip = clipPlayer.currentClip;
            buffer = world.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>().CreateCommandBuffer();
            updateAnimationTimeTagType = ComponentType.ReadWrite<UpdateAnimationTimeTag>();
            applyAnimationResultsTagType = ComponentType.ReadWrite<ApplyAnimationResultTag>();
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void ValidateEntity(World world, Entity entity)
        {
            var tinyAnimationPlayerType = ComponentType.ReadWrite<TinyAnimationPlayer>();
            world.EntityManager.EntityComponentStore->AssertEntityHasComponent(entity, tinyAnimationPlayerType);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int GetCurrentClipIndex_Internal(World world, Entity entity)
        {
            return world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity).currentIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int GetClipsCount_Internal(World world, Entity entity)
        {
            return world.EntityManager.GetBuffer<TinyAnimationClipRef>(entity).Length;
        }

        static void SelectClip_Internal(World world, Entity clipPlayerEntity, int clipIndex)
        {
            var clipPlayer = world.EntityManager.GetComponentData<TinyAnimationPlayer>(clipPlayerEntity);

            if (clipPlayer.currentIndex == clipIndex)
                return;

            var clipsBuffer = world.EntityManager.GetBuffer<TinyAnimationClipRef>(clipPlayerEntity);
            var selectedClip = clipsBuffer[clipIndex].value;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var tinyAnimPlayback = ComponentType.ReadWrite<TinyAnimationClip>();
            world.EntityManager.EntityComponentStore->AssertEntityHasComponent(clipPlayer.currentClip, tinyAnimPlayback);
            world.EntityManager.EntityComponentStore->AssertEntityHasComponent(selectedClip, tinyAnimPlayback);
#endif
            var wasPlaying = IsPlaying(world, clipPlayerEntity);
            var wasPaused = !wasPlaying && IsPaused(world, clipPlayerEntity);

            Stop(world, clipPlayerEntity);

            world.EntityManager.SetComponentData(clipPlayerEntity, new TinyAnimationPlayer {currentClip = selectedClip, currentIndex = clipIndex});

            if (wasPlaying)
                Play(world, clipPlayerEntity);
            else if (wasPaused)
                Pause(world, clipPlayerEntity);
        }

        [PublicAPI]
        public static int GetCurrentClipIndex(World world, Entity entity)
        {
            ValidateEntity(world, entity);
            return GetCurrentClipIndex_Internal(world, entity);
        }

        [PublicAPI]
        public static int GetClipsCount(World world, Entity entity)
        {
            ValidateEntity(world, entity);
            return GetClipsCount_Internal(world, entity);
        }

        [PublicAPI]
        public static void SelectClip(World world, Entity entity, int clipIndex)
        {
            ValidateEntity(world, entity);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // Give exceptions with a *bit* more context when collection checks are on.
            if (clipIndex < 0)
                throw new ArgumentException($"Invalid TinyAnimation clip index: {clipIndex.ToString()}");

            if (clipIndex >= GetClipsCount_Internal(world, entity))
                throw new ArgumentException($"Invalid TinyAnimation clip index: {clipIndex.ToString()}");
#endif

            SelectClip_Internal(world, entity, clipIndex);
        }

        [PublicAPI]
        public static void SelectNextClip(World world, Entity entity)
        {
            ValidateEntity(world, entity);
            var index = GetCurrentClipIndex_Internal(world, entity) + 1;
            var count = GetClipsCount_Internal(world, entity);

            if (index >= count)
                index = 0;

            SelectClip_Internal(world, entity, index);
        }

        [PublicAPI]
        public static void SelectPreviousClip(World world, Entity entity)
        {
            ValidateEntity(world, entity);
            var index = GetCurrentClipIndex_Internal(world, entity) - 1;

            if (index < 0)
                index = GetClipsCount_Internal(world, entity) - 1;

            SelectClip_Internal(world, entity, index);
        }

        [PublicAPI]
        public static void Play(World world, Entity entity)
        {
            ValidateEntity(world, entity);
            Configure(world, entity, out Entity currentClip, out EntityCommandBuffer buffer, out ComponentType _, out ComponentType _);

            buffer.AddComponent<UpdateAnimationTimeTag>(currentClip);
            buffer.AddComponent<ApplyAnimationResultTag>(currentClip);
        }

        [PublicAPI]
        public static void Pause(World world, Entity entity)
        {
            ValidateEntity(world, entity);
            Configure(world, entity, out Entity currentClip, out EntityCommandBuffer buffer, out ComponentType updateAnimationTimeTagType, out ComponentType applyAnimationResultsTagType);

            if (world.EntityManager.HasComponent(currentClip, updateAnimationTimeTagType))
                buffer.RemoveComponent<UpdateAnimationTimeTag>(currentClip);

            // When a clip is paused, it retains control of all the animated values
            if (!world.EntityManager.HasComponent(currentClip, applyAnimationResultsTagType))
                buffer.AddComponent<ApplyAnimationResultTag>(currentClip);
        }

        [PublicAPI]
        public static void Stop(World world, Entity entity)
        {
            ValidateEntity(world, entity);
            Configure(world, entity, out Entity currentClip, out EntityCommandBuffer buffer, out ComponentType updateAnimationTimeTagType, out ComponentType applyAnimationResultsTagType);

            if (world.EntityManager.HasComponent(currentClip, updateAnimationTimeTagType))
                buffer.RemoveComponent<UpdateAnimationTimeTag>(currentClip);

            if (world.EntityManager.HasComponent(currentClip, applyAnimationResultsTagType))
                buffer.RemoveComponent<ApplyAnimationResultTag>(currentClip);

            var entityComponentStore = world.EntityManager.EntityComponentStore;
            var tinyAnimationPlayback = (TinyAnimationClip*) entityComponentStore->GetComponentDataWithTypeRW(currentClip, TypeManager.GetTypeIndex(typeof(TinyAnimationClip)), entityComponentStore->GlobalSystemVersion);
            tinyAnimationPlayback->time = 0;
        }

        [PublicAPI]
        public static bool IsPlaying(World world, Entity entity)
        {
            ValidateEntity(world, entity);

            var clipPlayer = world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var tinyAnimPlayback = ComponentType.ReadWrite<TinyAnimationClip>();
            world.EntityManager.EntityComponentStore->AssertEntityHasComponent(clipPlayer.currentClip, tinyAnimPlayback);
#endif
            var currentClip = clipPlayer.currentClip;

            return world.EntityManager.HasComponent(currentClip, ComponentType.ReadOnly<UpdateAnimationTimeTag>()) && // Time is updating
                   world.EntityManager.HasComponent(currentClip, ComponentType.ReadOnly<ApplyAnimationResultTag>());  // Values are applied
        }

        [PublicAPI]
        public static bool IsPaused(World world, Entity entity)
        {
            ValidateEntity(world, entity);

            var clipPlayer = world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var tinyAnimPlayback = ComponentType.ReadWrite<TinyAnimationClip>();
            world.EntityManager.EntityComponentStore->AssertEntityHasComponent(clipPlayer.currentClip, tinyAnimPlayback);
#endif
            var currentClip = clipPlayer.currentClip;

            return !world.EntityManager.HasComponent(currentClip, ComponentType.ReadOnly<UpdateAnimationTimeTag>()) && // Time is not updated
                   world.EntityManager.HasComponent(currentClip, ComponentType.ReadOnly<ApplyAnimationResultTag>());   // Values are still applied
        }

        [PublicAPI]
        public static bool IsStopped(World world, Entity entity)
        {
            ValidateEntity(world, entity);

            var clipPlayer = world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var tinyAnimPlayback = ComponentType.ReadWrite<TinyAnimationClip>();
            world.EntityManager.EntityComponentStore->AssertEntityHasComponent(clipPlayer.currentClip, tinyAnimPlayback);
#endif
            var currentClip = clipPlayer.currentClip;

            return !world.EntityManager.HasComponent(currentClip, ComponentType.ReadOnly<UpdateAnimationTimeTag>()) && // Time is not updated
                   !world.EntityManager.HasComponent(currentClip, ComponentType.ReadOnly<ApplyAnimationResultTag>());  // Values are not applied
        }

        [PublicAPI]
        public static float GetDuration(World world, Entity entity)
        {
            ValidateEntity(world, entity);

            var clipPlayer = world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var tinyAnimPlayback = ComponentType.ReadWrite<TinyAnimationClip>();
            world.EntityManager.EntityComponentStore->AssertEntityHasComponent(clipPlayer.currentClip, tinyAnimPlayback);
#endif
            var currentClip = clipPlayer.currentClip;

            return world.EntityManager.GetComponentData<TinyAnimationClip>(currentClip).duration;
        }

        [PublicAPI]
        public static float GetDuration(World world, Entity entity, int clipIndex)
        {
            ValidateEntity(world, entity);

            var clipsBuffer = world.EntityManager.GetBuffer<TinyAnimationClipRef>(entity);
            var clip = clipsBuffer[clipIndex].value;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var tinyAnimPlayback = ComponentType.ReadWrite<TinyAnimationClip>();
            world.EntityManager.EntityComponentStore->AssertEntityHasComponent(clip, tinyAnimPlayback);
#endif

            return world.EntityManager.GetComponentData<TinyAnimationClip>(clip).duration;
        }

        [PublicAPI]
        public static NativeArray<float> GetDurations(World world, Entity entity, Allocator allocator)
        {
            ValidateEntity(world, entity);

            var clipsBuffer = world.EntityManager.GetBuffer<TinyAnimationClipRef>(entity);
            var durations = new NativeArray<float>(clipsBuffer.Length, allocator, NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < clipsBuffer.Length; ++i)
            {
                var clip = clipsBuffer[i].value;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                var tinyAnimPlayback = ComponentType.ReadWrite<TinyAnimationClip>();
                world.EntityManager.EntityComponentStore->AssertEntityHasComponent(clip, tinyAnimPlayback);
#endif
                durations[i] = world.EntityManager.GetComponentData<TinyAnimationClip>(clip).duration;
            }

            return durations;
        }

        [PublicAPI]
        public static float GetTime(World world, Entity entity)
        {
            ValidateEntity(world, entity);

            var clipPlayer = world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var tinyAnimPlayback = ComponentType.ReadWrite<TinyAnimationClip>();
            world.EntityManager.EntityComponentStore->AssertEntityHasComponent(clipPlayer.currentClip, tinyAnimPlayback);
#endif
            var currentClip = clipPlayer.currentClip;

            return world.EntityManager.GetComponentData<TinyAnimationClip>(currentClip).time;
        }

        [PublicAPI]
        public static void SetTime(World world, Entity entity, float newTime)
        {
            ValidateEntity(world, entity);

            var clipPlayer = world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var tinyAnimPlayback = ComponentType.ReadWrite<TinyAnimationClip>();
            world.EntityManager.EntityComponentStore->AssertEntityHasComponent(clipPlayer.currentClip, tinyAnimPlayback);
#endif
            var currentClip = clipPlayer.currentClip;

            var entityComponentStore = world.EntityManager.EntityComponentStore;
            var tinyAnimationPlayback = (TinyAnimationClip*) entityComponentStore->GetComponentDataWithTypeRW(currentClip, TypeManager.GetTypeIndex(typeof(TinyAnimationClip)), entityComponentStore->GlobalSystemVersion);

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
