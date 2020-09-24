using System;
using UnityEngine;
using Unity.Entities;

namespace Unity.Tiny.Authoring
{
    [AddComponentMenu("Tiny/AutoMovingDirectionalLight")]
    public class AutoMovingDirectionalLight : MonoBehaviour
    {
        public bool autoBounds = true;
        public GameObject mainCamera;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    public class AutoMovingDirectionalLightSystem : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((AutoMovingDirectionalLight uLight) =>
            {
                if (uLight.mainCamera == null)
                    throw new ArgumentException($"No camera found in the AutoMovingDirectionalLight authoring component of the gameobject: {uLight.name}. Please assign one");

                var entity = GetPrimaryEntity(uLight);
                var entityCamera = GetPrimaryEntity(uLight.mainCamera);
                DstEntityManager.AddComponentData(entity, new Tiny.Rendering.AutoMovingDirectionalLight()
                {
                    autoBounds = uLight.autoBounds,
                    clipToCamera = entityCamera
                });

                DeclareDependency(uLight.gameObject, uLight.mainCamera);
            });
        }
    }
}
