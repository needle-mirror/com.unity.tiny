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
            {
                var ambientLight = new Unity.Tiny.Rendering.AmbientLight
                {
                    ambientSkyColor = ToFloat3(RenderSettings.ambientSkyColor), // used only for selection when multiple render settings are present.
                    intensity = 1.0f,
                };

                if (RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Flat)
                {
                    ambientLight.SetAmbientColor(ToFloat3(RenderSettings.ambientSkyColor.linear));
                }
                else if (RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Trilight)
                {
                    ambientLight.SetAmbientColor(
                        ToFloat3(RenderSettings.ambientSkyColor.linear),
                        ToFloat3(RenderSettings.ambientEquatorColor.linear),
                        ToFloat3(RenderSettings.ambientGroundColor.linear)
                        );
                }
                else
                {
                    ambientLight.ambientProbe = ToSHCoefficients(RenderSettings.ambientProbe);
                }

                DstEntityManager.AddComponentData(e, ambientLight);
            }

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

        static float3 ToFloat3(Color color)
        {
            return new float3(color.r, color.g, color.b);
        }

        static Unity.Tiny.Rendering.SHCoefficients ToSHCoefficients(UnityEngine.Rendering.SphericalHarmonicsL2 sh)
        {
            var result = new Unity.Tiny.Rendering.SHCoefficients();

            // Constant (DC terms):
            result.SHAr.w = sh[0, 0];
            result.SHAg.w = sh[1, 0];
            result.SHAb.w = sh[2, 0];

            // Linear: (used by L1 and L2)
            // Swizzle the coefficients to be in { x, y, z } order.
            result.SHAr.x = sh[0, 3];
            result.SHAr.y = sh[0, 1];
            result.SHAr.z = sh[0, 2];

            result.SHAg.x = sh[1, 3];
            result.SHAg.y = sh[1, 1];
            result.SHAg.z = sh[1, 2];

            result.SHAb.x = sh[2, 3];
            result.SHAb.y = sh[2, 1];
            result.SHAb.z = sh[2, 2];

            // Quadratic: (used by L2)
            result.SHBr.x = sh[0, 4];
            result.SHBr.y = sh[0, 5];
            result.SHBr.z = sh[0, 6];
            result.SHBr.w = sh[0, 7];

            result.SHBg.x = sh[1, 4];
            result.SHBg.y = sh[1, 5];
            result.SHBg.z = sh[1, 6];
            result.SHBg.w = sh[1, 7];

            result.SHBb.x = sh[2, 4];
            result.SHBb.y = sh[2, 5];
            result.SHBb.z = sh[2, 6];
            result.SHBb.w = sh[2, 7];

            result.SHC.x = sh[0, 8];
            result.SHC.y = sh[1, 8];
            result.SHC.z = sh[2, 8];

            return result;
        }
    }
}
