using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Text;
using Unity.Transforms;

namespace Unity.Tiny.UI
{
    [UpdateInGroup(typeof(TransformSystemGroup))]
    [UpdateBefore(typeof(RectangleTransformSystem))]
    public class UpdateTextTransform : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities
                .WithoutBurst()
                .WithChangeFilter<TextRenderer>()
                .ForEach((Entity e, ref RectTransform rxform, in TextRenderer textRenderer) =>
                {
                    switch (textRenderer.VerticalAlignment)
                    {
                        // Center and Baseline are the same to the positioning code, by design.
                        // LayoutString will give slightly different vertical results.
                        case VerticalAlignment.Center:
                        case VerticalAlignment.Baseline:
                            rxform.AnchorMin.y = .5f;
                            rxform.AnchorMax.y = .5f;
                            rxform.Pivot.y = .5f;
                            break;
                        case VerticalAlignment.Bottom:
                            rxform.AnchorMin.y = 0f;
                            rxform.AnchorMax.y = 0f;
                            rxform.Pivot.y = 0f;
                            break;
                        case VerticalAlignment.Top:
                            rxform.AnchorMin.y = 1f;
                            rxform.AnchorMax.y = 1f;
                            rxform.Pivot.y = 1f;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    switch(textRenderer.HorizontalAlignment)
                    {
                        case HorizontalAlignment.Center:
                            rxform.AnchorMin.x = 0.5f;
                            rxform.AnchorMax.x = 0.5f;
                            rxform.Pivot.x = 0.5f;
                            break;
                        case HorizontalAlignment.Left:
                            rxform.AnchorMin.x = 0f;
                            rxform.AnchorMax.x = 0f;
                            rxform.Pivot.x = 0f;
                            break;
                        case HorizontalAlignment.Right:
                            rxform.AnchorMin.x = 1f;
                            rxform.AnchorMax.x = 1f;
                            rxform.Pivot.x = 1f;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }).Run();
        }
    }
}
