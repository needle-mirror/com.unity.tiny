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
                Convert(animationComponent);
            });
        }

        void Convert(UnityEngine.Animation animationComponent)
        {
            var animationClips = ConversionUtils.GetAllAnimationClips(animationComponent);
            if (animationClips.Length == 0)
                return;

            var gameObjectEntity = TryGetPrimaryEntity(animationComponent.gameObject);
            if (gameObjectEntity == Entity.Null)
                throw new Exception($"Could not get a conversion entity for {animationComponent.GetType().Name} on {animationComponent.gameObject}.");

            DstEntityManager.AddBuffer<TinyAnimationClipRef>(gameObjectEntity);

            var anythingToAnimate = false;

            foreach (var clip in animationClips)
            {
                if (clip != null)
                    anythingToAnimate |= Convert(animationComponent, clip, gameObjectEntity);
            }

            if (anythingToAnimate)
            {
                var clipReferences = DstEntityManager.GetBuffer<TinyAnimationClipRef>(gameObjectEntity);
                var currentClipEntity = clipReferences[0].Value;
                DstEntityManager.AddComponentData(gameObjectEntity, new TinyAnimationPlayer { CurrentClip = currentClipEntity, CurrentIndex = 0 });

                if (animationComponent.playAutomatically)
                {
                    DstEntityManager.AddComponent<UpdateAnimationTimeTag>(currentClipEntity);
                    DstEntityManager.AddComponent<ApplyAnimationResultTag>(currentClipEntity);
                }
            }
            else
            {
                // Not needed
                DstEntityManager.RemoveComponent<TinyAnimationClipRef>(gameObjectEntity);
            }
        }

        bool Convert(UnityEngine.Animation animationComponent, AnimationClip clip, Entity gameObjectEntity)
        {
            if (clip == null)
                return false;

            var clipInfoEntity = TryGetPrimaryEntity(clip);
            if (clipInfoEntity == Entity.Null || !DstEntityManager.HasComponent<BakedAnimationClip>(clipInfoEntity))
                throw new Exception($"Something went wrong while retrieving the Entity for animation clip: {clip.name}");

            var bakedAnimationClip = DstEntityManager.GetComponentData<BakedAnimationClip>(clipInfoEntity);
            var floatCurvesInfo = bakedAnimationClip.FloatCurvesInfo;
            var pPtrCurvesInfo = bakedAnimationClip.PPtrCurvesInfo;

            var hasFloatCurves = floatCurvesInfo != BlobAssetReference<CurvesInfo>.Null;
            var hasPPtrCurves = pPtrCurvesInfo != BlobAssetReference<CurvesInfo>.Null;

            if (!hasFloatCurves && !hasPPtrCurves)
                return false; // Nothing to animate

            var rootGameObject = animationComponent.gameObject;
            var clipEntity = CreateAdditionalEntity(rootGameObject);
            DstEntityManager.SetName(clipEntity, $"{clip.name} ({rootGameObject.name})");

            // With pruning, it's possible that nothing remains to be animated
            var anythingToAnimate = false;

            if (hasFloatCurves)
                anythingToAnimate |= ConvertFloatCurves(rootGameObject, clipEntity, floatCurvesInfo);

            if (hasPPtrCurves)
                anythingToAnimate |= ConvertPPtrCurves(rootGameObject, clipEntity, pPtrCurvesInfo);

            if (anythingToAnimate)
            {
                var (wrapMode, cycleOffset) = ConversionUtils.GetWrapInfo(clip);

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
                clipReferences.Add(new TinyAnimationClipRef { Value = clipEntity} );
            }

            return anythingToAnimate;
        }

        bool ConvertFloatCurves(GameObject rootGameObject, Entity entity, BlobAssetReference<CurvesInfo> floatCurvesInfo)
        {
            ref var curvesInfo = ref floatCurvesInfo.Value;

            if (curvesInfo.BindingNames.Length != curvesInfo.GetCurvesCount())
                throw new Exception($"{nameof(CurvesInfo.BindingNames)} and {nameof(CurvesInfo.CurveOffsets)} must be of the same length.");

            var length = curvesInfo.BindingNames.Length;
            var animationBindings = new List<AnimationBinding>(length);
            var animationBindingsNames = new List<AnimationBindingName>(length);

            var rootTransform = rootGameObject.transform;
            var shouldPatchScale = (curvesInfo.ConversionActions & RequiredConversionActions.PatchScale) > 0 &&
                                   rootGameObject.GetComponent<TinyAnimationScalePatcher>()?.disableScalePatching == false;

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

                var curve = curvesInfo.GetCurve(i, Allocator.Temp);
                animationBindings.Add(new AnimationBinding
                {
                    Curve = curve.ToBlobAssetRef(),
                    TargetEntity = targetEntity
                });

                curve.Dispose();
                animationBindingsNames.Add(new AnimationBindingName
                {
                    Value = curvesInfo.BindingNames[i]
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

        bool ConvertPPtrCurves(GameObject rootGameObject, Entity entity, BlobAssetReference<CurvesInfo> pPtrCurvesInfo)
        {
            ref var curvesInfo = ref pPtrCurvesInfo.Value;

            var length = curvesInfo.GetCurvesCount();
            var pPtrBindings = new List<AnimationPPtrBinding>(length);

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

                if (!DstEntityManager.HasComponent<PPtrIndex>(targetEntity))
                {
                    DstEntityManager.AddComponent<PPtrIndex>(targetEntity);
                    DstEntityManager.AddComponentData(targetEntity, new AnimatedAssetGroupingRef { Value = curvesInfo.AnimatedAssetGroupings[i]});
                }
                else
                {
                    Debug.LogWarning($"Multiple PPtr bindings target {target.gameObject.name} ({targetEntity.ToString()} " +
                                     "which is not currently supported. Only one binding will be applied.)");
                    continue;
                }

                var curve = curvesInfo.GetCurve(i, Allocator.Temp);
                pPtrBindings.Add(new AnimationPPtrBinding
                {
                    Curve = curve.ToBlobAssetRef(),
                    TargetEntity = targetEntity
                });

                curve.Dispose();
            }

            // Length may have changed due to discarded bindings
            length = pPtrBindings.Count;

            if (length == 0)
                return false;

            var pPtrBindingsBuffer = DstEntityManager.AddBuffer<AnimationPPtrBinding>(entity);
            pPtrBindingsBuffer.CopyFrom(pPtrBindings.ToArray());

            return true;
        }
    }
}
