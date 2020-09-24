using System;
using Unity.Entities;
using Unity.Tiny.Rendering;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;
using Unity.Jobs;
using System.Collections.Generic;
using Debug = Unity.Tiny.Debug;

namespace Unity.TinyConversion
{
    internal struct UMeshSettings
    {
        public Hash128 hash;
        public int subMeshCount;
        public float3 center;
        public float3 extents;
        public int blobIndex;
        public int vertexCount;

        public UMeshSettings(Hash128 h, UnityEngine.Mesh uMesh, int i)
        {
            hash = h;
            subMeshCount = uMesh.subMeshCount;
            center = uMesh.bounds.center;
            extents = uMesh.bounds.extents;
            blobIndex = i;
            vertexCount = uMesh.vertexCount;
        }
    }

    internal struct UMeshDataCache
    {
        public NativeArray<Vector3> uPositions;
        public NativeArray<Vector2> uUVs;
        public NativeArray<Vector3> uNormals;
        public NativeArray<Vector3> uTangents;
        public NativeArray<Vector3> uBiTangents;
        public NativeArray<Vector4> uBoneWeights;
        public NativeArray<Vector4> uBoneIndices;
        public NativeArray<Color> uColors;
        public NativeArray<ushort> uIndices;

        public unsafe void RetrieveSimpleMeshData(Mesh uMesh, int vertexCapacity = 0)
        {
            if (vertexCapacity == 0)
                vertexCapacity = uMesh.vertexCount;
            //Invert uvs
            var uvs = uMesh.uv;
            uUVs = new NativeArray<Vector2>(vertexCapacity, Allocator.TempJob);
            for (int i = 0; i < uvs.Length; i++)
            {
                uvs[i].y = 1 - uvs[i].y;
                uUVs[i] = uvs[i];
            }

            var vertices = uMesh.vertices;
            int vertexCount = vertices.Length;
            uPositions = new NativeArray<Vector3>(vertexCapacity, Allocator.TempJob);
            NativeArray<Vector3>.Copy(vertices, uPositions, vertexCount);

            int indexCount = 0;
            for (int i = 0; i < uMesh.subMeshCount; i++)
            {
                indexCount += (int)uMesh.GetIndexCount(i);
            }

            int offset = 0;
            uIndices = new NativeArray<ushort>(indexCount, Allocator.TempJob);
            for (int i = 0; i < uMesh.subMeshCount; i++)
            {
                int[] indices = uMesh.GetIndices(i);
                for (int j = 0; j < indices.Length; j++)
                {
                    uIndices[offset + j] = Convert.ToUInt16(indices[j]);
                }
                offset += indices.Length;
            }

            int skinDataCount = uMesh.boneWeights.Length;
            if (skinDataCount > 0)
            {
                uBoneWeights = new NativeArray<Vector4>(vertexCapacity, Allocator.TempJob);
                uBoneIndices = new NativeArray<Vector4>(vertexCapacity, Allocator.TempJob);
                BoneWeight[] boneWeights = uMesh.boneWeights;
                for (int i = 0; i < skinDataCount; i++)
                {
                    BoneWeight boneWeight = boneWeights[i];
                    uBoneWeights[i] = new Vector4(boneWeight.weight0, boneWeight.weight1, boneWeight.weight2, boneWeight.weight3);
                    uBoneIndices[i] = new Vector4(boneWeight.boneIndex0, boneWeight.boneIndex1, boneWeight.boneIndex2, boneWeight.boneIndex3);
                }
            }
            else
            {
                uBoneWeights = new NativeArray<Vector4>(skinDataCount, Allocator.Temp);
                uBoneIndices = new NativeArray<Vector4>(skinDataCount, Allocator.Temp);
            }
        }

