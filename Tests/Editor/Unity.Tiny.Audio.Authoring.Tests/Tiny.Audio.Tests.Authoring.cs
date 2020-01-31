using NUnit.Framework;
using Unity.Entities;
using UnityEngine;

class TinyAudioAuthoringTest
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
    public void TestAudioSourceConversion()
    {
        var gameObject = new GameObject("TestObject");
        gameObject.AddComponent<AudioSource>();
        var settings = new GameObjectConversionSettings { DestinationWorld = World, ConversionFlags = GameObjectConversionUtility.ConversionFlags.AssignName };
        var entity = GameObjectConversionUtility.ConvertGameObjectHierarchy(gameObject, settings);
        Assert.IsTrue(m_Manager.HasComponent<Unity.Tiny.Audio.AudioSource>(entity));
    }

    [Test]
    public void TestAudioSourceValues()
    {
        var gameObject = new GameObject("TestObject");
        var audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.volume = 0.5f;
        audioSource.playOnAwake = true;
        audioSource.loop = true;
        var settings = new GameObjectConversionSettings { DestinationWorld = World, ConversionFlags = GameObjectConversionUtility.ConversionFlags.AssignName };
        var entity = GameObjectConversionUtility.ConvertGameObjectHierarchy(gameObject, settings);
        var tinyAudioSource = m_Manager.GetComponentData<Unity.Tiny.Audio.AudioSource>(entity);
        Assert.IsTrue(tinyAudioSource.loop);
        Assert.AreEqual(tinyAudioSource.volume, 0.5f);
        Assert.IsTrue(m_Manager.HasComponent<Unity.Tiny.Audio.AudioSourceStart>(entity));
    }

    [TearDown]
    public virtual void TearDown()
    {
        World.DisposeAllWorlds(); 
        World = null;
        m_Manager = null;
    }

}