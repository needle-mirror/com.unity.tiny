using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Assertions;
using Unity.Tiny.Rendering;
using Unity.Transforms;

namespace Unity.Tiny.UI
{
    [UpdateInGroup(typeof(TransformSystemGroup))]
    [UpdateBefore(typeof(RectangleTransformSystem))]
    public class UpdateCanvas : SystemBase
    {
        protected override void OnUpdate()
        {
            var displayInfo =  GetSingleton<DisplayInfo>();
            var renderMode = GetSingleton<RenderGraphConfig>().Mode;
            var renderGraphState = GetSingleton<RenderGraphState>();

            int canvasHeight = renderMode == RenderGraphMode.FixedRenderBuffer ? renderGraphState.RenderBufferCurrentHeight : displayInfo.height;
            int canvasWidth = renderMode == RenderGraphMode.FixedRenderBuffer ? renderGraphState.RenderBufferCurrentWidth : displayInfo.width;

            // For every RectCanvas, initialize / update it's rect transform based on the display info.
            Entities
                .WithoutBurst()
                .ForEach((ref RectTransform rxform, in RectCanvas rcanvas, in RectCanvasScaleWithCamera rcCam) =>
                {
                    rxform.Pivot = new float2(0.5f, 0.5f);
                    rxform.AnchorMin = float2.zero;
                    rxform.AnchorMax = float2.zero;

                    Camera cam = EntityManager.GetComponentData<Camera>(rcCam.Camera);
                    var s = new float2(canvasWidth, canvasHeight);

                    switch (rcanvas.RenderMode)
                    {
                        case RenderMode.ScreenSpaceOverlay:
                        {
                            // Update our camera with a correct field of view.
                            // This only works because - in ScreenSpaceOverlay - we construct a camera
                            // specifically for the UI.

                            cam.fov = canvasHeight * 0.5f;
                            EntityManager.SetComponentData(rcCam.Camera, cam);

                            if (!rxform.SizeDelta.Equals(s))
                            {
                                rxform.SizeDelta = s;
                                rxform.AnchoredPosition = s * 0.5f;
                            }

                            break;
                        }

                        case RenderMode.ScreenSpaceCamera:
                        {
                            s = rcanvas.ReferenceResolution;
                            s.x = s.x * cam.aspect;

                            if (!rxform.SizeDelta.Equals(s))
                            {
                                rxform.SizeDelta = s;
                                rxform.AnchoredPosition = s * 0.5f;
                            }

                            break;
                        }

                        case RenderMode.WorldSpace:
                        {
                            rxform.SizeDelta = rcanvas.ReferenceResolution;
                            rxform.AnchoredPosition = rcanvas.ReferenceResolution * 0.5f;
                            break;
                        }
                    }
                }).Run();
        }
    }
}
