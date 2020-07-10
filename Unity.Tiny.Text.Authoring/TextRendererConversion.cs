using TMPro;
using Unity.Entities;
using Unity.Tiny.Rendering;
using Unity.Entities.Runtime.Build;
using Unity.TinyConversion;
using UnityEngine.TextCore.LowLevel;

namespace Unity.Tiny.Text.Authoring
{
    [UpdateInGroup(typeof(GameObjectDeclareReferencedObjectsGroup))]
    [UpdateBefore(typeof(MeshRendererDeclareAssets))]
    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    public class TextRendererDeclareAssets : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
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
            Entities.ForEach((TextRenderer displayText) =>
            {
                var textEntity = GetPrimaryEntity(displayText);

                var fontAsset = GetPrimaryEntity(displayText.Font);
                DstEntityManager.AddComponentData(textEntity, new Text.TextRenderer
                {
                    FontMaterial = fontAsset,
                    MeshColor = displayText.Color.linear.ToTiny(),
                    Size = displayText.Size,
                    HorizontalAlignment = displayText.Alignment
                });

                var text = displayText.Text;
                DstEntityManager.AddBufferFromString<TextRendererString>(textEntity, text);
            });
        }
    }
}
