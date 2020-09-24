using System;
using Unity.Tiny.Rendering;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Burst;

namespace Unity.TinyConversion
{
    [BurstCompile]
    internal struct BlendShapeConversionJob : IJob
    {
        public NativeArray<BlobAssetReference<BlendShapeData>>
            BlobAssets; //Blob asset array shared across job instances and Deallocated from MeshConversion

        public UMeshSettings MeshSettings;
        [DeallocateOnJobCompletion] public NativeArray<UBlendShapeChannel> Channels;
        [DeallocateOnJobCompletion] public NativeArray<Vector3> DeltaValues;
        [DeallocateOnJobCompletion] public NativeArray<float> Weights;

        public BlendShapeConversionJob(UMeshSettings settings, UBlendShapeDataCache data,
            NativeArray<BlobAssetReference<BlendShapeData>> blobArray)
        {
            MeshSettings = settings;
            Channels = data.channels;
            DeltaValues = data.deltaValues;
            Weights = data.weights;
            BlobAssets = blobArray;
        }

        public bool HasValueInBlendShapeVertex(NativeArray<Vector3> deltaValues)
        {
            int valuesCount = deltaValues.Length;
            for (int i = 0; i < valuesCount; i++)
            {
                if (deltaValues[i] != Vector3.zero)
                    return true;
            }

            return false;
        }

        public void Execute()
        {
            BlobBuilder allocator = new BlobBuilder(Allocator.Temp);
            ref BlendShapeData root = ref allocator.ConstructRoot<BlendShapeData>();
            BlobBuilderArray<BlendShapeChannel> channelBuilder = allocator.Allocate(ref root.Channels, Channels.Length);
            unsafe
            {
                byte* deltaValues = (byte*)DeltaValues.GetUnsafePtr<Vector3>();
                int deltaValuesOffset = 0;
                int weightsOffset = 0;
                int sizeOfVector3 = UnsafeUtility.SizeOf<Vector3>();
                int totalVertexSize = MeshSettings.vertexCount * sizeOfVector3;

                NativeArray<Vector3> deltaNormals = new NativeArray<Vector3>(MeshSettings.vertexCount, Allocator.Temp);
                void* normalDestAddr = deltaNormals.GetUnsafePtr();
                NativeArray<Vector3> deltaTangents = new NativeArray<Vector3>(MeshSettings.vertexCount, Allocator.Temp);
                void* tangentDestAddr = deltaNormals.GetUnsafePtr();

                for (int i = 0; i < Channels.Length; i++)
                {
                    UBlendShapeChannel uBlendShapeChannel = Channels[i];
                    ref BlendShapeChannel channel = ref channelBuilder[i];
                    channel.NameHash = uBlendShapeChannel.nameHash;

                    BlobBuilderArray<BlendShapeFrame> frameBuilder =
                        allocator.Allocate(ref channel.Frames, uBlendShapeChannel.frameCount);
                    for (int j = 0; j < uBlendShapeChannel.frameCount; j++)
                    {
                        ref BlendShapeFrame frame = ref frameBuilder[j];
                        frame.Weight = Weights[weightsOffset];
                        weightsOffset++;

                        BlobBuilderArray<BlendShapeVertexPosition> positionBuilder =
                            allocator.Allocate(ref frame.VerticesPosition, MeshSettings.vertexCount);
                        void* positionPtr = positionBuilder.GetUnsafePtr();
                        UnsafeUtility.MemCpy(positionPtr, deltaValues + deltaValuesOffset, totalVertexSize);
                        deltaValuesOffset += totalVertexSize;

                        UnsafeUtility.MemCpy(normalDestAddr, deltaValues + deltaValuesOffset, totalVertexSize);
                        deltaValuesOffset += totalVertexSize;

                        UnsafeUtility.MemCpy(tangentDestAddr, deltaValues + deltaValuesOffset, totalVertexSize);
                        deltaValuesOffset += totalVertexSize;

                        frame.HasNormals = HasValueInBlendShapeVertex(deltaNormals);
                        frame.HasTangents = HasValueInBlendShapeVertex(deltaTangents);
                        if (frame.HasNormals)
                        {
                            BlobBuilderArray<BlendShapeVertexNormal> normalBuilder =
                                allocator.Allocate(ref frame.VerticesNormal, MeshSettings.vertexCount);
                            void* normalPtr = normalBuilder.GetUnsafePtr();
                            UnsafeUtility.MemCpy(normalPtr, normalDestAddr, totalVertexSize);
                        }

                        if (frame.HasTangents)
                        {
                            BlobBuilderArray<BlendShapeVertexTangent> tangentBuilder =
                                allocator.Allocate(ref frame.VerticesTangent, MeshSettings.vertexCount);
                            void* tangentPtr = tangentBuilder.GetUnsafePtr();
                            UnsafeUtility.MemCpy(tangentPtr, tangentDestAddr, totalVertexSize);
                        }
                    }
                }

                deltaNormals.Dispose();
                deltaTangents.Dispose();
            }

            BlobAssets[MeshSettings.blobIndex] = allocator.CreateBlobAssetReference<BlendShapeData>(Allocator.Persistent);
            allocator.Dispose();
        }
    }
}
