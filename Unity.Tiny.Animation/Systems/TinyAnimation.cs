using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Tiny.Animation
{
    /// <summary>
    /// A collection of utility methods to control the playback of animation clips.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Most of the TinyAnimation methods require both a <see cref="World"/> and an <see cref="Entity"/> to perform the required operations.
    /// The entity parameter is always the player <see cref="Entity"/> identifying the data with which to perform the operation. This entity must have
    /// a <see cref="TinyAnimationClipRef"/> buffer component, which contains a list of animation clips, and a <see cref="TinyAnimationPlayer"/>
    /// component, which identifies an animation clip in that list. The clip identified by <see cref="TinyAnimationPlayer"/> is the animation
    /// affected by subsequent calls to TinyAnimation functions. For example, to play a specifc clip in the <see cref="TinyAnimationClipRef"/>
    /// list, first call <see cref="SelectClip(World, Entity, int)"/>, which updates the <see cref="TinyAnimationPlayer"/> data, and then call
    /// <see cref="Play(World, Entity)"/>.
    /// </para>
    /// <para>
    /// The world parameter always represents the <see cref="World"/> in which the player entity exists.
    /// </para>
    /// 
    /// </remarks>
    public static class TinyAnimation
    {
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void AssertEntityIsValidAnimationPlayer(World world, Entity entity)
        {
            if (!world.EntityManager.HasComponent<TinyAnimationPlayer>(entity))
                throw new ArgumentException($"Trying to use a TinyAnimation API on an entity without a component of type {typeof(TinyAnimationPlayer)}.");

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

        static int GetClipIndex(World world, Entity clipPlayerEntity, uint clipHash)
        {
            var clipsBuffer = world.EntityManager.GetBuffer<TinyAnimationClipRef>(clipPlayerEntity);

            for (int i = 0; i < clipsBuffer.Length; ++i)
            {
                if (clipsBuffer[i].Hash == clipHash)
                    return i;
            }

            return -1;
        }

        static void SelectClip_Internal(World world, Entity clipPlayerEntity, uint clipHash)
        {
            var index = GetClipIndex(world, clipPlayerEntity, clipHash);
            if (index == -1)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new ArgumentException($"A tiny animation clip with hash: {clipHash} could not be found for the selected entity ({clipPlayerEntity}).");
#else
                return;
#endif
            }

            SelectClip_Internal(world, clipPlayerEntity, index);
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

        /// <summary>
        /// A simple hashing function used to generate efficient deterministic identifiers for animation clips.
        /// </summary>
        /// <param name="source">The string to hash. Generally, the name of the animation clip asset.</param>
        /// <returns>The hashed value for the input string.</returns>
        [PublicAPI]
        public static uint StringToHash(string source)
        {
            if (string.IsNullOrEmpty(source))
                return 0;

            uint hash;
            unsafe
            {
                fixed (char* ptr = source)
                {
                    // This could be replaced to be compatible with FixedStringN; but if a user is storing
                    // their clip name as a FixedStringN, why not simply store it as its hash instead?
                    hash = math.hash(ptr, UnsafeUtility.SizeOf<char>() * source.Length);
                }
            }

            return hash;
        }

        /// <summary>
        /// The index of the clip currently selected for playback on the specified entity.
        /// </summary>
        /// <param name="world">The world containing <paramref name="entity"/>.</param>
        /// <param name="entity">
        /// <para>The entity on which to perform the operation.</para>
        /// <para>
        /// Note that <paramref name="entity"/> must have both a <see cref="TinyAnimationPlayer"/> component and
        /// a <see cref="TinyAnimationClipRef"/> buffer to be considered valid.
        /// </para>
        /// </param>
        /// <returns>
        /// The index, in the <see cref="TinyAnimationClipRef"/> buffer, of the currently selected clip.
        /// </returns>
        [PublicAPI]
        public static int GetCurrentClipIndex(World world, Entity entity)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);
            return GetCurrentClipIndex_Internal(world, entity);
        }

        /// <summary>
        /// The hashed name used as an identifier for the clip currently selected for playback on the specified entity.
        /// </summary>
        /// <param name="world">The world containing <paramref name="entity"/>.</param>
        /// <param name="entity">
        /// <para>The entity on which to perform the operation.</para>
        /// <para>
        /// Note that <paramref name="entity"/> must have both a <see cref="TinyAnimationPlayer"/> component and
        /// a <see cref="TinyAnimationClipRef"/> buffer to be considered valid.
        /// </para>
        /// </param>
        /// <returns>The hashed value used to identify the currently selected clip.</returns>
        /// <seealso cref="StringToHash(string)"/>
        [PublicAPI]
        public static uint GetCurrentClipHash(World world, Entity entity)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);
            var index = GetCurrentClipIndex_Internal(world, entity);
            var clipsBuffer = world.EntityManager.GetBuffer<TinyAnimationClipRef>(entity);
            return clipsBuffer[index].Hash;
        }

        /// <summary>
        /// The number of animation clips associated with the specified entity.
        /// </summary>
        /// <param name="world">The world containing <paramref name="entity"/>.</param>
        /// <param name="entity">
        /// <para>The entity on which to perform the operation.</para>
        /// <para>
        /// Note that <paramref name="entity"/> must have both a <see cref="TinyAnimationPlayer"/> component and
        /// a <see cref="TinyAnimationClipRef"/> buffer to be considered valid.
        /// </para>
        /// </param>
        /// <returns>
        /// The number of clips in the <see cref="TinyAnimationClipRef"/> buffer of this entity.
        /// </returns>
        [PublicAPI]
        public static int GetClipsCount(World world, Entity entity)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);
            return GetClipsCount_Internal(world, entity);
        }

        /// <summary>
        /// Selects an animation clip for playback using its clip name. 
        /// </summary>
        /// <remarks>
        /// After you select a clip, subsequent calls to <see cref="TinyAnimation.Play(World, Entity)"/>,
        /// <see cref="TinyAnimation.Pause(World, Entity)"/>, <see cref="TinyAnimation.SetTime(World, Entity, float)"/>,
        /// etc. apply to the selected clip.
        /// 
        /// Selecting a new animation clip does not change the current playback status. If the previous clip was playing,
        /// the new clip continues playing. If the previous clip was paused, playback remains paused.
        /// 
        /// Selecting a clip using its name is very inefficient and unless you have a good reason for using it, we recommend
        /// selecting clips by <see cref="TinyAnimationClipRef.Hash"/> value using the
        /// <see cref="TinyAnimation.SelectClip(World,Entity,uint)"/> overload instead.
        /// </remarks>
        /// <param name="world">The world containing <paramref name="entity"/>.</param>
        /// <param name="entity">
        /// <para>The entity on which to perform the operation.</para>
        /// <para>
        /// Note that <paramref name="entity"/> must have both a <see cref="TinyAnimationPlayer"/> component and
        /// a <see cref="TinyAnimationClipRef"/> buffer to be considered valid.
        /// </para>
        /// </param>
        /// <param name="clipName"> The name of the animation clip asset to select for playback.</param>
        [PublicAPI]
        public static void SelectClip(World world, Entity entity, string clipName)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);
            SelectClip_Internal(world, entity, StringToHash(clipName));
        }

        /// <summary>
        /// Selects an animation clip for playback using its hash.
        /// </summary>
        /// After you select a clip, subsequent calls to <see cref="TinyAnimation.Play(World, Entity)"/>,
        /// <see cref="TinyAnimation.Pause(World, Entity)"/>, <see cref="TinyAnimation.SetTime(World, Entity, float)"/>,
        /// etc. apply to the selected clip.
        /// 
        /// <remarks>
        /// Selecting a new animation clip does not change the current playback status. If the previous clip was playing,
        /// the new clip continues playing. If the previous clip was paused, playback remains paused.
        /// </remarks>
        /// <param name="world">The world containing <paramref name="entity"/>.</param>
        /// <param name="entity">
        /// <para>The entity on which to perform the operation.</para>
        /// <para>
        /// Note that <paramref name="entity"/> must have both a <see cref="TinyAnimationPlayer"/> component and
        /// a <see cref="TinyAnimationClipRef"/> buffer to be considered valid.
        /// </para>
        /// </param>
        /// <param name="clipHash">
        /// <para>The hashed name of the animation clip asset to select for playback.</para>
        /// <para>
        /// You can obtain a hashed name by using <see cref="TinyAnimation.StringToHash"/>.
        /// </para>
        /// </param>
        [PublicAPI]
        public static void SelectClip(World world, Entity entity, uint clipHash)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);
            SelectClip_Internal(world, entity, clipHash);
        }

        [PublicAPI]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("SelectClip(World, Entity, int) has been renamed to SelectClipAtIndex. (RemovedAfter 2020-10-30). (UnityUpgradable) -> SelectClipAtIndex(*)", true)]
        public static void SelectClip(World world, Entity entity, int clipIndex) => SelectClipAtIndex(world, entity, clipIndex);

        /// <summary>
        /// Selects an animation clip for playback using its index into the <see cref="TinyAnimationClipRef"/> buffer
        /// of <paramref name="entity"/>.
        /// </summary>
        /// <remarks>
        /// After you select a clip, subsequent calls to <see cref="TinyAnimation.Play(World, Entity)"/>,
        /// <see cref="TinyAnimation.Pause(World, Entity)"/>, <see cref="TinyAnimation.SetTime(World, Entity, float)"/>,
        /// etc. apply to the selected clip.
        /// 
        /// Selecting a new animation clip does not change the current playback status. If the previous clip was playing,
        /// the new clip continues playing. If the previous clip was paused, playback remains paused.
        /// 
        /// Selecting a clip by index is unpredictable: there are no guarantees for the order in which the
        /// clips are stored. This method is useful if you want to play a random clip; otherwise, we recommend
        /// selecting a clip by its hash using
        /// <see cref="TinyAnimation.SelectClip(Unity.Entities.World,Unity.Entities.Entity,uint)"/>.
        /// </remarks>
        /// <param name="world">The world containing <paramref name="entity"/>.</param>
        /// <param name="entity">
        /// <para>The entity on which to perform the operation.</para>
        /// <para>
        /// Note that <paramref name="entity"/> must have both a <see cref="TinyAnimationPlayer"/> component and
        /// a <see cref="TinyAnimationClipRef"/> buffer to be considered valid.
        /// </para>
        /// </param>
        /// <param name="clipIndex"></param>
        [PublicAPI]
        public static void SelectClipAtIndex(World world, Entity entity, int clipIndex)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // Give exceptions with a *bit* more context when collection checks are on.
            if (clipIndex < 0 || clipIndex >= GetClipsCount_Internal(world, entity))
                throw new IndexOutOfRangeException($"Invalid TinyAnimation clip index: {clipIndex.ToString()}");
