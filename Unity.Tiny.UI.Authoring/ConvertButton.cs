using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Tiny.UI;
using UnityEngine;
using UnityEngine.UI;
using RectTransform = UnityEngine.RectTransform;
using Selectable = Unity.Tiny.UI.Selectable;

namespace Unity.TinyConversion
{
    [UpdateInGroup(typeof(GameObjectBeforeConversionGroup))]
    [UpdateAfter(typeof(TransformConversion))]
    [ConverterVersion("2d", 1)]
    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    public class ConvertButton : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            // only difference between toggle and button in dots runtime is the additional Toggleable component
            Entities.ForEach((RectTransform urc, Button button) =>
            {
                var e = GetPrimaryEntity(button);
                var buttonColors = button.colors;
                DstEntityManager.AddComponentData(e, new Selectable
                {
                    Graphic = GetPrimaryEntity(button.targetGraphic),
                    IsInteractable = button.IsInteractable(),
                    NormalColor = buttonColors.normalColor.linear.ToTiny(),
                    HighlightedColor = buttonColors.highlightedColor.linear.ToTiny(),
                    PressedColor = buttonColors.pressedColor.linear.ToTiny(),
                    SelectedColor = buttonColors.selectedColor.linear.ToTiny(),
                    DisabledColor = buttonColors.disabledColor.linear.ToTiny(),
                });
                var uiName = new UIName() {Name = button.name };
                DstEntityManager.AddComponentData(e, uiName);
                if (button.name.Length > uiName.Name.Capacity)
                    Debug.LogWarning($"UIName '{button.name}' is too long and is being truncated. It may not be found correctly at runtime.");
                DstEntityManager.AddComponent<UIState>(e);
            });
        }
    }

}
