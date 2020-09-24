using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;

namespace Unity.Tiny.Rendering
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ConvertMeshAssetToDynamicMeshSystem))]
    [UpdateBefore(typeof(CPUMeshSkinningSystem))]
    public class MeshBlendShapingSystem : SystemBase
    {
        [BurstCompile]
        unsafe struct MeshBlendShapingJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<SkinnedMeshRenderer> SkinnedMeshRendererType;
            [ReadOnly] public BufferTypeHandle<BlendShapeWeight> SMRBlendShapeWeightType;
            [NativeDisableContainerSafetyRestriction]
            public BufferFromEntity<DynamicLitVertex> BufferFromDynamicLitVertex;
            [NativeDisableContainerSafetyRestriction]
            public BufferFromEntity<DynamicSimpleVertex> BufferFromDynamicSimpleVertex;
            [ReadOnly] public ComponentDataFromEntity<LitMeshRenderData> ComponentLitMeshRenderData;
            [ReadOnly] public ComponentDataFromEntity<SimpleMeshRenderData> ComponentSimpleMeshRenderData;
            [NativeDisableContainerSafetyRestriction]
            public ComponentDataFromEntity<DynamicMeshData> ComponentDataFromEntity_DynamicMeshData;
            [ReadOnly] public ComponentDataFromEntity<MeshBlendShapeData> ComponentDataFromEntity_MeshBlendShapeData;
            [ReadOnly] public EntityTypeHandle ChunkEntityType;

            public EntityCommandBuffer.ParallelWriter CommandBuffer;

            public int CalcBlendShapeFrameIndex(ref BlendShapeWeight weightData, ref BlendShapeChannel channelData)
            {
                int frameCount = channelData.Frames.Length;
                if (frameCount == 0)
                    return -1;

                ref BlendShapeFrame frame = ref channelData.Frames[0];
                if (frameCount == 1 || weightData.CurWeight <= frame.Weight)
                    return 0;

                int frameIndex = 0;
                for (frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    frame = ref channelData.Frames[frameIndex];
                    if (weightData.CurWeight <= frame.Weight)
                        return frameIndex;
                }

                return frameIndex;
            }

            public void ApplyBlendShapeToVertices(float weightInPercent, ref BlendShapeFrame frame, byte* retVerticesPtr, bool isLit)
            {
                int deltaValueLength = frame.VerticesPosition.Length;
                int vertexSize = 0;
                if (isLit)
                    vertexSize = UnsafeUtility.SizeOf<DynamicLitVertex>();
                else
                    vertexSize = UnsafeUtility.SizeOf<DynamicSimpleVertex>();
                for (int i = 0; i < deltaValueLength; i++)
                {
                    ref float3 deltaValue = ref frame.VerticesPosition[i].DeltaPosition;
                    if (isLit)
                    {
                        DynamicLitVertex* dynamicLitVertexPttr = (DynamicLitVertex*)(retVerticesPtr + i * vertexSize);
                        dynamicLitVertexPttr->Value.Position += weightInPercent * deltaValue;
                        if (frame.HasNormals)
                        {
                            deltaValue = ref frame.VerticesNormal[i].DeltaNormal;
                            dynamicLitVertexPttr->Value.Normal += weightInPercent * deltaValue;
                        }

                        if (frame.HasTangents)
                        {
                            deltaValue = ref frame.VerticesTangent[i].DeltaTangent;
                            dynamicLitVertexPttr->Value.Tangent += weightInPercent * deltaValue;
                        }
                    }
                    else
                    {
                        DynamicSimpleVertex* dynamicSimpleVertex = (DynamicSimpleVertex*)(retVerticesPtr + i * vertexSize);
                        dynamicSimpleVertex->Value.Position += weightInPercent * deltaValue;
                    }
                }
            }

            public void ApplyBlendShape(int frameIndex, ref BlendShapeWeight weightData, ref BlendShapeChannel channelData, byte* retVerticesPtr, bool isLit)
            {
                float targetWeight = weightData.CurWeight;
                if (frameIndex == 0)
                {
                    targetWeight = math.max(targetWeight, 0f);
                    ref BlendShapeFrame frame = ref channelData.Frames[frameIndex];
                    targetWeight = math.min(targetWeight, frame.Weight);
                    float relativeWeight = targetWeight / frame.Weight;
                    ApplyBlendShapeToVertices(relativeWeight, ref frame, retVerticesPtr, isLit);
                }
                else
                {
                    int leftShapeFrameIndex = frameIndex - 1;
                    int rightShapeFrameIndex = frameIndex;
                    ref BlendShapeFrame leftFrame = ref channelData.Frames[leftShapeFrameIndex];
                    ref BlendShapeFrame rightFrame = ref channelData.Frames[rightShapeFrameIndex];
                    targetWeight = math.max(targetWeight, leftFrame.Weight);
                    targetWeight = math.min(targetWeight, rightFrame.Weight);
                    float relativeWeight = (targetWeight - leftFrame.Weight) / (rightFrame.Weight - leftFrame.Weight);
                    ApplyBlendShapeToVertices(relativeWeight, ref leftFrame, retVerticesPtr, isLit);
                }
            }

            public unsafe void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                BufferAccessor<BlendShapeWeight> smrBlendShapeWeightBufferAccessor =
                    chunk.GetBufferAccessor(SMRBlendShapeWeightType);
                NativeArray<SkinnedMeshRenderer> chunkSkinnedMeshRenderer = chunk.GetNativeArray(SkinnedMeshRendererType);
                NativeArray<Entity> chunkEntities = chunk.GetNativeArray(ChunkEntityType);

                int chunkCount = chunk.Count;
                for (int entityIndex = 0; entityIndex < chunkCount; entityIndex++)
                {
                    Entity entity = chunkEntities[entityIndex];
                    CommandBuffer.AddComponent<BlendShapeUpdated>(chunkIndex, entity);

                    DynamicBuffer<BlendShapeWeight> smrBlendShapeWeightBuffer = smrBlendShapeWeightBufferAccessor[entityIndex];
                    SkinnedMeshRenderer smr = chunkSkinnedMeshRenderer[entityIndex];

                    MeshBlendShapeData meshBlendShapeData = ComponentDataFromEntity_MeshBlendShapeData[smr.sharedMesh];
                    ref BlendShapeData blendShapeData = ref meshBlendShapeData.BlendShapeDataRef.Value;

                    DynamicMeshData dynamicMeshData = ComponentDataFromEntity_DynamicMeshData[smr.dynamicMesh];
                    dynamicMeshData.Dirty = true;
                    CommandBuffer.SetComponent(chunkIndex, smr.dynamicMesh, dynamicMeshData);

                    if (ComponentLitMeshRenderData.HasComponent(smr.sharedMesh))
                    {
                        LitMeshRenderData litMeshRenderData = ComponentLitMeshRenderData[smr.sharedMesh];
                        ref LitMeshData staticMesh = ref litMeshRenderData.Mesh.Value;
                        void* originalVerticesPtr = staticMesh.Vertices.GetUnsafePtr();
                        int size = staticMesh.Vertices.Length * UnsafeUtility.SizeOf<LitVertex>();

                        DynamicBuffer<DynamicLitVertex> dlvBuffer = BufferFromDynamicLitVertex[smr.dynamicMesh];
                        void* dlvBufferPtr = dlvBuffer.GetUnsafePtr();
                        UnsafeUtility.MemCpy(dlvBufferPtr, originalVerticesPtr, size);

                        int shapeCount = smrBlendShapeWeightBuffer.Length;
                        for (int shapeIndex = 0; shapeIndex < shapeCount; shapeIndex++)
                        {
                            BlendShapeWeight weightData = smrBlendShapeWeightBuffer[shapeIndex];
                            ref BlendShapeChannel channelData = ref blendShapeData.Channels[shapeIndex];
                            int frameIndex = CalcBlendShapeFrameIndex(ref weightData, ref channelData);
                            ApplyBlendShape(frameIndex, ref weightData, ref channelData, (byte*)dlvBuffer.GetUnsafePtr(), true);
                        }
                    }
                    else
                    {
                        SimpleMeshRenderData simpleMeshRenderData = ComponentSimpleMeshRenderData[smr.sharedMesh];
                        ref SimpleMeshData simpleMeshData = ref simpleMeshRenderData.Mesh.Value;
                        void* originalVerticesPtr = simpleMeshData.Vertices.GetUnsafePtr();
                        int size = simpleMeshData.Vertices.Length * UnsafeUtility.SizeOf<SimpleVertex>();

                        DynamicBuffer<DynamicSimpleVertex> dlvBuffer = BufferFromDynamicSimpleVertex[smr.dynamicMesh];
                        void* dlvBufferPtr = dlvBuffer.GetUnsafePtr();
                        UnsafeUtility.MemCpy(dlvBufferPtr, originalVerticesPtr, size);

                        int shapeCount = smrBlendShapeWeightBuffer.Length;
                        for (int shapeIndex = 0; shapeIndex < shapeCount; shapeIndex++)
                        {
                            BlendShapeWeight weightData = smrBlendShapeWeightBuffer[shapeIndex];
                            ref BlendShapeChannel channelData = ref blendShapeData.Channels[shapeIndex];
                            int frameIndex = CalcBlendShapeFrameIndex(ref weightData, ref channelData);
                            ApplyBlendShape(frameIndex, ref weightData, ref channelData, (byte*)dlvBuffer.GetUnsafePtr(), false);
                        }
                    }
                }
            }
        }

        protected override void OnUpdate()
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
            MeshBlendShapingJob shapingJob = new MeshBlendShapingJob()
            {
                SkinnedMeshRendererType = GetComponentTypeHandle<SkinnedMeshRenderer>(true),
                SMRBlendShapeWeightType = GetBufferTypeHandle<BlendShapeWeight>(true),
                BufferFromDynamicLitVertex = GetBufferFromEntity<DynamicLitVertex>(),
                BufferFromDynamicSimpleVertex = GetBufferFromEntity<DynamicSimpleVertex>(),
                ComponentLitMeshRenderData = GetComponentDataFromEntity<LitMeshRenderData>(true),
                ComponentSimpleMeshRenderData = GetComponentDataFromEntity<SimpleMeshRenderData>(true),
                ComponentDataFromEntity_DynamicMeshData = GetComponentDataFromEntity<DynamicMeshData>(),
                ComponentDataFromEntity_MeshBlendShapeData = GetComponentDataFromEntity<MeshBlendShapeData>(true),
                ChunkEntityType = EntityManager.GetEntityTypeHandle(),
                CommandBuffer = ecb.AsParallelWriter()
            };

            JobHandle handle = shapingJob.Schedule(m_query);
            handle.Complete();
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        EntityQuery m_query;

        protected override void OnCreate()
        {
            NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
            m_query = GetEntityQuery(
                ComponentType.ReadOnly<SkinnedMeshRenderer>(),
                ComponentType.ReadOnly<BlendShapeWeight>(),
                ComponentType.Exclude<BlendShapeUpdated>()
            );
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ConvertMeshAssetToDynamicMeshSystem))]
    [UpdateBefore(typeof(MeshBlendShapingSystem))]
    public unsafe class ChangeBlendShapeWeightsSystem : ComponentSystem
    {
        protected override void OnUpdate()
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
            Entities.ForEach((Entity e, DynamicBuffer<BlendShapeWeight> currentWeights,
                DynamicBuffer<SetBlendShapeWeight> weightsToBeSet) =>
            {
                int shapeCount = currentWeights.Length;
                for (int i = 0; i < weightsToBeSet.Length; i++)
                {
                    SetBlendShapeWeight weightToBeSet = weightsToBeSet[i];
                    for (int j = 0; j < shapeCount; j++)
                    {
                        BlendShapeWeight curWeight = currentWeights[j];
                        if (curWeight.NameHash != weightToBeSet.NameHash)
                            continue;

                        float delta = math.abs(curWeight.CurWeight - weightToBeSet.ModifiedWeight);
                        if (delta <= float.Epsilon)
                            continue;

                        curWeight.CurWeight = weightToBeSet.ModifiedWeight;
                        currentWeights[j] = curWeight;
                        if (EntityManager.HasComponent<BlendShapeUpdated>(e))
                            ecb.RemoveComponent<BlendShapeUpdated>(e);
                    }
                }

                ecb.RemoveComponent<SetBlendShapeWeight>(e);
            });
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
