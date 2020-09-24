using Unity.Build.Common;
using Unity.Build.DotsRuntime;
using Unity.Mathematics;
using UnityEngine;
using Unity.Entities;
using Unity.Entities.Runtime.Build;
using UnityEditor;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.TinyConversion
{
    internal static partial class ConversionUtils
    {
        public static Unity.Tiny.Rendering.Fog.Mode ToTiny(this UnityEngine.FogMode fogMode, bool useFog)
        {
            if (!useFog)
                return Unity.Tiny.Rendering.Fog.Mode.None;

            switch (fogMode)
            {
                case UnityEngine.FogMode.Linear:
                    return Unity.Tiny.Rendering.Fog.Mode.Linear;
                case UnityEngine.FogMode.Exponential:
                    return Unity.Tiny.Rendering.Fog.Mode.Exponential;
                case UnityEngine.FogMode.ExponentialSquared:
                    return Unity.Tiny.Rendering.Fog.Mode.ExponentialSquared;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(fogMode), fogMode, null);
            }
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    public class RenderSettingsConversion : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            if (Settings == null)
                return;

            if (TryGetBuildConfigurationComponent<DotsRuntimeBuildProfile>(out var profile))
            {
                if (!IsExportingRootScene())
                    return;
            }

            // Get render settings from the current active scene
            Entity e = DstEntityManager.CreateEntity();

            // Render settings should go into the main section of the subscene they are coming from
            DstEntityManager.AddSharedComponentData(e, new SceneSection(){ SceneGUID = Settings.SceneGUID, Section = 0});

            // Ambient light
            DstEntityManager.AddComponentData<Unity.Tiny.Rendering.Light>(e, new Unity.Tiny.Rendering.Light()
            {
                color = new float3(RenderSettings.ambientLight.r, RenderSettings.ambientLight.g, RenderSettings.ambientLight.b),
                intensity = 1.0f
            });
            DstEntityManager.AddComponent<Unity.Tiny.Rendering.AmbientLight>(e);

            // Fog
            var fogLinear = RenderSettings.fogColor.linear;
            DstEntityManager.AddComponentData<Unity.Tiny.Rendering.Fog>(e, new Unity.Tiny.Rendering.Fog()
            {
                mode = RenderSettings.fogMode.ToTiny(RenderSettings.fog),
                color = new float4(fogLinear.r, fogLinear.g, fogLinear.b, fogLinear.a),
                density = RenderSettings.fogDensity,
                startDistance = RenderSettings.fogStartDistance,
                endDistance = RenderSettings.fogEndDistance
            });
        }

        private bool IsExportingRootScene()
        {
            var sceneList = Settings.BuildConfiguration.GetComponent<SceneList>();
            bool convertingRootScene = false;
            foreach (var sceneInfo in sceneList.SceneInfos)
            {
                var sceneHash = new Hash128(sceneInfo.Scene.assetGUID.ToString());
                if (sceneHash == Settings.SceneGUID)
                {
                    convertingRootScene = true;
                    break;
                }
            }

            return convertingRootScene;
        }
    }
}
