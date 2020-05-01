using TinyInternal.Bridge;
using UnityEngine;

namespace Unity.Tiny.Animation.Editor
{
    static class ConversionUtils
    {
        public static WrapMode GetWrapMode(AnimationClip clip)
        {
            if (!clip.legacy)
            {
                var clipSettings = TinyAnimationConversionState.GetAnimationClipSettings(clip);
                return clipSettings.loopTime ? WrapMode.Loop : WrapMode.ClampForever;
            }

            switch (clip.wrapMode)
            {
                case UnityEngine.WrapMode.Once: // Shares value with UnityEngine.WrapMode.Clamp
                {
                    return WrapMode.Once;
                }
                case UnityEngine.WrapMode.Loop:
                {
                    return WrapMode.Loop;
                }
                case UnityEngine.WrapMode.PingPong:
                {
                    return WrapMode.PingPong;
                }
                case UnityEngine.WrapMode.ClampForever:
                case UnityEngine.WrapMode.Default:
                default:
                {
                    return WrapMode.ClampForever;
                }
            }
        }

        public static float GetCycleOffset(AnimationClip clip)
        {
            return clip.legacy ? 0.0f : TinyAnimationConversionState.GetAnimationClipSettings(clip).cycleOffset;
        }

        public static bool ValidateGameObjectAndWarn(GameObject gameObject)
        {
            var hasAnimationComponent = gameObject.GetComponent<UnityEngine.Animation>() != null;
            var hasTinyAnimationAuthoring = gameObject.GetComponent<TinyAnimationAuthoring>() != null;

            if (!hasAnimationComponent && !hasTinyAnimationAuthoring)
            {
                Debug.LogWarning($"The GameObject {gameObject.name} has no support for TinyAnimation.");
                return false;
            }

            if (hasAnimationComponent && hasTinyAnimationAuthoring)
            {
                Debug.LogWarning($"The GameObject {gameObject.name} has both an Animation component and an Animator component, which is not supported by TinyAnimation.");
                return false;
            }

            if (!hasTinyAnimationAuthoring && gameObject.GetComponent<Animator>() != null)
            {
                Debug.LogWarning($"The GameObject {gameObject.name} has an animator but no {typeof(TinyAnimationAuthoring).Name} component was added.");
                return false;
            }

            return true;
        }

        public static void WarnAboutUnsupportedFeatures(UnityEngine.Animation animationComponent)
        {
            if (animationComponent.cullingType != AnimationCullingType.AlwaysAnimate)
                Debug.LogWarning($"The Animation component on {animationComponent.gameObject.name} has a culling type of {animationComponent.cullingType}, but it is not supported by TinyAnimation.");

            if (animationComponent.animatePhysics)
                Debug.LogWarning($"The Animation component on {animationComponent.gameObject.name} has \"animatePhysics\" set to true, but it is not supported by TinyAnimation.");
        }

        public static void WarnAboutUnsupportedFeatures(Animator animatorComponent)
        {
            if (animatorComponent == null)
                return;

            if (animatorComponent.applyRootMotion)
                Debug.LogWarning($"The Animator component on {animatorComponent.gameObject.name} requires Root Motion, but it is not supported by TinyAnimation.");

            if (animatorComponent.updateMode != AnimatorUpdateMode.Normal)
                Debug.LogWarning($"The Animator component on {animatorComponent.gameObject.name} has the update mode: {animatorComponent.updateMode}, but only {AnimatorUpdateMode.Normal} is supported by TinyAnimation.");

            if (animatorComponent.cullingMode != AnimatorCullingMode.AlwaysAnimate)
                Debug.LogWarning($"The Animator component on {animatorComponent.gameObject.name} has the culling mode: {animatorComponent.cullingMode}, but only {AnimatorCullingMode.AlwaysAnimate} is supported by TinyAnimation.");
        }

        public static void WarnAboutUnsupportedFeatures(AnimationClip clip)
        {
            if (clip.events.Length > 0)
                Debug.LogWarning($"The animation clip {clip.name} contains some animation events, but they are not supported by TinyAnimation.");

            if (clip.legacy) return;

            var clipSettings = TinyAnimationConversionState.GetAnimationClipSettings(clip);

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
