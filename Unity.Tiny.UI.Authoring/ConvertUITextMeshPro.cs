using TMPro;
using Unity.Entities;
using Unity.Tiny.UI;
using Unity.TinyConversion;
using Unity.Transforms;

namespace Unity.Tiny.Text.Authoring
{
    [UpdateInGroup(typeof(GameObjectDeclareReferencedObjectsGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    public class TMPUIDeclareAssets : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            // UI text conversion
            Entities.ForEach((TextMeshProUGUI displayText) =>
            {
                DeclareReferencedAsset(displayText.font);
                DeclareReferencedAsset(displayText.font.atlasTexture);

                DeclareAssetDependency(displayText.gameObject, displayText.font);
                DeclareAssetDependency(displayText.gameObject, displayText.font.atlasTexture);
            });
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    public class ConvertUITextMeshPro : GameObjectConversionSystem
    {
        HorizontalAlignment ConvertHorizontalAlignment(TextAlignmentOptions option)
        {
            switch (option)
            {
                case TextAlignmentOptions.Center:
                case TextAlignmentOptions.Bottom:
                case TextAlignmentOptions.Top:
                    return HorizontalAlignment.Center;

                case TextAlignmentOptions.Left:
                case TextAlignmentOptions.BottomLeft:
                case TextAlignmentOptions.TopLeft:
                    return HorizontalAlignment.Left;

                case TextAlignmentOptions.Right:
                case TextAlignmentOptions.TopRight:
                case TextAlignmentOptions.BottomRight:
                    return HorizontalAlignment.Right;

                default:
                    Debug.Log($"TMP text alignment option {option} is not supported. Defaulting to center alignment.");
                    return HorizontalAlignment.Center;
            }
        }

        VerticalAlignment ConvertVerticalAlignment(TextAlignmentOptions option)
        {
            switch (option)
            {
                case TextAlignmentOptions.Left:
                case TextAlignmentOptions.Center:
                case TextAlignmentOptions.Right:
                    return VerticalAlignment.Center;

                case TextAlignmentOptions.BottomLeft:
                case TextAlignmentOptions.Bottom:
                case TextAlignmentOptions.BottomRight:
                    return VerticalAlignment.Bottom;

                case TextAlignmentOptions.TopLeft:
                case TextAlignmentOptions.Top:
                case TextAlignmentOptions.TopRight:
                    return VerticalAlignment.Top;

                default:
                    Debug.Log($"TMP text alignment option {option} is not supported. Defaulting to center alignment.");
                    return VerticalAlignment.Center;
            }
        }

        protected override void OnUpdate()
        {
            // UI text conversion
            Entities.ForEach((TextMeshProUGUI displayText) =>
            {
                // We are converting an entity (textEntity) and creating another (eSubText).
                // Why?
                // We want to use the Rectangle Transform system to also position text, rather
                // than introduce something special. So we need a parent element that is the frame
                // of the text (textEntity) and a child element that has a pivet on the text string
                // itself. Systems later will process the child element.

                var textEntity = GetPrimaryEntity(displayText);

                // Sub-text child transform
                SceneSection sceneSection = DstEntityManager.GetSharedComponentData<SceneSection>(textEntity);
                var eSubText = DstEntityManager.CreateEntity();
                DstEntityManager.AddSharedComponentData(eSubText, sceneSection);

                DstEntityManager.AddComponentData(eSubText, new Tiny.UI.RectTransform
                {
                    AnchorMin = 0.5f,
                    AnchorMax = 0.5f,
                    SizeDelta = 1f,
                    AnchoredPosition = 0f,
                    Pivot = 0.5f
                });

                DstEntityManager.AddComponentData(eSubText, new UIName() {Name = displayText.name});

                DstEntityManager.AddComponentData(eSubText, new RectParent()
                {
                    Value = textEntity
                });

                DstEntityManager.AddComponentData(eSubText, new Unity.Tiny.Rendering.CameraMask
                {
                    mask = (ulong) (1 << displayText.gameObject.layer)
                });

                DstEntityManager.AddComponent<RectTransformResult>(eSubText);

                var fontAsset = GetPrimaryEntity(displayText.font);
                DstEntityManager.AddComponentData(eSubText, new Tiny.Text.TextRenderer
                {
                    FontMaterial = fontAsset,
                    MeshColor = displayText.color.linear.ToTiny(),
                    Size = displayText.fontSize * 10,
                    HorizontalAlignment = ConvertHorizontalAlignment(displayText.alignment),
                    VerticalAlignment = ConvertVerticalAlignment(displayText.alignment),
                });

                var text = displayText.text;
                DstEntityManager.AddBufferFromString<TextRendererString>(eSubText, text);
                DstEntityManager.AddComponent<LocalToWorld>(eSubText);
            });
        }
    }
}
