using Unity.Entities;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEditor;
using Unity.Build;
using Unity.Build.DotsRuntime;
using Unity.Build.Common;
using UnityEngine;
using Unity.Tiny.Rendering;

namespace Unity.Tiny.Authoring.Tests
{
    class LightConversionTests
    {
        protected World world;
        protected EntityManager entityManager;
        protected GameObjectConversionSettings settings;
        protected Scene scene;
        protected GameObject camera;

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

            camera = new GameObject();
            camera.AddComponent<UnityEngine.Camera>();
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.CloseScene(scene, true);
            if (world != null)
                world.Dispose();
            entityManager = default;

            if (settings != null)
                settings.BlobAssetStore.Dispose();
        }

        [Test]
        public void TinyDirectionalLightComponentsTest()
        {
            GameObject go = new GameObject();
            go.AddComponent<UnityEngine.Light>();
            var lightComp = go.GetComponent<UnityEngine.Light>();
            lightComp.type = LightType.Directional;
            lightComp.shadows = LightShadows.Soft;
            var automovingLComp = go.AddComponent<Unity.Tiny.Authoring.AutoMovingDirectionalLight>();
            automovingLComp.mainCamera = camera;
            var cascadeShadowMapComp = go.AddComponent<Unity.Tiny.Authoring.CascadedShadowMappedLight>();
            cascadeShadowMapComp.mainCamera = camera;

            //Run GO conversion
            var entity = GameObjectConversionUtility.ConvertGameObjectHierarchy(go, settings);
            Assert.IsTrue(entityManager.HasComponent<Unity.Tiny.Rendering.Light>(entity));
            Assert.IsTrue(entityManager.HasComponent<Unity.Tiny.Rendering.AutoMovingDirectionalLight>(entity));
            Assert.IsTrue(entityManager.HasComponent<Unity.Tiny.Rendering.CascadeShadowmappedLight>(entity));
            Assert.IsTrue(entityManager.HasComponent<Unity.Transforms.NonUniformScale>(entity));
            Assert.IsTrue(entityManager.HasComponent<DirectionalLight>(entity));
            Assert.IsTrue(entityManager.HasComponent<ShadowmappedLight>(entity));
        }

        [Test]
        public void TinySpotLightComponentsTest()
        {
            GameObject go = new GameObject();
            go.AddComponent<UnityEngine.Light>();
            var lightComp = go.GetComponent<UnityEngine.Light>();
            lightComp.type = LightType.Spot;
            lightComp.shadows = LightShadows.Soft;

            //Run GO conversion
            var entity = GameObjectConversionUtility.ConvertGameObjectHierarchy(go, settings);
            Assert.IsTrue(entityManager.HasComponent<Unity.Tiny.Rendering.Light>(entity));
            Assert.IsTrue(entityManager.HasComponent<Unity.Tiny.Rendering.SpotLight>(entity));
            Assert.IsTrue(entityManager.HasComponent<ShadowmappedLight>(entity));
            Assert.IsFalse(entityManager.HasComponent<Unity.Transforms.NonUniformScale>(entity));
            Assert.IsFalse(entityManager.HasComponent<DirectionalLight>(entity));
        }

        [Test]
        public void TinyPointLightComponentsTest()
        {
            GameObject go = new GameObject();
            go.AddComponent<UnityEngine.Light>();
            var lightComp = go.GetComponent<UnityEngine.Light>();
            lightComp.type = LightType.Point;

            //Run GO conversion
            var entity = GameObjectConversionUtility.ConvertGameObjectHierarchy(go, settings);
            Assert.IsTrue(entityManager.HasComponent<Unity.Tiny.Rendering.Light>(entity));
            Assert.IsFalse(entityManager.HasComponent<DirectionalLight>(entity));
            Assert.IsFalse(entityManager.HasComponent<Unity.Tiny.Rendering.SpotLight>(entity));
            Assert.IsFalse(entityManager.HasComponent<Unity.Transforms.NonUniformScale>(entity));
        }
    }
}
