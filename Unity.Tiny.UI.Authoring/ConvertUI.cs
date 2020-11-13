using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Rendering;
using Unity.Tiny.UI;
using Unity.Transforms;
using UnityEngine;

namespace Unity.TinyConversion
{
    [UpdateInGroup(typeof(GameObjectBeforeConversionGroup))]
    [UpdateAfter(typeof(TransformConversion))]
    [ConverterVersion("2d", 3)]
    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    public class ConvertUI : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((UnityEngine.RectTransform urc) =>
            {
                var e = GetPrimaryEntity(urc);

                var ucanvas = urc.gameObject.GetComponent<UnityEngine.Canvas>();
                if (ucanvas != null && ucanvas)
                {
                    ConvertCanvas(ucanvas, e, urc);
                }
                else
                {
                    DstEntityManager.AddComponentData(e, new Tiny.UI.RectTransform
                    {
                        AnchorMin = urc.anchorMin,
                        AnchorMax = urc.anchorMax,
                        SizeDelta = urc.sizeDelta,
                        AnchoredPosition = urc.anchoredPosition,
                        Pivot = urc.pivot,
                        SiblingIndex = urc.GetSiblingIndex()
                    });

                    DstEntityManager.AddComponentData(e, new RectParent()
                    {
                        Value = GetPrimaryEntity(urc.parent)
                    });

                    DstEntityManager.AddComponent<Tiny.UI.RectTransform>(e);
                    DstEntityManager.AddComponent<RectTransformResult>(e);
                }
            });
        }

        void ConvertCanvas(Canvas ucanvas, Entity e, UnityEngine.RectTransform urc)
        {
            if (!ucanvas.isRootCanvas)
            {
                UnityEngine.Debug.LogError($"Only root canvases are supported", ucanvas);
                return;
            }

            Entity eCam;
            if (ucanvas.renderMode == UnityEngine.RenderMode.ScreenSpaceOverlay)
            {
                eCam = ConstructScreenSpaceCamera(e, urc);
            }
            else
            {
                eCam = GetPrimaryEntity(ucanvas.worldCamera);
            }

            DstEntityManager.AddComponentData(e, new RectCanvas()
            {
                ReferenceResolution = urc.sizeDelta,
                RenderMode = (Tiny.UI.RenderMode) ucanvas.renderMode
            });

            DstEntityManager.AddComponentData(e, new RectCanvasScaleWithCamera
            {
                Camera = eCam
            });

            // filled in at runtime
            DstEntityManager.AddComponentData(e, new Unity.Tiny.UI.RectTransform
            {
                AnchorMin = urc.anchorMin,
                AnchorMax = urc.anchorMax,
                SizeDelta = urc.sizeDelta,
                AnchoredPosition = urc.anchoredPosition,
                Pivot = urc.pivot
            });
            DstEntityManager.AddComponent<RectTransformResult>(e);
        }

        Entity ConstructScreenSpaceCamera(Entity e, UnityEngine.RectTransform urc)
        {
            SceneSection sceneSection = DstEntityManager.GetSharedComponentData<SceneSection>(e);
            Entity eCam = DstEntityManager.CreateEntity();
            DstEntityManager.AddSharedComponentData(eCam, sceneSection);

            var sizeDelta = urc.sizeDelta;

            var camera = new Unity.Tiny.Rendering.Camera
            {
                clearFlags = Tiny.Rendering.CameraClearFlags.DepthOnly,
                backgroundColor = new Unity.Tiny.Color(0, 0, 0, 0),
                viewportRect = new Tiny.Rect(0, 0, 1, 1),
                depth = 0.0f,
                fov = sizeDelta.y * 0.5f,
                mode = ProjectionMode.Orthographic,
                clipZNear = 0,
                clipZFar = 102,
                aspect = sizeDelta.x / sizeDelta.y
            };

            DstEntityManager.AddComponentData(eCam, camera);
            DstEntityManager.AddComponentData(eCam,
                new Unity.Tiny.Rendering.CameraMask {mask = (ulong)(1<<urc.gameObject.layer)});

            // For CameraSettings2D
            float3 customSortAxisSetting = new float3(0, 0, 1.0f);
            if (UnityEngine.Rendering.GraphicsSettings.transparencySortMode ==
                UnityEngine.TransparencySortMode.CustomAxis)
                customSortAxisSetting = UnityEngine.Rendering.GraphicsSettings.transparencySortAxis;
            DstEntityManager.AddComponentData(eCam, new Unity.Tiny.Rendering.CameraSettings2D
                {customSortAxis = customSortAxisSetting});

            // tag the camera as auto aspect
            DstEntityManager.AddComponentData(eCam, new CameraAutoAspectFromNode {Node = Entity.Null});

            DstEntityManager.AddComponentData(eCam, new LocalToWorld {Value = float4x4.identity});
            DstEntityManager.AddComponentData(eCam,
                new Translation {Value = new float3(sizeDelta.x / 2, sizeDelta.y / 2, -10)});
            DstEntityManager.AddComponentData(eCam, new Rotation {Value = quaternion.identity});
            DstEntityManager.AddComponent<UICamera>(eCam);
            return eCam;
        }

    }
}
