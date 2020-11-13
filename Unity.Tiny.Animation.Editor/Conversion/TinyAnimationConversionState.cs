using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using TinyInternal.Bridge;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.Tiny.Animation.Editor
{
    /// <summary>
    /// Contains the state accumulated during the conversion of animation clips
    /// by TinyAnimation.
    /// </summary>
    public static class TinyAnimationConversionState
    {
        static readonly HashSet<UnityObject> k_DeclaredPPtrCurveAssets = new HashSet<UnityObject>();
        static readonly Dictionary<Component, AnimationClip[]> k_ClipsCache = new Dictionary<Component, AnimationClip[]>(16);
        static readonly Dictionary<AnimationClip, AnimationClipSettings> k_ClipSettingsCache = new Dictionary<AnimationClip, AnimationClipSettings>(32);
        static readonly Dictionary<GameObject, bool> k_GameObjectValidityMap = new Dictionary<GameObject, bool>(16);

        /// <summary>
        /// Gets a list of all the assets referenced by all the animation clips converted
        /// by TinyAnimation.
        /// </summary>
        /// <remarks>
        /// This list is populated during the discovery phase of conversion, taking place in
        /// <see cref="GameObjectDeclareReferencedObjectsGroup"/>.
        /// </remarks>
        /// <returns>An untyped list of all the assets referenced by converted clips.</returns>
        [PublicAPI]
        public static IEnumerable<UnityObject> GetDeclaredPPtrCurvesAssets()
        {
            return k_DeclaredPPtrCurveAssets;
        }

        /// <summary>
        /// Gets a list of all the assets of a specific type referenced by all the animation clips
        /// converted by TinyAnimation.
        /// </summary>
        /// <remarks>
        /// This list is populated during the discovery phase of conversion, taking place in
        /// <see cref="GameObjectDeclareReferencedObjectsGroup"/>.
        /// </remarks>
        /// <returns>A list of all the assets of the specified type referenced by converted clips.</returns>
        [PublicAPI]
        public static IEnumerable<T> GetDeclaredPPtrCurvesAssets<T>()
        {
            return k_DeclaredPPtrCurveAssets.OfType<T>();
        }

        internal static bool ValidateGameObjectAndWarn([NotNull] GameObject gameObject)
        {
            if (!k_GameObjectValidityMap.ContainsKey(gameObject))
                k_GameObjectValidityMap.Add(gameObject, ConversionUtils.ValidateGameObjectAndWarn(gameObject));

            return k_GameObjectValidityMap[gameObject];
        }

        internal static void RegisterDeclaredAsset(UnityObject asset)
        {
            k_DeclaredPPtrCurveAssets.Add(asset);
        }

        internal static AnimationClip[] GetAllAnimationClips([NotNull] Component animationComponent)
        {
            switch (animationComponent)
            {
                case UnityEngine.Animation animation:
                {
                    return GetAllAnimationClips(animation);
                }
                case TinyAnimationAuthoring tinyAnimationAuthoring:
                {
                    return GetAllAnimationClips(tinyAnimationAuthoring);
                }
                default:
                {
                    throw new ArgumentException($"Component {animationComponent} is not of a recognized type.");
                }
            }
        }

        internal static AnimationClip[] GetAllAnimationClips([NotNull] UnityEngine.Animation animationComponent)
        {
            if (!k_ClipsCache.ContainsKey(animationComponent))
            {
                var clips = FilterAnimationClips(AnimationUtility.GetAnimationClips(animationComponent.gameObject));

                if (animationComponent.clip != null)
                {
                    var defaultClipIndex = Array.IndexOf(clips, animationComponent.clip);

                    // Ensures that the default clip always comes first, so it's used when Play Automatically is selected
                    if (defaultClipIndex > 0)
                    {
                        clips[defaultClipIndex] = clips[0];
                        clips[0] = animationComponent.clip;
                    }
                }

                k_ClipsCache.Add(animationComponent, clips);
            }

            return k_ClipsCache[animationComponent];
        }

        internal static AnimationClip[] GetAllAnimationClips([NotNull] TinyAnimationAuthoring animationComponent)
        {
            if (!k_ClipsCache.ContainsKey(animationComponent))
            {
                animationComponent.UpdateAdditionalAnimatorClips();

                var clips = new List<AnimationClip>(animationComponent.animationClips.Count + animationComponent.additionalAnimatorClips.Count);
                animationComponent.GetAnimationClips(clips);
                clips.AddRange(animationComponent.additionalAnimatorClips);
                k_ClipsCache.Add(animationComponent, FilterAnimationClips(clips));
            }

            return k_ClipsCache[animationComponent];
        }

        internal static AnimationClipSettings GetAnimationClipSettings([NotNull] AnimationClip clip)
        {
            if (!k_ClipSettingsCache.ContainsKey(clip))
                k_ClipSettingsCache.Add(clip, clip.legacy ? null : TinyAnimationEditorBridge.GetAnimationClipSettings(clip));

            return k_ClipSettingsCache[clip];
        }

        internal static void Clear()
        {
            k_DeclaredPPtrCurveAssets.Clear();
            k_ClipsCache.Clear();
            k_ClipSettingsCache.Clear();
            k_GameObjectValidityMap.Clear();
        }

        static AnimationClip[] FilterAnimationClips(IEnumerable<AnimationClip> clips)
        {
            if (clips == null)
                return new AnimationClip[0];

            return clips.Where(clip => clip != null).ToArray();
        }
    }
}