        public unsafe void RetrieveLitMeshData(Mesh uMesh, int vertexCapacity = 0)
        {
            if (vertexCapacity == 0)
                vertexCapacity = uMesh.vertexCount;

            RetrieveSimpleMeshData(uMesh, vertexCapacity);

            Vector4[] tang4 = uMesh.tangents; //uMesh.tangents is vector4 with x,y,z components, and w used to flip the binormal.
            Vector3[] nor = uMesh.normals;

            if (tang4.Length != nor.Length)
                UnityEngine.Debug.LogWarning($"The mesh {uMesh.name} should have the same number of normals {nor.Length} and tangents {tang4.Length}");

            uNormals = new NativeArray<Vector3>(vertexCapacity, Allocator.TempJob);
            uTangents = new NativeArray<Vector3>(vertexCapacity, Allocator.TempJob);
            uBiTangents = new NativeArray<Vector3>(vertexCapacity, Allocator.TempJob);

            for (int i = 0; i < Math.Min(tang4.Length, nor.Length); i++)
            {
                Vector3 tangent = tang4[i];
                Vector3 normal = nor[i];
                tangent.Normalize();
                normal.Normalize();

                // Orthogonalize
                tangent = tangent - normal * Vector3.Dot(normal, tangent);
                tangent.Normalize();

                // Fix T orientation
                if (Vector3.Dot(Vector3.Cross(normal, tangent), uBiTangents[i]) < 0.0f)
                {
                    tangent = tangent * -1.0f;
                }

                uBiTangents[i] = Vector3.Cross(normal, tangent) * tang4[i].w; // tang.w should be 1 or -1
                uNormals[i] = normal;
                uTangents[i] = tangent;
            }

            var colors = uMesh.colors;
            uColors = new NativeArray<Color>(vertexCapacity, Allocator.TempJob);
            NativeArray<Color>.Copy(colors, uColors, colors.Length);
        }

        private bool IsValidBoneIndex(float weight)
        {
            return math.abs(weight) > 0.00001f;
        }

        private int CalcToBeAddBoneIndexCount(Dictionary<int, int> boneIndexCounter, BoneWeight boneWeight)
        {
            int counter = 0;
            if (IsValidBoneIndex(boneWeight.weight0) && !boneIndexCounter.ContainsKey(boneWeight.boneIndex0))
                counter++;
            if (IsValidBoneIndex(boneWeight.weight1) && !boneIndexCounter.ContainsKey(boneWeight.boneIndex1))
                counter++;
            if (IsValidBoneIndex(boneWeight.weight2) && !boneIndexCounter.ContainsKey(boneWeight.boneIndex2))
                counter++;
            if (IsValidBoneIndex(boneWeight.weight3) && !boneIndexCounter.ContainsKey(boneWeight.boneIndex3))
                counter++;
            return counter;
        }

        private int GetNewBoneIndex(Dictionary<int, int> boneIndexCounter, float weight, int boneIndex)
        {
            if (!IsValidBoneIndex(weight))
                return 0;

            int newBoneIndex = 0;
            bool existing = boneIndexCounter.TryGetValue(boneIndex, out newBoneIndex);
            if (existing)
                return newBoneIndex;

            newBoneIndex = boneIndexCounter.Count;
            boneIndexCounter[boneIndex] = newBoneIndex;
            return newBoneIndex;
        }

