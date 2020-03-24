using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Tiny.Animation
{
    public static class TinyAnimation
    {
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void AssertEntityIsValidAnimationPlayer(World world, Entity entity)
        {
            if (!world.EntityManager.HasComponent<TinyAnimationPlayer>(entity))
                throw new ArgumentException($"Trying to use a TinyAnimation API on an entity without a component of type:{typeof(TinyAnimationPlayer)}.");

            AssertEntityIsValidAnimationClip(world, world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity).CurrentClip);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void AssertEntityIsValidAnimationClip(World world, Entity entity)
        {
            if (entity == Entity.Null)
                throw new ArgumentException("A tiny animation clip player should always have something to play.");

            if (!world.EntityManager.HasComponent<TinyAnimationTime>(entity))
                throw new ArgumentException($"A TinyAnimation clip player references a clip entity without a component of type {typeof(TinyAnimationTime)}.");

            if (!world.EntityManager.HasComponent<TinyAnimationPlaybackInfo>(entity))
                throw new ArgumentException($"A TinyAnimation clip player references a clip entity without a component of type {typeof(TinyAnimationPlaybackInfo)}.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int GetCurrentClipIndex_Internal(World world, Entity entity)
        {
            return world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity).CurrentIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int GetClipsCount_Internal(World world, Entity entity)
        {
            return world.EntityManager.GetBuffer<TinyAnimationClipRef>(entity).Length;
        }

        static void SelectClip_Internal(World world, Entity clipPlayerEntity, int clipIndex)
        {
            var clipPlayer = world.EntityManager.GetComponentData<TinyAnimationPlayer>(clipPlayerEntity);

            if (clipPlayer.CurrentIndex == clipIndex)
                return;

            var clipsBuffer = world.EntityManager.GetBuffer<TinyAnimationClipRef>(clipPlayerEntity);
            var selectedClip = clipsBuffer[clipIndex].Value;

            AssertEntityIsValidAnimationClip(world, selectedClip);

            var wasPlaying = IsPlaying(world, clipPlayerEntity);
            var wasPaused = !wasPlaying && IsPaused(world, clipPlayerEntity);

            Stop(world, clipPlayerEntity);

            world.EntityManager.SetComponentData(clipPlayerEntity, new TinyAnimationPlayer {CurrentClip = selectedClip, CurrentIndex = clipIndex});

            if (wasPlaying)
                Play(world, clipPlayerEntity);
            else if (wasPaused)
                Pause(world, clipPlayerEntity);
        }

        [PublicAPI]
        public static int GetCurrentClipIndex(World world, Entity entity)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);
            return GetCurrentClipIndex_Internal(world, entity);
        }

        [PublicAPI]
        public static int GetClipsCount(World world, Entity entity)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);
            return GetClipsCount_Internal(world, entity);
        }

        [PublicAPI]
        public static void SelectClip(World world, Entity entity, int clipIndex)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // Give exceptions with a *bit* more context when collection checks are on.
            if (clipIndex < 0 || clipIndex >= GetClipsCount_Internal(world, entity))
                throw new IndexOutOfRangeException($"Invalid TinyAnimation clip index: {clipIndex.ToString()}");
