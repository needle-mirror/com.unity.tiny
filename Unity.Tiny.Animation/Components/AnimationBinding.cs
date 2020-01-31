using JetBrains.Annotations;
using Unity.Collections;
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
        public BlobAssetReference<KeyframeCurveBlob> curve;
        public Entity targetEntity;
        public int targetComponentTypeIndex;
        public ushort fieldOffset;
        public ushort fieldSize;
    }

    struct AnimationBindingName : IBufferElementData
    {
        public NativeString512 value;
    }

    // Used for runtime retargetting; also marks an animation as "not fully bound"
    struct AnimationBindingRetarget : IBufferElementData
    {
        public ulong stableTypeHash;
    }

    struct AnimationPPtrBinding : IBufferElementData
    {
        public BlobAssetReference<KeyframeCurveBlob> curve;
        public Entity targetEntity;
    }

    [PublicAPI]
    public struct PPtrIndex : IComponentData
    {
        // Updated using type index. Not consumed by this package.
        [UsedImplicitly]
        public ushort value;
    }
}
