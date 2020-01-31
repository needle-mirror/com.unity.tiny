using System;
using Unity.Collections;
using Unity.Entities;

namespace Unity.Tiny.Animation.Editor
{
    // Used as a cache during conversion
    struct BakedAnimationClip : IComponentData
    {
        public BlobAssetReference<CurvesInfo> floatCurvesInfo;
        public BlobAssetReference<CurvesInfo> pPtrCurvesInfo;
    }

    struct CurvesInfo
    {
        public BlobArray<Keyframe> keyframes;
        public BlobArray<int> curveOffsets;
        public BlobArray<NativeString512> targetGameObjectPaths;
        public BlobArray<NativeString512> bindingNames;
        public BlobArray<Entity> animatedAssetGroupings;

        public RequiredConversionActions conversionActions;

        public int GetCurvesCount() => curveOffsets.Length;

        public KeyframeCurve GetCurve(int curveIndex, Allocator allocator)
        {
            var start = curveOffsets[curveIndex];
            var end = curveIndex + 1 < curveOffsets.Length ? curveOffsets[curveIndex + 1] : keyframes.Length;
            var curve = new KeyframeCurve(end - start, allocator);

            for (int i = start; i < end; ++i)
            {
                curve[i - start] = keyframes[i];
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
}
