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
    internal struct SimpleMeshConversionJob : IJob
    {
        public int MeshBlobIndex;
        public NativeArray<BlobAssetReference<SimpleMeshData>> MeshBlobAssets; //Blob asset array shared across job instances and Deallocated from MeshConversion
        [DeallocateOnJobCompletion] public NativeArray<Vector3> Positions;
        [DeallocateOnJobCompletion] public NativeArray<Vector2> UVs;
        [DeallocateOnJobCompletion] public NativeArray<ushort> Indices;

        public SimpleMeshConversionJob(UMeshDataCache data, int meshBlobIndex, NativeArray<BlobAssetReference<SimpleMeshData>> meshBlob)
        {
            MeshBlobIndex = meshBlobIndex;
            MeshBlobAssets = meshBlob;

            Positions = data.uPositions;
            UVs = data.uUVs;
            Indices = data.uIndices;
        }

        public unsafe void CheckVertexLayout()
        {
            SimpleVertex tv;
            SimpleVertex* p = &tv;
            {
                Debug.Assert((long)&(p->Position) - (long)p == 0);
                Debug.Assert((long)&(p->TexCoord0) - (long)p == 12);
                Debug.Assert((long)&(p->Color) - (long)p == 20);
                Debug.Assert((long)&(p->BillboardPos) - (long)p == 36);
            }
        }

        public void Execute()
        {
            CheckVertexLayout();
            CreateBlobAssetForSimpleVertex();
        }

        private void CreateBlobAssetForSimpleVertex()
        {
            var allocator = new BlobBuilder(Allocator.Temp);
            ref var root = ref allocator.ConstructRoot<SimpleMeshData>();
            var vertices = allocator.Allocate(ref root.Vertices, Positions.Length);

            unsafe
            {
                int offset = 0;
                byte* dest = (byte*)vertices.GetUnsafePtr();
                //Copy vertices
                if (Positions.Length != 0)
                {
                    byte* positions = (byte*)(Positions.GetUnsafePtr<Vector3>());
                    UnsafeUtility.MemCpyStride(dest + offset, sizeof(SimpleVertex), positions, sizeof(float3), sizeof(float3), Positions.Length);
                    offset += sizeof(float3);

                    byte* uvs = (byte*)UVs.GetUnsafePtr<Vector2>();
                    UnsafeUtility.MemCpyStride(dest + offset, sizeof(SimpleVertex), uvs, sizeof(float2), sizeof(float2), Positions.Length);
                    offset += sizeof(float2);
                }

                //Vertex color is not supported in URP lit shader, override to white for now
                float4 albedo = new float4(1);
                UnsafeUtility.MemCpyStride(dest + offset, sizeof(SimpleVertex), &albedo, 0, sizeof(float4), Positions.Length);
                offset += sizeof(float4);

                //Billboard position not present in UnityEngine.Mesh
                float3 billboardPos = float3.zero;
                UnsafeUtility.MemCpyStride(dest + offset, sizeof(SimpleVertex), &billboardPos, 0, sizeof(float3), Positions.Length);

                //Copy indices
                if (Indices.Length != 0)
                {
                    byte* indices = (byte*)Indices.GetUnsafePtr<ushort>();
                    var dIndices = allocator.Allocate(ref root.Indices, Indices.Length);
                    byte* desti = (byte*)dIndices.GetUnsafePtr();
                    UnsafeUtility.MemCpy(desti, indices, sizeof(ushort) * Indices.Length);
                }
            }
            MeshBlobAssets[MeshBlobIndex] = allocator.CreateBlobAssetReference<SimpleMeshData>(Allocator.Persistent);
            allocator.Dispose();
        }
    }
}
