using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Tiny.Scenes;
using Unity.Platforms;
using Unity.Core;
using Unity.Entities.Runtime;
using Unity.Scenes;
using Unity.Assertions;
#if ENABLE_PLAYERCONNECTION
using Unity.Development.PlayerConnection;
using System.Diagnostics;
using Unity.Tiny.IO;
#endif
#if ENABLE_DOTSRUNTIME_PROFILER
using Unity.Development.Profiling;
#endif
#if !EXPERIMENTAL_SCENE_LOADING
using SceneSystem = Unity.Tiny.Scenes.SceneStreamingSystem;
#else
using SceneSystem = Unity.Scenes.SceneSystem;
#endif
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Tiny
{
    public class UnityInstance
    {
        public RunLoop.RunLoopDelegate OnTick;

        public enum BootPhase
        {
            Booting = 0,
            LoadingConfig,
            Running
        }

        private readonly World m_World;
        private readonly TinyEnvironment m_Environment;
        private readonly EntityManager m_EntityManager;
        private readonly SceneSystem m_SceneSystem;
#if EXPERIMENTAL_SCENE_LOADING
        private readonly SceneSystemGroup m_SceneSystemGroup;
        private AsyncOp m_CatalogOp;
#endif
        private BootPhase m_BootPhase;
        private Entity m_ConfigScene;
        private NativeList<Entity> m_StartupScenes;

        private double m_StartTimeInSeconds;
        private double m_ElapsedTimeInSeconds;
        private double m_PreviousElapsedTimeInSeconds;

        public World World => m_World;
        public TinyEnvironment Environment => m_Environment;
        public BootPhase Phase => m_BootPhase;

        [DllImport("lib_unity_lowlevel")]
        public static extern void BurstInit();

        private UnityInstance()
        {
            m_World = DefaultWorldInitialization.Initialize("Default World");
#if UNITY_DOTSRUNTIME_EXPERIMENTAL_FIXED_SIM
            TinyInternals.SetSimFixedRate(m_World, 1.0f / 60.0f);
#endif

            m_BootPhase = BootPhase.Booting;
            m_Environment = m_World.GetOrCreateSystem<TinyEnvironment>();
            m_EntityManager = m_World.EntityManager;

#if !EXPERIMENTAL_SCENE_LOADING
            m_SceneSystem = m_World.GetOrCreateSystem<SceneStreamingSystem>();
#else
            m_SceneSystemGroup = m_World.GetOrCreateSystem<SceneSystemGroup>();
            m_SceneSystem = m_World.GetOrCreateSystem<SceneSystem>();
#endif
        }

        public static UnityInstance Initialize()
        {
#if DEBUG
            if (!DotsRuntime.Initialized)
                throw new InvalidOperationException("Unity.Core.DotsRuntime.Initialize() must be called before a UnityInistance can be initialized");
#endif
            BurstInit();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeLeakDetection.Mode = NativeLeakDetectionMode.Enabled;
#endif
            TempMemoryScope.EnterScope();
            TypeManager.Initialize();
            var inst = new UnityInstance();
            TempMemoryScope.ExitScope();
            return inst;
        }

        public void Deinitialize()
        {
            if (m_StartupScenes.IsCreated)
                m_StartupScenes.Dispose();

            TempMemoryScope.EnterScope();

            m_World.Dispose();
            TypeManager.Shutdown();

            // This is a static instance that leaks on first use, so there is no matching
            // Init/Setup call, but we do need to dispose the static instance before shutdown to avoid
            // leaking the native containers in the instance.
            WordStorage.Instance.Dispose();

            TempMemoryScope.ExitScope();
        }

        private double GetDeltaTime(double timestampInSeconds)
        {
            m_ElapsedTimeInSeconds = timestampInSeconds - m_StartTimeInSeconds;
            var dt = m_ElapsedTimeInSeconds - m_PreviousElapsedTimeInSeconds;

            const double k_MaxDeltaTime = 0.5;
            if (dt > k_MaxDeltaTime)
            {
                m_StartTimeInSeconds += dt - k_MaxDeltaTime;
                m_ElapsedTimeInSeconds -= dt - k_MaxDeltaTime;
                dt = k_MaxDeltaTime;
            }

            m_PreviousElapsedTimeInSeconds = m_ElapsedTimeInSeconds;

            Assert.IsTrue(dt > 0.0);

            return dt;
        }

        /// <summary>
        /// Updates UnityInstance state. In Running Phase will set time and update world/worlds
        /// </summary>
        /// <param name="timestampInSeconds">
        /// Timestamp in seconds as a double since some platform-dependent point in time, that will be used to calculate delta and elapsed time.
        /// It is expected to be a timestamp from monotonic high-frequency timer, but on some platforms it is received from a wallclock timer (emscripten, html5)
        ///</param>
        /// <returns>True if Update should be called again, or False if not (fatal error / quit command received)</returns>
        public bool Update(double timestampInSeconds)
        {
            var shouldContinue = true;

            if (m_BootPhase == BootPhase.Running)
            {
                double dt = GetDeltaTime(timestampInSeconds);

                DotsRuntime.UpdatePreFrame();

                // If elapsed time starts from a non-zero point in time, it can break assumptions made in other code
                // (with Entities fixed time stepping logic being a prime example). Even if our code is protected against
                // this, users will often expect elapsed time to accumulate from 0.
                m_World.SetTime(new TimeData(elapsedTime: m_ElapsedTimeInSeconds, deltaTime: (float)dt));
                m_World.Update();

                shouldContinue = !m_World.QuitUpdate;

                DotsRuntime.UpdatePostFrame(shouldContinue);
            }
            else
            {
                TempMemoryScope.EnterScope();
                if (m_BootPhase == BootPhase.Booting)
                {
                    UpdateBooting();
                }
                else if (m_BootPhase == BootPhase.LoadingConfig)
                {
                    shouldContinue = UpdateLoading();

                    if (m_BootPhase == BootPhase.Running)
                    {
                        // Loaded - set up dots runtime with config info
                        var env = World.TinyEnvironment();
                        var config = env.GetConfigData<CoreConfig>();
#if ENABLE_PLAYERCONNECTION
                        Connection.InitializeMulticast(config.editorGuid32, "DOTS_Runtime_Game");
#endif

#if ENABLE_PROFILER
                        ProfilerStats.Stats.debugStats.m_UnityVersionMajor = config.editorVersionMajor;
                        ProfilerStats.Stats.debugStats.m_UnityVersionMinor = config.editorVersionMinor;
                        ProfilerStats.Stats.debugStats.m_UnityVersionRevision = config.editorVersionRevision;
                        ProfilerStats.Stats.debugStats.m_UnityVersionReleaseType = config.editorVersionReleaseType;
                        ProfilerStats.Stats.debugStats.m_UnityVersionIncrementalVersion = config.editorVersionInc;
#endif

                        // Start now so fixed stepping doesn't try to catch up after time spent in booting and loading
                        m_StartTimeInSeconds = timestampInSeconds;
                        m_PreviousElapsedTimeInSeconds = 0;
                    }
                }
                else
                {
                    throw new Exception("Invalid BootPhase specified");
                }
                TempMemoryScope.ExitScope();
            }

            return shouldContinue;
        }

        private void UpdateBooting()
        {
            // Destroy current config entity
            if (m_EntityManager.Exists(m_Environment.configEntity))
            {
                m_EntityManager.DestroyEntity(m_Environment.configEntity);
                m_Environment.configEntity = Entity.Null;
            }

            LoadConfigScene();

            m_BootPhase = BootPhase.LoadingConfig;
        }

        private bool UpdateLoading()
        {
            // On all platforms but web, we can be certain that if we can't load the config scene in 5 seconds
            // we should abort as something is wrong.
#if !UNITY_WEBGL
#if DEBUG
            if (!Debugger.IsAttached)
#endif
            {
                if (m_ElapsedTimeInSeconds > 5.0)
                {
                    Debug.LogError("Failed to load the configuration scene at boot. Shutting down....");
                    return false;
                }
            }
#endif

            // Tick this world specifically to ensure our load requests are handled
            UpdateSceneSystems();

            if (IsSceneLoaded(m_ConfigScene))
            {
                if (m_Environment.configEntity == Entity.Null)
                {
                    using (var configurationQuery = m_EntityManager.CreateEntityQuery(typeof(ConfigurationTag)))
                    {
                        if (configurationQuery.CalculateEntityCount() == 0)
                            throw new Exception($"Failed to load boot configuration scene.");

                        using (var configEntityList = configurationQuery.ToEntityArray(Allocator.TempJob))
                        {
                            // Set new config entity
                            if (configEntityList.Length > 1)
                                throw new Exception($"More than one configuration entity found in boot configuration scene.");

                            m_Environment.configEntity = configEntityList[0];
                        }
                    }
                    return true;
                }
                else if (!LoadStartupScenes(m_Environment))
                    return true;
                else if (!IsStartupDataLoaded())
                    return true;
            }
            else
                return true;

            m_BootPhase = BootPhase.Running;
            return true;
        }

        void LoadConfigScene()
        {
#if !EXPERIMENTAL_SCENE_LOADING
            m_ConfigScene = SceneService.LoadConfigAsync(m_World);
#else
            // TODO: Emscripten seems to have problems loading statics so reading
            // from ConfigurationScene.Guid will pretty reliably come back as all zeros
            var configGuid = new Hash128("46b433b264c69cbd39f04ad2e5d12be8"); // ConfigurationScene.Guid; 
            m_ConfigScene = m_SceneSystem.LoadSceneAsync(configGuid, new SceneSystem.LoadParameters() { AutoLoad = true, Flags = SceneLoadFlags.LoadAdditive });
            m_CatalogOp = IOService.RequestAsyncRead(SceneSystem.GetSceneInfoPath());
#endif
        }

        void UpdateSceneSystems()
        {
#if !EXPERIMENTAL_SCENE_LOADING
            m_SceneSystem.Update();
#else
            m_SceneSystemGroup.Update();
#endif
        }

        bool IsSceneLoaded(Entity sceneEntity)
        {
#if !EXPERIMENTAL_SCENE_LOADING
            var status = SceneService.GetSceneStatus(m_World, sceneEntity);
            if (status == SceneStatus.FailedToLoad)
                throw new Exception("Failed to load scene during boot-up. Aborting...");

            return status == SceneStatus.Loaded;
#else
            return m_SceneSystem.IsSceneLoaded(sceneEntity);
#endif
        }

        private unsafe bool IsStartupDataLoaded()
        {
            bool allLoaded = true;
            foreach (var sceneEntity in m_StartupScenes)
                allLoaded &= IsSceneLoaded(sceneEntity);

            if (allLoaded)
                m_StartupScenes.Dispose();

            return allLoaded;
        }

        private unsafe bool LoadStartupScenes(TinyEnvironment environment)
        {
            if (m_StartupScenes.IsCreated)
                return true;

#if !EXPERIMENTAL_SCENE_LOADING
            using (var startupScenes = environment.GetConfigBufferData<StartupScenes>().ToNativeArray(Allocator.Temp))
            {
                m_StartupScenes = new NativeList<Entity>(startupScenes.Length, Allocator.Persistent);
                for (var i = 0; i < startupScenes.Length; ++i)
                {
                    m_StartupScenes.Add(SceneService.LoadSceneAsync(m_World, startupScenes[i].SceneReference));
                }
            }
            return true;
#else
            var status = m_CatalogOp.GetStatus();
            if (status <= AsyncOp.Status.InProgress)
                return false;

            if (status == AsyncOp.Status.Failure)
            {
                var failureStatus = m_CatalogOp.GetErrorStatus();
                if (failureStatus == AsyncOp.ErrorStatus.FileNotFound)
                    Debug.LogWarning("Missing catalog file from '" + SceneSystem.GetSceneInfoPath() + "'");
                else
                    throw new ArgumentException("Failed to load catalog from '" + SceneSystem.GetSceneInfoPath() + "'. status=" + status + ", errorStatus=" + failureStatus);

                // a missing catalog file is not fatal, as some runtimes don't use scene data
                return true;
            }
            Assert.IsTrue(status == AsyncOp.Status.Success);

            m_CatalogOp.GetData(out var data, out var dataLen);

            if (!BlobAssetReference<ResourceCatalogData>.TryRead(data, ResourceCatalogData.CurrentFileFormatVersion, out var catalogData))
                throw new ArgumentException("Unable to parse catalog data from " + SceneSystem.GetSceneInfoPath());

            m_SceneSystem.SetCatalogData(catalogData);
            m_CatalogOp.Dispose();

            //if running in LiveLink mode, the initial scenes list is sent from the editor.  otherwise use the flags in the scene data.
            //if (!LiveLinkUtility.LiveLinkEnabled)
            {
                m_StartupScenes = new NativeList<Entity>(catalogData.Value.resources.Length, Allocator.Persistent);
                for (int i = 0; i < catalogData.Value.resources.Length; i++)
                {
                    if (catalogData.Value.resources[i].ResourceType == ResourceMetaData.Type.Scene &&
                        (catalogData.Value.resources[i].ResourceFlags & ResourceMetaData.Flags.AutoLoad) == ResourceMetaData.Flags.AutoLoad)
                    {
                        var sceneGuid = catalogData.Value.resources[i].ResourceId;
                        m_SceneSystem.LoadSceneAsync(sceneGuid, new SceneSystem.LoadParameters() { Flags = SceneLoadFlags.LoadAdditive });
                        m_StartupScenes.Add(m_SceneSystem.GetSceneEntity(sceneGuid));
                    }
                }
            }

            return true;
#endif
        }
    }
}
