using UnityEngine;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using Unity.Entities;
using Unity.Tiny.Rendering;
using Unity.Mathematics;
using UnityEditor;
using Unity.Build;
using System.Collections.Generic;
using Unity.Build.DotsRuntime;
using Unity.Build.Common;
using Unity.PerformanceTesting;
using MeshRenderer = Unity.Tiny.Rendering.MeshRenderer;

namespace Unity.Tiny.Authoring.Tests
{
    class MeshConversionTests
    {
        protected World world;
        protected EntityManager entityManager;
        protected GameObjectConversionSettings settings;
        protected BlobAssetStore blobAssetStore;
        protected Scene scene;

        [SetUp]
        public void Init()
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            SceneManager.SetActiveScene(scene);
            world = World.DefaultGameObjectInjectionWorld = new World("Test World");
            entityManager = world.EntityManager;
            blobAssetStore = new BlobAssetStore();

            settings = GameObjectConversionSettings.FromWorld(world, blobAssetStore);
            settings.BuildConfiguration = BuildConfiguration.CreateInstance(c => c.SetComponent(new DotsRuntimeBuildProfile ()));
            settings.BuildConfiguration.SetComponent(new SceneList());
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.CloseScene(scene, true);
            if (blobAssetStore != null)
                blobAssetStore.Dispose();
            if (world != null)
                world.Dispose();
            entityManager = default;
        }