#endif

            SelectClip_Internal(world, entity, clipIndex);
        }

        [PublicAPI]
        public static void SelectNextClip(World world, Entity entity)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);

            var index = GetCurrentClipIndex_Internal(world, entity) + 1;
            var count = GetClipsCount_Internal(world, entity);

            if (index >= count)
                index = 0;

            SelectClip_Internal(world, entity, index);
        }

        [PublicAPI]
        public static void SelectPreviousClip(World world, Entity entity)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);

            var index = GetCurrentClipIndex_Internal(world, entity) - 1;

            if (index < 0)
                index = GetClipsCount_Internal(world, entity) - 1;

            SelectClip_Internal(world, entity, index);
        }

        [PublicAPI]
        public static void Play(World world, Entity entity)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);

            var currentClip = world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity).CurrentClip;

            // Are we playing a stopped clip?
            if (!world.EntityManager.HasComponent<UpdateAnimationTimeTag>(currentClip) &&
                !world.EntityManager.HasComponent<ApplyAnimationResultTag>(currentClip))
            {
                var playbackInfo = world.EntityManager.GetComponentData<TinyAnimationPlaybackInfo>(currentClip);

                // Are we updating a cyclical wrap mode?
                if (playbackInfo.WrapMode == WrapMode.Loop || playbackInfo.WrapMode == WrapMode.PingPong)
                {
                    var startOffset = playbackInfo.CycleOffset * playbackInfo.Duration;
                    world.EntityManager.SetComponentData(currentClip, new TinyAnimationTime { Value = startOffset, InternalWorkTime = startOffset});
                }
            }

            var buffer = world.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>().CreateCommandBuffer();
            buffer.AddComponent<UpdateAnimationTimeTag>(currentClip);
            buffer.AddComponent<ApplyAnimationResultTag>(currentClip);
        }

        [PublicAPI]
        public static void Pause(World world, Entity entity)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);

            var currentClip = world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity).CurrentClip;
            var buffer = world.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>().CreateCommandBuffer();

            if (world.EntityManager.HasComponent<UpdateAnimationTimeTag>(currentClip))
                buffer.RemoveComponent<UpdateAnimationTimeTag>(currentClip);

            // When a clip is paused, it retains control of all the animated values
            if (!world.EntityManager.HasComponent<ApplyAnimationResultTag>(currentClip))
                buffer.AddComponent<ApplyAnimationResultTag>(currentClip);
        }

        [PublicAPI]
        public static void Stop(World world, Entity entity)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);

            var currentClip = world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity).CurrentClip;
            var buffer = world.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>().CreateCommandBuffer();

            if (world.EntityManager.HasComponent<UpdateAnimationTimeTag>(currentClip))
                buffer.RemoveComponent<UpdateAnimationTimeTag>(currentClip);

            if (world.EntityManager.HasComponent<ApplyAnimationResultTag>(currentClip))
                buffer.RemoveComponent<ApplyAnimationResultTag>(currentClip);

            world.EntityManager.SetComponentData(currentClip, new TinyAnimationTime { Value = 0.0f, InternalWorkTime = 0.0f});
        }

        [PublicAPI]
        public static bool IsPlaying(World world, Entity entity)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);

            var currentClip = world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity).CurrentClip;

            return world.EntityManager.HasComponent<UpdateAnimationTimeTag>(currentClip) && // Time is updating
                   world.EntityManager.HasComponent<ApplyAnimationResultTag>(currentClip);  // Values are applied
        }

        [PublicAPI]
        public static bool IsPaused(World world, Entity entity)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);

            var currentClip = world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity).CurrentClip;

            return !world.EntityManager.HasComponent<UpdateAnimationTimeTag>(currentClip) && // Time is not updated
                   world.EntityManager.HasComponent<ApplyAnimationResultTag>(currentClip);   // Values are still applied
        }

        [PublicAPI]
        public static bool IsStopped(World world, Entity entity)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);

            var currentClip = world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity).CurrentClip;

            return !world.EntityManager.HasComponent<UpdateAnimationTimeTag>(currentClip) && // Time is not updated
                   !world.EntityManager.HasComponent<ApplyAnimationResultTag>(currentClip);  // Values are not applied
        }

        [PublicAPI]
        public static float GetDuration(World world, Entity entity)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);

            var currentClip = world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity).CurrentClip;

            return world.EntityManager.GetComponentData<TinyAnimationPlaybackInfo>(currentClip).Duration;
        }

        [PublicAPI]
        public static float GetDuration(World world, Entity entity, int clipIndex)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);

            var clipsBuffer = world.EntityManager.GetBuffer<TinyAnimationClipRef>(entity);
            var clip = clipsBuffer[clipIndex].Value;

            return world.EntityManager.GetComponentData<TinyAnimationPlaybackInfo>(clip).Duration;
        }

        [PublicAPI]
        public static NativeArray<float> GetDurations(World world, Entity entity, Allocator allocator)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);

            var clipsBuffer = world.EntityManager.GetBuffer<TinyAnimationClipRef>(entity);
            var durations = new NativeArray<float>(clipsBuffer.Length, allocator, NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < clipsBuffer.Length; ++i)
            {
                var clip = clipsBuffer[i].Value;
                durations[i] = world.EntityManager.GetComponentData<TinyAnimationPlaybackInfo>(clip).Duration;
            }

            return durations;
        }

        [PublicAPI]
        public static float GetTime(World world, Entity entity)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);

            var currentClip = world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity).CurrentClip;

            return world.EntityManager.GetComponentData<TinyAnimationTime>(currentClip).Value;
        }

        [PublicAPI]
        public static void SetTime(World world, Entity entity, float newTime)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);

            var currentClip = world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity).CurrentClip;

            if (newTime <= 0.0f)
            {
                newTime = 0.0f;
            }
            else
            {
                var length = world.EntityManager.GetComponentData<TinyAnimationPlaybackInfo>(currentClip).Duration;
                var remainder = math.floor(newTime / length) * length;
                if (math.abs(remainder - newTime) < 0.0001f)
                {
                    newTime = length;
                }
                else
                {
                    newTime = math.clamp(newTime - remainder, 0.0f, length);
                }
            }

            world.EntityManager.SetComponentData(currentClip, new TinyAnimationTime { Value = newTime, InternalWorkTime = newTime});
        }

        [PublicAPI]
        public static WrapMode GetWrapMode(World world, Entity entity)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);

            var currentClip = world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity).CurrentClip;

            return world.EntityManager.GetComponentData<TinyAnimationPlaybackInfo>(currentClip).WrapMode;
        }

        [PublicAPI]
        public static void SetWrapMode(World world, Entity entity, WrapMode wrapMode)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);

            var currentClip = world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity).CurrentClip;
            var playbackInfo = world.EntityManager.GetComponentData<TinyAnimationPlaybackInfo>(currentClip);

            if (playbackInfo.WrapMode == wrapMode)
                return;

            playbackInfo.WrapMode = wrapMode;
            world.EntityManager.SetComponentData(currentClip, playbackInfo);

            if (wrapMode == WrapMode.PingPong)
            {
                var time = world.EntityManager.GetComponentData<TinyAnimationTime>(currentClip);
                time.InternalWorkTime = time.Value;
                world.EntityManager.SetComponentData(currentClip, time);
            }
        }

        [PublicAPI]
        public static float GetCycleOffset(World world, Entity entity)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);

            var currentClip = world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity).CurrentClip;

            return world.EntityManager.GetComponentData<TinyAnimationPlaybackInfo>(currentClip).CycleOffset;
        }

        [PublicAPI]
        public static void SetCycleOffset(World world, Entity entity, float cycleOffset)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);

            var currentClip = world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity).CurrentClip;

            var playbackInfo = world.EntityManager.GetComponentData<TinyAnimationPlaybackInfo>(currentClip);
            playbackInfo.CycleOffset = AnimationMath.NormalizeCycle(cycleOffset);
            world.EntityManager.SetComponentData(currentClip, playbackInfo);
        }
    }
}
