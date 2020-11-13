using Unity.Entities;
using Unity.Mathematics;
using Unity.TinyConversion;

namespace Unity.Tiny.UI.Authoring
{
    [UpdateInGroup(typeof(GameObjectBeforeConversionGroup))]
    [UpdateAfter(typeof(TransformConversion))]
    [ConverterVersion("2d", 1)]
    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    public class ConvertSlider : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((UnityEngine.RectTransform urc, UnityEngine.UI.Slider uslider) =>
            {
                var sliderEntity = GetPrimaryEntity(uslider);
                var fillRectEntity = GetPrimaryEntity(uslider.fillRect);
                var fillRectParentEntity = GetPrimaryEntity(uslider.fillRect.parent);
                var handleRectEntity = GetPrimaryEntity(uslider.handleRect);

                DstEntityManager.AddComponentData(sliderEntity, new Slider
                {
                    FillRect = uslider.fillRect == null ? default : fillRectEntity,
                    HandleRect = uslider.handleRect == null ? default : handleRectEntity,
                    Direction = (SliderDirection)uslider.direction,
                    Range = new float2(uslider.minValue, uslider.maxValue),
                    UseWholeNumbers = uslider.wholeNumbers,
                    Value = uslider.value,
                    IsInteractable = uslider.IsInteractable()
                });
                DstEntityManager.AddComponent<UIState>(sliderEntity);

                // for changing fill by clicking directly on fill rect parent
                DstEntityManager.AddComponentData(fillRectParentEntity, new Selectable { IsInteractable = uslider.IsInteractable() });
                DstEntityManager.AddComponent<UIState>(fillRectParentEntity);
                DstEntityManager.AddComponentData(fillRectParentEntity, new UIName { Name = uslider.fillRect.parent.name });

                // The slider handle is a selectable with a SliderHandle flag for runtime filtering
                if (uslider.handleRect != null)
                {
                    var buttonColors = uslider.colors;
                    DstEntityManager.AddComponentData(handleRectEntity, new Selectable
                    {
                        Graphic = GetPrimaryEntity(uslider.targetGraphic),
                        IsInteractable = uslider.IsInteractable(),
                        NormalColor = buttonColors.normalColor.linear.ToTiny(),
                        HighlightedColor = buttonColors.highlightedColor.linear.ToTiny(),
                        PressedColor = buttonColors.pressedColor.linear.ToTiny(),
                        SelectedColor = buttonColors.selectedColor.linear.ToTiny(),
                        DisabledColor = buttonColors.disabledColor.linear.ToTiny(),
                    });

                    var uiToggleHandleName = new UIName() {Name = uslider.handleRect.name };
                    DstEntityManager.AddComponentData(handleRectEntity, uiToggleHandleName);
                    if (uslider.handleRect.name.Length > uiToggleHandleName.Name.Capacity)
                        Debug.LogWarning($"UIName '{uslider.handleRect.name}' is too long and is being truncated. It may not be found correctly at runtime.");
                    DstEntityManager.AddComponent<UIState>(handleRectEntity);
                }

                var uiName = new UIName() {Name = uslider.name };
                DstEntityManager.AddComponentData(sliderEntity, uiName);
                if (uslider.name.Length > uiName.Name.Capacity)
                    Debug.LogWarning($"UIName '{uslider.name}' is too long as is being truncated. It may not be found correctly at runtime.");
            });
        }
    }
}
