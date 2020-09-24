using UnityEngine;
using NUnit.Framework;
using Unity.Entities;

public class AuthoringTestFixture
{
    protected GameObject Root { get; set; }
    protected GameObject Child { get; private set; }
    protected BlobAssetStore BlobStore { get; set; }
    protected World World { get; set; }
    protected EntityManager EntityManager => World.EntityManager;

    [SetUp]
    protected virtual void Setup()
    {
        World = new World("Test Conversion World");
        BlobStore = new BlobAssetStore();
    }

    [TearDown]
    protected virtual void TearDown()
    {
        if (Root != null)
        {
            GameObject.DestroyImmediate(Root);
            Root = null;
        }

        Child = null;

        BlobStore.Dispose();
        World.Dispose();
    }

    protected T CreateClassicComponent<T>(GameObject gameObject) where T : Component => gameObject.AddComponent<T>();

    protected bool RunConversion(GameObject gameObject)
    {
        var settings = GameObjectConversionSettings.FromWorld(World, BlobStore);
        settings.FilterFlags = WorldSystemFilterFlags.GameObjectConversion;

        var convertedEntity = GameObjectConversionUtility.ConvertGameObjectHierarchy(gameObject, settings);

        var wasConversionSuccessful = convertedEntity != Entity.Null;
        return wasConversionSuccessful;
    }

    protected void CleanupWorld()
    {
        var allEntities = EntityManager.GetAllEntities();
        EntityManager.DestroyEntity(allEntities);
    }
}
