using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unity.Tiny.Rendering
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public unsafe class ConvertMeshAssetToDynamicMeshSystem : SystemBase
    {
        protected void ConvertMeshRenderDataToDynamicMeshData(EntityCommandBuffer ecb, Entity smrEntity, SkinnedMeshRenderer smr,
            bool needSkinningData)
        {
            if (smr.dynamicMesh != Entity.Null)
                return;

            Entity meshEntity = smr.sharedMesh;
            Entity dynamicMeshEntity = ecb.CreateEntity();
            smr.dynamicMesh = dynamicMeshEntity;
            ecb.SetComponent(smrEntity, smr);

            if (EntityManager.HasComponent<LitMeshRenderData>(meshEntity))
            {
                LitMeshRenderData litMeshRenderData = EntityManager.GetComponentData<LitMeshRenderData>(meshEntity);
                ref LitMeshData litMeshData = ref litMeshRenderData.Mesh.Value;
                int indicesCount = litMeshData.Indices.Length;
                int verticesCount = litMeshData.Vertices.Length;

                DynamicMeshData dmd = new DynamicMeshData
                {
                    Dirty = true,
                    IndexCapacity = indicesCount,
                    VertexCapacity = verticesCount,
                    NumIndices = indicesCount,
                    NumVertices = verticesCount,
                    UseDynamicGPUBuffer = true,
                    CopyFrom = needSkinningData ? meshEntity : Entity.Null
                };
                ecb.AddComponent(dynamicMeshEntity, dmd);

                DynamicBuffer<DynamicLitVertex> dlvBuffer = ecb.AddBuffer<DynamicLitVertex>(dynamicMeshEntity);
                dlvBuffer.ResizeUninitialized(verticesCount);
                void* verticesPtr = litMeshData.Vertices.GetUnsafePtr();
                byte* dlvBufferPtr = (byte*) dlvBuffer.GetUnsafePtr();
                int vertexSize = UnsafeUtility.SizeOf<LitVertex>();
                UnsafeUtility.MemCpy(dlvBufferPtr, verticesPtr, verticesCount * vertexSize);

                DynamicBuffer<DynamicIndex> diBuffer = ecb.AddBuffer<DynamicIndex>(dynamicMeshEntity);
                diBuffer.ResizeUninitialized(indicesCount);
                void* indicesPtr = litMeshData.Indices.GetUnsafePtr();
                void* dlBufferPtr = diBuffer.GetUnsafePtr();
                int indexSize = UnsafeUtility.SizeOf<DynamicIndex>();
                UnsafeUtility.MemCpy(dlBufferPtr, indicesPtr, indicesCount * indexSize);
            }
            else if (EntityManager.HasComponent<SimpleMeshRenderData>(meshEntity))
            {
                SimpleMeshRenderData simpleMeshRenderData = EntityManager.GetComponentData<SimpleMeshRenderData>(meshEntity);
                ref SimpleMeshData simpleMeshData = ref simpleMeshRenderData.Mesh.Value;
                int indicesCount = simpleMeshData.Indices.Length;
                int verticesCount = simpleMeshData.Vertices.Length;

                DynamicMeshData dmd = new DynamicMeshData
                {
                    Dirty = true,
                    IndexCapacity = indicesCount,
                    VertexCapacity = verticesCount,
                    NumIndices = indicesCount,
                    NumVertices = verticesCount,
                    UseDynamicGPUBuffer = true,
                    CopyFrom = needSkinningData ? meshEntity : Entity.Null
                };
                ecb.AddComponent(dynamicMeshEntity, dmd);

                DynamicBuffer<DynamicSimpleVertex> dsvBuffer = ecb.AddBuffer<DynamicSimpleVertex>(dynamicMeshEntity);
                dsvBuffer.ResizeUninitialized(verticesCount);
                void* verticesPtr = simpleMeshData.Vertices.GetUnsafePtr();
                byte* dlvBufferPtr = (byte*) dsvBuffer.GetUnsafePtr();
                int vertexSize = UnsafeUtility.SizeOf<SimpleVertex>();
                UnsafeUtility.MemCpy(dlvBufferPtr, verticesPtr, verticesCount * vertexSize);

                DynamicBuffer<DynamicIndex> diBuffer = ecb.AddBuffer<DynamicIndex>(dynamicMeshEntity);
                diBuffer.ResizeUninitialized(indicesCount);
                void* indicesPtr = simpleMeshData.Indices.GetUnsafePtr();
                void* dlBufferPtr = diBuffer.GetUnsafePtr();
                int indexSize = UnsafeUtility.SizeOf<DynamicIndex>();
                UnsafeUtility.MemCpy(dlBufferPtr, indicesPtr, indicesCount * indexSize);
            }
        }

        protected void DeleteDynamicMeshData(EntityCommandBuffer ecb, Entity smrEntity, SkinnedMeshRenderer smr)
        {
            if (smr.dynamicMesh == Entity.Null)
                return;

            ecb.DestroyEntity(smr.dynamicMesh);
            smr.dynamicMesh = Entity.Null;
            ecb.SetComponent(smrEntity, smr);
        }

        //if bonecount == 0
        //type        canUseCPUSkinning   canUseGPUSkinning
        //original    false                false

        //if bonecount <= MeshSkinningConfig.GPU_SKINNING_MAX_BONES, then one SkinnedMeshRenderer may split to
        //type        canUseCPUSkinning   canUseGPUSkinning
        //original    true                true

        //if bonecount > MeshSkinningConfig.GPU_SKINNING_MAX_BONES, then one SkinnedMeshRenderer may split to servals
        //type        canUseCPUSkinning   canUseGPUSkinning
        //original    true                false
        //additional  false               true
        //additional  false               true
        //additional  false               true

        protected override void OnUpdate()
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
            DisplayInfo di = GetSingleton<DisplayInfo>();
            Entities.ForEach((Entity e, ref SkinnedMeshRenderer smr) =>
            {
                bool hasBlendShape = EntityManager.HasComponent<BlendShapeWeight>(e);
                if (hasBlendShape && !smr.canUseCPUSkinning && !smr.canUseGPUSkinning) //no mesh skinning, only blend shape
                {
                    ConvertMeshRenderDataToDynamicMeshData(ecb, e, smr, false);
                    return;
                }

                if (di.gpuSkinning)
                {
                    if (smr.canUseCPUSkinning && !hasBlendShape)
                        DeleteDynamicMeshData(ecb, e, smr);

                    if (smr.canUseGPUSkinning && hasBlendShape)
                        ConvertMeshRenderDataToDynamicMeshData(ecb, e, smr, true);
                }
                else
                {
                    if (smr.canUseCPUSkinning || (hasBlendShape && !smr.canUseGPUSkinning))
                        ConvertMeshRenderDataToDynamicMeshData(ecb, e, smr, false);

                    if (!smr.canUseCPUSkinning && smr.canUseGPUSkinning)
                        DeleteDynamicMeshData(ecb, e, smr);
                }
            }).WithoutBurst().Run();
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ConvertMeshAssetToDynamicMeshSystem))]
    public class CalcMeshBoneMatrixSystem : SystemBase
    {
        [BurstCompile]
        unsafe struct CalcMeshBoneMatrixJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<LocalToWorld> LocalToWorldType;
            public ComponentTypeHandle<SkinnedMeshBoneInfo> SkinnedMeshBoneInfoType;
            [ReadOnly] public ComponentDataFromEntity<LocalToWorld> ComponentDataFromEntityLocalToWorld;

            public unsafe void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                NativeArray<SkinnedMeshBoneInfo> chunkSkinnedMeshBoneInfo = chunk.GetNativeArray(SkinnedMeshBoneInfoType);
                NativeArray<LocalToWorld> chunkLocalToWorld = chunk.GetNativeArray(LocalToWorldType);

                int chunkCount = chunk.Count;
                for (int entityIndex = 0; entityIndex < chunkCount; entityIndex++)
                {
                    SkinnedMeshBoneInfo smbi = chunkSkinnedMeshBoneInfo[entityIndex];
                    float4x4 smrLocalToWorld = ComponentDataFromEntityLocalToWorld[smbi.smrEntity].Value;
                    float4x4 smrWorldToLocal = math.inverse(smrLocalToWorld);
                    float4x4 boneLocalToWorld = chunkLocalToWorld[entityIndex].Value;
                    smbi.bonematrix = math.mul(smrWorldToLocal, boneLocalToWorld);
                    smbi.bonematrix = math.mul(smbi.bonematrix, smbi.bindpose);
                    chunkSkinnedMeshBoneInfo[entityIndex] = smbi;
                }
            }
        }

        protected override void OnUpdate()
        {
            CalcMeshBoneMatrixJob calcJob = new CalcMeshBoneMatrixJob()
            {
                LocalToWorldType = GetComponentTypeHandle<LocalToWorld>(true),
                SkinnedMeshBoneInfoType = GetComponentTypeHandle<SkinnedMeshBoneInfo>(),
                ComponentDataFromEntityLocalToWorld = GetComponentDataFromEntity<LocalToWorld>(),
            };

            this.Dependency = calcJob.Schedule(m_query, this.Dependency);
        }

        EntityQuery m_query;
        protected override void OnCreate()
        {
            m_query = GetEntityQuery(
                ComponentType.ReadWrite<SkinnedMeshBoneInfo>()
            );
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CalcMeshBoneMatrixSystem))]
    public unsafe class CPUMeshSkinningSystem : SystemBase
    {
        [BurstCompile]
        unsafe struct CPUMeshSkinningJob : IJobChunk
        {
            //skinned mesh renderer info
            [ReadOnly] public BufferTypeHandle<SkinnedMeshBoneRef> SkinnedMeshBoneRefType;
            [ReadOnly] public ComponentTypeHandle<SkinnedMeshRenderer> SkinnedMeshRendererType;
            //dynamic mesh data
            [NativeDisableContainerSafetyRestriction]
            public ComponentDataFromEntity<DynamicMeshData> ComponentDynamicMeshData;
            [NativeDisableContainerSafetyRestriction]
            public BufferFromEntity<DynamicLitVertex> BufferDynamicLitVertex;
            [NativeDisableContainerSafetyRestriction]
            public BufferFromEntity<DynamicSimpleVertex> BufferDynamicSimpleVertex;
            //all skinned mesh render data and bone info
            [ReadOnly] public ComponentDataFromEntity<SkinnedMeshRenderData> ComponentSkinnedMeshRenderData;
            [ReadOnly] public ComponentDataFromEntity<SkinnedMeshBoneInfo> ComponentSkinnedMeshBoneInfo;
            [ReadOnly] public BufferFromEntity<OriginalVertexBoneIndex> BufferOriginalBoneIndex;
            //lit skinned mesh data
            [ReadOnly] public ComponentDataFromEntity<LitMeshRenderData> ComponentLitMeshRenderData;
            //simple skinned mesh data
            [ReadOnly] public ComponentDataFromEntity<SimpleMeshRenderData> ComponentSimpleMeshRenderData;

            private float4x4 GetBoneMatrix(DynamicBuffer<SkinnedMeshBoneRef> smbrBuffer, int boneIndex)
            {
                SkinnedMeshBoneRef boneRef = smbrBuffer[boneIndex];
                SkinnedMeshBoneInfo boneInfo = ComponentSkinnedMeshBoneInfo[boneRef.bone];
                return boneInfo.bonematrix;
            }

            public float4x4 GetSkinningMatrix(DynamicBuffer<SkinnedMeshBoneRef> smbrBuffer, SkinnedMeshRenderer skinnedMeshRenderer,
                ref SkinnedMeshData skinnedMeshData, int vertexIndex, bool hasOriginalBoneIndex)
            {
                float4x4 mat = float4x4.zero;
                SkinnedMeshVertex skinnedVertex = skinnedMeshData.Vertices[vertexIndex];
                float4 boneWeight = skinnedVertex.BoneWeight;
                float4 boneIndex = float4.zero;
                if (!hasOriginalBoneIndex)
                    boneIndex = skinnedVertex.BoneIndex;
                else
                    boneIndex = BufferOriginalBoneIndex[skinnedMeshRenderer.sharedMesh][vertexIndex].BoneIndex;
                switch (skinnedMeshRenderer.skinQuality)
                {
                    case SkinQuality.Bone1:
                        mat = GetBoneMatrix(smbrBuffer, (int) boneIndex.x);
                        break;
                    case SkinQuality.Bone2:
                        float4x4 boneMatrix = GetBoneMatrix(smbrBuffer, (int) boneIndex.x);
                        float invSum = 1 / (boneWeight.x + boneWeight.y);
                        float4x4 matX = boneWeight.x * invSum * boneMatrix;
                        boneMatrix = GetBoneMatrix(smbrBuffer, (int) boneIndex.y);
                        float4x4 matY = boneWeight.y * invSum * boneMatrix;
                        mat = matX + matY;
                        break;
                    case SkinQuality.Bone4:
                        boneMatrix = GetBoneMatrix(smbrBuffer, (int) boneIndex.x);
                        matX = boneWeight.x * boneMatrix;
                        boneMatrix = GetBoneMatrix(smbrBuffer, (int) boneIndex.y);
                        matY = boneWeight.y * boneMatrix;
                        boneMatrix = GetBoneMatrix(smbrBuffer, (int) boneIndex.z);
                        float4x4 matZ = boneWeight.z * boneMatrix;
                        boneMatrix = GetBoneMatrix(smbrBuffer, (int) boneIndex.w);
                        float4x4 matW = boneWeight.w * boneMatrix;
                        mat = matX + matY + matZ + matW;
                        break;
                }

                return mat;
            }

            public unsafe void Skinning(ref DynamicBuffer<DynamicSimpleVertex> retBuffer, ref BlobArray<SimpleVertex> staticVertices,
                DynamicBuffer<SkinnedMeshBoneRef> smbrBuffer, SkinnedMeshRenderer skinnedMeshRenderer, ref SkinnedMeshData skinnedMeshData)
            {
                int vertexCount = staticVertices.Length;
                bool hasOriginalBoneIndex = BufferOriginalBoneIndex.HasComponent(skinnedMeshRenderer.sharedMesh);
                for (int i = 0; i < vertexCount; i++)
                {
                    DynamicSimpleVertex dynamicVertex = retBuffer[i];
                    SimpleVertex staticVertex = staticVertices[i];
                    float4x4 mat = GetSkinningMatrix(smbrBuffer, skinnedMeshRenderer, ref skinnedMeshData, i, hasOriginalBoneIndex);
                    float4 retPosition = math.mul(mat, new float4(staticVertex.Position, 1));
                    dynamicVertex.Value.Position = retPosition.xyz;
                    retBuffer[i] = dynamicVertex;
                }
            }

            public unsafe void Skinning(ref DynamicBuffer<DynamicLitVertex> retBuffer, ref BlobArray<LitVertex> staticVertices,
                DynamicBuffer<SkinnedMeshBoneRef> smbrBuffer, SkinnedMeshRenderer skinnedMeshRenderer, ref SkinnedMeshData skinnedMeshData)
            {
                int vertexCount = staticVertices.Length;
                bool hasOriginalBoneIndex = BufferOriginalBoneIndex.HasComponent(skinnedMeshRenderer.sharedMesh);
                for (int i = 0; i < vertexCount; i++)
                {
                    DynamicLitVertex dynamicVertex = retBuffer[i];
                    LitVertex staticVertex = staticVertices[i];
                    float4x4 mat = GetSkinningMatrix(smbrBuffer, skinnedMeshRenderer, ref skinnedMeshData, i, hasOriginalBoneIndex);
                    float4 retPosition = math.mul(mat, new float4(staticVertex.Position, 1));
                    float4 retNormal = math.mul(mat, new float4(staticVertex.Normal, 1));
                    dynamicVertex.Value.Position = retPosition.xyz;
                    dynamicVertex.Value.Normal = retNormal.xyz;
                    retBuffer[i] = dynamicVertex;
                }
            }

            public unsafe void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                BufferAccessor<SkinnedMeshBoneRef> smbrBufferAccessor = chunk.GetBufferAccessor(SkinnedMeshBoneRefType);
                NativeArray<SkinnedMeshRenderer> chunkSkinnedMeshRenderer = chunk.GetNativeArray(SkinnedMeshRendererType);
                for (int j = 0; j < chunk.Count; j++)
                {
                    SkinnedMeshRenderer skinnedMeshRenderer = chunkSkinnedMeshRenderer[j];
                    if (!skinnedMeshRenderer.canUseCPUSkinning || skinnedMeshRenderer.dynamicMesh == Entity.Null)
                        continue;

                    DynamicBuffer<SkinnedMeshBoneRef> smbrBuffer = smbrBufferAccessor[j];

                    SkinnedMeshRenderData skinnedMeshRenderData = ComponentSkinnedMeshRenderData[skinnedMeshRenderer.sharedMesh];
                    ref SkinnedMeshData skinnedMeshData = ref skinnedMeshRenderData.SkinnedMeshDataRef.Value;

                    bool isLit = ComponentLitMeshRenderData.HasComponent(skinnedMeshRenderer.sharedMesh);
                    if (isLit)
                    {
                        LitMeshRenderData litMeshRenderData = ComponentLitMeshRenderData[skinnedMeshRenderer.sharedMesh];
                        ref LitMeshData litMeshData = ref litMeshRenderData.Mesh.Value;
                        DynamicBuffer<DynamicLitVertex> dlvBuffer = BufferDynamicLitVertex[skinnedMeshRenderer.dynamicMesh];
                        Skinning(ref dlvBuffer, ref litMeshData.Vertices, smbrBuffer, skinnedMeshRenderer, ref skinnedMeshData);
                    }
                    else
                    {
                        SimpleMeshRenderData simpleMeshRenderData = ComponentSimpleMeshRenderData[skinnedMeshRenderer.sharedMesh];
                        ref SimpleMeshData simpleMeshData = ref simpleMeshRenderData.Mesh.Value;
                        DynamicBuffer<DynamicSimpleVertex> dsvBuffer = BufferDynamicSimpleVertex[skinnedMeshRenderer.dynamicMesh];
                        Skinning(ref dsvBuffer, ref simpleMeshData.Vertices, smbrBuffer, skinnedMeshRenderer, ref skinnedMeshData);
                    }

                    DynamicMeshData dmd = ComponentDynamicMeshData[skinnedMeshRenderer.dynamicMesh];
                    dmd.Dirty = true;
                    ComponentDynamicMeshData[skinnedMeshRenderer.dynamicMesh] = dmd;
                }
            }
        }

        protected override void OnUpdate()
        {
            DisplayInfo di = GetSingleton<DisplayInfo>();
            if (di.gpuSkinning)
                return;

            CPUMeshSkinningJob skinningJob = new CPUMeshSkinningJob()
            {
                SkinnedMeshBoneRefType = GetBufferTypeHandle<SkinnedMeshBoneRef>(true),
                SkinnedMeshRendererType = GetComponentTypeHandle<SkinnedMeshRenderer>(true),
                ComponentDynamicMeshData = GetComponentDataFromEntity<DynamicMeshData>(),
                BufferDynamicLitVertex = GetBufferFromEntity<DynamicLitVertex>(),
                BufferDynamicSimpleVertex = GetBufferFromEntity<DynamicSimpleVertex>(),
                ComponentSkinnedMeshRenderData = GetComponentDataFromEntity<SkinnedMeshRenderData>(true),
                ComponentSkinnedMeshBoneInfo = GetComponentDataFromEntity<SkinnedMeshBoneInfo>(true),
                BufferOriginalBoneIndex = GetBufferFromEntity<OriginalVertexBoneIndex>(true),
                ComponentLitMeshRenderData = GetComponentDataFromEntity<LitMeshRenderData>(true),
                ComponentSimpleMeshRenderData = GetComponentDataFromEntity<SimpleMeshRenderData>(true)
            };

            this.Dependency = skinningJob.Schedule(m_query, this.Dependency);
        }

        EntityQuery m_query;

        protected override void OnCreate()
        {
            m_query = GetEntityQuery(
                ComponentType.ReadOnly<SkinnedMeshBoneRef>(),
                ComponentType.ReadOnly<SkinnedMeshRenderer>()
            );
        }
    }
}
