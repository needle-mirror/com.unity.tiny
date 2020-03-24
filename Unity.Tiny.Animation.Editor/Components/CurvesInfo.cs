using System;
using Unity.Collections;
using Unity.Entities;

namespace Unity.Tiny.Animation.Editor
{
    // Used as a cache during conversion
    struct BakedAnimationClip : IComponentData
    {
        public BlobAssetReference<CurvesInfo> FloatCurvesInfo;
        public BlobAssetReference<CurvesInfo> PPtrCurvesInfo;
    }

    struct CurvesInfo
    {
        public BlobArray<Keyframe> Keyframes;
        public BlobArray<int> CurveOffsets;
        public BlobArray<NativeString512> TargetGameObjectPaths;
        public BlobArray<NativeString512> BindingNames;
        public BlobArray<Entity> AnimatedAssetGroupings;

        public RequiredConversionActions ConversionActions;

        public int GetCurvesCount() => CurveOffsets.Length;

        public KeyframeCurve GetCurve(int curveIndex, Allocator allocator)
        {
            var start = CurveOffsets[curveIndex];
            var end = curveIndex + 1 < CurveOffsets.Length ? CurveOffsets[curveIndex + 1] : Keyframes.Length;
            var curve = new KeyframeCurve(end - start, allocator);

            for (int i = start; i < end; ++i)
            {
                curve[i - start] = Keyframes[i];
            }

            return curve;
        }
    }

    [Flags]
    enum RequiredConversionActions : byte
    {
        None = 0,
        PatchScale = 1
    }

    struct AnimationBindingName : IBufferElementData
    {
        public NativeString512 Value;
    }
}