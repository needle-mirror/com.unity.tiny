using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Assertions;
using Unity.Transforms;

namespace Unity.Tiny.UI
{
    [UpdateInGroup(typeof(TransformSystemGroup))]
    [UpdateBefore(typeof(RectangleTransformSystem))]
    public class UpdateSliderTransform : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities
                .WithoutBurst()
                .ForEach((Entity e, ref RectTransform sliderTransform, ref Slider slider) =>
                {
                    // Change fill rect transform based on value
                    var fillRectTransform = EntityManager.GetComponentData<RectTransform>(slider.FillRect);
                    fillRectTransform.Hidden = sliderTransform.Hidden;
                    var anchorVal = CalculateAnchorValueFromSliderValue(slider.Value, slider.Range);
                    fillRectTransform = FillRectTransformFromSlider(fillRectTransform, anchorVal, slider.Direction);
                    EntityManager.SetComponentData(slider.FillRect, fillRectTransform);

                    // Change handle transform rect based on value
                    if (slider.HandleRect == Entity.Null)
                    {
                        return;
                    }

                    var handleTransform = EntityManager.GetComponentData<RectTransform>(slider.HandleRect);
                    handleTransform.Hidden = sliderTransform.Hidden;
                    handleTransform = HandleRectTransformFromSlider(handleTransform, anchorVal, slider.Direction);
                    EntityManager.SetComponentData(slider.HandleRect, handleTransform);
                }).Run();
        }

        static RectTransform HandleRectTransformFromSlider(RectTransform handleTransform, float anchorVal, SliderDirection direction)
        {
            switch (direction)
            {
                case SliderDirection.LeftToRight:
                    handleTransform.AnchorMin = new float2(anchorVal, 0f);
                    handleTransform.AnchorMax = new float2(anchorVal, 1f);
                    break;
                case SliderDirection.RightToLeft:
                    handleTransform.AnchorMin = new float2(1 - anchorVal, 0f);
                    handleTransform.AnchorMax = new float2(1 - anchorVal, 1f);
                    break;
                case SliderDirection.BottomToTop:
                    handleTransform.AnchorMin = new float2(0f, anchorVal);
                    handleTransform.AnchorMax = new float2(1f, anchorVal);
                    break;
                case SliderDirection.TopToBottom:
                    handleTransform.AnchorMin = new float2(0f, 1 - anchorVal);
                    handleTransform.AnchorMax = new float2(1f, 1 - anchorVal);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return handleTransform;
        }

        static RectTransform FillRectTransformFromSlider(RectTransform rectTransform, float anchorVal, SliderDirection direction)
        {
            switch (direction)
            {
                case SliderDirection.LeftToRight:
                    rectTransform.AnchorMin = 0f;
                    rectTransform.AnchorMax = new float2(anchorVal, 1f);
                    break;
                case SliderDirection.RightToLeft:
                    rectTransform.AnchorMin = new float2(1f - anchorVal, 0f);
                    rectTransform.AnchorMax = 1f;
                    break;
                case SliderDirection.BottomToTop:
                    rectTransform.AnchorMin = 0f;
                    rectTransform.AnchorMax = new float2(1f, anchorVal);
                    break;
                case SliderDirection.TopToBottom:
                    rectTransform.AnchorMin = new float2(0f, 1f - anchorVal);
                    rectTransform.AnchorMax = 1f;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return rectTransform;
        }

        // Returns a normalized value [0..1] that an anchor should be set to
        // based on the min and max value of the slider and the current value within that range
        static float CalculateAnchorValueFromSliderValue(float value, float2 range)
        {
            var anchorVal =  (value - range.x) / (range.y - range.x);
            return math.clamp(anchorVal, 0f, 1f);
        }
    }
}
