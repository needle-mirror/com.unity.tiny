using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using Unity.Build;
using UnityEditor;

class TinyAudioAuthoringTest
{
    protected World World;
    protected EntityManager m_Manager;
    protected GameObjectConversionSettings settings;
    private const string buildConfigPath =
        "Packages/com.unity.tiny/Tests/Editor/Unity.Tiny.Audio.Authoring.Tests/DotsRuntimeBuildConfiguration.buildconfiguration";

    [SetUp]
    public virtual void Setup()
    {
        World = World.DefaultGameObjectInjectionWorld = new World("Test World");
        m_Manager = World.EntityManager;
        settings = GameObjectConversionSettings.FromWorld(World, new BlobAssetStore());
        string buildConfigurationGuid = AssetDatabase.AssetPathToGUID(buildConfigPath);
        var buildConfig = BuildConfiguration.LoadAsset(new GUID(buildConfigurationGuid));
        buildConfig.hideFlags = HideFlags.HideAndDontSave;
        settings.BuildConfiguration = buildConfig;
    }

    [Test]
    public void TestAudioSourceConversion()
    {
        var gameObject = new GameObject("TestObject");
        gameObject.AddComponent<AudioSource>();
        var entity = GameObjectConversionUtility.ConvertGameObjectHierarchy(gameObject, settings);
        Assert.IsTrue(m_Manager.HasComponent<Unity.Tiny.Audio.AudioSource>(entity));
        Assert.IsTrue(m_Manager.HasComponent<Unity.Tiny.Audio.Audio2dPanning>(entity));
    }

    [Test]
    public void TestAudioSourceValues()
    {
        var gameObject = new GameObject("TestObject");
        var audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.volume = 0.5f;
        audioSource.playOnAwake = true;
        audioSource.loop = true;
        audioSource.panStereo = -0.1f;
        var entity = GameObjectConversionUtility.ConvertGameObjectHierarchy(gameObject, settings);
        Assert.IsTrue(m_Manager.HasComponent<Unity.Tiny.Audio.AudioSource>(entity));
        Assert.IsTrue(m_Manager.HasComponent<Unity.Tiny.Audio.Audio2dPanning>(entity));
        Assert.IsTrue(m_Manager.HasComponent<Unity.Tiny.Audio.AudioSourceStart>(entity));
        var tinyAudioSource = m_Manager.GetComponentData<Unity.Tiny.Audio.AudioSource>(entity);
        var tinyAudio2dPanning = m_Manager.GetComponentData<Unity.Tiny.Audio.Audio2dPanning>(entity);
        Assert.IsTrue(tinyAudioSource.loop);
        Assert.AreEqual(tinyAudioSource.volume, 0.5f);
        Assert.AreEqual(tinyAudio2dPanning.pan, -0.1f);
    }

    [Test]
    public void TestAudioSource3dConversion()
    {
        var gameObject = new GameObject("TestObject");
        var audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1.0f;
        var entity = GameObjectConversionUtility.ConvertGameObjectHierarchy(gameObject, settings);
        Assert.IsTrue(m_Manager.HasComponent<Unity.Tiny.Audio.AudioSource>(entity));
        Assert.IsTrue(m_Manager.HasComponent<Unity.Tiny.Audio.Audio3dPanning>(entity));
        Assert.IsTrue(m_Manager.HasComponent<Unity.Tiny.Audio.AudioDistanceAttenuation>(entity));
    }

    [Test]
    public void TestAudioSource3dValues()
    {
        var gameObject = new GameObject("TestObject");
        var audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1.0f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.minDistance = 2.0f;
        audioSource.maxDistance = 40.0f;
        var entity = GameObjectConversionUtility.ConvertGameObjectHierarchy(gameObject, settings);
        var tinyAudioDistanceAttenuation = m_Manager.GetComponentData<Unity.Tiny.Audio.AudioDistanceAttenuation>(entity);
        Assert.AreEqual(tinyAudioDistanceAttenuation.rolloffMode.ToString(), AudioRolloffMode.Logarithmic.ToString());
        Assert.AreEqual(tinyAudioDistanceAttenuation.minDistance, 2.0f);
        Assert.AreEqual(tinyAudioDistanceAttenuation.maxDistance, 40.0f);
    }

    [Test]
    public void TestAudioSourcePitchConversionAndValues()
    {
        var gameObject = new GameObject("TestObject");
        var audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.pitch = 2.0f;
        var entity = GameObjectConversionUtility.ConvertGameObjectHierarchy(gameObject, settings);
        Assert.IsTrue(m_Manager.HasComponent<Unity.Tiny.Audio.AudioSource>(entity));
        Assert.IsTrue(m_Manager.HasComponent<Unity.Tiny.Audio.AudioPitch>(entity));
        var tinyAudioPitch = m_Manager.GetComponentData<Unity.Tiny.Audio.AudioPitch>(entity);
        Assert.AreEqual(tinyAudioPitch.pitch, 2.0f);
    }

    [TearDown]
    public virtual void TearDown()
    {
        World.DisposeAllWorlds();
        World = null;
        m_Manager = default;
        if (settings != null)
            settings.BlobAssetStore.Dispose();
    }
}
