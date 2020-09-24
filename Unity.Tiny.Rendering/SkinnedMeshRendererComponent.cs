using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Tiny.Rendering
{
    public class MeshSkinningConfig
    {
        public static int GPU_SKINNING_MAX_BONES = 16;
    }

    public enum ShadowCastingMode
    {
        Off,
        On,
        TwoSided,
        ShadowsOnly,
    }

    public enum SkinQuality
    {
        Bone1 = 1,
        Bone2 = 2,
        Bone4 = 4,
    }

    public struct SkinnedMeshRenderer : IComponentData
    {
        public Entity material;     // points to the entity with a material, must be a lit material
        public Entity sharedMesh;         // points to the entity with the mesh, this must be a lit mesh
        public Entity dynamicMesh;
        public int startIndex;      // sub mesh indexing
        public int indexCount;
        public bool canUseGPUSkinning;
        public bool canUseCPUSkinning;
        public ShadowCastingMode shadowCastingMode;
        public SkinQuality skinQuality;
    }

    public struct SkinnedMeshBoneRef : IBufferElementData
    {
        public Entity bone;
    }

    public struct SkinnedMeshBoneInfo : IComponentData
    {
        public Entity smrEntity;
        public float4x4 bindpose;
        public float4x4 bonematrix;
    }

    public struct BlendShapeWeight : IBufferElementData
    {
        public uint NameHash;
        public float CurWeight;
    }

    public struct SetBlendShapeWeight : IBufferElementData
    {
        public uint NameHash;
        public float ModifiedWeight;
    }

    public struct BlendShapeUpdated : IComponentData
    {

    }
}