        public void RetrieveSkinnedMeshData(Mesh uMesh, Entity meshEntity, EntityManager entityManager, bool isLit)
        {
            //There's no need to generate special vertex data for gpu skinning
            if (uMesh.bindposes.Length <= MeshSkinningConfig.GPU_SKINNING_MAX_BONES)
            {
                if (isLit)
                    RetrieveLitMeshData(uMesh);
                else
                    RetrieveSimpleMeshData(uMesh);
                return;
            }

            List<int> duplicateVertexIndex = new List<int>();
            List<Vector4> duplicateBoneIndex = new List<Vector4>();
            Dictionary<int, Vector4> existingVertex2NewBoneIndex = new Dictionary<int, Vector4>();
            Dictionary<int, int> boneIndexCounter = new Dictionary<int, int>();
            List<Vector2> gpuDrawRange = new List<Vector2>();

            BoneWeight[] boneWeights = uMesh.boneWeights;
            int[] triangles = uMesh.triangles;

            //Separate mesh into different draw range for gpu skinning use
            for (int subMeshIndex = 0; subMeshIndex < uMesh.subMeshCount; subMeshIndex++)
            {
                UnityEngine.Rendering.SubMeshDescriptor uSubMeshDescriptor = uMesh.GetSubMesh(subMeshIndex);
                int curIndex = uSubMeshDescriptor.indexStart;
                int lastIndex = uSubMeshDescriptor.indexStart;
                int endIndex = curIndex + uSubMeshDescriptor.indexCount;
                while (curIndex < endIndex)
                {
                    int curBoneCount = boneIndexCounter.Count;
                    for (int offset = 0; offset < 3; offset++)
                    {
                        int vertexIndex = triangles[curIndex + offset];
                        BoneWeight boneWeight = boneWeights[vertexIndex];
                        curBoneCount += CalcToBeAddBoneIndexCount(boneIndexCounter, boneWeight);
                    }

                    if (curBoneCount > MeshSkinningConfig.GPU_SKINNING_MAX_BONES)
                    {
                        gpuDrawRange.Add(new Vector2(curIndex, subMeshIndex));
                        Debug.Log("GPU SkinnedMesh Draw Range[" + lastIndex + ":" + curIndex + "] BoneCount:" + boneIndexCounter.Count);
                        lastIndex = curIndex;
                        boneIndexCounter.Clear();
                    }
                    else
                    {
                        for (int offset = 0; offset < 3; offset++)
                        {
                            int vertexIndex = triangles[curIndex + offset];
                            BoneWeight curBoneWeight = boneWeights[vertexIndex];

                            //restore the new bone index and set it to the mesh later
                            Vector4 newBoneIndex = new Vector4();
                            newBoneIndex.x = GetNewBoneIndex(boneIndexCounter, curBoneWeight.weight0, curBoneWeight.boneIndex0);
                            newBoneIndex.y = GetNewBoneIndex(boneIndexCounter, curBoneWeight.weight1, curBoneWeight.boneIndex1);
                            newBoneIndex.z = GetNewBoneIndex(boneIndexCounter, curBoneWeight.weight2, curBoneWeight.boneIndex2);
                            newBoneIndex.w = GetNewBoneIndex(boneIndexCounter, curBoneWeight.weight3, curBoneWeight.boneIndex3);

                            Vector4 existingNewBoneIndex = new Vector4();
                            bool isExist = existingVertex2NewBoneIndex.TryGetValue(vertexIndex, out existingNewBoneIndex);
                            if (isExist && newBoneIndex != existingNewBoneIndex)
                            {
                                bool needAdd = true;
                                int newVertexIndex = 0;
                                for (int j = 0; j < duplicateVertexIndex.Count; j++)
                                {
                                    if (duplicateVertexIndex[j] == vertexIndex && duplicateBoneIndex[j] == newBoneIndex)
                                    {
                                        newVertexIndex = uMesh.vertexCount + j;
                                        triangles[curIndex + offset] = newVertexIndex;
                                        needAdd = false;
                                        break;
                                    }
                                }

                                if (needAdd)
                                {
                                    duplicateVertexIndex.Add(vertexIndex);
                                    duplicateBoneIndex.Add(newBoneIndex);
                                    newVertexIndex =  uMesh.vertexCount + duplicateVertexIndex.Count - 1;
                                    triangles[curIndex + offset] = newVertexIndex;
                                    existingVertex2NewBoneIndex[newVertexIndex] = newBoneIndex;
                                }
                            }
                            else
                            {
                                existingVertex2NewBoneIndex[vertexIndex] = newBoneIndex;
                            }
                        }

                        curIndex += 3;
                    }
                }

                if (lastIndex != curIndex)
                {
                    gpuDrawRange.Add(new Vector2(curIndex, subMeshIndex));
                    Debug.Log("GPU SkinnedMesh Draw Range[" + lastIndex + ":" + curIndex + "] BoneCount:" + boneIndexCounter.Count);
                }
            }
            Debug.Log("GPU SkinnedMesh Duplicate VertexCount:" + duplicateVertexIndex.Count);
            Debug.Log("GPU SkinnedMesh DrawCalls: " + gpuDrawRange.Count);

            //generate UMeshDataCache and adding duplicate vertices into UMeshDataCache
            int newVertexCount = uMesh.vertexCount + duplicateVertexIndex.Count;
            if (isLit)
                RetrieveLitMeshData(uMesh, newVertexCount);
            else
                RetrieveSimpleMeshData(uMesh, newVertexCount);
            for (int i = 0; i < duplicateVertexIndex.Count; i++)
            {
                int curVertexIndex = uMesh.vertexCount + i;
                int originalVertexIndex = duplicateVertexIndex[i];
                uPositions[curVertexIndex] = uPositions[originalVertexIndex];
                uUVs[curVertexIndex] = uUVs[originalVertexIndex];
                uBoneWeights[curVertexIndex] = uBoneWeights[originalVertexIndex];
                uBoneIndices[curVertexIndex] = uBoneIndices[originalVertexIndex];

                if (!isLit)
                    continue;
                uNormals[curVertexIndex] = uNormals[originalVertexIndex];
                uTangents[curVertexIndex] = uTangents[originalVertexIndex];
                uBiTangents[curVertexIndex] = uBiTangents[originalVertexIndex];
                uColors[curVertexIndex] = uColors[originalVertexIndex];
            }
            //Update the indices, some of the triangles reference to the duplicate vertex
            for (int i = 0; i < triangles.Length; i++)
            {
                uIndices[i] = Convert.ToUInt16(triangles[i]);
            }
            //Restore the original vertex bone index for switching GPU skinning to CPU skinning in the runtime
            DynamicBuffer<OriginalVertexBoneIndex> obiBuffer = entityManager.AddBuffer<OriginalVertexBoneIndex>(meshEntity);
            for (int i = 0; i < newVertexCount; i++)
            {
                Vector4 uBoneIndex = uBoneIndices[i];
                obiBuffer.Add(new OriginalVertexBoneIndex { BoneIndex = new float4(uBoneIndex.x, uBoneIndex.y, uBoneIndex.z, uBoneIndex.w)});
                Vector4 newBoneIndex = existingVertex2NewBoneIndex[i];
                uBoneIndices[i] = newBoneIndex;
            }
            //Add GPUSkinnedMeshDrawRange for SkinnedMeshRendererConversion use.
            DynamicBuffer<GPUSkinnedMeshDrawRange> gsmdrBuffer = entityManager.AddBuffer<GPUSkinnedMeshDrawRange>(meshEntity);
            for (int i = 0; i < gpuDrawRange.Count; i++)
            {
                gsmdrBuffer.Add(new GPUSkinnedMeshDrawRange
                {
                    TriangleIndex =  (int)gpuDrawRange[i].x,
                    SubMeshIndex = (int)gpuDrawRange[i].y,
                });
            }
        }
    }

