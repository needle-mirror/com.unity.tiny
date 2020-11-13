using TMPro;
using Unity.Entities;
using Unity.TinyConversion;

namespace Unity.Tiny.Text.Authoring
{
    [UpdateInGroup(typeof(GameObjectDeclareReferencedObjectsGroup))]
    [UpdateBefore(typeof(MeshRendererDeclareAssets))]
    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    public class TextRendererDeclareAssets : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            // Custom authoring component text conversion
            Entities.ForEach((TextRenderer displayText) =>
            {
                DeclareReferencedAsset(displayText.Font);
                DeclareReferencedAsset(displayText.Font.atlasTexture);

                DeclareAssetDependency(displayText.gameObject, displayText.Font);
                DeclareAssetDependency(displayText.gameObject, displayText.Font.atlasTexture);
            });
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    public class TextRendererConversion : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            // Custom authoring component text conversion
            Entities.ForEach((TextRenderer displayText) =>
            {
                var textEntity = GetPrimaryEntity(displayText);

                var fontAsset = GetPrimaryEntity(displayText.Font);
                DstEntityManager.AddComponentData(textEntity, new Text.TextRenderer
                {
                    FontMaterial = fontAsset,
                    MeshColor = displayText.Color.linear.ToTiny(),
                    Size = displayText.Size,
                    HorizontalAlignment = displayText.Alignment,
                    VerticalAlignment = VerticalAlignment.Baseline
                });
                DstEntityManager.AddComponentData(textEntity, new Unity.Tiny.Rendering.CameraMask {
                   mask = (ulong)(1<<displayText.gameObject.layer)
                });

                var text = displayText.Text;
                DstEntityManager.AddBufferFromString<TextRendererString>(textEntity, text);
            });
        }
    }
}
