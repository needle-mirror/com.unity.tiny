using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Platforms;
using Unity.Core;
using Unity.Entities.Runtime;
using Unity.Scenes;
using Unity.Assertions;
using Unity.Tiny.IO;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Linker.StrippingControls.Balanced; // Workaround for case 1276535
#if ENABLE_PLAYERCONNECTION
using Unity.Development.PlayerConnection;
using System.Diagnostics;
#endif
#if ENABLE_DOTSRUNTIME_PROFILER
using Unity.Development.Profiling;
#endif
using SceneSystem = Unity.Scenes.SceneSystem;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Tiny
{
    public unsafe class UnityInstance
    {
        public RunLoop.RunLoopDelegate OnTick;

        public enum BootPhase
        {
            Booting = 0,
            LoadingConfig,
            Running,
        }

        public enum RunState
        {
            Running = 0,
            Suspended,
            Resuming,     // Adjust the time base so that elapsed time continues from "now" after a suspend event or similarly after Booting/LoadConfig
        }

        private readonly World m_World;
        private readonly EntityManager m_EntityManager;
        private readonly SceneSystem m_SceneSystem;
        private readonly SceneSystemGroup m_SceneSystemGroup;
        private AsyncOp m_CatalogOp;
        private BootPhase m_BootPhase;
        private RunState m_RunState;
        private Entity m_ConfigScene;
        private Entity m_ConfigEntity;
        private NativeList<Entity> m_StartupScenes;

        private double m_StartTimeInSeconds;
        private double m_ElapsedTimeInSeconds;
        private double m_PreviousElapsedTimeInSeconds;
        private double m_DeltaTimeInSeconds;
        private TimeData* m_TimeData;

        public World World => m_World;
        public BootPhase Phase => m_BootPhase;

        public bool Suspended
        {
            get
            {
                return m_RunState == RunState.Suspended;
            }
            set
            {
                m_RunState = value ? RunState.Suspended : RunState.Resuming;
            }
        }

        [DllImport("lib_unity_lowlevel")]
        public static extern void BurstInit();

        private UnityInstance()
        {
            m_World = DefaultWorldInitialization.Initialize("Default World");

            m_BootPhase = BootPhase.Booting;
            m_RunState = RunState.Resuming;
            m_EntityManager = m_World.EntityManager;

            m_StartTimeInSeconds = 0;
            m_ElapsedTimeInSeconds = 0;
            m_PreviousElapsedTimeInSeconds = 0;

            m_SceneSystemGroup = m_World.GetOrCreateSystem<SceneSystemGroup>();
            m_SceneSystem = m_World.GetOrCreateSystem<SceneSystem>();

            m_TimeData = (TimeData*)UnsafeUtility.Malloc(
                UnsafeUtility.SizeOf<TimeData>(),
                0,
                Collections.Allocator.Persistent);
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
            UnsafeUtility.Free(m_TimeData, Collections.Allocator.Persistent);
            TempMemoryScope.ExitScope();
        }

        private void ComputeTime(double timestampInSeconds)
        {
            m_ElapsedTimeInSeconds = timestampInSeconds - m_StartTimeInSeconds;
            var dt = m_ElapsedTimeInSeconds - m_PreviousElapsedTimeInSeconds;

            double maxDeltaTime = m_World.MaximumDeltaTime;
            if (dt > maxDeltaTime)
            {
                m_StartTimeInSeconds += dt - maxDeltaTime;
                m_ElapsedTimeInSeconds -= dt - maxDeltaTime;
                dt = maxDeltaTime;
            }

            m_PreviousElapsedTimeInSeconds = m_ElapsedTimeInSeconds;

            Assert.IsTrue(dt > 0.0);
            m_DeltaTimeInSeconds = dt;
        }

        /// <summary>
        /// Updates UnityInstance state. In Running Phase will set time and update world/worlds
        /// </summary>
        /// <param name="timestampInSeconds">
        /// Timestamp in seconds as a double since some platform-dependent point in time, that will be used to calculate delta and elapsed time.
        /// It is expected to be a timestamp from monotonic high-frequency timer, but on some platforms it is received from a wallclock timer (emscripten, html5)
        ///</param>
        /// <returns>True if Update should be called again, or False if not (fatal error / quit command received)</returns>
        [PreserveBody] // Workaround for case 1276535
        public bool Update(double timestampInSeconds)
        {
            var shouldContinue = true;

            if (m_BootPhase == BootPhase.Running)
            {
                switch (m_RunState)
                {
                    case RunState.Resuming:
                        m_RunState = RunState.Running;

                        m_StartTimeInSeconds = timestampInSeconds - m_ElapsedTimeInSeconds;
                        m_PreviousElapsedTimeInSeconds = m_ElapsedTimeInSeconds;
                        goto case RunState.Running;

                    case RunState.Running:
                        ComputeTime(timestampInSeconds);
                        *m_TimeData = new TimeData(
                            elapsedTime: m_ElapsedTimeInSeconds,
                            deltaTime: (float)m_DeltaTimeInSeconds);

                        DotsRuntime.UpdatePreFrame();

                        m_World.Update();
                        shouldContinue = !m_World.QuitUpdate;

                        DotsRuntime.UpdatePostFrame(shouldContinue);
                        break;
                }
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
                        var config = m_EntityManager.GetComponentData<CoreConfig>(m_ConfigEntity);

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
                        // Hook instance time into each world if the required system exists
                        foreach (var world in World.All)
                        {
                            var timeSystem = world.GetExistingSystem<UpdateWorldTimeSystem>();
                            if (timeSystem == null) continue;
                            timeSystem.SetInstanceTime(m_TimeData);
                        }
                    }
                }
                else
                {
                    throw new Exception($"Invalid BootPhase specified: {(int)m_BootPhase}");
                }
                TempMemoryScope.ExitScope();
            }

            return shouldContinue;
        }

        private void UpdateBooting()
        {
            // Destroy current config entity
            if (m_EntityManager.Exists(m_ConfigEntity))
            {
                m_EntityManager.DestroyEntity(m_ConfigEntity);
                m_ConfigEntity = Entity.Null;
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
                    UnityEngine.Debug.LogError("Failed to load the configuration scene at boot. Shutting down....");
                    return false;
                }
            }