    internal struct UBlendShapeChannel
    {
        public ulong nameHash;
        public int frameCount;
    }

    internal struct UBlendShapeDataCache
    {
        public NativeArray<UBlendShapeChannel> channels;
        public NativeArray<Vector3> deltaValues;
        public NativeArray<float> weights;

        public unsafe void RetrieveBlendShapeData(Mesh uMesh)
        {
            UBlendShapeChannel[] uBlendShapeChannels = new UBlendShapeChannel[uMesh.blendShapeCount];
            int totalDeltaValuesCount = 0;
            int totalWeightCount = 0;
            for (int i = 0; i < uMesh.blendShapeCount; i++)
            {
                string blendShapeName = uMesh.GetBlendShapeName(i);
                int frameCount = uMesh.GetBlendShapeFrameCount(i);
                totalDeltaValuesCount += frameCount * uMesh.vertexCount * 3;
                totalWeightCount += frameCount;

                UBlendShapeChannel uBlendShapeChannel = new UBlendShapeChannel();
                uBlendShapeChannel.nameHash = TypeHash.FNV1A64(blendShapeName);
                uBlendShapeChannel.frameCount = frameCount;
                uBlendShapeChannels[i] = uBlendShapeChannel;
            }
            channels = new NativeArray<UBlendShapeChannel>(uMesh.blendShapeCount, Allocator.TempJob);
            NativeArray<UBlendShapeChannel>.Copy(uBlendShapeChannels, channels, uBlendShapeChannels.Length);

            Vector3[] uDeltaValues = new Vector3[totalDeltaValuesCount];
            float[] uWeights = new float[totalWeightCount];
            int deltaValuesOffset = 0;
            int weightOffset = 0;
            for (int shapeIndex = 0; shapeIndex < uMesh.blendShapeCount; shapeIndex++)
            {
                UBlendShapeChannel uBlendShapeChannel = uBlendShapeChannels[shapeIndex];
                int frameCount = uBlendShapeChannel.frameCount;
                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    Vector3[] deltaVetices = new Vector3[uMesh.vertexCount];
                    Vector3[] deltaNormals = new Vector3[uMesh.vertexCount];
                    Vector3[] deltaTangents = new Vector3[uMesh.vertexCount];
                    uMesh.GetBlendShapeFrameVertices(shapeIndex, frameIndex, deltaVetices, deltaNormals, deltaTangents);
                    Array.Copy(deltaVetices, 0, uDeltaValues, deltaValuesOffset, deltaVetices.Length);
                    deltaValuesOffset += uMesh.vertexCount;
                    Array.Copy(deltaNormals, 0, uDeltaValues, deltaValuesOffset, deltaVetices.Length);
                    deltaValuesOffset += uMesh.vertexCount;
                    Array.Copy(deltaTangents, 0, uDeltaValues, deltaValuesOffset, deltaVetices.Length);
                    deltaValuesOffset += uMesh.vertexCount;
                    uWeights[weightOffset] = uMesh.GetBlendShapeFrameWeight(shapeIndex, frameIndex);
                    weightOffset++;
                }
            }
            deltaValues = new NativeArray<Vector3>(totalDeltaValuesCount, Allocator.TempJob);
            NativeArray<Vector3>.Copy(uDeltaValues, deltaValues, uDeltaValues.Length);

