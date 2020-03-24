using JetBrains.Annotations;
using TinyInternal.Bridge;
using UnityEditor;
using UnityEngine;

namespace Unity.Tiny.Animation.Editor
{
    static class ConversionUtils
    {
        public static AnimationClip[] GetAllAnimationClips([NotNull] UnityEngine.Animation animationComponent)
        {
            // TODO: Cache this result in TinyAnimationConversionState, once that PR gets merged on the 2D side.
            var animations = AnimationUtility.GetAnimationClips(animationComponent.gameObject);
            return animations;
        }

        public static (WrapMode wrapMode, float cycleOffset) GetWrapInfo(AnimationClip clip)
        {
            if (!clip.legacy)
            {
                var clipSettings = TinyAnimationEditorBridge.GetAnimationClipSettings(clip);
                if (clipSettings.loopTime)
                    return (WrapMode.Loop, AnimationMath.NormalizeCycle(clipSettings.cycleOffset));
            }

            var result = (wrapMode: WrapMode.ClampForever, cycleOffset: 0.0f);

            switch (clip.wrapMode)
            {
                case UnityEngine.WrapMode.Once: // Shares value with UnityEngine.WrapMode.Clamp
                {
                    result.wrapMode = WrapMode.Once;
                    break;
                }
                case UnityEngine.WrapMode.Loop:
                {
                    result.wrapMode = WrapMode.Loop;
                    break;
                }
                case UnityEngine.WrapMode.PingPong:
                {
                    result.wrapMode = WrapMode.PingPong;
                    break;
                }
                case UnityEngine.WrapMode.ClampForever:
                case UnityEngine.WrapMode.Default:
                default:
                {
                    result.wrapMode = WrapMode.ClampForever;
                    break;
                }
            }

            return result;
        }

        public static void WarnAboutUnsupportedFeatures(UnityEngine.Animation animationComponent)
        {
            if (animationComponent.cullingType != AnimationCullingType.AlwaysAnimate)
                Debug.LogWarning($"The Animation component on {animationComponent.gameObject.name} has a culling type of {animationComponent.cullingType}, but it is not supported by TinyAnimation.");

            if (animationComponent.animatePhysics)
                Debug.LogWarning($"The Animation component on {animationComponent.gameObject.name} has \"animatePhysics\" set to true, but it is not supported by TinyAnimation.");
        }

        public static void WarnAboutUnsupportedFeatures(AnimationClip clip)
        {
            if (clip.events.Length > 0)
                Debug.LogWarning($"The animation clip {clip.name} contains some animation events, but they are not supported by TinyAnimation.");

            if (clip.legacy) return;

            var clipSettings = TinyAnimationEditorBridge.GetAnimationClipSettings(clip);

            if (clipSettings.loopBlend)
                Debug.LogWarning($"The animation clip {clip.name} has enabled Loop Pose, but it is not supported by TinyAnimation.");

            if (clipSettings.hasAdditiveReferencePose)
                Debug.LogWarning($"The animation clip {clip.name} has an additive reference pose, but it is not supported by TinyAnimation.");

            if (TinyAnimationEngineBridge.HasRootMotion(clip))
                Debug.LogWarning($"The animation clip {clip.name} has root motion data, but it is not supported by TinyAnimation.");

            if (clip.isHumanMotion)
                Debug.LogWarning($"{clip.name} is a humanoid animation clip, but it is not supported by TinyAnimation.");

            if (clip.hasMotionCurves)
                Debug.LogWarning($"The animation clip {clip.name} has motion curves, but it is not supported by TinyAnimation.");
        }
    }
}