#endif

            // Tick this world specifically to ensure our load requests are handled
            UpdateSceneSystems();

            if (IsSceneLoaded(m_ConfigScene))
            {
                if (m_ConfigEntity == Entity.Null)
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

                            m_ConfigEntity = configEntityList[0];
                        }
                    }
                    return true;
                }
                else if (!LoadStartupScenes())
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
            // TODO: Emscripten seems to have problems loading statics so reading
            // from ConfigurationScene.Guid will pretty reliably come back as all zeros
            var configGuid = new Hash128("46b433b264c69cbd39f04ad2e5d12be8"); // ConfigurationScene.Guid; 
            m_ConfigScene = m_SceneSystem.LoadSceneAsync(configGuid, new SceneSystem.LoadParameters() { AutoLoad = true, Flags = SceneLoadFlags.LoadAdditive });
            m_CatalogOp = IOService.RequestAsyncRead(SceneSystem.GetSceneInfoPath());
        }

        void UpdateSceneSystems()
        {
            m_SceneSystemGroup.Update();
        }

        bool IsSceneLoaded(Entity sceneEntity)
        {
            return m_SceneSystem.IsSceneLoaded(sceneEntity);
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

        private unsafe bool LoadStartupScenes()
        {
            if (m_StartupScenes.IsCreated)
                return true;

            var status = m_CatalogOp.GetStatus();
            if (status <= AsyncOp.Status.InProgress)
                return false;

            if (status == AsyncOp.Status.Failure)
            {
                var failureStatus = m_CatalogOp.GetErrorStatus();
                if (failureStatus == AsyncOp.ErrorStatus.FileNotFound)
                    UnityEngine.Debug.LogWarning("Missing catalog file from '" + SceneSystem.GetSceneInfoPath() + "'");
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
        }
    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public unsafe class UpdateWorldTimeSystem : ComponentSystem
    {
        private TimeData* m_TimeData;

        internal void SetInstanceTime(TimeData* timeData)
        {
            m_TimeData = timeData;
        }

        protected override void OnUpdate()
        {
            if (m_TimeData == null)
            {
                // Look for a valid Time reference (NB: assumes one UnityInstance!)
                foreach (var world in World.All)
                {
                    var timeSystem = world.GetExistingSystem<UpdateWorldTimeSystem>();
                    if (timeSystem == null) continue;
                    m_TimeData = timeSystem.m_TimeData;
                    break;
                }

                if (m_TimeData == null)
                {
                    throw new Exception(
                          "UpdateWorldTimeSystem must either be disabled "
                        + "or initialized with SetInstanceTime(TimeData*) before use.");
                }
            }

            // If elapsed time starts from a non-zero point in time, it can break assumptions made in other code
            // (with Entities fixed time stepping logic being a prime example). Even if our code is protected against
            // this, users will often expect elapsed time to accumulate from 0.
            World.SetTime(*m_TimeData);
        }
    }
}
