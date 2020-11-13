using System;
using System.Collections.Generic;
using Unity.Entities.Runtime.Build;
using UnityEngine;
using System.Linq;
using Unity.Build.Common;
using Unity.Entities;
using Unity.Tiny.Rendering;
using UnityEditor;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Tiny.Authoring
{
    //This configuration system is checking if we are not exceeding a maximum number of lights in all autoloaded scene and subscenes
    [DisableAutoCreation]
    public class LightConfigurationSystem : ConfigurationSystemBase
    {
        int m_NumberOfPointOrDirLights;
        int m_NumberOfShadowMappedLights;
        int m_NumberOfCascadedShadowMappedLights;
        void CalculateNumberOfLights(Hash128 sceneGuid)
        {
            //DOTSR-2180: There is still a bug to fix where if a subscene is loaded as a scene in the Editor instead of being loaded as part of a root scene, the build will unload it and create a new empty scene.
            //SubScene.UnloadScene here will be called on the subscene since we are closing the root scene it will unload the subscene without saving it first (so changes might be lost) and will load a new empty scene.
            using (var loadedScene = new LoadedSceneScope(sceneGuid))
            {
                List<GameObject> rootObjectsInScene = new List<GameObject>();
                loadedScene.ProjectScene.GetRootGameObjects(rootObjectsInScene);

                for (int i = 0; i < rootObjectsInScene.Count; i++)
                {
                    var lights = rootObjectsInScene[i].GetComponentsInChildren<UnityEngine.Light>(true);
                    foreach(var light in lights)
                    {
                        var cascadeComp = light.gameObject.GetComponent<Tiny.Authoring.CascadedShadowMappedLight>();
                        if (light.type == LightType.Directional || light.type == LightType.Point)
                            m_NumberOfPointOrDirLights++;
                        if (light.type == LightType.Directional || light.type == LightType.Spot)
                        {
                            if (light.shadows != LightShadows.None)
                            {
                                if (cascadeComp != null)
                                {
                                    m_NumberOfCascadedShadowMappedLights++;
                                }
                                else
                                {
                                    m_NumberOfShadowMappedLights++;
                                }
                            }
                        }
                    }
                    var subScene = rootObjectsInScene[i].GetComponent<Unity.Scenes.SubScene>();
                    if(subScene != null && subScene.AutoLoadScene && subScene.SceneAsset != null)
                        CalculateNumberOfLights(subScene.SceneGUID);
                }
            }
        }

        protected override void OnUpdate()
        {
            m_NumberOfPointOrDirLights = 0;
            m_NumberOfShadowMappedLights = 0;
            m_NumberOfCascadedShadowMappedLights = 0;

            var sceneList = BuildContext.GetComponentOrDefault<SceneList>();
            var autoLoadedScenes = sceneList.GetSceneInfosForBuild().Where(s => s.AutoLoad).Select(s => new Hash128(AssetDatabase.AssetPathToGUID(s.Path))).ToList();

            //Calculate the number of lights in all autoloaded scene and subscenes recursively
            foreach (var scene in autoLoadedScenes)
                CalculateNumberOfLights(scene);

            if (m_NumberOfPointOrDirLights > LightingSetup.maxPointOrDirLights)
                throw new ArgumentException($"Only a maximum of a total of {LightingSetup.maxPointOrDirLights} directional or point lights is supported at once on the runtime, " +
                    $"and there is currently a total of {m_NumberOfPointOrDirLights} directional or point lights in your auto-loaded scenes. Reduce this number");
            if (m_NumberOfShadowMappedLights > LightingSetup.maxMappedLights)
                throw new ArgumentException($"Only a maximum of {LightingSetup.maxMappedLights} shadow mapped lights (directional or spot) is supported at once on the runtime, " +
                    $"and there is currently {m_NumberOfShadowMappedLights} shadow mapped lights in your auto-loaded scenes. Reduce this number");
            if (m_NumberOfCascadedShadowMappedLights > LightingSetup.maxCsmLights)
                throw new ArgumentException($"Only a maximum of {LightingSetup.maxCsmLights} cascaded shadow mapped directional lights is supported at once on the runtime, " +
                    $"and there is currently {m_NumberOfCascadedShadowMappedLights} cascaded shadow mapped directional lights in your auto-loaded scenes." +
                    $"Use only one {nameof(Tiny.Authoring.CascadedShadowMappedLight)} component in your scene on a Directional Light in your auto-loaded scenes.");

        }
    }
}