#endif

            SelectClip_Internal(world, entity, clipIndex);
        }

        /// <summary>
        /// Selects the next clip in the list of available clips for playback.
        /// </summary>
        /// <remarks>
        /// After you select a clip, subsequent calls to <see cref="TinyAnimation.Play(World, Entity)"/>,
        /// <see cref="TinyAnimation.Pause(World, Entity)"/>, <see cref="TinyAnimation.SetTime(World, Entity, float)"/>,
        /// etc. apply to the selected clip.
        /// 
        /// This operation is cyclical, meaning that once it reaches the end of the list, the *next* clip is the first one
        /// in the list. Therefore, this method is always safe to call and will never go out of bounds.
        ///
        /// There are no guarantees for the order in which the clips are stored. Use this method only if you want to cycle
        /// randomly through a list of clips. If you need precise control over which clip plays when, we recommend using
        /// <see cref="TinyAnimation.SelectClip(Unity.Entities.World,Unity.Entities.Entity,uint)"/>.
        /// </remarks>
        /// <param name="world">The world containing <paramref name="entity"/>.</param>
        /// <param name="entity">
        /// <para>The entity on which to perform the operation.</para>
        /// <para>
        /// Note that <paramref name="entity"/> must have both a <see cref="TinyAnimationPlayer"/> component and
        /// a <see cref="TinyAnimationClipRef"/> buffer to be considered valid.
        /// </para>
        /// </param>
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

        /// <summary>
        /// Selects the previous clip in the list of available clips for playback.
        /// </summary>
        /// <remarks>
        /// After you select a clip, subsequent calls to <see cref="TinyAnimation.Play(World, Entity)"/>,
        /// <see cref="TinyAnimation.Pause(World, Entity)"/>, <see cref="TinyAnimation.SetTime(World, Entity, float)"/>,
        /// etc. apply to the selected clip.
        /// 
        /// This operation is cyclical, meaning that once it reaches the first clip of the list, the *previous* clip is the
        /// last one in the list. Therefore, this method is always safe to call and will never go out of bounds.
        /// 
        /// There are no guarantees for the order in which the clips are stored. Use this method only if you wand to cycle
        /// randomly through a list of clips. If you need precise control over which clip plays when, we recommend using
        /// <see cref="TinyAnimation.SelectClip(Unity.Entities.World,Unity.Entities.Entity,uint)"/>.
        /// </remarks>
        /// <param name="world">The world containing <paramref name="entity"/>.</param>
        /// <param name="entity">
        /// <para>The entity on which to perform the operation.</para>
        /// <para>
        /// Note that <paramref name="entity"/> must have both a <see cref="TinyAnimationPlayer"/> component and
        /// a <see cref="TinyAnimationClipRef"/> buffer to be considered valid.
        /// </para>
        /// </param>
        [PublicAPI]
        public static void SelectPreviousClip(World world, Entity entity)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);

            var index = GetCurrentClipIndex_Internal(world, entity) - 1;

            if (index < 0)
                index = GetClipsCount_Internal(world, entity) - 1;

            SelectClip_Internal(world, entity, index);
        }

        /// <summary>
        /// Starts or resumes playback of the currently selected animation clip.
        /// </summary>
        /// <remarks>
        /// This operation takes effect at the beginning of the next frame.
        /// </remarks>
        /// <param name="world">The world containing <paramref name="entity"/>.</param>
        /// <param name="entity">
        /// <para>The entity on which to perform the operation.</para>
        /// <para>
        /// Note that <paramref name="entity"/> must have both a <see cref="TinyAnimationPlayer"/> component and
        /// a <see cref="TinyAnimationClipRef"/> buffer to be considered valid.
        /// </para>
        /// </param>
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

        /// <summary>
        /// Pauses playback of the currently selected animation clip.
        /// </summary>
        /// <remarks>
        /// This operation takes effect at the beginning of the next frame.
        /// </remarks>
        /// <param name="world">The world containing <paramref name="entity"/>.</param>
        /// <param name="entity">
        /// <para>The entity on which to perform the operation.</para>
        /// <para>
        /// Note that <paramref name="entity"/> must have both a <see cref="TinyAnimationPlayer"/> component and
        /// a <see cref="TinyAnimationClipRef"/> buffer to be considered valid.
        /// </para>
        /// </param>
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

        /// <summary>
        /// Stops playback of the currently selected animation clip. A stopped clip has it's time set to 0.
        /// </summary>
        /// <remarks>
        /// This operation takes effect at the beginning of the next frame.
        /// </remarks>
        /// <param name="world">The world containing <paramref name="entity"/>.</param>
        /// <param name="entity">
        /// <para>The entity on which to perform the operation.</para>
        /// <para>
        /// Note that <paramref name="entity"/> must have both a <see cref="TinyAnimationPlayer"/> component and
        /// a <see cref="TinyAnimationClipRef"/> buffer to be considered valid.
        /// </para>
        /// </param>
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

        /// <summary>
        /// Reports whether the currently selected animation clip is playing.
        /// </summary>
        /// <remarks>
        /// An animation clip is playing when its <see cref="Entity"/> has both the <see cref="UpdateAnimationTimeTag"/>
        /// and the <see cref="ApplyAnimationResultTag"/> tags.
        /// </remarks>
        /// <param name="world">The world containing <paramref name="entity"/>.</param>
        /// <param name="entity">
        /// <para>The entity on which to perform the operation.</para>
        /// <para>
        /// Note that <paramref name="entity"/> must have both a <see cref="TinyAnimationPlayer"/> component and
        /// a <see cref="TinyAnimationClipRef"/> buffer to be considered valid.
        /// </para>
        /// </param>
        /// <returns>Whether the currently selected animation clip is playing or not.</returns>
        [PublicAPI]
        public static bool IsPlaying(World world, Entity entity)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);

            var currentClip = world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity).CurrentClip;

            return world.EntityManager.HasComponent<UpdateAnimationTimeTag>(currentClip) && // Time is updating
                world.EntityManager.HasComponent<ApplyAnimationResultTag>(currentClip);     // Values are applied
        }

        /// <summary>
        /// Reports whether the currently selected animation clip is paused.
        /// </summary>
        /// <remarks>
        /// An animation clip is paused when its <see cref="Entity"/> has the <see cref="ApplyAnimationResultTag"/> tag but
        /// doesn't have the <see cref="UpdateAnimationTimeTag"/> tag.
        /// </remarks>
        /// <param name="world">The world containing <paramref name="entity"/>.</param>
        /// <param name="entity">
        /// <para>The entity on which to perform the operation.</para>
        /// <para>
        /// Note that <paramref name="entity"/> must have both a <see cref="TinyAnimationPlayer"/> component and
        /// a <see cref="TinyAnimationClipRef"/> buffer to be considered valid.
        /// </para>
        /// </param>
        /// <returns>Whether the currently selected animation clip is paused or not.</returns>
        [PublicAPI]
        public static bool IsPaused(World world, Entity entity)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);

            var currentClip = world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity).CurrentClip;

            return !world.EntityManager.HasComponent<UpdateAnimationTimeTag>(currentClip) && // Time is not updated
                world.EntityManager.HasComponent<ApplyAnimationResultTag>(currentClip);      // Values are still applied
        }

        /// <summary>
        /// Reports whether the currently selected animation clip is stopped.
        /// </summary>
        /// <remarks>
        /// An animation clip is stopped when its <see cref="Entity"/> has neither the <see cref="ApplyAnimationResultTag"/>
        /// or the <see cref="UpdateAnimationTimeTag"/> tag.
        /// </remarks>
        /// <param name="world">The world containing <paramref name="entity"/>.</param>
        /// <param name="entity">
        /// <para>The entity on which to perform the operation.</para>
        /// <para>
        /// Note that <paramref name="entity"/> must have both a <see cref="TinyAnimationPlayer"/> component and
        /// a <see cref="TinyAnimationClipRef"/> buffer to be considered valid.
        /// </para>
        /// </param>
        /// <returns>Whether the currently selected animation clip is stopped or not.</returns>
        [PublicAPI]
        public static bool IsStopped(World world, Entity entity)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);

            var currentClip = world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity).CurrentClip;

            return !world.EntityManager.HasComponent<UpdateAnimationTimeTag>(currentClip) && // Time is not updated
                !world.EntityManager.HasComponent<ApplyAnimationResultTag>(currentClip);     // Values are not applied
        }

        /// <summary>
        /// Gets the duration of the currently selected animation clip.
        /// </summary>
        /// <param name="world">The world containing <paramref name="entity"/>.</param>
        /// <param name="entity">
        /// <para>The entity on which to perform the operation.</para>
        /// <para>
        /// Note that <paramref name="entity"/> must have both a <see cref="TinyAnimationPlayer"/> component and
        /// a <see cref="TinyAnimationClipRef"/> buffer to be considered valid.
        /// </para>
        /// </param>
        /// <returns>The duration in seconds, of the currently selected clip.</returns>
        /// <seealso cref="TinyAnimationPlaybackInfo.Duration"/>
        [PublicAPI]
        public static float GetDuration(World world, Entity entity)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);

            var currentClip = world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity).CurrentClip;

            return world.EntityManager.GetComponentData<TinyAnimationPlaybackInfo>(currentClip).Duration;
        }

        /// <summary>
        /// Gets the duration of the specified animation clip.
        /// </summary>
        /// <param name="world">The world containing <paramref name="entity"/>.</param>
        /// <param name="entity">
        /// <para>The entity on which to perform the operation.</para>
        /// <para>
        /// Note that <paramref name="entity"/> must have both a <see cref="TinyAnimationPlayer"/> component and
        /// a <see cref="TinyAnimationClipRef"/> buffer to be considered valid.
        /// </para>
        /// </param>
        /// <param name="clipHash">
        /// <para>The hashed name of the animation clip asset whose duration you want to know.</para>
        /// <para>
        /// You can obtain a hashed name by using <see cref="TinyAnimation.StringToHash"/>.
        /// </para>
        /// </param>
        /// <returns>The duration in seconds, of the specified clip.</returns>
        /// <seealso cref="TinyAnimationPlaybackInfo.Duration"/>
        [PublicAPI]
        public static float GetDuration(World world, Entity entity, uint clipHash)
        {
            var index = GetClipIndex(world, entity, clipHash);
            if (index == -1)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new ArgumentException($"A tiny animation clip with hash: {clipHash} could not be found for the selected entity ({entity}).");
#else
                return 0.0f;
#endif
            }

            return GetDurationAtIndex(world, entity, index);
        }

        [PublicAPI]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("GetDuration(World, Entity, int) has been renamed to GetDurationAtIndex. (RemovedAfter 2020-10-30). (UnityUpgradable) -> GetDurationAtIndex(*)", true)]
        public static float GetDuration(World world, Entity entity, int clipIndex) => GetDurationAtIndex(world, entity, clipIndex);

        /// <summary>
        /// Gets the duration of the animation clip stored at the specified index.
        /// </summary>
        /// <remarks>
        /// There are no guarantees for the order in which the clips are stored. If you want to know the duration of a
        /// specific clip, we recommend using its hash with <see cref="TinyAnimation.GetDuration(Unity.Entities.World,Unity.Entities.Entity,uint)"/>.
        /// </remarks>
        /// <param name="world">The world containing <paramref name="entity"/>.</param>
        /// <param name="entity">
        /// <para>The entity on which to perform the operation.</para>
        /// <para>
        /// Note that <paramref name="entity"/> must have both a <see cref="TinyAnimationPlayer"/> component and
        /// a <see cref="TinyAnimationClipRef"/> buffer to be considered valid.
        /// </para>
        /// </param>
        /// <param name="clipIndex">The index of the clip whose duration you want to know.</param>
        /// <returns>The duration in seconds, of the clip at the specified index.</returns>
        /// <seealso cref="TinyAnimationPlaybackInfo.Duration"/>
        [PublicAPI]
        public static float GetDurationAtIndex(World world, Entity entity, int clipIndex)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (clipIndex < 0 || clipIndex >= GetClipsCount_Internal(world, entity))
                throw new IndexOutOfRangeException($"Invalid TinyAnimation clip index: {clipIndex.ToString()}");
