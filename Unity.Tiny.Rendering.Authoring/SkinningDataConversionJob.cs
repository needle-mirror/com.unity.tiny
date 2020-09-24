using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Tiny.Rendering;
using UnityEngine;

namespace Unity.TinyConversion
{

    [BurstCompile]
    internal struct SkinningDataConversionJob : IJob
    {
        public int SkinDataBlobIndex;
        public NativeArray<BlobAssetReference<SkinnedMeshData>> SkinDataBlobAssets;
        [DeallocateOnJobCompletion] public NativeArray<Vector4> BoneWeights;
        [DeallocateOnJobCompletion] public NativeArray<Vector4> BoneIndices;

        public SkinningDataConversionJob(UMeshDataCache data, int skinDataBlobIndex, NativeArray<BlobAssetReference<SkinnedMeshData>> skinnedBlob)
        {
            SkinDataBlobIndex = skinDataBlobIndex;
            SkinDataBlobAssets = skinnedBlob;
            BoneWeights = data.uBoneWeights;
            BoneIndices = data.uBoneIndices;
        }

        public unsafe void CheckVertexLayout()
        {
            SkinnedMeshVertex smv;
            SkinnedMeshVertex* smvPtr = &smv;
            {
                Debug.Assert((long)&(smvPtr->BoneWeight) - (long)smvPtr == 0);
                Debug.Assert((long)&(smvPtr->BoneIndex) - (long)smvPtr == 16);
            }
        }

        public void Execute()
        {
            CheckVertexLayout();
            CreateBlobAssetForSkinnedMeshVertex();
        }

        private void CreateBlobAssetForSkinnedMeshVertex()
        {
            if (BoneWeights.Length == 0 || SkinDataBlobIndex == -1)
                return;

            var allocator = new BlobBuilder(Allocator.Temp);
            ref var root = ref allocator.ConstructRoot<SkinnedMeshData>();
            var verticesCount = BoneWeights.Length;
            var vertices = allocator.Allocate(ref root.Vertices, verticesCount);
            unsafe
            {
                int offset = 0;
                byte* dest = (byte*) vertices.GetUnsafePtr();
                byte* boneWeights = (byte*)BoneWeights.GetUnsafePtr<Vector4>();
                UnsafeUtility.MemCpyStride(dest + offset, sizeof(SkinnedMeshVertex), boneWeights, sizeof(float4), sizeof(float4), verticesCount);
                offset += sizeof(float4);

                byte* boneIndices = (byte*)BoneIndices.GetUnsafePtr<Vector4>();
                UnsafeUtility.MemCpyStride(dest + offset, sizeof(SkinnedMeshVertex), boneIndices, sizeof(float4), sizeof(float4), verticesCount);
            }

            SkinDataBlobAssets[SkinDataBlobIndex] = allocator.CreateBlobAssetReference<SkinnedMeshData>(Allocator.Persistent);
            allocator.Dispose();
        }
    }
}
