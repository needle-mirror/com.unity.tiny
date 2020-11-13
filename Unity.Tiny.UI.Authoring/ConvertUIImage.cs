using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Rendering;
using Unity.Tiny.UI;

namespace Unity.TinyConversion
{
    [ConverterVersion("2d", 1)]
    [UpdateInGroup(typeof(GameObjectDeclareReferencedObjectsGroup))]
    internal class UIImageDeclareAssets : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities
                .WithAll(typeof(UnityEngine.RectTransform))
                .WithNone(typeof(UnityEngine.Canvas))
                .ForEach((UnityEngine.UI.Image image) =>
                {
                    DeclareReferencedAsset(image.sprite.texture);
                    DeclareAssetDependency(image.gameObject, image.sprite.texture);
                });
        }
    }

    [UpdateInGroup(typeof(GameObjectBeforeConversionGroup))]
    [UpdateAfter(typeof(TransformConversion))]
    [ConverterVersion("2d", 1)]
    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    public class ConvertUIImage : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities
                .WithNone(typeof(UnityEngine.Canvas))
                .ForEach((UnityEngine.UI.Image uimage, UnityEngine.RectTransform urc) =>
                {
                    var e = GetPrimaryEntity(uimage);

                    var type = (ImageRenderType)uimage.type;
                    var eTex = GetPrimaryEntity(uimage.sprite.texture);

                    DstEntityManager.AddComponentData(e, new SimpleMaterial
                    {
                        texAlbedoOpacity = eTex,
                        constAlbedo = uimage.color.linear.ToTiny().AsFloat4().xyz,
                        constOpacity = uimage.color.linear.ToTiny().AsFloat4().w,
                        twoSided = false,
                        blend = Unity.Tiny.BlendOp.Add,
                        transparent = true,
                        billboarded = false,
                        scale = new float2(1, 1),
                        offset = new float2(0, 0)
                    });

                    float2 baseSize = new float2(100, 100);
                    float4 border = new float4(0.25f, 0.25f, 0.25f, 0.25f);
                    float4 outer = new float4(0, 0, 1, 1);

                    if (uimage.sprite != null) {
                        var p = uimage.sprite.border;
                        var r = uimage.sprite.rect; // location on original sprite sheet
                        border.x = p.x / r.width;   // left
                        border.y = p.y / r.height;  // bottom
                        border.z = p.z / r.width;   // right
                        border.w = p.w / r.height;  // top

                        baseSize.x = r.width;
                        baseSize.y = r.height;

                        // size of the sprite sheet
                        if (uimage.sprite.texture != null) {
                            var h = uimage.sprite.texture.height;
                            var w = uimage.sprite.texture.width;

                            outer.x = r.xMin / w;
                            outer.y = r.yMin / h;
                            outer.z = r.xMax / w;
                            outer.w = r.yMax / h;
                        }
                    }

                    DstEntityManager.AddComponentData(e, new RectangleRenderState
                    {
                        ImageRenderType = type,
                        PixelsPerUnit = uimage.pixelsPerUnit,
                        PixelsPerUnitMultiplier = uimage.pixelsPerUnitMultiplier,
                        BaseSize = baseSize,
                        Border = border,
                        Outer = outer
                    });

                    DstEntityManager.AddComponentData(e, new Unity.Tiny.Rendering.CameraMask {
                        mask = (ulong)(1<<urc.gameObject.layer)
                    });
                });
        }
    }
}
