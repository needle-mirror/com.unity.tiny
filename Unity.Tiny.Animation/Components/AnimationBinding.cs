using Unity.Entities;

namespace Unity.Tiny.Animation
{
    // Only 8 can be local to a chunk using the default capacity and TRS alone requires 10
    // Vectorisation will help a lot here, bringing TRS down to 3 bindings
    // Splitting TRS bindings from Component bindings will also help maintain locality
    // Even without vectorisation and without splitting TRS, 16 should be enough for a majority of cases
    [InternalBufferCapacity(16)]
    struct AnimationBinding : IBufferElementData
    {
        public BlobAssetReference<KeyframeCurveBlob> Curve;
        public Entity TargetEntity;
        public int TargetComponentTypeIndex;
        public ushort FieldOffset;
        public ushort FieldSize;
    }

    struct AnimationPPtrBinding : IBufferElementData
    {
        public BlobAssetReference<KeyframeCurveBlob> Curve;
        public Entity SourceEntity;
        public Entity TargetEntity;
        public int TargetComponentTypeIndex;
        public ushort FieldOffset;
    }

    struct AnimationPPtrBindingSources : IBufferElementData
    {
        public Entity Value;
    }

    // These components are used for runtime retargetting; also marks an animation as "not fully bound"

    // Must be a buffer matching an AnimationBinding buffer 1:1
    struct AnimationBindingRetarget : IBufferElementData
    {
        public ulong StableTypeHash;
    }

    // Must be a buffer matching an AnimationPPtrBinding buffer 1:1
    struct AnimationPPtrBindingRetarget : IBufferElementData
    {
        public ulong StableTypeHash;
    }
}
