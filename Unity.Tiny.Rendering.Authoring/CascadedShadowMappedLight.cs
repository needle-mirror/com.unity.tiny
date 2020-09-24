using System;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Rendering;
using Unity.Entities.Runtime.Build;

namespace Unity.Tiny.Authoring
{
    [AddComponentMenu("Tiny/CascadedShadowMappedLight")]
    public class CascadedShadowMappedLight : MonoBehaviour
    {
        public float3 cascadeScale = new float3(.5f, .15f, .020f);
        public GameObject mainCamera;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    public class CascadedShadowMappedLightSystem : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((CascadedShadowMappedLight uLight) =>
            {
                if (uLight.mainCamera == null)
                    throw new ArgumentException($"No camera found in the CascadedShadowMappedLight authoring component of the gameobject: {uLight.name}. Please assign one for cascade shadow mapping.");

                bool scale = 0.0f < uLight.cascadeScale.z && uLight.cascadeScale.z < uLight.cascadeScale.y && uLight.cascadeScale.y < uLight.cascadeScale.x && uLight.cascadeScale.x < 1.0f;
                if (!scale)
                    throw new ArgumentException($"Cascade scale values on the game object: {uLight.name} should be clamped between 0 and 1 with cascadeScale.z < cascadeScale.y <cascadeScale.x");

                var entityCamera = GetPrimaryEntity(uLight.mainCamera);
                var comp = new CascadeShadowmappedLight();
                comp.cascadeScale = uLight.cascadeScale;
                comp.cascadeBlendWidth = 0.0f;
                comp.camera = entityCamera;
                var entity = GetPrimaryEntity(uLight);
                DstEntityManager.AddComponentData(entity, comp);

                DeclareDependency(uLight.gameObject, uLight.mainCamera);
            });
        }
    }
}
