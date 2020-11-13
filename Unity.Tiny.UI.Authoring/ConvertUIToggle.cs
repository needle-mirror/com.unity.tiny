using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Rendering;
using Unity.Tiny.UI;
using UnityEngine;
using UnityEngine.UI;
using Selectable = Unity.Tiny.UI.Selectable;

namespace Unity.TinyConversion
{
    [ConverterVersion("2d", 1)]
    [UpdateInGroup(typeof(GameObjectDeclareReferencedObjectsGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    internal class UIToggleDeclareAssets : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities
                .WithAll(typeof(UnityEngine.RectTransform))
                .WithNone(typeof(UnityEngine.Canvas))
                .ForEach((UnityEngine.UI.Toggle toggle) =>
                {
                    DeclareReferencedAsset(toggle.targetGraphic.mainTexture);
                    DeclareAssetDependency(toggle.gameObject, toggle.targetGraphic.mainTexture);

                    DeclareReferencedAsset(toggle.graphic.mainTexture);
                    DeclareAssetDependency(toggle.gameObject, toggle.graphic.mainTexture);
                });
        }
    }

    [UpdateInGroup(typeof(GameObjectBeforeConversionGroup))]
    [UpdateAfter(typeof(TransformConversion))]
    [ConverterVersion("2d", 1)]
    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    public class ConvertUIToggle : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities
                .WithNone(typeof(UnityEngine.Canvas))
                .ForEach((UnityEngine.UI.Toggle utoggle, UnityEngine.RectTransform urc) =>
                {
                    var toggleEntity = GetPrimaryEntity(utoggle);
                    var baseGraphicEntity = GetPrimaryEntity(utoggle.targetGraphic);
                    var toggledGraphicEntity = GetPrimaryEntity(utoggle.graphic);

                    DstEntityManager.AddComponentData(toggleEntity, new Toggleable
                    {
                        IsToggled = utoggle.isOn,
                        ToggledGraphic = toggledGraphicEntity
                    });

                    var buttonColors = utoggle.colors;
                    DstEntityManager.AddComponentData(toggleEntity, new Selectable
                    {
                        Graphic = baseGraphicEntity,
                        IsInteractable = utoggle.IsInteractable(),
                        NormalColor = buttonColors.normalColor.linear.ToTiny(),
                        HighlightedColor = buttonColors.highlightedColor.linear.ToTiny(),
                        PressedColor = buttonColors.pressedColor.linear.ToTiny(),
                        SelectedColor = buttonColors.selectedColor.linear.ToTiny(),
                        DisabledColor = buttonColors.disabledColor.linear.ToTiny(),
                    });

                    var uiName = new UIName() { Name = utoggle.name };
                    DstEntityManager.AddComponentData(toggleEntity, uiName);
                    if (utoggle.name.Length > uiName.Name.Capacity)
                        Debug.LogWarning($"UIName '{utoggle.name}' is too long and is being truncated. It may not be found correctly at runtime.");
                    DstEntityManager.AddComponent<UIState>(toggleEntity);
                });
        }
    }
}
