using System;
using Unity.Assertions;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Unity.Tiny.Rendering;
using Unity.Mathematics;
using SkinnedMeshRenderer = UnityEngine.SkinnedMeshRenderer;
using SkinQuality = Unity.Tiny.Rendering.SkinQuality;

namespace Unity.TinyConversion
{
    [UpdateInGroup(typeof(GameObjectDeclareReferencedObjectsGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    [ConverterVersion("christine-johnson", 1)]
    public class SkinnedMeshRendererDeclareAssets : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((UnityEngine.SkinnedMeshRenderer uSkinnedMeshRenderer) =>
            {
                foreach (Material mat in uSkinnedMeshRenderer.sharedMaterials)
                {
                    DeclareReferencedAsset(mat);
                    DeclareAssetDependency(uSkinnedMeshRenderer.gameObject, mat);

                    // NOTE: we depend on the output of the Unity shader importer so we don't have to recompute shader dependencies (e.g. include files)
                    Settings.AssetImportContext.DependsOnArtifact(UnityEditor.AssetDatabase.GetAssetPath(mat.shader));

                    int[] ids = mat.GetTexturePropertyNameIDs();
                    for (int i = 0; i < ids.Length; i++)
                    {
                        var texture = mat.GetTexture(ids[i]);
                        if (texture != null)
                            DeclareAssetDependency(uSkinnedMeshRenderer.gameObject, texture);
                    }
                }

                if (uSkinnedMeshRenderer.sharedMesh == null)
                    UnityEngine.Debug.LogWarning("Missing mesh in SkinnedMeshRenderer on gameobject: " +
                                                 uSkinnedMeshRenderer.gameObject.name);

                DeclareReferencedAsset(uSkinnedMeshRenderer.sharedMesh);
                DeclareAssetDependency(uSkinnedMeshRenderer.gameObject, uSkinnedMeshRenderer.sharedMesh);
            });
        }
    }

    [UpdateInGroup(typeof(GameObjectConversionGroup))]
    [UpdateBefore(typeof(MeshConversion))]
    [UpdateAfter(typeof(MaterialConversion))]
    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    public class PrepareForMeshConversion : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((UnityEngine.SkinnedMeshRenderer uSkinnedMeshRenderer) =>
            {
                var sharedMaterials = uSkinnedMeshRenderer.sharedMaterials;
                UnityEngine.Mesh uMesh = uSkinnedMeshRenderer.sharedMesh;
                var meshEntity = GetPrimaryEntity(uMesh);

                for (int i = 0; i < uMesh.subMeshCount; i++)
                {
                    // Find the target material entity to be used for this submesh
                    Entity targetMaterial = MeshRendererConversion.FindTargetMaterialEntity(this, sharedMaterials, i);
                    if (DstEntityManager.HasComponent<LitMaterial>(targetMaterial))
                    {
                        DstEntityManager.AddComponent<LitMeshRenderData>(meshEntity);
                        DstEntityManager.RemoveComponent<SimpleMeshRenderData>(meshEntity);
                    }
                    else if (DstEntityManager.HasComponent<SimpleMaterial>(targetMaterial))
                    {
                        if (!DstEntityManager.HasComponent<LitMeshRenderData>(meshEntity))
                            DstEntityManager.AddComponent<SimpleMeshRenderData>(meshEntity);
                    }
                }
            });
        }
    }

    [UpdateInGroup(typeof(GameObjectConversionGroup))]
    [UpdateAfter(typeof(MeshConversion))]
    [UpdateAfter(typeof(MaterialConversion))]
    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    [ConverterVersion("WeixianLiu", 1)]
    public class SkinnedMeshRendererConversion : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((UnityEngine.SkinnedMeshRenderer uSkinnedMeshRenderer) =>
            {
                var sharedMaterials = uSkinnedMeshRenderer.sharedMaterials;
                UnityEngine.Mesh uMesh = uSkinnedMeshRenderer.sharedMesh;
                var meshEntity = GetPrimaryEntity(uMesh);

                for (int i = 0; i < uMesh.subMeshCount; i++)
                {
                    Entity targetMaterial = MeshRendererConversion.FindTargetMaterialEntity(this, sharedMaterials, i);
                    ConvertOriginalSubMesh(this, uSkinnedMeshRenderer, uMesh, meshEntity, i, targetMaterial);
                }

                if (DstEntityManager.HasComponent<GPUSkinnedMeshDrawRange>(meshEntity))
                    ConvertGPUSkinnedSubMesh(this, uSkinnedMeshRenderer, uMesh, meshEntity);

                ConvertSkinnedMeshBoneInfoToTransformEntity(this, uSkinnedMeshRenderer);
            });
        }

        private static Entity GenerateMeshRendererEntity(GameObjectConversionSystem gsys,
            UnityEngine.SkinnedMeshRenderer uSkinnedMeshRenderer, Entity meshEntity, Entity materialEntity, int startIndex,
            int indexCount, bool createAdditionlEntity, bool canUseGPUSkinning, bool canUseCPUSkinning)
        {
            Entity primarySkinnedMeshRenderer = gsys.GetPrimaryEntity(uSkinnedMeshRenderer);
            Entity meshRendererEntity = primarySkinnedMeshRenderer;

            if (createAdditionlEntity)
            {
                meshRendererEntity = gsys.CreateAdditionalEntity(uSkinnedMeshRenderer);
                MeshRendererConversion.AddTransformComponent(gsys, primarySkinnedMeshRenderer, meshRendererEntity);
            }

            Unity.Tiny.Rendering.SkinnedMeshRenderer smr = new Unity.Tiny.Rendering.SkinnedMeshRenderer();
            smr.sharedMesh = meshEntity;
            smr.material = materialEntity;
            smr.startIndex = startIndex;
            smr.indexCount = indexCount;
            smr.canUseGPUSkinning = canUseGPUSkinning;
            smr.canUseCPUSkinning = canUseCPUSkinning;
            smr.shadowCastingMode = (Unity.Tiny.Rendering.ShadowCastingMode) uSkinnedMeshRenderer.shadowCastingMode;
            smr.skinQuality = ConvertSkinQuality(uSkinnedMeshRenderer);
            gsys.DstEntityManager.AddComponentData(meshRendererEntity, smr);

            gsys.DstEntityManager.AddComponentData(meshRendererEntity, new WorldBounds());
            return meshRendererEntity;
        }

        private static SkinQuality ConvertSkinQuality(SkinnedMeshRenderer uSkinnedMeshRenderer)
        {
            int boneCount = (int) uSkinnedMeshRenderer.quality;
            if (uSkinnedMeshRenderer.quality == UnityEngine.SkinQuality.Auto)
                boneCount = (int) QualitySettings.skinWeights;

            if (boneCount > (int) SkinQuality.Bone4)
                return SkinQuality.Bone4;

            return (SkinQuality) boneCount;
        }

        private static void ConvertBlendShapeData(GameObjectConversionSystem gsys,
            UnityEngine.SkinnedMeshRenderer uSkinnedMeshRenderer, Entity meshRendererEntity)
        {
            Mesh uMesh = uSkinnedMeshRenderer.sharedMesh;
            if (uMesh.blendShapeCount <= 0)
                return;
            DynamicBuffer<BlendShapeWeight> blendShapeWeightsBuffer =
                gsys.DstEntityManager.AddBuffer<BlendShapeWeight>(meshRendererEntity);
            for (int shapeIndex = 0; shapeIndex < uMesh.blendShapeCount; shapeIndex++)
            {
                string blendShapeName = uMesh.GetBlendShapeName(shapeIndex);
                float curWeight = uSkinnedMeshRenderer.GetBlendShapeWeight(shapeIndex);

                BlendShapeWeight blendShapeWeight = new BlendShapeWeight();
                blendShapeWeight.NameHash = BlendShapeChannel.GetNameHash(blendShapeName);
                blendShapeWeight.CurWeight = curWeight;
                blendShapeWeightsBuffer.Add(blendShapeWeight);
            }
        }

        private static void ConvertOriginalSubMesh(GameObjectConversionSystem gsys,
            UnityEngine.SkinnedMeshRenderer uSkinnedMeshRenderer, UnityEngine.Mesh uMesh, Entity meshEntity, int subMeshIndex,
            Entity materialEntity)
        {
            int boneCount = uSkinnedMeshRenderer.bones.Length;
            bool canUseCPUSkinning = boneCount > 0;
            bool canUseGPUSkinning = boneCount > 0 && boneCount <= MeshSkinningConfig.GPU_SKINNING_MAX_BONES;

            int startIndex = Convert.ToUInt16(uMesh.GetIndexStart(subMeshIndex));
            int indexCount = Convert.ToUInt16(uMesh.GetIndexCount(subMeshIndex));
            Entity meshRendererEntity = GenerateMeshRendererEntity(gsys, uSkinnedMeshRenderer, meshEntity, materialEntity,
                startIndex, indexCount, subMeshIndex > 0, canUseGPUSkinning, canUseCPUSkinning);

            ConvertBlendShapeData(gsys, uSkinnedMeshRenderer, meshRendererEntity);

            DynamicBuffer<SkinnedMeshBoneRef>
                smbrBuffer = gsys.DstEntityManager.AddBuffer<SkinnedMeshBoneRef>(meshRendererEntity);
            for (int i = 0; i < boneCount; i++)
            {
                smbrBuffer.Add(new SkinnedMeshBoneRef
                {
                    bone = gsys.GetPrimaryEntity(uSkinnedMeshRenderer.bones[i]),
                });
            }

            var isLit = gsys.DstEntityManager.HasComponent<LitMaterial>(materialEntity);
            var isSimple = gsys.DstEntityManager.HasComponent<SimpleMaterial>(materialEntity);
            if (isLit || isSimple)
            {
                if (isLit)
                    gsys.DstEntityManager.AddComponent<LitMeshRenderer>(meshRendererEntity);
                else if (isSimple)
                    gsys.DstEntityManager.AddComponent<SimpleMeshRenderer>(meshRendererEntity);
            }
        }

        private static void ConvertGPUSkinnedSubMesh(GameObjectConversionSystem gsys,
            UnityEngine.SkinnedMeshRenderer uSkinnedMeshRenderer, UnityEngine.Mesh uMesh, Entity meshEntity)
        {
            //Get the gpu draw range And calc the bone reference for the draw range
            var sharedMaterials = uSkinnedMeshRenderer.sharedMaterials;
            NativeArray<GPUSkinnedMeshDrawRange> gsmdrBuffer =
                gsys.DstEntityManager.GetBuffer<GPUSkinnedMeshDrawRange>(meshEntity).ToNativeArray(Allocator.Temp);
            NativeArray<OriginalVertexBoneIndex> ovbiBuffer =
                gsys.DstEntityManager.GetBuffer<OriginalVertexBoneIndex>(meshEntity).ToNativeArray(Allocator.Temp);
            SkinnedMeshRenderData skinnedMeshRenderData =
                gsys.DstEntityManager.GetComponentData<SkinnedMeshRenderData>(meshEntity);
            ref BlobArray<SkinnedMeshVertex> skinnedVertices = ref skinnedMeshRenderData.SkinnedMeshDataRef.Value.Vertices;
            int startIndex = 0;
            for (int i = 0; i < gsmdrBuffer.Length; i++)
            {
                int endIndex = gsmdrBuffer[i].TriangleIndex;
                int[] boneRefs = new int[MeshSkinningConfig.GPU_SKINNING_MAX_BONES];
                for (int j = 0; j < MeshSkinningConfig.GPU_SKINNING_MAX_BONES; j++)
                    boneRefs[j] = 0;

                Entity materialEntity =
                    MeshRendererConversion.FindTargetMaterialEntity(gsys, sharedMaterials, gsmdrBuffer[i].SubMeshIndex);
                var isLit = gsys.DstEntityManager.HasComponent<LitMaterial>(materialEntity);
                var isSimple = gsys.DstEntityManager.HasComponent<SimpleMaterial>(materialEntity);

                for (int index = startIndex; index < endIndex; index++)
                {
                    int vertexIndex = -1;
                    if (gsys.DstEntityManager.HasComponent<LitMeshRenderData>(meshEntity))
                    {
                        LitMeshRenderData litMeshRenderData = gsys.DstEntityManager.GetComponentData<LitMeshRenderData>(meshEntity);
                        ref LitMeshData litMeshData = ref litMeshRenderData.Mesh.Value;
                        vertexIndex = litMeshData.Indices[index];
                    }
                    else if (gsys.DstEntityManager.HasComponent<SimpleMeshRenderData>(meshEntity))
                    {
                        SimpleMeshRenderData simpleMeshRenderData = gsys.DstEntityManager.GetComponentData<SimpleMeshRenderData>(meshEntity);
                        ref SimpleMeshData simpleMeshData = ref simpleMeshRenderData.Mesh.Value;
                        vertexIndex = simpleMeshData.Indices[index];
                    }
                    Assert.IsTrue(vertexIndex != -1);

                    SkinnedMeshVertex skinnedVertex = skinnedVertices[vertexIndex];
                    OriginalVertexBoneIndex originalVertexBoneIndex = ovbiBuffer[vertexIndex];
                    if (math.abs(skinnedVertex.BoneWeight.x) > float.Epsilon)
                        boneRefs[(int) skinnedVertex.BoneIndex.x] = (int) originalVertexBoneIndex.BoneIndex.x;
                    if (math.abs(skinnedVertex.BoneWeight.y) > float.Epsilon)
                        boneRefs[(int) skinnedVertex.BoneIndex.y] = (int) originalVertexBoneIndex.BoneIndex.y;
                    if (math.abs(skinnedVertex.BoneWeight.z) > float.Epsilon)
                        boneRefs[(int) skinnedVertex.BoneIndex.z] = (int) originalVertexBoneIndex.BoneIndex.z;
                    if (math.abs(skinnedVertex.BoneWeight.w) > float.Epsilon)
                        boneRefs[(int) skinnedVertex.BoneIndex.w] = (int) originalVertexBoneIndex.BoneIndex.w;
                }

                int indexCount = endIndex - startIndex;
                Entity meshRendererEntity = GenerateMeshRendererEntity(gsys, uSkinnedMeshRenderer, meshEntity, materialEntity,
                    startIndex, indexCount, true, true, false);

                ConvertBlendShapeData(gsys, uSkinnedMeshRenderer, meshRendererEntity);
                gsys.DstEntityManager.AddBuffer<SkinnedMeshBoneRef>(meshRendererEntity);
                for (int j = 0; j < boneRefs.Length; j++)
                {
                    gsys.DstEntityManager.GetBuffer<SkinnedMeshBoneRef>(meshRendererEntity).Add(new SkinnedMeshBoneRef
                    {
                        bone = gsys.GetPrimaryEntity(uSkinnedMeshRenderer.bones[boneRefs[j]]),
                    });
                }

                if (isLit)
                    gsys.DstEntityManager.AddComponent<LitMeshRenderer>(meshRendererEntity);
                else if (isSimple)
                    gsys.DstEntityManager.AddComponent<SimpleMeshRenderer>(meshRendererEntity);
                startIndex = endIndex;
            }
        }

        private static void ConvertSkinnedMeshBoneInfoToTransformEntity(GameObjectConversionSystem gsys,
            UnityEngine.SkinnedMeshRenderer uSkinnedMeshRenderer)
        {
            UnityEngine.Mesh mesh = uSkinnedMeshRenderer.sharedMesh;
            Entity skinnedMeshRendererEntity = gsys.GetPrimaryEntity(uSkinnedMeshRenderer);
            for (int i = 0; i < uSkinnedMeshRenderer.bones.Length; i++)
            {
                Entity transformEntity = gsys.GetPrimaryEntity(uSkinnedMeshRenderer.bones[i]);
                gsys.DstEntityManager.AddComponentData(transformEntity, new SkinnedMeshBoneInfo
                {
                    smrEntity = skinnedMeshRendererEntity,
                    bindpose = mesh.bindposes[i],
                });
            }
        }
    }
}
