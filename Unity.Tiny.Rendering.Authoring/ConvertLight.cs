using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Light = UnityEngine.Light;
using Unity.Entities.Runtime.Build;
using Unity.Transforms;

namespace Unity.TinyConversion
{
    [UpdateInGroup(typeof(GameObjectBeforeConversionGroup))]
    [UpdateAfter(typeof(TransformConversion))]
    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    public class LightConversion : GameObjectConversionSystem
    {
        void CheckLightLimitations()
        {
            int numberOfPointOrDirLights = 0;
            int numberOfShadowMappedLights = 0;
            int numberOfCascadedShadowMappedLights = 0;
            Entities.ForEach((Light uLight) =>
            {
                var cascadeComp = uLight.gameObject.GetComponent<Tiny.Authoring.CascadedShadowMappedLight>();

                if (uLight.type == LightType.Directional || uLight.type == LightType.Point)
                    numberOfPointOrDirLights++;
                if (uLight.type == LightType.Directional || uLight.type == LightType.Spot)
                {
                    if (uLight.shadows != LightShadows.None)
                    {
                        if (cascadeComp != null)
                        {
                            numberOfCascadedShadowMappedLights++;
                        }
                        else
                        {
                            numberOfShadowMappedLights++;
                        }
                    }
                }
                if (uLight.type != LightType.Directional && cascadeComp != null)
                    throw new ArgumentException($"The {nameof(Tiny.Authoring.CascadedShadowMappedLight)} component is only supported for Directional Lights. Use it on only one Directional light.");
            });

            if (numberOfPointOrDirLights > LightingSetup.maxPointOrDirLights)
                throw new ArgumentException($"Only a maximum of a total of {LightingSetup.maxPointOrDirLights} directional or point lights is supported. Please reduce the number of directional and/or point lights in your scene.");
            if (numberOfShadowMappedLights > LightingSetup.maxMappedLights)
                throw new ArgumentException($"Only a maximum of {LightingSetup.maxMappedLights} shadow mapped lights is supported. Please reduce the number of shadow mapped lights (directional or spot) in your scene.");
            if (numberOfCascadedShadowMappedLights > LightingSetup.maxCsmLights)
                throw new ArgumentException($"Only a maximum of {LightingSetup.maxCsmLights} cascaded shadow mapped directional lights is supported. Use only one {nameof(Tiny.Authoring.CascadedShadowMappedLight)} component in your scene on a Directional Light.");
        }

        protected override void OnUpdate()
        {
            CheckLightLimitations();

            Entities.ForEach((Light uLight) =>
            {
                Entity eLighting = GetPrimaryEntity(uLight);
                DstEntityManager.AddComponentData(eLighting, new Unity.Tiny.Rendering.Light()
                {
                    intensity = uLight.intensity, //TODO: Need to fix this
                    color = new float3(uLight.color.r, uLight.color.g, uLight.color.b),
                    clipZFar = uLight.range,
                    clipZNear = 0.0f
                });
                if (uLight.type == LightType.Directional)
                {
                    DstEntityManager.AddComponentData(eLighting, new DirectionalLight());
                    DstEntityManager.AddComponentData(eLighting, new Unity.Transforms.NonUniformScale
                    {
                        Value = new float3(1)
                    });
                    if (uLight.shadows != LightShadows.None)
                    {
                        var authoComp = uLight.gameObject.GetComponent<Tiny.Authoring.AutoMovingDirectionalLight>();
                        if (authoComp == null)
                            Debug.LogWarning($"The gameobject {uLight.gameObject.name} has a directional light using shadow mapping but does not have a {nameof(Tiny.Authoring.AutoMovingDirectionalLight)} component. The {nameof(Tiny.Authoring.AutoMovingDirectionalLight)} component will automatically update the directional light's position and size based on its camera frustrum for shadow mapping. Please add a {nameof(Tiny.Authoring.AutoMovingDirectionalLight)} component");
                    }
                }
                else if (uLight.type == LightType.Spot)
                {
                    if (uLight.shadows == LightShadows.None)
                        Debug.LogWarning("Spot lights with no shadows are not supported. Set a shadow type in light: " + uLight.name);
                    else
                    {
                        Debug.Assert(uLight.innerSpotAngle <= uLight.spotAngle);
                        float ir = uLight.innerSpotAngle / uLight.spotAngle;
                        if (ir == 1.0f)
                            ir = 0.999f;
                        DstEntityManager.AddComponentData(eLighting, new SpotLight()
                        {
                            fov = uLight.spotAngle,
                            innerRadius = ir,
                            ratio = 1.0f
                        });
                    }
                }
                if (uLight.shadows != LightShadows.None)
                {
                    int shadowMapResolution = 1024;
                    if (uLight.type == LightType.Directional)
                    {
                        shadowMapResolution = 2048;
                    }
                    DstEntityManager.AddComponentData(eLighting, new ShadowmappedLight
                    {
                        shadowMapResolution = shadowMapResolution, //TODO: Shadow resolutions in Big-U are set in the Quality Settings (or URP settings) globally. (API: Light.LightShadowResolution.Low/Medium/High/VeryHigh)
                        shadowMap = Entity.Null, // auto created
                        shadowMapRenderNode = Entity.Null // auto created
                    });
                    var light = DstEntityManager.GetComponentData<Unity.Tiny.Rendering.Light>(eLighting);
                    light.clipZNear = uLight.shadowNearPlane;
                    DstEntityManager.SetComponentData(eLighting, light);
                }

                if (DstEntityManager.HasComponent<NonUniformScale>(eLighting))
                    DstEntityManager.SetComponentData(eLighting, new NonUniformScale(){Value = new float3(1f)});
            });
        }
    }
}
