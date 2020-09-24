using UnityEngine;
using NUnit.Framework;
using Unity.Collections;

using Object = UnityEngine.Object;
using Sprite = UnityEngine.Sprite;
using SpriteRenderer = UnityEngine.SpriteRenderer;

[TestFixture]
public class ClassicSpriteRendererConversionTests : AuthoringTestFixture
{
    private SortingLayer[] m_StartingLayers;

    [SetUp]
    protected override void Setup()
    {
        base.Setup();
        m_StartingLayers = SortingLayer.layers;
    }

    [TearDown]
    protected override void TearDown()
    {
        base.TearDown();

        var finalLayers = SortingLayer.layers;
        for (var i = finalLayers.Length - 1; i >= 0; i--)
        {
            if (System.Array.FindIndex(m_StartingLayers, layer => layer.id == finalLayers[i].id) != -1)
                continue;

            EditorTestUtilities.RemoveSortingLayerFromTagManager(finalLayers[i].name);
        }
    }

    [Test]
    public void ConvertSpriteRenderer_ConvertDefault_SuccessfulConversion()
    {
        {
            Root = new GameObject();
            CreateClassicComponent<SpriteRenderer>(Root);
        }

        Assert.DoesNotThrow(() => { RunConversion(Root); });

        using (var spriteRendererQuery = EntityManager.CreateEntityQuery(typeof(Unity.Tiny.SpriteRenderer)))
        using (var renderer2DQuery = EntityManager.CreateEntityQuery(typeof(Unity.Tiny.Renderer2D)))
        {
            Assert.That(spriteRendererQuery.CalculateEntityCount(), Is.EqualTo(1));
            Assert.That(renderer2DQuery.CalculateEntityCount(), Is.EqualTo(1));
        }
    }

    private static Color[] s_Colors = { Color.red, Color.blue, Color.black, Color.clear, Color.green };
    [Test, TestCaseSource("s_Colors")]
    public void ConvertSpriteRenderer_ConvertTint_SameTint(Color tintColor)
    {
        var tint = tintColor;

        {
            Root = new GameObject();
            var spriteRenderer = CreateClassicComponent<SpriteRenderer>(Root);
            spriteRenderer.color = tint;
        }

        Assert.DoesNotThrow(() => { RunConversion(Root); });

        using (var query = EntityManager.CreateEntityQuery(typeof(Unity.Tiny.SpriteRenderer)))
        using (var spriteRenderers = query.ToComponentDataArray<Unity.Tiny.SpriteRenderer>(Allocator.TempJob))
        {
            var convertedTint = new Color()
            {
                a = spriteRenderers[0].Color.w,
                r = spriteRenderers[0].Color.x,
                g = spriteRenderers[0].Color.y,
                b = spriteRenderers[0].Color.z,
            };
            Assert.That(convertedTint, Is.EqualTo(tint).Within(EditorTestUtilities.Epsilon));
        }
    }

    [Test]
    public void ConvertSpriteRenderer_ConvertSortLayer_SameSortLayer(
        [Values("Default", "AdditionalSortingLayer")] string sortingLayerName,
        [Values(1, 20, 123)] int sortOrder)
    {
        var newSortingLayerAdded = EditorTestUtilities.AddSortingLayerToTagManager(sortingLayerName);
        var sortingLayerId = SortingLayer.NameToID(sortingLayerName);

        {
            Root = new GameObject();
            var spriteRenderer = CreateClassicComponent<SpriteRenderer>(Root);
            spriteRenderer.sortingOrder = sortOrder;
            spriteRenderer.sortingLayerID = sortingLayerId;
        }

        Assert.DoesNotThrow(() => { RunConversion(Root); });

        using (var query = EntityManager.CreateEntityQuery(typeof(Unity.Tiny.Renderer2D)))
        using (var renderers = query.ToComponentDataArray<Unity.Tiny.Renderer2D>(Allocator.TempJob))
        {
            var layer = renderers[0].SortingLayer;
            var order = renderers[0].OrderInLayer;

            Assert.That(layer, Is.EqualTo(SortingLayer.GetLayerValueFromID(sortingLayerId)));
            Assert.That(order, Is.EqualTo(sortOrder));
        }

        if (newSortingLayerAdded)
            EditorTestUtilities.RemoveSortingLayerFromTagManager(sortingLayerName);
    }

