using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Unity.Tiny.Animation.Editor
{
    [UpdateInGroup(typeof(GameObjectAfterConversionGroup))]
    [UpdateAfter(typeof(BeforeTinyAnimationResolution))]
    [UpdateBefore(typeof(TinyAnimationBindingResolution))]
    class TinyAnimationBindingsConversion : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((TinyAnimationAuthoring tinyAnimationAuthoring) =>
            {
                Convert(tinyAnimationAuthoring);
            });
        }

        void Convert(TinyAnimationAuthoring tinyAnimationAuthoring)
        {
            var animationClips = tinyAnimationAuthoring.animationClips;
            if (animationClips == null || animationClips.Count == 0)
                return;

            var gameObjectEntity = TryGetPrimaryEntity(tinyAnimationAuthoring.gameObject);
            if (gameObjectEntity == Entity.Null)
                throw new Exception($"Could not get a conversion entity for {tinyAnimationAuthoring.GetType().Name} on {tinyAnimationAuthoring.gameObject}.");

            DstEntityManager.AddBuffer<TinyAnimationClipRef>(gameObjectEntity);

            var anythingToAnimate = false;

            foreach (var clip in animationClips)
            {
                if (clip != null)
                    anythingToAnimate |= Convert(tinyAnimationAuthoring, clip, gameObjectEntity);
            }

            if (anythingToAnimate)
            {
                var clipReferences = DstEntityManager.GetBuffer<TinyAnimationClipRef>(gameObjectEntity);
                var currentClipEntity = clipReferences[0].value;
                DstEntityManager.AddComponentData(gameObjectEntity, new TinyAnimationPlayer { currentClip = currentClipEntity, currentIndex = 0 });

                if (tinyAnimationAuthoring.playAutomatically)
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

        bool Convert(TinyAnimationAuthoring tinyAnimationAuthoring, AnimationClip clip, Entity gameObjectEntity)
        {
            if (clip == null)
                return false;

            var clipInfoEntity = TryGetPrimaryEntity(clip);
            if (clipInfoEntity == Entity.Null || !DstEntityManager.HasComponent<BakedAnimationClip>(clipInfoEntity))
                throw new Exception($"Something went wrong while retrieving the Entity for animation clip: {clip.name}");

            var bakedAnimationClip = DstEntityManager.GetComponentData<BakedAnimationClip>(clipInfoEntity);
            var floatCurvesInfo = bakedAnimationClip.floatCurvesInfo;
            var pPtrCurvesInfo = bakedAnimationClip.pPtrCurvesInfo;

            var hasFloatCurves = floatCurvesInfo != BlobAssetReference<CurvesInfo>.Null;
            var hasPPtrCurves = pPtrCurvesInfo != BlobAssetReference<CurvesInfo>.Null;

            if (!hasFloatCurves && !hasPPtrCurves)
                return false; // Nothing to animate

            var rootGameObject = tinyAnimationAuthoring.gameObject;
            var clipEntity = CreateAdditionalEntity(rootGameObject);
            DstEntityManager.SetName(clipEntity, $"{clip.name} ({rootGameObject.name})");

            // With pruning, it's possible that nothing remains to be animated
            var anythingToAnimate = false;

            if (hasFloatCurves)
                anythingToAnimate |= ConvertFloatCurves(rootGameObject, clipEntity, tinyAnimationAuthoring, floatCurvesInfo);

            if (hasPPtrCurves)
                anythingToAnimate |= ConvertPPtrCurves(rootGameObject, clipEntity, pPtrCurvesInfo);

            if (anythingToAnimate)
            {
                DstEntityManager.AddComponentData(
                    clipEntity, new TinyAnimationClip
                    {
                        time = 0.0f,
                        duration = clip.length
                    });

                var clipReferences = DstEntityManager.GetBuffer<TinyAnimationClipRef>(gameObjectEntity);
                clipReferences.Add(new TinyAnimationClipRef { value = clipEntity} );
            }

            return anythingToAnimate;
        }

        bool ConvertFloatCurves(GameObject rootGameObject, Entity entity, TinyAnimationAuthoring tinyAnimationAuthoring, BlobAssetReference<CurvesInfo> floatCurvesInfo)
        {
            ref var curvesInfo = ref floatCurvesInfo.Value;

            if (curvesInfo.bindingNames.Length != curvesInfo.GetCurvesCount())
                throw new Exception($"{nameof(CurvesInfo.bindingNames)} and {nameof(CurvesInfo.curveOffsets)} must be of the same length.");

            var length = curvesInfo.bindingNames.Length;
            var animationBindings = new List<AnimationBinding>(length);
            var animationBindingsNames = new List<AnimationBindingName>(length);

            var rootTransform = rootGameObject.transform;
            var shouldPatchScale = (curvesInfo.conversionActions & RequiredConversionActions.PatchScale) > 0 && tinyAnimationAuthoring.patchMissingScaleIfNeeded;

            for (var i = 0; i < length; i++)
            {
                var target = rootTransform.Find(curvesInfo.targetGameObjectPaths[i].ToString());

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
                    curve = curve.ToBlobAssetRef(),
                    targetEntity = targetEntity
                });

                curve.Dispose();
                animationBindingsNames.Add(new AnimationBindingName
                {
                    value = curvesInfo.bindingNames[i]
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
                var target = rootTransform.Find(curvesInfo.targetGameObjectPaths[i].ToString());

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
                    DstEntityManager.AddComponentData(targetEntity, new AnimatedAssetGroupingRef { value = curvesInfo.animatedAssetGroupings[i]});
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
                    curve = curve.ToBlobAssetRef(),
                    targetEntity = targetEntity
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