#endif

            var clipsBuffer = world.EntityManager.GetBuffer<TinyAnimationClipRef>(entity);
            var clip = clipsBuffer[clipIndex].Value;

            return world.EntityManager.GetComponentData<TinyAnimationPlaybackInfo>(clip).Duration;
        }

        /// <summary>
        /// Gets the duration of all the animation clips associated with the specified Entity.
        /// </summary>
        /// <remarks>
        /// The order of the durations in the returned array matches the order in which the clips are stored.
        /// </remarks>
        /// <param name="world">The world containing <paramref name="entity"/>.</param>
        /// <param name="entity">
        /// <para>The entity on which to perform the operation.</para>
        /// <para>
        /// Note that <paramref name="entity"/> must have both a <see cref="TinyAnimationPlayer"/> component and
        /// a <see cref="TinyAnimationClipRef"/> buffer to be considered valid.
        /// </para>
        /// </param>
        /// <param name="allocator">The allocator to use for the creation of the <see cref="NativeArray{T}"/>. You can use
        /// <see cref="Allocator.Temp"/> if you are going to use the results immediately (in the same scope that you call this
        /// function). Otherwise, use <see cref="Allocator.TempJob"/> or <see cref="Allocator.Persistent"/> and dispose
        /// of the returned array when done.</param>
        /// <returns>
        /// A native array of floats containing the duration of every clip associated with <paramref name="entity"/>.
        /// </returns>
        /// <seealso cref="TinyAnimationPlaybackInfo.Duration"/>
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

        /// <summary>
        /// Gets the time in seconds at which the currently selected animation clip will be or was evaluated during this frame.
        /// </summary>
        /// <remarks>The clip time is a value, in seconds, between 0 and the duration of the clip. </remarks>
        /// <param name="world">The world containing <paramref name="entity"/>.</param>
        /// <param name="entity">
        /// <para>The entity on which to perform the operation.</para>
        /// <para>
        /// Note that <paramref name="entity"/> must have both a <see cref="TinyAnimationPlayer"/> component and
        /// a <see cref="TinyAnimationClipRef"/> buffer to be considered valid.
        /// </para>
        /// </param>
        /// <returns>The time in seconds at which the currently selected clip is being evaluated.</returns>
        [PublicAPI]
        public static float GetTime(World world, Entity entity)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);

            var currentClip = world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity).CurrentClip;

            return world.EntityManager.GetComponentData<TinyAnimationTime>(currentClip).Value;
        }

        /// <summary>
        /// Specifies the time in seconds at which the currently selected animation should be evaluated.
        /// </summary>
        /// <remarks>
        /// The <paramref name="newTime"/> value is clamped between 0 and the duration of the clip, in
        /// accordance with the rules of the <see cref="WrapMode"/> associated with the clip.
        /// 
        /// If the animation clip is playing, it is very likely that the delta time for the frame will be added to
        /// the time specified.
        /// </remarks>
        /// <param name="world">The world containing <paramref name="entity"/>.</param>
        /// <param name="entity">
        /// <para>The entity on which to perform the operation.</para>
        /// <para>
        /// Note that <paramref name="entity"/> must have both a <see cref="TinyAnimationPlayer"/> component and
        /// a <see cref="TinyAnimationClipRef"/> buffer to be considered valid.
        /// </para>
        /// </param>
        /// <param name="newTime">The time in seconds at which you want to evaluate the currently selected clip.</param>
        /// <seealso cref="TinyAnimationPlaybackInfo.Duration"/>
        /// <seealso cref="TinyAnimationPlaybackInfo.WrapMode"/>
        [PublicAPI]
        public static void SetTime(World world, Entity entity, float newTime)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);

            var currentClip = world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity).CurrentClip;
            var info = world.EntityManager.GetComponentData<TinyAnimationPlaybackInfo>(currentClip);

            if (newTime < 0.0f)
            {
                newTime = 0.0f;
            }
            else if (newTime > info.Duration)
            {
                switch (info.WrapMode)
                {
                    case WrapMode.Once:
                    {
                        newTime = 0.0f;
                        break;
                    }
                    case WrapMode.ClampForever:
                    {
                        newTime = info.Duration;
                        break;
                    }
                    case WrapMode.Loop:
                    {
                        newTime = AnimationMath.Repeat(newTime, info.Duration);
                        break;
                    }
                    case WrapMode.PingPong:
                    {
                        newTime = AnimationMath.PingPong(newTime, info.Duration);
                        break;
                    }
                }
            }

            world.EntityManager.SetComponentData(currentClip, new TinyAnimationTime { Value = newTime, InternalWorkTime = newTime});
        }

        /// <summary>
        /// Gets the <see cref="WrapMode"/> of the currently selected animation clip.
        /// </summary>
        /// <param name="world">The world containing <paramref name="entity"/>.</param>
        /// <param name="entity">
        /// <para>The entity on which to perform the operation.</para>
        /// <para>
        /// Note that <paramref name="entity"/> must have both a <see cref="TinyAnimationPlayer"/> component and
        /// a <see cref="TinyAnimationClipRef"/> buffer to be considered valid.
        /// </para>
        /// </param>
        /// <returns>The <see cref="WrapMode"/> of the currently selected clip.</returns>
        /// <seealso cref="TinyAnimationPlaybackInfo.WrapMode"/>
        [PublicAPI]
        public static WrapMode GetWrapMode(World world, Entity entity)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);

            var currentClip = world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity).CurrentClip;

            return world.EntityManager.GetComponentData<TinyAnimationPlaybackInfo>(currentClip).WrapMode;
        }

        /// <summary>
        /// Sets the <see cref="WrapMode"/> for the currently selected animation clip.
        /// </summary>
        /// <param name="world">The world containing <paramref name="entity"/>.</param>
        /// <param name="entity">
        /// <para>The entity on which to perform the operation.</para>
        /// <para>
        /// Note that <paramref name="entity"/> must have both a <see cref="TinyAnimationPlayer"/> component and
        /// a <see cref="TinyAnimationClipRef"/> buffer to be considered valid.
        /// </para>
        /// </param>
        /// <param name="wrapMode">The desired <see cref="WrapMode"/></param>
        /// <seealso cref="TinyAnimationPlaybackInfo.WrapMode"/>
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

        /// <summary>
        /// Gets the cycle offset of the currently selected animation clip.
        /// </summary>
        /// <remarks>
        /// The cycle offset is a normalized value between 0.0 and 1.0 representing a percentage of the
        /// duration of the clip.
        /// </remarks>
        /// <param name="world">The world containing <paramref name="entity"/>.</param>
        /// <param name="entity">
        /// <para>The entity on which to perform the operation.</para>
        /// <para>
        /// Note that <paramref name="entity"/> must have both a <see cref="TinyAnimationPlayer"/> component and
        /// a <see cref="TinyAnimationClipRef"/> buffer to be considered valid.
        /// </para>
        /// </param>
        /// <returns>The normalized cycle offset for the currently selected clip.</returns>
        /// <seealso cref="TinyAnimationPlaybackInfo.Duration"/>
        /// <seealso cref="TinyAnimationPlaybackInfo.CycleOffset"/>
        [PublicAPI]
        public static float GetCycleOffset(World world, Entity entity)
        {
            AssertEntityIsValidAnimationPlayer(world, entity);

            var currentClip = world.EntityManager.GetComponentData<TinyAnimationPlayer>(entity).CurrentClip;

            return world.EntityManager.GetComponentData<TinyAnimationPlaybackInfo>(currentClip).CycleOffset;
        }

        /// <summary>
        /// Sets the cycle offset for the currently selected animation clip.
        /// </summary>
        /// <remarks>
        /// The cycle offset is a normalized value between 0.0 and 1.0 representing a percentage of the
        /// duration of the clip.
        /// </remarks>
        /// <remarks>
        /// The cycle offset is only applied for clips with a <see cref="WrapMode"/> set to
        /// <see cref="WrapMode.Loop"/> or <see cref="WrapMode.PingPong"/>
        /// </remarks>
        /// <param name="world">The world containing <paramref name="entity"/>.</param>
        /// <param name="entity">
        /// <para>The entity on which to perform the operation.</para>
        /// <para>
        /// Note that <paramref name="entity"/> must have both a <see cref="TinyAnimationPlayer"/> component and
        /// a <see cref="TinyAnimationClipRef"/> buffer to be considered valid.
        /// </para>
        /// </param>
        /// <param name="cycleOffset">The desired cycle offset.</param>
        /// <seealso cref="TinyAnimationPlaybackInfo.Duration"/>
        /// <seealso cref="TinyAnimationPlaybackInfo.WrapMode"/>
        /// <seealso cref="TinyAnimationPlaybackInfo.CycleOffset"/>
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
