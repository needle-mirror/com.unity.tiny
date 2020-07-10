using UnityEditor;
using Unity.Entities.Runtime.Build;
using Unity.Entities;
using Unity.Entities.Runtime;
using System.Linq;
using Unity.Build;
using Unity.Build.DotsRuntime;

namespace Unity.Tiny.Authoring
{
    [DisableAutoCreation]
    public class ConfigurationSystem : ConfigurationSystemBase
    {
        protected override void OnUpdate()
        {
            if (ProjectScene == null || !ProjectScene.IsValid())
                return;

            Entity configEntity;
            configEntity = EntityManager.CreateEntity();
            EntityManager.AddComponent<ConfigurationTag>(configEntity);

            if (BuildConfiguration.TryGetComponent<DotsRuntimeBuildProfile>(out var profile) && !profile.UseNewPipeline)
            {
                var startupScenes = EntityManager.AddBuffer<StartupScenes>(configEntity);
                var subScenes = ProjectScene.GetRootGameObjects()
                    .Select(go => go.GetComponent<Unity.Scenes.SubScene>())
                    .Where(g => g != null && g);
                // Add all our subscenes with AutoLoadScene to StartupScenes
                // (technically not necessary?)
                var subSceneGuids = subScenes
                    .Where(s => s != null && s.SceneAsset != null && s.AutoLoadScene);
                foreach (var scene in subSceneGuids)
                    startupScenes.Add(new StartupScenes()
                        {SceneReference = new SceneReference() {SceneGUID = scene.SceneGUID}});
            }
        }
    }
}