        GameObject InitGameObjectQuad(string shaderName, int vertexCount)
        {
            GameObject quad = new GameObject();
            var meshRenderer = quad.AddComponent<UnityEngine.MeshRenderer>();
            var meshFilter = quad.AddComponent<UnityEngine.MeshFilter>();

            meshRenderer.sharedMaterial = new Material(Shader.Find(shaderName));

            // Mesh Topology:
            // x---x,x---x,x---x ...
            // |   |,|   |,|   | ...
            // x---x,x---x,x---x....
            // x---x,x---x,x---x....
            // |   |,|   |,|   |....
            // x---x,x---x,x---x....

            var mesh = new Mesh();
            List<Vector3> vertices = new List<Vector3>();
            List<int> indices = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            float subdivCount = (int)Mathf.Sqrt(vertexCount) * 0.5f;
            int index = 0;
            //Create quad with new vertices per subquad
            for (int i = 0; i < subdivCount; i++)
            {
                for (int j = 0; j < subdivCount; j++)
                {
                    vertices.Add(new Vector3(i, j, .0f));
                    vertices.Add(new Vector3(i + 1, j, .0f));
                    vertices.Add(new Vector3(i + 1, j + 1, .0f));
                    vertices.Add(new Vector3(i, j + 1, .0f));

                    indices.Add(index++);
                    indices.Add(index++);
                    indices.Add(index++);
                    indices.Add(index++);

                    uvs.Add(new Vector2(0, 0));
                    uvs.Add(new Vector2(1, 0));
                    uvs.Add(new Vector2(1, 1));
                    uvs.Add(new Vector2(0, 1));
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetIndices(indices.ToArray(), MeshTopology.Quads, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            meshFilter.sharedMesh = mesh;

            return quad;
        }

        void UpdateMesh(ref GameObject go)
        {
            var meshFilter = go.GetComponent<UnityEngine.MeshFilter>();
            var mesh = meshFilter.sharedMesh;

            List<Vector3> vertices = new List<Vector3>();
            mesh.GetVertices(vertices);

            //Add new vertices
            for (int i = 0; i < 4; i++)
                vertices.Add(new Vector3(vertices[vertices.Count - 1].x + 1, vertices[vertices.Count - 1].y + 1, vertices[vertices.Count - 1].z + 1));

            mesh.SetVertices(vertices);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            meshFilter.sharedMesh = mesh;
        }

        [Test]
        public void TinySimpleMeshConversionTest()
        {
            GameObject go = InitGameObjectQuad("Universal Render Pipeline/Unlit", 4);

            //Run GO conversion
            var entity = GameObjectConversionUtility.ConvertGameObjectHierarchy(go, settings);

            //Test comp
            Assert.IsTrue(entityManager.HasComponent<SimpleMeshRenderer>(entity));
            var entityMesh = entityManager.GetComponentData<MeshRenderer>(entity).mesh;
            Assert.IsNotNull(entityMesh);
            Assert.IsTrue(entityManager.HasComponent<SimpleMeshRenderData>(entityMesh));
            var blobAsset = entityManager.GetComponentData<SimpleMeshRenderData>(entityMesh).Mesh;

            //Test bounds
            Assert.IsTrue(entityManager.HasComponent<MeshBounds>(entityMesh));
            var bounds = entityManager.GetComponentData<MeshBounds>(entityMesh);
            Assert.IsTrue(bounds.Bounds.Center.Equals(new float3(0.5f, 0.5f, 0f)));
            Assert.IsTrue(bounds.Bounds.Extents.Equals(new float3(0.5f, 0.5f, 0f)));

            //Test vertices
            Assert.IsTrue(blobAsset.Value.Vertices[0].Position.Equals(new float3(0f, 0f, 0f)));
            Assert.IsTrue(blobAsset.Value.Vertices[1].Position.Equals(new float3(1f, 0f, 0f)));
            Assert.IsTrue(blobAsset.Value.Vertices[2].Position.Equals(new float3(1f, 1f, 0f)));
            Assert.IsTrue(blobAsset.Value.Vertices[3].Position.Equals(new float3(0f, 1f, 0f)));

            //Test indices
            Assert.IsTrue(blobAsset.Value.Indices[0].Equals(0));
            Assert.IsTrue(blobAsset.Value.Indices[1].Equals(1));
            Assert.IsTrue(blobAsset.Value.Indices[2].Equals(2));
            Assert.IsTrue(blobAsset.Value.Indices[3].Equals(3));
        }

        [Test]
        public void TinyLitMeshConversionTest()
        {
            GameObject go = InitGameObjectQuad("Universal Render Pipeline/Lit", 4);

            //Run GO conversion
            var entity = GameObjectConversionUtility.ConvertGameObjectHierarchy(go, settings);

            //Test comp
            Assert.IsTrue(entityManager.HasComponent<LitMeshRenderer>(entity));
            var entityMesh = entityManager.GetComponentData<MeshRenderer>(entity).mesh;
            Assert.IsNotNull(entityMesh);
            Assert.IsTrue(entityManager.HasComponent<LitMeshRenderData>(entityMesh));
            var blobAsset = entityManager.GetComponentData<LitMeshRenderData>(entityMesh).Mesh;

            //Test bounds
            Assert.IsTrue(entityManager.HasComponent<MeshBounds>(entityMesh));
            var bounds = entityManager.GetComponentData<MeshBounds>(entityMesh);
            Assert.IsTrue(bounds.Bounds.Center.Equals(new float3(0.5f, 0.5f, 0f)));
            Assert.IsTrue(bounds.Bounds.Extents.Equals(new float3(.5f, 0.5f, .0f)));

            //Test vertices
            Assert.IsTrue(blobAsset.Value.Vertices[0].Position.Equals(new float3(0f, 0f, 0f)));
            Assert.IsTrue(blobAsset.Value.Vertices[1].Position.Equals(new float3(1f, 0f, 0f)));
            Assert.IsTrue(blobAsset.Value.Vertices[2].Position.Equals(new float3(1f, 1f, 0f)));
            Assert.IsTrue(blobAsset.Value.Vertices[3].Position.Equals(new float3(0f, 1f, 0f)));

            Assert.IsTrue(blobAsset.Value.Vertices[0].Normal.Equals(new float3(0, 0, 1)));
            Assert.IsTrue(blobAsset.Value.Vertices[1].Normal.Equals(new float3(0, 0, 1)));
            Assert.IsTrue(blobAsset.Value.Vertices[2].Normal.Equals(new float3(0, 0, 1)));
            Assert.IsTrue(blobAsset.Value.Vertices[3].Normal.Equals(new float3(0, 0, 1)));

            Assert.IsTrue(blobAsset.Value.Vertices[0].TexCoord0.Equals(new float2(0, 1)));
            Assert.IsTrue(blobAsset.Value.Vertices[1].TexCoord0.Equals(new float2(1, 1)));
            Assert.IsTrue(blobAsset.Value.Vertices[2].TexCoord0.Equals(new float2(1, 0)));
            Assert.IsTrue(blobAsset.Value.Vertices[3].TexCoord0.Equals(new float2(0, 0)));

            Assert.IsTrue(blobAsset.Value.Vertices[0].Tangent.Equals(new float3(1, 0, 0)));
            Assert.IsTrue(blobAsset.Value.Vertices[1].Tangent.Equals(new float3(1, 0, 0)));
            Assert.IsTrue(blobAsset.Value.Vertices[2].Tangent.Equals(new float3(1, 0, 0)));
            Assert.IsTrue(blobAsset.Value.Vertices[3].Tangent.Equals(new float3(1, 0, 0)));

            //Test indices
            Assert.IsTrue(blobAsset.Value.Indices[0].Equals(0));
            Assert.IsTrue(blobAsset.Value.Indices[1].Equals(1));
            Assert.IsTrue(blobAsset.Value.Indices[2].Equals(2));
            Assert.IsTrue(blobAsset.Value.Indices[3].Equals(3));
        }

        [Test]
        public unsafe void TinyValidBlobAssetStoreTest()
        {
            //Scene1
            var scene = SceneManager.GetActiveScene();
            GameObject go1 = InitGameObjectQuad("Universal Render Pipeline/Unlit", 4);
            var entity = GameObjectConversionUtility.ConvertGameObjectHierarchy(go1, settings);

            var uMesh = go1.GetComponent<MeshFilter>().sharedMesh;
            settings.BlobAssetStore.TryGet<SimpleMeshData>(new Unity.Entities.Hash128((uint)uMesh.GetHashCode()), out var blob1);

            //Scene2
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            SceneManager.SetActiveScene(scene);
            GameObject go2 = InitGameObjectQuad("Universal Render Pipeline/Unlit", 4);
            var entity2 = GameObjectConversionUtility.ConvertGameObjectHierarchy(go2, settings);

            uMesh = go2.GetComponent<MeshFilter>().sharedMesh;
            settings.BlobAssetStore.TryGet<SimpleMeshData>(new Unity.Entities.Hash128((uint)uMesh.GetHashCode()), out var blob2);

            Assert.IsTrue(blob1.GetUnsafePtr() == blob2.GetUnsafePtr());
        }

        void MeasurePerformanceWithUpdates(List<GameObject> unlitMeshes, List<GameObject> litMeshes, int meshCount, int updateCount)
        {
            if (updateCount > meshCount)
                return;

            for (int i = 0; i < updateCount; i++)
            {
                var goUnlit = unlitMeshes[i];
                UpdateMesh(ref goUnlit);
                var goLit = litMeshes[i];
                UpdateMesh(ref goLit);
            }
            Measure.Method(() =>
            {
                for (int i = 0; i < meshCount; i++)
                {
                    GameObjectConversionUtility.ConvertGameObjectHierarchy(unlitMeshes[i], settings);
                }
            })
                .SampleGroup($"TinyIncUnLitMeshConversionPerformanceTest_After_{updateCount}_Changes")
                .MeasurementCount(1)
                .Run();

            Measure.Method(() =>
            {
                for (int i = 0; i < meshCount; i++)
                {
                    GameObjectConversionUtility.ConvertGameObjectHierarchy(litMeshes[i], settings);
                }
            })
                .SampleGroup($"TinyIncLitMeshConversionPerformanceTest_After_{updateCount}_Changes")
                .MeasurementCount(1)
                .Run();
        }

        [Test, Performance]
        public void TinyIncMeshConversionPerformanceTest_Creation()
        {
            int meshCount = 200;
            int vertexCount = 10000;
            List<GameObject> unlitMeshes = new List<GameObject>();
            List<GameObject> litMeshes = new List<GameObject>();
            for (int i = 0; i < meshCount; i++)
            {
                unlitMeshes.Add(InitGameObjectQuad("Universal Render Pipeline/Unlit", vertexCount));
                litMeshes.Add(InitGameObjectQuad("Universal Render Pipeline/Lit", vertexCount));
            }

            //Test converting all unlit meshes
            Measure.Method(() =>
            {
                for (int i = 0; i < meshCount; i++)
                {
                    GameObjectConversionUtility.ConvertGameObjectHierarchy(unlitMeshes[i], settings);
                }
            })
                .SampleGroup($"TinyIncUnLitMeshConversionPerformanceTest_Creation")
                .MeasurementCount(1)
                .Run();

            //Test converting all lit meshes
            Measure.Method(() =>
            {
                for (int i = 0; i < meshCount; i++)
                {
                    GameObjectConversionUtility.ConvertGameObjectHierarchy(litMeshes[i], settings);
                }
            })
                .SampleGroup($"TinyIncLitMeshConversionPerformanceTest_Creation")
                .MeasurementCount(1)
                .Run();
        }

        [Test, Performance]
        public void TinyIncMeshConversionPerformanceTest_After_N_Changes([Values(0, 1, 10, 100, 200)] int updateCount)
        {
            int meshCount = 200;
            int vertexCount = 10000;
            List<GameObject> unlitMeshes = new List<GameObject>();
            List<GameObject> litMeshes = new List<GameObject>();
            for (int i = 0; i < meshCount; i++)
            {
                unlitMeshes.Add(InitGameObjectQuad("Universal Render Pipeline/Unlit", vertexCount));
                litMeshes.Add(InitGameObjectQuad("Universal Render Pipeline/Lit", vertexCount));
            }

            for (int i = 0; i < meshCount; i++)
            {
                GameObjectConversionUtility.ConvertGameObjectHierarchy(unlitMeshes[i], settings);
            }

            for (int i = 0; i < meshCount; i++)
            {
                GameObjectConversionUtility.ConvertGameObjectHierarchy(litMeshes[i], settings);
            }

            //Measuring re-create n blob assets
            MeasurePerformanceWithUpdates(unlitMeshes, litMeshes, meshCount, updateCount);
        }
    }
}
