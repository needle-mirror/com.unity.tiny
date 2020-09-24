using Unity.Entities;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEditor;
using Unity.Build;
using UnityEngine;
using Unity.Tiny.Rendering;
using System.Collections.Generic;
using Unity.Build.DotsRuntime;
using Unity.Build.Common;
using Unity.Collections;

namespace Unity.Tiny.Authoring.Tests
{
    class MeshRendererConversionTests
    {
        protected World world;
        protected EntityManager entityManager;
        protected GameObjectConversionSettings settings;
        protected Scene scene;

        [SetUp]
        public void Init()
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            SceneManager.SetActiveScene(scene);
            world = World.DefaultGameObjectInjectionWorld = new World("Test World");
            entityManager = world.EntityManager;

            settings = GameObjectConversionSettings.FromWorld(world, new BlobAssetStore());
            settings.BuildConfiguration = BuildConfiguration.CreateInstance(c => c.SetComponent(new DotsRuntimeBuildProfile ()));
            settings.BuildConfiguration.SetComponent(new SceneList());
        }

        [TearDown]
        public void TearDown()
        {
            if (settings != null)
                settings.BlobAssetStore.Dispose();
            EditorSceneManager.CloseScene(scene, true);
            if (world != null)
                world.Dispose();
            entityManager = default;
        }

        GameObject InitGameObjectQuad(string shaderName)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var mr = quad.GetComponent<UnityEngine.MeshRenderer>();
            Assert.IsTrue(mr != null);

            mr.sharedMaterial = new Material(Shader.Find(shaderName));
            var meshFilter = quad.GetComponent<MeshFilter>();
            List<Vector3> v = new List<Vector3>();
            v.Add(new Vector3(-.5f, -.5f, .0f));
            v.Add(new Vector3(.5f, -.5f, .0f));
            v.Add(new Vector3(-.5f, .5f, .0f));
            v.Add(new Vector3(.5f, .5f, .0f));
            meshFilter.sharedMesh.SetVertices(v);
            meshFilter.sharedMesh.RecalculateBounds();

            return quad;
        }

        [Test]
        public void TinySubMeshesConversionTest()
        {
            GameObject go1 = InitGameObjectQuad("Universal Render Pipeline/Unlit");
            GameObject go2 = InitGameObjectQuad("Universal Render Pipeline/Lit");

            //Combine meshes to a single mesh with submeshes
            CombineInstance[] combine = new CombineInstance[2];
            combine[0].mesh = go1.GetComponent<MeshFilter>().sharedMesh;
            combine[0].transform = go1.GetComponent<Transform>().localToWorldMatrix;
            combine[1].mesh = go2.GetComponent<MeshFilter>().sharedMesh;
            combine[1].transform = go2.GetComponent<Transform>().localToWorldMatrix;

            go2.GetComponent<MeshFilter>().sharedMesh = new Mesh();
            go2.GetComponent<MeshFilter>().sharedMesh.CombineMeshes(combine, false);
            Material[] mats = new Material[2];
            mats[0] = go1.GetComponent<UnityEngine.MeshRenderer>().sharedMaterials[0];
            mats[1] = go2.GetComponent<UnityEngine.MeshRenderer>().sharedMaterials[0];
            go2.GetComponent<UnityEngine.MeshRenderer>().sharedMaterials = mats;
            go1.gameObject.SetActive(false);

            //Run conversion
            var entity = GameObjectConversionUtility.ConvertGameObjectHierarchy(go2.gameObject, settings);

            //Test mesh renderer, mesh reference, material conversion
            var query1 = entityManager.CreateEntityQuery(typeof(Unity.Tiny.Rendering.MeshRenderer));
            int num = query1.CalculateEntityCount();

            Assert.IsTrue(num == 2);
            var query2 = entityManager.CreateEntityQuery(typeof(LitMeshRenderer));
            num = query2.CalculateEntityCount();

            Assert.IsTrue(num == 1);
            query2.Dispose();
            query2 = entityManager.CreateEntityQuery(typeof(SimpleMeshRenderer));
            num = query2.CalculateEntityCount();

            Assert.IsTrue(num == 1);
            query2.Dispose();

            using (var entities = query1.ToEntityArray(Allocator.TempJob))
            {
                foreach (var e in entities)
                {
                    var mr = entityManager.GetComponentData<Unity.Tiny.Rendering.MeshRenderer>(e);
                    if (entityManager.HasComponent<LitMeshRenderer>(e))
                    {
                        Assert.IsTrue(entityManager.HasComponent<LitMaterial>(mr.material));
                        Assert.IsTrue(mr.startIndex == 6);
                        Assert.IsTrue(mr.indexCount == 6);
                    }
                    else if (entityManager.HasComponent<SimpleMeshRenderer>(e))
                    {
                        Assert.IsTrue(entityManager.HasComponent<SimpleMaterial>(mr.material));
                        Assert.IsTrue(mr.startIndex == 0);
                        Assert.IsTrue(mr.indexCount == 6);
                    }
                    else
                        Assert.Fail($"MeshReference conversion failed. Entity mesh renderer {e.ToString()} doesn't have any mesh reference component.");
                }
            }

            query1.Dispose();
        }
    }
}
