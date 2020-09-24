using Unity.Entities;
using Unity.Mathematics;

using SpriteRenderer = Unity.Tiny.SpriteRenderer;

namespace Unity.Tiny.Authoring
{
    [ConverterVersion("2d", 1)]
    [UpdateInGroup(typeof(GameObjectDeclareReferencedObjectsGroup))]
    internal class SpriteRendererDeclareAssets : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((UnityEngine.SpriteRenderer spriteRenderer) =>
            {
                DeclareReferencedAsset(spriteRenderer.sprite);
                DeclareReferencedAsset(spriteRenderer.sharedMaterial);
            });
        }
    }

    [ConverterVersion("2d", 4)]
    [UpdateInGroup(typeof(GameObjectConversionGroup))]
    internal class SpriteRendererConversion : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((UnityEngine.SpriteRenderer uSpriteRenderer) =>
            {
                var entity = GetPrimaryEntity(uSpriteRenderer);

                DstEntityManager.SetName(entity, "SpriteRenderer: " + uSpriteRenderer.name);

                var uWorldToLocalMatrix = uSpriteRenderer.transform.worldToLocalMatrix;
                var uWorldBounds = uSpriteRenderer.bounds;
                var localBounds = new AABB()
                {
                    Center = uWorldToLocalMatrix.MultiplyPoint(uWorldBounds.center),
                    Extents = uSpriteRenderer.bounds.extents
                };

                var uSortingLayerId = uSpriteRenderer.sortingLayerID;
                var renderingLayer = uSpriteRenderer.gameObject.layer;
                DstEntityManager.AddComponentData(entity, new Renderer2D()
                {
                    RenderingLayer = renderingLayer,
                    SortingLayer = (short) UnityEngine.SortingLayer.GetLayerValueFromID(uSortingLayerId),
                    OrderInLayer = (short) uSpriteRenderer.sortingOrder,
                    Bounds = localBounds,
                });

                DstEntityManager.AddComponentData(entity, new SpriteRenderer
                {
                    Sprite = GetPrimaryEntity(uSpriteRenderer.sprite),
                    Material = GetPrimaryEntity(uSpriteRenderer.sharedMaterial),
                    Color = new float4(
                        uSpriteRenderer.color.r,
                        uSpriteRenderer.color.g,
                        uSpriteRenderer.color.b,
                        uSpriteRenderer.color.a),
                });
            });
        }
    }
}