    private static Vector2[] s_SpriteSizes = { Vector2.zero, Vector2.one, new Vector2(12f, 9f) };
    [Test, TestCaseSource("s_SpriteSizes")]
    public void ConvertSpriteRenderer_ConvertBoundsWithSpriteSize_SameExtents(Vector2 spriteSize)
    {
        var tmpTexture = new Texture2D(16, 16);
        var tmpSprite = Sprite.Create(tmpTexture, new Rect(Vector2.zero, spriteSize), Vector2.zero, 1f);

        {
            Root = new GameObject();
            var spriteRenderer = CreateClassicComponent<SpriteRenderer>(Root);
            spriteRenderer.sprite = tmpSprite;
        }

        Assert.DoesNotThrow(() => { RunConversion(Root); });

        using (var query = EntityManager.CreateEntityQuery(typeof(Unity.Tiny.Renderer2D)))
        using (var renderers = query.ToComponentDataArray<Unity.Tiny.Renderer2D>(Allocator.TempJob))
        {
            var objectBound = renderers[0].Bounds;
            var extents = objectBound.Extents;
            var boundExtentsX = spriteSize.x * 0.5f;
            var boundExtentsY = spriteSize.y * 0.5f;

            Assert.That(extents.x, Is.EqualTo(boundExtentsX).Within(EditorTestUtilities.Epsilon));
            Assert.That(extents.y, Is.EqualTo(boundExtentsY).Within(EditorTestUtilities.Epsilon));
        }

        Object.DestroyImmediate(tmpSprite);
        Object.DestroyImmediate(tmpTexture);
    }

    private static Vector2[] s_LocalScales = { Vector2.zero, Vector2.one, new Vector2(246f, 80f) };
    [Test, TestCaseSource("s_LocalScales")]
    public void ConvertSpriteRenderer_ConvertBoundsWithLocalScale_SameExtents(Vector2 localScale)
    {
        var tmpTexture = new Texture2D(16, 16);
        var tmpSprite = Sprite.Create(tmpTexture, new Rect(Vector2.zero, Vector2.one), Vector2.zero, 1f);

        {
            Root = new GameObject();
            var spriteRenderer = CreateClassicComponent<SpriteRenderer>(Root);
            spriteRenderer.sprite = tmpSprite;
            spriteRenderer.transform.localScale = localScale;
        }

        Assert.DoesNotThrow(() => { RunConversion(Root); });

        using (var query = EntityManager.CreateEntityQuery(typeof(Unity.Tiny.Renderer2D)))
        using (var renderers = query.ToComponentDataArray<Unity.Tiny.Renderer2D>(Allocator.TempJob))
        {
            var objectBound = renderers[0].Bounds;
            var extents = objectBound.Extents;
            var boundExtentsX = localScale.x * 0.5f;
            var boundExtentsY = localScale.y * 0.5f;

            Assert.That(extents.x, Is.EqualTo(boundExtentsX).Within(EditorTestUtilities.Epsilon));
            Assert.That(extents.y, Is.EqualTo(boundExtentsY).Within(EditorTestUtilities.Epsilon));
        }

        Object.DestroyImmediate(tmpSprite);
        Object.DestroyImmediate(tmpTexture);
    }

