using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Rendering;
using Unity.Transforms;
using Unity.Entities.Runtime.Build;

namespace Unity.TinyConversion
{
    internal static partial class ConversionUtils
    {
        public static CameraClearFlags ToTiny(this UnityEngine.CameraClearFlags flags)
        {
            switch (flags)
            {
                case UnityEngine.CameraClearFlags.Skybox:
                case UnityEngine.CameraClearFlags.Color:
                    return CameraClearFlags.SolidColor;
                case UnityEngine.CameraClearFlags.Depth:
                    return CameraClearFlags.DepthOnly;
                case UnityEngine.CameraClearFlags.Nothing:
                    return CameraClearFlags.Nothing;
                default:
                    throw new ArgumentOutOfRangeException(nameof(flags), flags, null);
            }
        }
    }

    [UpdateInGroup(typeof(GameObjectBeforeConversionGroup))]
    [UpdateAfter(typeof(TransformConversion))]
    public class CameraConversion : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((UnityEngine.Camera uCamera) =>
            {
                var entity = GetPrimaryEntity(uCamera);

                var camera = new Unity.Tiny.Rendering.Camera();
                camera.clearFlags = uCamera.clearFlags.ToTiny();
                camera.backgroundColor = uCamera.backgroundColor.linear.ToTiny();
                camera.viewportRect = uCamera.rect.ToTiny();
                camera.depth = uCamera.depth;
                camera.fov =  uCamera.orthographic ? uCamera.orthographicSize : uCamera.fieldOfView;
                camera.mode = uCamera.orthographic ? ProjectionMode.Orthographic : ProjectionMode.Perspective;
                camera.clipZNear = uCamera.nearClipPlane;
                camera.clipZFar = uCamera.farClipPlane;
                camera.aspect = uCamera.aspect;

                DstEntityManager.AddComponentData(entity, camera);
                DstEntityManager.AddComponentData (entity, new Unity.Tiny.Rendering.CameraMask { mask = (ulong)uCamera.cullingMask });

                // For CameraSettings2D
                float3 customSortAxisSetting = new float3(0, 0, 1.0f);
                if (UnityEngine.Rendering.GraphicsSettings.transparencySortMode == UnityEngine.TransparencySortMode.CustomAxis)
                    customSortAxisSetting = UnityEngine.Rendering.GraphicsSettings.transparencySortAxis;
                DstEntityManager.AddComponentData(entity, new Unity.Tiny.Rendering.CameraSettings2D
                    { customSortAxis = customSortAxisSetting });

                if (DstEntityManager.HasComponent<NonUniformScale>(entity))
                    DstEntityManager.SetComponentData(entity, new NonUniformScale(){Value = new float3(1f)});

                // tag the camera as auto aspect
                DstEntityManager.AddComponentData(entity, new CameraAutoAspectFromNode { Node = Entity.Null });
            });
        }
    }
}
