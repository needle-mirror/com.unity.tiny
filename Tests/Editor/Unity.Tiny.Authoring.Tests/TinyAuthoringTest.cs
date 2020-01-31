using NUnit.Framework;
using Unity.Tiny.Authoring;
using Unity.Entities;
using UnityEngine;
using  Unity.Tiny;
using UnityEditor;

class TinyAuthoringTest
{
    protected World World;
    protected EntityManager m_Manager;
    
    [SetUp]
    public virtual void Setup()
    {
        World = World.DefaultGameObjectInjectionWorld = new World("Test World");
        m_Manager = World.EntityManager;
    }
    
    [Test]
    public void TestDisplayInfoDefaults()
    {
        var go = new GameObject("Configuration");
        var di = go.AddComponent<TinyDisplayInfo>();
        Assert.True(di.AutoSizeToFrame);
        Assert.AreEqual(di.Resolution, new Vector2Int(0, 0));
    }
    
    [Test]
    public void TestDisplayInfoConversion()
    {
        var go = new GameObject("Configuration");
        var di = go.AddComponent<TinyDisplayInfo>();
        di.Resolution = new Vector2Int(100,100);
        var settings = new GameObjectConversionSettings { DestinationWorld = World, ConversionFlags = GameObjectConversionUtility.ConversionFlags.AssignName };
        var entity = GameObjectConversionUtility.ConvertGameObjectHierarchy(go, settings);
        Assert.IsTrue(m_Manager.HasComponent<DisplayInfo>(entity));
        Assert.IsTrue(m_Manager.GetComponentData<DisplayInfo>(entity).autoSizeToFrame);
        Assert.AreEqual(di.Resolution.x,m_Manager.GetComponentData<DisplayInfo>(entity).width);
        Assert.AreEqual(di.Resolution.y,m_Manager.GetComponentData<DisplayInfo>(entity).height);
        Assert.IsTrue(m_Manager.GetComponentData<DisplayInfo>(entity).visible);
        Assert.AreEqual(PlayerSettings.colorSpace == ColorSpace.Gamma, m_Manager.GetComponentData<DisplayInfo>(entity).disableSRGB);
    }
    
    [TearDown]
    public virtual void TearDown()
    {
        World.DisposeAllWorlds(); 
        World = null;
        m_Manager = null;
    }
}