            weights = new NativeArray<float>(totalWeightCount, Allocator.TempJob);
            NativeArray<float>.Copy(uWeights, weights, uWeights.Length);
        }
    }

    [UpdateInGroup(typeof(GameObjectConversionGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    [ConverterVersion("WeixianLiu", 1)]
    public class MeshConversion : GameObjectConversionSystem
    {
        void CheckForMeshLimitations(Mesh uMesh)
        {
            int vertexCount = uMesh.vertexCount;
            if (vertexCount > UInt16.MaxValue)
                throw new ArgumentException($"The maximum number of vertices supported per mesh is {UInt16.MaxValue} and the mesh {uMesh.name} has {vertexCount} vertices. Please use a lighter mesh instead.");
        }

        protected override void OnUpdate()
        {
            var simpleMeshContext = new BlobAssetComputationContext<UMeshSettings, SimpleMeshData>(BlobAssetStore, 128, Allocator.Temp);
            var litMeshContext = new BlobAssetComputationContext<UMeshSettings, LitMeshData>(BlobAssetStore, 128, Allocator.Temp);
            var blendShapeContext = new BlobAssetComputationContext<UMeshSettings, BlendShapeData>(BlobAssetStore, 128, Allocator.Temp);
            var skinnedMeshContext = new BlobAssetComputationContext<UMeshSettings, SkinnedMeshData>(BlobAssetStore, 128, Allocator.Temp);

            JobHandle combinedJH = new JobHandle();
            int simpleIndex = 0;
            int litIndex = 0;
            int blendShapeIndex = 0;
            int skinnedMeshIndex = 0;

            // Init blobasset arrays
            Entities.ForEach((UnityEngine.Mesh uMesh) =>
            {
                CheckForMeshLimitations(uMesh);
                var entity = GetPrimaryEntity(uMesh);
                if (DstEntityManager.HasComponent<SimpleMeshRenderData>(entity))
                    simpleIndex++;
                if (DstEntityManager.HasComponent<LitMeshRenderData>(entity))
                    litIndex++;
                if (uMesh.blendShapeCount > 0)
                    blendShapeIndex++;
                if (uMesh.boneWeights.Length > 0)
                    skinnedMeshIndex++;

            });
            NativeArray<BlobAssetReference<SimpleMeshData>> simpleblobs = new NativeArray<BlobAssetReference<SimpleMeshData>>(simpleIndex, Allocator.TempJob);
            NativeArray<BlobAssetReference<LitMeshData>> litblobs = new NativeArray<BlobAssetReference<LitMeshData>>(litIndex, Allocator.TempJob);
            NativeArray<BlobAssetReference<BlendShapeData>> blendshapeblobs = new NativeArray<BlobAssetReference<BlendShapeData>>(blendShapeIndex, Allocator.TempJob);
            NativeArray<BlobAssetReference<SkinnedMeshData>> skinnedMeshBlobs = new NativeArray<BlobAssetReference<SkinnedMeshData>>(litIndex, Allocator.TempJob);

            simpleIndex = 0;
            litIndex = 0;
            blendShapeIndex = 0;
            skinnedMeshIndex = 0;

            // Check which blob assets to re-compute
            Entities.ForEach((UnityEngine.Mesh uMesh) =>
            {
                var verticesHash = new Hash128((uint)uMesh.GetHashCode(), (uint)uMesh.vertexCount.GetHashCode(),
                    (uint)uMesh.subMeshCount.GetHashCode(), 0);
                uint boneCount = (uint)uMesh.bindposes.Length;
                var skinnedDataHash = new Hash128((uint)uMesh.GetHashCode(), (uint)uMesh.vertexCount.GetHashCode(),
                    (uint)uMesh.subMeshCount.GetHashCode(), (uint)boneCount.GetHashCode());

                var entity = GetPrimaryEntity(uMesh);

                //Schedule blob asset recomputation jobs
                if (DstEntityManager.HasComponent<SimpleMeshRenderData>(entity))
                {
                    simpleMeshContext.AssociateBlobAssetWithUnityObject(verticesHash, uMesh);
                    if (simpleMeshContext.NeedToComputeBlobAsset(verticesHash))
                    {
                        var simpleMeshData = new UMeshDataCache();
                        if (boneCount > 0)
                        {
                            simpleMeshData.RetrieveSkinnedMeshData(uMesh, entity, DstEntityManager, false);
                            if (skinnedMeshContext.NeedToComputeBlobAsset(skinnedDataHash))
                            {
                                UMeshSettings uSkinnedMeshSettings = new UMeshSettings(skinnedDataHash, uMesh, skinnedMeshIndex++);
                                skinnedMeshContext.AddBlobAssetToCompute(skinnedDataHash, uSkinnedMeshSettings);
                                var skinningJob = new SkinningDataConversionJob(simpleMeshData, uSkinnedMeshSettings.blobIndex, skinnedMeshBlobs);
                                combinedJH = JobHandle.CombineDependencies(combinedJH, skinningJob.Schedule(combinedJH));
                            }
                        }
                        else
                            simpleMeshData.RetrieveSimpleMeshData(uMesh);

                        UMeshSettings uMeshSettings = new UMeshSettings(verticesHash, uMesh, simpleIndex++);
                        simpleMeshContext.AddBlobAssetToCompute(verticesHash, uMeshSettings);
                        var job = new SimpleMeshConversionJob(simpleMeshData, uMeshSettings.blobIndex, simpleblobs);
                        combinedJH = JobHandle.CombineDependencies(combinedJH, job.Schedule(combinedJH));
                    }
                }

                if (DstEntityManager.HasComponent<LitMeshRenderData>(entity))
                {
                    litMeshContext.AssociateBlobAssetWithUnityObject(verticesHash, uMesh);
                    if (litMeshContext.NeedToComputeBlobAsset(verticesHash))
                    {
                        var litMeshData = new UMeshDataCache();
                        if (boneCount > 0)
                        {
                            litMeshData.RetrieveSkinnedMeshData(uMesh, entity, DstEntityManager, true);
                            if (skinnedMeshContext.NeedToComputeBlobAsset(skinnedDataHash))
                            {
                                UMeshSettings uSkinnedMeshSettings = new UMeshSettings(skinnedDataHash, uMesh, skinnedMeshIndex++);
                                skinnedMeshContext.AddBlobAssetToCompute(skinnedDataHash, uSkinnedMeshSettings);
                                var skinningJob = new SkinningDataConversionJob(litMeshData, uSkinnedMeshSettings.blobIndex, skinnedMeshBlobs);
                                combinedJH = JobHandle.CombineDependencies(combinedJH, skinningJob.Schedule(combinedJH));
                            }
                        }
                        else
                            litMeshData.RetrieveLitMeshData(uMesh);

                        UMeshSettings uMeshSettings = new UMeshSettings(verticesHash, uMesh, litIndex++);
                        litMeshContext.AddBlobAssetToCompute(verticesHash, uMeshSettings);
                        var job = new LitMeshConversionJob(litMeshData, uMeshSettings.blobIndex, litblobs);
                        combinedJH = JobHandle.CombineDependencies(combinedJH, job.Schedule(combinedJH));
                    }
                }

                if (uMesh.blendShapeCount > 0)
                {
                    verticesHash = new Hash128((uint) uMesh.GetHashCode(), (uint) uMesh.vertexCount.GetHashCode(),
                        (uint) uMesh.subMeshCount.GetHashCode(), (uint) uMesh.blendShapeCount);
                    blendShapeContext.AssociateBlobAssetWithUnityObject(verticesHash, uMesh);
                    if (blendShapeContext.NeedToComputeBlobAsset(verticesHash))
                    {
                        var blendShapeDataCache = new UBlendShapeDataCache();
                        blendShapeDataCache.RetrieveBlendShapeData(uMesh);

                        UMeshSettings uMeshSettings = new UMeshSettings(verticesHash, uMesh, blendShapeIndex++);
                        blendShapeContext.AddBlobAssetToCompute(verticesHash, uMeshSettings);

                        var job = new BlendShapeConversionJob(uMeshSettings, blendShapeDataCache, blendshapeblobs);
                        combinedJH = JobHandle.CombineDependencies(combinedJH, job.Schedule(combinedJH));
                    }
                }
            });

            // Re-compute the new blob assets
            combinedJH.Complete();

            // Update the BlobAssetStore
            using (var simpleMeshSettings = simpleMeshContext.GetSettings(Allocator.TempJob))
            {
                for (int i = 0; i < simpleMeshSettings.Length; i++)
                {
                    simpleMeshContext.AddComputedBlobAsset(simpleMeshSettings[i].hash, simpleblobs[simpleMeshSettings[i].blobIndex]);
                }
            }
            using (var litMeshSettings = litMeshContext.GetSettings(Allocator.TempJob))
            {
                for (int i = 0; i < litMeshSettings.Length; i++)
                {
                    litMeshContext.AddComputedBlobAsset(litMeshSettings[i].hash, litblobs[litMeshSettings[i].blobIndex]);
                }
            }
            using (var meshSettings = blendShapeContext.GetSettings(Allocator.TempJob))
            {
                for (int i = 0; i < meshSettings.Length; i++)
                {
                    blendShapeContext.AddComputedBlobAsset(meshSettings[i].hash, blendshapeblobs[meshSettings[i].blobIndex]);
                }
            }
            using (var meshSettings = skinnedMeshContext.GetSettings(Allocator.TempJob))
            {
                for (int i = 0; i < meshSettings.Length; i++)
                {
                    skinnedMeshContext.AddComputedBlobAsset(meshSettings[i].hash, skinnedMeshBlobs[meshSettings[i].blobIndex]);
                }
            }

            // Use blob assets in the conversion
            Entities.ForEach((UnityEngine.Mesh uMesh) =>
            {
                var entity = GetPrimaryEntity(uMesh);
                bool addBounds = false;
                if (DstEntityManager.HasComponent<SimpleMeshRenderData>(entity))
                {
                    Hash128 hash128 = new Hash128((uint) uMesh.GetHashCode(), (uint) uMesh.vertexCount.GetHashCode(),
                        (uint) uMesh.subMeshCount.GetHashCode(), 0);
                    simpleMeshContext.GetBlobAsset(hash128, out var blob);
                    DstEntityManager.AddComponentData(entity, new SimpleMeshRenderData()
                    {
                        Mesh = blob
                    });

                    addBounds = true;
                }
                if (DstEntityManager.HasComponent<LitMeshRenderData>(entity))
                {
                    Hash128 hash128 = new Hash128((uint) uMesh.GetHashCode(), (uint) uMesh.vertexCount.GetHashCode(),
                        (uint) uMesh.subMeshCount.GetHashCode(), 0);
                    litMeshContext.GetBlobAsset(hash128, out var blob);
                    DstEntityManager.AddComponentData(entity, new LitMeshRenderData()
                    {
                        Mesh = blob
                    });

                    addBounds = true;
                }
                if (addBounds)
                {
                    DstEntityManager.AddComponentData(entity, new MeshBounds
                    {
                        Bounds = new AABB
                        {
                            Center = uMesh.bounds.center,
                            Extents = uMesh.bounds.extents
                        }
                    });
                }
                if (uMesh.blendShapeCount > 0)
                {
                    Hash128 hash128 = new Hash128((uint) uMesh.GetHashCode(), (uint) uMesh.vertexCount.GetHashCode(),
                        (uint) uMesh.subMeshCount.GetHashCode(), (uint) uMesh.blendShapeCount);
                    blendShapeContext.GetBlobAsset(hash128, out var blendShapeBlob);
                    DstEntityManager.AddComponentData(entity, new MeshBlendShapeData()
                    {
                        BlendShapeDataRef = blendShapeBlob
                    });
                }

                if (uMesh.bindposes.Length > 0)
                {
                    var skinnedDataHash = new Hash128((uint)uMesh.GetHashCode(), (uint)uMesh.vertexCount.GetHashCode(),
                        (uint)uMesh.subMeshCount.GetHashCode(), (uint)uMesh.bindposes.Length.GetHashCode());
                    skinnedMeshContext.GetBlobAsset(skinnedDataHash, out var skinnedMeshBlob);
                    DstEntityManager.AddComponentData(entity, new SkinnedMeshRenderData()
                    {
                        SkinnedMeshDataRef = skinnedMeshBlob
                    });
                }
            });

            simpleMeshContext.Dispose();
            litMeshContext.Dispose();
            blendShapeContext.Dispose();
            simpleblobs.Dispose();
            litblobs.Dispose();
            blendshapeblobs.Dispose();
            skinnedMeshBlobs.Dispose();
        }
    }

    [UpdateInGroup(typeof(GameObjectConversionGroup))]
    [UpdateAfter(typeof(SkinnedMeshRendererConversion))]
    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    public class CleanupAfterConvertSkinnedMeshSystem : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((UnityEngine.Mesh uMesh) =>
            {
                Entity meshEntity = GetPrimaryEntity(uMesh);
                if (DstEntityManager.HasComponent<GPUSkinnedMeshDrawRange>(meshEntity))
                {
                    DstEntityManager.RemoveComponent<GPUSkinnedMeshDrawRange>(meshEntity);
                }
            });
        }
    }
}
