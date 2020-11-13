using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unity.Tiny.UI
{
    [UpdateAfter(typeof(ClearUIState))]
    public class UpdateSliderValue : SystemBase
    {
        protected override void OnUpdate()
        {
            var processUIEventsSys = World.GetExistingSystem<ProcessUIEvents>();

            var RectParentFromEntity = GetComponentDataFromEntity<RectParent>(isReadOnly: true);
            var RectTransformResultFromEntity = GetComponentDataFromEntity<RectTransformResult>(isReadOnly: true);
            var RectTransformFromEntity = GetComponentDataFromEntity<RectTransform>(isReadOnly: true);
            var LocalToWorldFromEntity = GetComponentDataFromEntity<LocalToWorld>(isReadOnly: true);

            Entities
                .WithoutBurst()
                .WithReadOnly(RectParentFromEntity)
                .WithReadOnly(LocalToWorldFromEntity)
                .WithReadOnly(RectTransformFromEntity)
                .WithReadOnly(RectTransformResultFromEntity)
                .ForEach((Entity sliderEntity, ref Slider slider, ref UIState uiState) =>
                {
                    // Preprocess slider value, make sure its clamped and following the UseWholeNumbers flag
                    slider.Value = math.clamp(slider.Value, slider.Range.x, slider.Range.y);
                    if (slider.UseWholeNumbers)
                    {
                        slider.Value = math.round(slider.Value);
                    }

                    // apply the sliders interactable state to its harder to reach
                    // selectable handle and fill rect parent.
                    if (slider.HandleRect != Entity.Null)
                    {
                        var handleSelectable = EntityManager.GetComponentData<Selectable>(slider.HandleRect);
                        handleSelectable.IsInteractable = slider.IsInteractable;
                        EntityManager.SetComponentData(slider.HandleRect, handleSelectable);
                    }

                    var fillRectParent = EntityManager.GetComponentData<RectParent>(slider.FillRect).Value;
                    var fillRectParentSelectable = EntityManager.GetComponentData<Selectable>(fillRectParent);
                    fillRectParentSelectable.IsInteractable = slider.IsInteractable;
                    EntityManager.SetComponentData(fillRectParent, fillRectParentSelectable);

                    if ((processUIEventsSys.IsPressOnSelectedEntityActive(fillRectParent, out var hitEvent)
                            || slider.HandleRect != Entity.Null && processUIEventsSys.IsPressOnSelectedEntityActive(slider.HandleRect, out hitEvent))
                        && hitEvent.Down)
                    {
                        // Determine the hitbox for the Slider's FillRect's parent
                        var rtr = EntityManager.GetComponentData<RectTransformResult>(fillRectParent);
                        var depth = RectangleTransformSystem.GetFinalPosAndDepth(slider.FillRect, rtr.LocalPosition,
                            RectTransformResultFromEntity,
                            RectParentFromEntity,
                            LocalToWorldFromEntity,
                            out var finalPosition,
                            out var pl2w);

                        float2 pos = finalPosition + rtr.PivotOffset;
                        float4x4 localToWorld = math.mul(pl2w, float4x4.Translate(new float3(pos, -depth)));
                        float2 lowLeft = localToWorld[3].xy;
                        float2 upperRight = localToWorld[3].xy + rtr.Size;

                        // Also grab the current mouse / touch position
                        var inputPos = hitEvent.InputPos;

                        // Use the input and hitbox calculated above to generate a normalized slider value
                        // Then convert to the expanded slider value as determined by its range
                        var normalizedValue = GetNormalizedValueFromCoords(slider.Direction, inputPos, lowLeft, upperRight);
                        uiState.pressed = 1;
                        slider.Value = NormalizedToFullRange(normalizedValue, slider.Range, slider.UseWholeNumbers);
                    }

                    // update UIState if slider value changed
                    if (slider.Value != slider.oldValue)
                    {
                        uiState.valueChanged = 1;
                    }

                    slider.oldValue = slider.Value;

                }).Run();
        }

        static float GetNormalizedValueFromCoords(SliderDirection sliderDirection, float3 inputPos, float2 lowLeft, float2 uppRight)
        {
            float inputPosSingleAxis;
            float lowerBoundSingleAxis;
            float upperBoundSingleAxis;

            switch (sliderDirection)
            {
                case SliderDirection.LeftToRight:
                    inputPosSingleAxis = inputPos.x;
                    lowerBoundSingleAxis = lowLeft.x;
                    upperBoundSingleAxis = uppRight.x;
                    return math.clamp((inputPosSingleAxis - lowerBoundSingleAxis) / (upperBoundSingleAxis - lowerBoundSingleAxis), 0f, 1f);
                case SliderDirection.RightToLeft:
                    inputPosSingleAxis = inputPos.x;
                    lowerBoundSingleAxis = lowLeft.x;
                    upperBoundSingleAxis = uppRight.x;
                    return 1 - math.clamp((inputPosSingleAxis - lowerBoundSingleAxis) / (upperBoundSingleAxis - lowerBoundSingleAxis), 0f, 1f);
                case SliderDirection.BottomToTop:
                    inputPosSingleAxis = inputPos.y;
                    lowerBoundSingleAxis = lowLeft.y;
                    upperBoundSingleAxis = uppRight.y;
                    return math.clamp((inputPosSingleAxis - lowerBoundSingleAxis) / (upperBoundSingleAxis - lowerBoundSingleAxis), 0f, 1f);
                case SliderDirection.TopToBottom:
                    inputPosSingleAxis = inputPos.y;
                    lowerBoundSingleAxis = lowLeft.y;
                    upperBoundSingleAxis = uppRight.y;
                    return 1 - math.clamp((inputPosSingleAxis - lowerBoundSingleAxis) / (upperBoundSingleAxis - lowerBoundSingleAxis), 0f, 1f);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        // Returns a slider value within the slider range based on anchorValue [0..1]
        static float NormalizedToFullRange(float anchorValue, float2 range, bool wholeNumbers)
        {
            var fullRangeVal = anchorValue * (range.y - range.x) + range.x;

            if (wholeNumbers)
            {
                fullRangeVal = math.round(fullRangeVal);
            }

            return fullRangeVal;
        }
    }
}