    private static System.Tuple<Vector2, Vector2>[] s_LocalScalesAndSpriteSizes = {
        new System.Tuple<Vector2, Vector2>(new Vector2(3f, 0.07f), new Vector2(3f, 14f)),
        new System.Tuple<Vector2, Vector2>(new Vector2(123f, 987f), new Vector2(2f, 16f)) };
    [Test, TestCaseSource("s_LocalScalesAndSpriteSizes")]
    public void ConvertSpriteRenderer_ConvertBoundsWithLocalScaleAndSpriteSize_SameExtents(System.Tuple<Vector2, Vector2> localScaleAndSpriteSize)
    {
        var localScale = localScaleAndSpriteSize.Item1;
        var spriteSize = localScaleAndSpriteSize.Item2;

        var tmpTexture = new Texture2D(16, 16);
        var tmpSprite = Sprite.Create(tmpTexture, new Rect(Vector2.zero, spriteSize), Vector2.zero, 1f);

        {
            Root = new GameObject();
            var spriteRenderer = CreateClassicComponent<SpriteRenderer>(Root);
            spriteRenderer.sprite = tmpSprite;
            spriteRenderer.transform.localScale = localScale;
        }

        Assert.DoesNotThrow(() => { RunConversion(Root); });

        using (var query = EntityManager.CreateEntityQuery(typeof(Unity.Tiny.Renderer2D)))
        using (var renderers = query.ToComponentDataArray<Unity.Tiny.Renderer2D>(Allocator.TempJob))
        {
            var objectBound = renderers[0].Bounds;
            var extents = objectBound.Extents;
            var boundExtentsX = localScale.x * spriteSize.x * 0.5f;
            var boundExtentsY = localScale.y * spriteSize.y * 0.5f;

            Assert.That(extents.x, Is.EqualTo(boundExtentsX).Within(EditorTestUtilities.Epsilon));
            Assert.That(extents.y, Is.EqualTo(boundExtentsY).Within(EditorTestUtilities.Epsilon));
        }

        Object.DestroyImmediate(tmpSprite);
        Object.DestroyImmediate(tmpTexture);
    }

    private static Vector2[] s_SpritePivots = { Vector2.one, Vector2.zero, new Vector2(0.5f, 0.25f), new Vector2(2f, 3f),  };
    [Test, TestCaseSource("s_SpritePivots")]
    public void ConvertSpriteRenderer_ConvertLocalBounds_SameCenter(Vector2 spritePivot)
    {
        var tmpTexture = new Texture2D(16, 16);
        var sprite = Sprite.Create(tmpTexture, new Rect(Vector2.zero, Vector2.one), spritePivot, 1f);
        Vector2 pivotInWorldSpace;

        {
            Root = new GameObject();
            var spriteRenderer = CreateClassicComponent<SpriteRenderer>(Root);
            spriteRenderer.sprite = sprite;

            var uWorldToLocalMatrix = spriteRenderer.transform.worldToLocalMatrix;
            pivotInWorldSpace = uWorldToLocalMatrix.MultiplyPoint(spriteRenderer.bounds.center);
        }

        Assert.DoesNotThrow(() => { RunConversion(Root); });

        using (var query = EntityManager.CreateEntityQuery(typeof(Unity.Tiny.Renderer2D)))
        using (var renderers = query.ToComponentDataArray<Unity.Tiny.Renderer2D>(Allocator.TempJob))
        {
            var objectBound = renderers[0].Bounds;
            var pivot = objectBound.Center;

            Assert.That(pivot.x, Is.EqualTo(pivotInWorldSpace.x).Within(EditorTestUtilities.Epsilon));
            Assert.That(pivot.y, Is.EqualTo(pivotInWorldSpace.y).Within(EditorTestUtilities.Epsilon));
        }

        Object.DestroyImmediate(sprite);
        Object.DestroyImmediate(tmpTexture);
    }

    [Test]
    public void ConvertSpriteRenderer_ConvertRenderingLayer_SameRenderingLayer(
        [Values(0, 1, 5, 31)] int renderingLayer)
    {
        {
            Root = new GameObject();
            CreateClassicComponent<SpriteRenderer>(Root);

            Root.layer = renderingLayer;
        }

        Assert.DoesNotThrow(() => { RunConversion(Root); });

        using (var query = EntityManager.CreateEntityQuery(typeof(Unity.Tiny.Renderer2D)))
        using (var renderers = query.ToComponentDataArray<Unity.Tiny.Renderer2D>(Allocator.TempJob))
        {
            var convertedLayer = renderers[0].RenderingLayer;
            Assert.That(convertedLayer, Is.EqualTo(renderingLayer));
        }
    }
}
