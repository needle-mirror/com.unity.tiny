using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using JetBrains.Annotations;
using TinyInternal.Bridge;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.Tiny.Animation.Editor
{
    [UpdateInGroup(typeof(GameObjectConversionGroup))]
    [UpdateAfter(typeof(BeforeTinyAnimationConversion))]
    [UpdateBefore(typeof(AfterTinyAnimationConversion))]
    class AnimationClipConversion : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((UnityEngine.Animation animationComponent) =>
            {
                if (!TinyAnimationConversionState.ValidateGameObjectAndWarn(animationComponent.gameObject))
                    return;

                var animationClips = TinyAnimationConversionState.GetAllAnimationClips(animationComponent);
                if (animationClips.Length == 0)
                    return;

                ConversionUtils.WarnAboutUnsupportedFeatures(animationComponent);

                foreach (var clip in animationClips)
                {
                    Convert(clip);
                }
            });

            Entities.ForEach((TinyAnimationAuthoring animationComponent) =>
            {
                if (!TinyAnimationConversionState.ValidateGameObjectAndWarn(animationComponent.gameObject))
                    return;

                var animationClips = TinyAnimationConversionState.GetAllAnimationClips(animationComponent);
                if (animationClips.Length == 0)
                    return;

                ConversionUtils.WarnAboutUnsupportedFeatures(animationComponent.GetComponent<Animator>());

                foreach (var clip in animationClips)
                {
                    Convert(clip);
                }
            });
        }

        void Convert(AnimationClip clip)
        {
            if (clip == null || clip.empty)
                return;

            var entity = TryGetPrimaryEntity(clip);

            if (entity == Entity.Null)
                throw new Exception($"Something went wrong while creating an Entity for animation clip: {clip.name}");

            if (DstEntityManager.HasComponent<BakedAnimationClip>(entity))
                return; // Already converted

            ConversionUtils.WarnAboutUnsupportedFeatures(clip);

            var floatCurvesInfo = ConvertFloatCurves(clip);
            var pPtrCurvesInfo = ConvertPPtrCurves(clip);

            DstEntityManager.AddComponentData(entity, new BakedAnimationClip
            {
                FloatCurvesInfo = floatCurvesInfo,
                PPtrCurvesInfo = pPtrCurvesInfo
            });
        }

        static BlobAssetReference<CurvesInfo> ConvertFloatCurves([NotNull] AnimationClip clip)
        {
            var bindings = AnimationUtility.GetCurveBindings(clip);
            var numBindings = bindings.Length;

            var blobAssetRef = BlobAssetReference<CurvesInfo>.Null;
            if (numBindings == 0)
                return blobAssetRef;

            var targetPaths = new List<FixedString512>(numBindings);
            var animationBindingsConvertedNames = new List<FixedString512>(numBindings);
            var keyframeCurves = new List<KeyframeCurve>(numBindings);

            var scaleRequired = false;
            var numKeys = 0;
            var numBindingsAfterConversion = numBindings;

            for (var i = 0; i < numBindings; i++)
            {
                var binding = bindings[i];
                var rotationMode = TinyAnimationEditorBridge.GetRotationMode(binding);

                if (rotationMode != TinyAnimationEditorBridge.RotationMode.Undefined && rotationMode != TinyAnimationEditorBridge.RotationMode.RawQuaternions)
                {
                    // Handle non quaternion rotation
                    //TODO: Handle other modes as they show up
                    if (rotationMode == TinyAnimationEditorBridge.RotationMode.RawEuler)
                    {
                        if (i + 2 >= numBindings)
                            throw new InvalidDataException($"Euler rotation curve for {binding.type.Name}.{binding.propertyName} is malformed");

                        var xBinding = binding;
                        var yBinding = bindings[i + 1];
                        var zBinding = bindings[i + 2];

                        var xCurveOriginal = AnimationUtility.GetEditorCurve(clip, xBinding);
                        var yCurveOriginal = AnimationUtility.GetEditorCurve(clip, yBinding);
                        var zCurveOriginal = AnimationUtility.GetEditorCurve(clip, zBinding);

                        var xCurveNew = new AnimationCurve();
                        var yCurveNew = new AnimationCurve();
                        var zCurveNew = new AnimationCurve();
                        var wCurveNew = new AnimationCurve();

                        // We *need* to resample at the framerate when converting from Euler to Quaternion
                        var step = clip.length / clip.frameRate;
                        var time = 0.0f;
                        while (time <= clip.length + 0.001f) // Offset end in case of imprecision
                        {
                            var euler = new float3(xCurveOriginal.Evaluate(time), yCurveOriginal.Evaluate(time), zCurveOriginal.Evaluate(time));
                            var quat = quaternion.Euler(math.radians(euler));

                            // No interpolation between keyframes is not ideal, but shouldn't be super noticeable
                            xCurveNew.AddKey(new UnityEngine.Keyframe(time, quat.value.x, Mathf.Infinity, Mathf.Infinity));
                            yCurveNew.AddKey(new UnityEngine.Keyframe(time, quat.value.y, Mathf.Infinity, Mathf.Infinity));
                            zCurveNew.AddKey(new UnityEngine.Keyframe(time, quat.value.z, Mathf.Infinity, Mathf.Infinity));
                            wCurveNew.AddKey(new UnityEngine.Keyframe(time, quat.value.w, Mathf.Infinity, Mathf.Infinity));

                            time += step;
                        }

                        var resampledKeysCount = xCurveNew.length;
                        var lastKeyIndex = resampledKeysCount - 1;

                        // Fixup in case of imprecision
                        var lastKey = xCurveNew.keys[lastKeyIndex];
                        lastKey.time = clip.length;
                        xCurveNew.keys[lastKeyIndex] = lastKey;

                        lastKey = yCurveNew.keys[lastKeyIndex];
                        lastKey.time = clip.length;
                        yCurveNew.keys[lastKeyIndex] = lastKey;

                        lastKey = zCurveNew.keys[lastKeyIndex];
                        lastKey.time = clip.length;
                        zCurveNew.keys[lastKeyIndex] = lastKey;

                        lastKey = wCurveNew.keys[lastKeyIndex];
                        lastKey.time = clip.length;
                        wCurveNew.keys[lastKeyIndex] = lastKey;

                        var xPropName = GetConvertedName(binding.type, TinyAnimationEditorBridge.CreateRawQuaternionsBindingName("x"));
                        var yPropName = GetConvertedName(binding.type, TinyAnimationEditorBridge.CreateRawQuaternionsBindingName("y"));
                        var zPropName = GetConvertedName(binding.type, TinyAnimationEditorBridge.CreateRawQuaternionsBindingName("z"));
                        var wPropName = GetConvertedName(binding.type, TinyAnimationEditorBridge.CreateRawQuaternionsBindingName("w"));

                        animationBindingsConvertedNames.Add(new FixedString512(xPropName));
                        animationBindingsConvertedNames.Add(new FixedString512(yPropName));
                        animationBindingsConvertedNames.Add(new FixedString512(zPropName));
                        animationBindingsConvertedNames.Add(new FixedString512(wPropName));

                        keyframeCurves.Add(xCurveNew.ToKeyframeCurve());
                        keyframeCurves.Add(yCurveNew.ToKeyframeCurve());
                        keyframeCurves.Add(zCurveNew.ToKeyframeCurve());
                        keyframeCurves.Add(wCurveNew.ToKeyframeCurve());

                        numKeys += resampledKeysCount * 4;

                        var targetPath = string.IsNullOrEmpty(binding.path) ? string.Empty : binding.path;
                        targetPaths.Add(targetPath);
                        targetPaths.Add(targetPath);
                        targetPaths.Add(targetPath);
                        targetPaths.Add(targetPath);

                        numBindingsAfterConversion += 1;
                        i += 2;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Rotation mode: {rotationMode} is not supported.");
                    }
                }
                else
                {
                    // Note: Empty string maps to self in Transform.Find()
                    targetPaths.Add(string.IsNullOrEmpty(binding.path) ? string.Empty : binding.path);

                    var bindingPropertyName = binding.propertyName;
                    var convertedName = GetConvertedName(binding.type, bindingPropertyName);

                    animationBindingsConvertedNames.Add(new FixedString512(convertedName));

                    var animationCurve = AnimationUtility.GetEditorCurve(clip, binding);
                    var curve = animationCurve.ToKeyframeCurve();
                    keyframeCurves.Add(curve);
                    numKeys += curve.Length;

                    if (binding.type == typeof(Transform) && bindingPropertyName.StartsWith("m_LocalScale.", StringComparison.Ordinal))
                        scaleRequired = true;
                }
            }

            using (var blobBuilder = new BlobBuilder(Allocator.Temp))
            {
                ref var builderRoot = ref blobBuilder.ConstructRoot<CurvesInfo>();

                var keyframesBuilder = blobBuilder.Allocate(ref builderRoot.Keyframes, numKeys);
                var curveOffsetsBuilder = blobBuilder.Allocate(ref builderRoot.CurveOffsets, numBindingsAfterConversion);
                var bindingNamesBuilder = blobBuilder.Allocate(ref builderRoot.BindingNames, numBindingsAfterConversion);
                var targetPathsBuilder = blobBuilder.Allocate(ref builderRoot.TargetGameObjectPaths, numBindingsAfterConversion);

                // We don't care about that field in this case
                blobBuilder.Allocate(ref builderRoot.AnimatedAssetGroupings, 0);

                for (int bindingIndex = 0, keyIndex = 0, curveOffset = 0; bindingIndex < numBindingsAfterConversion; ++bindingIndex)
                {
                    var keyframeCurve = keyframeCurves[bindingIndex];
                    for (var i = 0; i < keyframeCurve.Length; ++i)
                    {
                        keyframesBuilder[keyIndex++] = keyframeCurve[i];
                    }

                    curveOffsetsBuilder[bindingIndex] = curveOffset;
                    curveOffset += keyframeCurve.Length;
                    bindingNamesBuilder[bindingIndex] = animationBindingsConvertedNames[bindingIndex];
                    targetPathsBuilder[bindingIndex] = targetPaths[bindingIndex];
                }

                builderRoot.ConversionActions = scaleRequired ? RequiredConversionActions.PatchScale : RequiredConversionActions.None;

                blobAssetRef = blobBuilder.CreateBlobAssetReference<CurvesInfo>(Allocator.Persistent);
            }

            foreach (var curve in keyframeCurves)
            {
                curve.Dispose();
            }

            return blobAssetRef;
        }

        // TODO: Lots of copy-paste from previous method, worth refactoring?
        BlobAssetReference<CurvesInfo> ConvertPPtrCurves([NotNull] AnimationClip clip)
        {
            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            var numBindings = bindings.Length;

            var blobAssetRef = BlobAssetReference<CurvesInfo>.Null;
            if (numBindings == 0)
                return blobAssetRef;

            var targetPaths = new FixedString512[numBindings];
            var animationBindingsConvertedNames = new FixedString512[numBindings];
            var keyframeCurves = new KeyframeCurve[numBindings];
            var animatedAssetGroupings = new Entity[numBindings];

            var numKeys = 0;

            // Cache to avoid adding duplicates to a group
            var foundObjects = new List<UnityObject>(numBindings);

            for (var bindingIndex = 0; bindingIndex < numBindings; bindingIndex++)
            {
                var binding = bindings[bindingIndex];
                targetPaths[bindingIndex] = string.IsNullOrEmpty(binding.path) ? string.Empty : binding.path;

                var bindingPropertyName = binding.propertyName;
                var convertedName = GetConvertedName(binding.type, bindingPropertyName);
                animationBindingsConvertedNames[bindingIndex] = new FixedString512(convertedName);

                foundObjects.Clear();

                var curveData = AnimationUtility.GetObjectReferenceCurve(clip, binding);

                if (curveData.Length == 0)
                    continue;

                var assetGroupEntity = CreateAdditionalEntity(clip);

                DstEntityManager.SetName(assetGroupEntity, $"{clip.name} - Asset Group {bindingIndex.ToString(NumberFormatInfo.InvariantInfo)}");

                var assetRefBuffer = DstEntityManager.AddBuffer<AnimationPPtrBindingSources>(assetGroupEntity);

                animatedAssetGroupings[bindingIndex] = assetGroupEntity;

                var pPtrIndexCurve = new AnimationCurve();

                for (var keyframeIndex = 0; keyframeIndex < curveData.Length; keyframeIndex++)
                {
                    var keyframe = curveData[keyframeIndex];
                    var referencedAsset = keyframe.value;
                    var index = foundObjects.IndexOf(referencedAsset);
                    if (index == -1)
                    {
                        index = foundObjects.Count;
                        foundObjects.Add(referencedAsset);
                        assetRefBuffer.Add(new AnimationPPtrBindingSources { Value = GetPrimaryEntity(referencedAsset) });
                    }

                    // Infinite tangents result in a stepped curve, which is an adequate representation for an integer curve
                    pPtrIndexCurve.AddKey(new UnityEngine.Keyframe(keyframe.time, index, Mathf.Infinity, Mathf.Infinity));
                }

                var curve = pPtrIndexCurve.ToKeyframeCurve();
                keyframeCurves[bindingIndex] = curve;
                numKeys += curve.Length;
            }

            using (var blobBuilder = new BlobBuilder(Allocator.Temp))
            {
                ref var builderRoot = ref blobBuilder.ConstructRoot<CurvesInfo>();

                var keyframesBuilder = blobBuilder.Allocate(ref builderRoot.Keyframes, numKeys);
                var curveOffsetsBuilder = blobBuilder.Allocate(ref builderRoot.CurveOffsets, numBindings);
                var bindingNamesBuilder = blobBuilder.Allocate(ref builderRoot.BindingNames, numBindings);
                var animatedAssetGroupingsBuilder = blobBuilder.Allocate(ref builderRoot.AnimatedAssetGroupings, numBindings);

                var targetPathsBuilder = blobBuilder.Allocate(ref builderRoot.TargetGameObjectPaths, numBindings);

                for (int bindingIndex = 0, keyIndex = 0, curveOffset = 0; bindingIndex < numBindings; ++bindingIndex)
                {
                    var keyframeCurve = keyframeCurves[bindingIndex];
                    for (var i = 0; i < keyframeCurve.Length; ++i)
                    {
                        keyframesBuilder[keyIndex++] = keyframeCurve[i];
                    }

                    curveOffsetsBuilder[bindingIndex] = curveOffset;
                    curveOffset += keyframeCurve.Length;
                    targetPathsBuilder[bindingIndex] = targetPaths[bindingIndex];
                    bindingNamesBuilder[bindingIndex] = animationBindingsConvertedNames[bindingIndex];
                    animatedAssetGroupingsBuilder[bindingIndex] = animatedAssetGroupings[bindingIndex];
                }

                builderRoot.ConversionActions = RequiredConversionActions.None;

                blobAssetRef = blobBuilder.CreateBlobAssetReference<CurvesInfo>(Allocator.Persistent);
            }

            foreach (var curve in keyframeCurves)
            {
                curve.Dispose();
            }

            return blobAssetRef;
        }

        static string GetConvertedName(Type type, string bindingPropertyName)
        {
            var propertyPath = $"{type.Name}.{bindingPropertyName}";

            if (!BindingsStore.TryGetConvertedBindingName(propertyPath, out var convertedName))
            {
                convertedName = string.Empty;
                Debug.LogWarning(
                    $"Tiny Animation doesn't know how to handle binding to: \"{propertyPath}\". You can help it by declaring a remap manually " +
                    "in a conversion system using BindingStore.CreateBindingNameRemap(). " +
                    "Make sure your conversion system is tagged with [UpdateInGroup(typeof(GameObjectDeclareReferencedObjectsGroup))]");
            }

            return convertedName;
        }
    }
}
