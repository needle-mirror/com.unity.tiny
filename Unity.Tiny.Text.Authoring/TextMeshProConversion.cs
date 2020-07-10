using TMPro;
using Unity.Entities;
using Unity.Tiny.Rendering;
using Unity.Entities.Runtime.Build;
using Unity.TinyConversion;

namespace Unity.Tiny.Text.Authoring
{
    [UpdateInGroup(typeof(GameObjectDeclareReferencedObjectsGroup))]
    [UpdateBefore(typeof(MeshRendererDeclareAssets))]
    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    public class TextMeshProDeclareAssets : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((TMPro.TextMeshPro tmpro) =>
            {
                DeclareReferencedAsset(tmpro.font);
                DeclareReferencedAsset(tmpro.font.atlasTexture);

                DeclareAssetDependency(tmpro.gameObject, tmpro.font);
                DeclareAssetDependency(tmpro.gameObject, tmpro.font.atlasTexture);
            });
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    public class TextMeshProTextConversion : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((TMPro.TextMeshPro tmpro) =>
            {
                var textEntity = GetPrimaryEntity(tmpro);

                HorizontalAlignment halign = HorizontalAlignment.Left;
                if (tmpro.alignment.HasFlag(TextAlignmentOptions.Left)) halign = HorizontalAlignment.Left;
                else if (tmpro.alignment.HasFlag(TextAlignmentOptions.Center)) halign = HorizontalAlignment.Center;
                else if (tmpro.alignment.HasFlag(TextAlignmentOptions.Right)) halign = HorizontalAlignment.Right;

                var fontAsset = GetPrimaryEntity(tmpro.font);
                DstEntityManager.AddComponentData(textEntity, new Text.TextRenderer
                {
                    FontMaterial = fontAsset,
                    MeshColor = tmpro.color.linear.ToTiny(),
                    Size = tmpro.fontSize,
                    HorizontalAlignment = halign,
                });

                var text = tmpro.text;
                DstEntityManager.AddBufferFromString<TextRendererString>(textEntity, text);
            });
        }
    }
}
