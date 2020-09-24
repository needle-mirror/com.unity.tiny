using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Unity.Tiny.Animation.Editor
{
    [UpdateInGroup(typeof(GameObjectAfterConversionGroup))]
    [UpdateAfter(typeof(BeforeTinyAnimationResolution))]
    [UpdateBefore(typeof(TinyAnimationBindingResolution))]
    class TinyAnimationBindingsConversion : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((UnityEngine.Animation animationComponent) =>
            {
                if (!TinyAnimationConversionState.ValidateGameObjectAndWarn(animationComponent.gameObject))
                    return;

                Convert(animationComponent);
            });

            Entities.ForEach((TinyAnimationAuthoring animationComponent) =>
            {
                if (!TinyAnimationConversionState.ValidateGameObjectAndWarn(animationComponent.gameObject))
                    return;

                Convert(animationComponent);
            });
        }

        void Convert(Component animationComponent)
        {
            var animationClips = TinyAnimationConversionState.GetAllAnimationClips(animationComponent);
            if (animationClips.Length == 0)
                return;

            var rootGameObject = animationComponent.gameObject;
            var gameObjectEntity = TryGetPrimaryEntity(rootGameObject);
            if (gameObjectEntity == Entity.Null)
                throw new Exception($"Could not get a conversion entity for {animationComponent.GetType().Name} on {rootGameObject}.");

            DstEntityManager.AddBuffer<TinyAnimationClipRef>(gameObjectEntity);

            foreach (var clip in animationClips)
            {
                Convert(rootGameObject, clip, gameObjectEntity);
            }

            var clipReferences = DstEntityManager.GetBuffer<TinyAnimationClipRef>(gameObjectEntity);
            var currentClipEntity = clipReferences[0].Value;
            DstEntityManager.AddComponentData(gameObjectEntity, new TinyAnimationPlayer { CurrentClip = currentClipEntity, CurrentIndex = 0 });

            if (PlaysAutomatically(animationComponent))
            {
                DstEntityManager.AddComponent<UpdateAnimationTimeTag>(currentClipEntity);
                DstEntityManager.AddComponent<ApplyAnimationResultTag>(currentClipEntity);
            }
        }

        void Convert(GameObject rootGameObject, AnimationClip clip, Entity gameObjectEntity)
        {
            if (clip == null)
                return;

            var clipName = clip.name;

            var clipInfoEntity = TryGetPrimaryEntity(clip);
            if (clipInfoEntity == Entity.Null || !DstEntityManager.HasComponent<BakedAnimationClip>(clipInfoEntity))
                throw new Exception($"Something went wrong while retrieving the Entity for animation clip: {clipName}");

            var bakedAnimationClip = DstEntityManager.GetComponentData<BakedAnimationClip>(clipInfoEntity);
            var floatCurvesInfo = bakedAnimationClip.FloatCurvesInfo;
            var pPtrCurvesInfo = bakedAnimationClip.PPtrCurvesInfo;

            var hasFloatCurves = floatCurvesInfo != BlobAssetReference<CurvesInfo>.Null;
            var hasPPtrCurves = pPtrCurvesInfo != BlobAssetReference<CurvesInfo>.Null;

            var clipEntity = CreateAdditionalEntity(rootGameObject);
            DstEntityManager.SetName(clipEntity, $"{clipName} ({rootGameObject.name})");

            // With pruning, it's possible that nothing remains to be animated
            var anythingToAnimate = false;

            if (hasFloatCurves)
                anythingToAnimate |= ConvertFloatCurves(rootGameObject, clipEntity, floatCurvesInfo, clipName);

            if (hasPPtrCurves)
                anythingToAnimate |= ConvertPPtrCurves(rootGameObject, clipEntity, pPtrCurvesInfo, clipName);

            if ((hasFloatCurves || hasPPtrCurves) && !anythingToAnimate)
                Debug.LogWarning($"Due to the pruning of invalid bindings, the animation clip {clipName} has become empty.");

            var wrapMode = ConversionUtils.GetWrapMode(clip);
            var cycleOffset = ConversionUtils.GetCycleOffset(clip);

            DstEntityManager.AddComponentData(
                clipEntity, new TinyAnimationPlaybackInfo
                {
                    Duration = clip.length,
                    CycleOffset = cycleOffset,
                    WrapMode = wrapMode
                });

            var initialTime = (wrapMode == WrapMode.Loop || wrapMode == WrapMode.PingPong) ? cycleOffset * clip.length : 0.0f;

            DstEntityManager.AddComponentData(
                clipEntity, new TinyAnimationTime
                {
                    InternalWorkTime = initialTime,
                    Value = initialTime
                });

            var clipReferences = DstEntityManager.GetBuffer<TinyAnimationClipRef>(gameObjectEntity);
            clipReferences.Add(new TinyAnimationClipRef { Value = clipEntity, Hash = bakedAnimationClip.ClipHash });
        }

        bool ConvertFloatCurves(GameObject rootGameObject, Entity entity, BlobAssetReference<CurvesInfo> floatCurvesInfo, string clipName)
        {
            ref var curvesInfo = ref floatCurvesInfo.Value;

            if (curvesInfo.BindingNames.Length != curvesInfo.GetCurvesCount())
                throw new Exception($"{nameof(CurvesInfo.BindingNames)} and {nameof(CurvesInfo.CurveOffsets)} must be of the same length.");

            var length = curvesInfo.BindingNames.Length;
            var animationBindings = new List<AnimationBinding>(length);
            var animationBindingsNames = new List<AnimationBindingName>(length);

            var rootTransform = rootGameObject.transform;
            var shouldPatchScale = (curvesInfo.ConversionActions & RequiredConversionActions.PatchScale) > 0 && GameObjectAllowsScalePatching(rootGameObject);

            for (var i = 0; i < length; i++)
            {
                var target = rootTransform.Find(curvesInfo.TargetGameObjectPaths[i].ToString());

                if (target == null || target.gameObject == null)
                    continue;

                var targetEntity = TryGetPrimaryEntity(target.gameObject);

                if (targetEntity == Entity.Null)
                {
                    Debug.LogWarning($"Could not find a corresponding entity for Game Object: {target.gameObject}");
                    continue;
                }

                using (var curve = curvesInfo.GetCurve(i, Allocator.Temp))
                {
                    animationBindings.Add(
                        new AnimationBinding
                        {
                            Curve = curve.ToBlobAssetRef(),
                            TargetEntity = targetEntity
                        });
                }

                animationBindingsNames.Add(new AnimationBindingName
                {
                    Value = curvesInfo.BindingNames[i],

                    // Debug Info
                    ClipName = clipName,
                    SourceGameObjectName = rootGameObject.name,
                    TargetGameObjectName = target.gameObject.name
                });

                // TODO: This could be a bit less greedy
                if (shouldPatchScale && !DstEntityManager.HasComponent<NonUniformScale>(targetEntity))
                {
                    DstEntityManager.AddComponentData(
                        targetEntity, new NonUniformScale
                        {
                            Value = target.localScale
                        });
                }
            }

            // Length may have changed due to discarded bindings
            length = animationBindings.Count;

            if (length == 0)
                return false;

            var animationBindingsBuffer = DstEntityManager.AddBuffer<AnimationBinding>(entity);
            animationBindingsBuffer.CopyFrom(animationBindings.ToArray());

            var animationBindingsNamesBuffer = DstEntityManager.AddBuffer<AnimationBindingName>(entity);
            animationBindingsNamesBuffer.CopyFrom(animationBindingsNames.ToArray());

            var animationBindingRetargetBuffer = DstEntityManager.AddBuffer<AnimationBindingRetarget>(entity);
            animationBindingRetargetBuffer.ResizeUninitialized(length);

            return true;
        }

        bool ConvertPPtrCurves(GameObject rootGameObject, Entity entity, BlobAssetReference<CurvesInfo> pPtrCurvesInfo, string clipName)
        {
            ref var curvesInfo = ref pPtrCurvesInfo.Value;

            var length = curvesInfo.GetCurvesCount();
            var pPtrBindings = new List<AnimationPPtrBinding>(length);
            var pPtrBindingNames = new List<AnimationPPtrBindingName>(length);

            var rootTransform = rootGameObject.transform;

            for (var i = 0; i < length; i++)
            {
                var target = rootTransform.Find(curvesInfo.TargetGameObjectPaths[i].ToString());

                if (target == null || target.gameObject == null)
                    continue;

                var targetEntity = TryGetPrimaryEntity(target.gameObject);

                if (targetEntity == Entity.Null)
                {
                    Debug.LogWarning($"Could not find a corresponding entity for Game Object: {target.gameObject}");
                    continue;
                }

                using (var curve = curvesInfo.GetCurve(i, Allocator.Temp))
                {
                    pPtrBindings.Add(new AnimationPPtrBinding
                    {
                        Curve = curve.ToBlobAssetRef(),
                        SourceEntity = curvesInfo.AnimatedAssetGroupings[i],
                        TargetEntity = targetEntity
                    });
                }

                pPtrBindingNames.Add(new AnimationPPtrBindingName
                {
                    Value = curvesInfo.BindingNames[i],

                    // Debug Info
                    ClipName = clipName,
                    SourceGameObjectName = rootGameObject.name,
                    TargetGameObjectName = target.gameObject.name
                });
            }

            // Length may have changed due to discarded bindings
            length = pPtrBindings.Count;

            if (length == 0)
                return false;

            var pPtrBindingsBuffer = DstEntityManager.AddBuffer<AnimationPPtrBinding>(entity);
            pPtrBindingsBuffer.CopyFrom(pPtrBindings.ToArray());

            var pPtrBindingsNamesBuffer = DstEntityManager.AddBuffer<AnimationPPtrBindingName>(entity);
            pPtrBindingsNamesBuffer.CopyFrom(pPtrBindingNames.ToArray());

            var animationPPtrBindingRetargetBuffer = DstEntityManager.AddBuffer<AnimationPPtrBindingRetarget>(entity);
            animationPPtrBindingRetargetBuffer.ResizeUninitialized(length);

            return true;
        }

        static bool PlaysAutomatically(Component animationComponent)
        {
            switch (animationComponent)
            {
                case UnityEngine.Animation animation:
                {
                    return animation.playAutomatically;
                }
                case TinyAnimationAuthoring tinyAnimationAuthoring:
                {
                    return tinyAnimationAuthoring.playAutomatically;
                }
                default:
                {
                    throw new ArgumentException($"Component {animationComponent} is not of a recognized type.");
                }
            }
        }

        static bool GameObjectAllowsScalePatching(GameObject gameObject)
        {
            var tinyAnimationAuthoringComponent = gameObject.GetComponent<TinyAnimationAuthoring>();
            if (tinyAnimationAuthoringComponent != null)
                return tinyAnimationAuthoringComponent.patchMissingScaleIfNeeded;

            var scalePatcherComponent = gameObject.GetComponent<TinyAnimationScalePatcher>();
            if (scalePatcherComponent != null)
                return !scalePatcherComponent.disableScalePatching;

            // We are an Animation component with no TinyAnimationScalePatcher component, so we can't opt out
            return true;
        }
    }
}
