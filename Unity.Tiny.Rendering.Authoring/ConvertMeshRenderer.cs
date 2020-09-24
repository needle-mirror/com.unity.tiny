using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Unity.Tiny;
using Unity.Tiny.Rendering;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.Assertions;
using Unity.Entities.Runtime.Build;

namespace Unity.TinyConversion
{
    [UpdateInGroup(typeof(GameObjectDeclareReferencedObjectsGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    [ConverterVersion("christine-johnson", 1)]
    public class MeshRendererDeclareAssets : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((UnityEngine.MeshRenderer uMeshRenderer) =>
            {
                if ((uMeshRenderer.hideFlags & HideFlags.HideAndDontSave) == HideFlags.HideAndDontSave)
                    return;

                foreach (Material mat in uMeshRenderer.sharedMaterials)
                {
                    DeclareReferencedAsset(mat);
                    DeclareAssetDependency(uMeshRenderer.gameObject, mat);

                    // NOTE: we depend on the output of the Unity shader importer so we don't have to recompute shader dependencies (e.g. include files)
                    Settings.AssetImportContext.DependsOnArtifact(UnityEditor.AssetDatabase.GetAssetPath(mat.shader));

                    int[] ids = mat.GetTexturePropertyNameIDs();
                    for (int i = 0; i < ids.Length; i++)
                    {
                        var texture = mat.GetTexture(ids[i]);
                        if( texture != null)
                            DeclareAssetDependency(uMeshRenderer.gameObject, texture);
                    }
                }

                MeshFilter uMeshFilter = uMeshRenderer.gameObject.GetComponent<MeshFilter>();
                if (uMeshFilter == null)
                    UnityEngine.Debug.LogWarning("Missing MeshFilter component on gameobject " + uMeshRenderer.gameObject);

                if (uMeshFilter.sharedMesh == null)
                    UnityEngine.Debug.LogWarning("Missing mesh in MeshFilter on gameobject: " + uMeshRenderer.gameObject.name);

                DeclareReferencedAsset(uMeshFilter.sharedMesh);
                DeclareAssetDependency(uMeshRenderer.gameObject, uMeshFilter.sharedMesh);
            });
        }
    }

    [UpdateInGroup(typeof(GameObjectConversionGroup))]
    [UpdateBefore(typeof(MeshConversion))]
    [UpdateAfter(typeof(MaterialConversion))]
    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    public class MeshRendererConversion : GameObjectConversionSystem
    {
        void CheckForSubMeshLimitations(Mesh uMesh, int index)
        {
            uint indexCount = uMesh.GetIndexCount(index);
            if (indexCount > int.MaxValue)
                throw new ArgumentException($"The maximum number of indices supported per submesh is {int.MaxValue} and the submesh index {index} in {uMesh.name} has {indexCount} indices. Please use a lighter submesh instead.");
        }

        protected override void OnUpdate()
        {
            Entities.ForEach((UnityEngine.MeshRenderer uMeshRenderer) =>
            {
                if ((uMeshRenderer.hideFlags & HideFlags.HideAndDontSave) == HideFlags.HideAndDontSave)
                    return;

                UnityEngine.Mesh uMesh = uMeshRenderer.gameObject.GetComponent<MeshFilter>().sharedMesh;

                var sharedMaterials = uMeshRenderer.sharedMaterials;
                var meshEntity = GetPrimaryEntity(uMesh);

                for (int i = 0; i < uMesh.subMeshCount; i++)
                {
                    // Find the target material entity to be used for this submesh
                    Entity targetMaterial = FindTargetMaterialEntity(this, sharedMaterials, i);

                    var isLit = DstEntityManager.HasComponent<LitMaterial>(targetMaterial);
                    var isSimple = DstEntityManager.HasComponent<SimpleMaterial>(targetMaterial);

                    // We only handle these two materials here
                    if (isLit || isSimple)
                    {
                        CheckForSubMeshLimitations(uMesh, i);
                        Entity subMeshRenderer = ConvertSubmesh(this, uMeshRenderer, uMesh, meshEntity, i, targetMaterial);

                        if (isLit)
                        {
                            DstEntityManager.AddComponent<LitMeshRenderer>(subMeshRenderer);

                            DstEntityManager.AddComponent<LitMeshRenderData>(meshEntity);
                            // Remove simple data if it was there, we don't need it
                            DstEntityManager.RemoveComponent<SimpleMeshRenderData>(meshEntity);
                        }
                        else
                        {
                            DstEntityManager.AddComponent<SimpleMeshRenderer>(subMeshRenderer);

                            // Remove simple data if we have lit already
                            if (!DstEntityManager.HasComponent<LitMeshRenderData>(meshEntity))
                                DstEntityManager.AddComponent<SimpleMeshRenderData>(meshEntity);
                        }
                    }
                }
            });
        }

        // For the given MeshRenderer, find the Entity corresponding to the Material we will use to render the submesh at the given index
        public static Entity FindTargetMaterialEntity(GameObjectConversionSystem gsys, Material[] sharedMaterials, int materialIndex)
        {
            // If there are more materials than sub-meshes, the last submesh will be rendered with each of the remaining materials.
            // If there are less materials than submeshes, just use the last material on the remaining meshrenderers

            materialIndex = materialIndex < sharedMaterials.Length ? materialIndex : sharedMaterials.Length - 1;
            return gsys.GetPrimaryEntity(sharedMaterials[materialIndex]);
        }

        public static Entity ConvertSubmesh(GameObjectConversionSystem gsys, UnityEngine.MeshRenderer uMeshRenderer,
            UnityEngine.Mesh uMesh, Entity meshEntity, int subMeshIndex, Entity materialEntity)
        {
            Entity primaryMeshRenderer = gsys.GetPrimaryEntity(uMeshRenderer);
            Entity meshRendererEntity = primaryMeshRenderer;

            if (subMeshIndex > 0)
            {
                meshRendererEntity = gsys.CreateAdditionalEntity(uMeshRenderer);
                AddTransformComponent(gsys, primaryMeshRenderer, meshRendererEntity);
            }

            gsys.DstEntityManager.AddComponentData(meshRendererEntity, new Unity.Tiny.Rendering.MeshRenderer()
            {
                mesh = meshEntity,
                material = materialEntity,
                startIndex = Convert.ToInt32(uMesh.GetIndexStart(subMeshIndex)),
                indexCount = Convert.ToInt32(uMesh.GetIndexCount(subMeshIndex))
            });

            gsys.DstEntityManager.AddComponentData(meshRendererEntity, new Unity.Tiny.Rendering.CameraMask() {
                mask = (ulong)(1<<uMeshRenderer.gameObject.layer)
            });

            gsys.DstEntityManager.AddComponentData(meshRendererEntity, new WorldBounds());

            return meshRendererEntity;
        }

        public static void AddTransformComponent(GameObjectConversionSystem gsys, Entity uMeshRenderer, Entity subMeshRendererEntity)
        {
            gsys.DstEntityManager.AddComponentData<Parent>(subMeshRendererEntity, new Parent()
            {
                Value = uMeshRenderer
            });

            gsys.DstEntityManager.AddComponentData<LocalToWorld>(subMeshRendererEntity, new LocalToWorld());
            gsys.DstEntityManager.AddComponentData<LocalToParent>(subMeshRendererEntity, new LocalToParent() {
                Value = float4x4.identity
            });
        }
    }
}
