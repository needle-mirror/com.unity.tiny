using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Tiny.Scenes;
using Unity.Platforms;
using Unity.Core;
using Unity.Entities.Runtime;

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
        private readonly SceneStreamingSystem m_SceneStreamingSystem;
        private BootPhase m_BootPhase;
        private Entity m_ConfigScene;

        public World World => m_World;
        public BootPhase Phase => m_BootPhase;

        [DllImport("lib_unity_lowlevel")]
        public static extern void BurstInit();

        private UnityInstance()
        {
            m_World = DefaultWorldInitialization.InitializeWorld("Default World");
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(m_World);
#if UNITY_DOTSPLAYER_EXPERIMENTAL_FIXED_SIM
            TinyInternals.SetSimFixedRate(m_World, 1.0f / 60.0f);
#endif
            m_BootPhase = BootPhase.Booting;
            m_Environment = m_World.GetOrCreateSystem<TinyEnvironment>();
            m_EntityManager = m_World.EntityManager;
            m_SceneStreamingSystem = m_World.GetExistingSystem<SceneStreamingSystem>();
        }

        public static UnityInstance Initialize()
        {
            BurstInit();

#if UNITY_DOTSPLAYER
            DotsRuntime.Initialize();
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeLeakDetection.Mode = NativeLeakDetectionMode.Enabled;
#endif

            TypeManager.Initialize();
            return new UnityInstance();
        }

        public void Deinitialize()
        {
            m_World.Dispose();
            TypeManager.Shutdown();
#if UNITY_DOTSPLAYER
            DotsRuntime.Shutdown();
#endif
        }

        public bool Update()
        {
            var shouldContinue = true;

            if (m_BootPhase == BootPhase.Running)
            {
#if UNITY_DOTSPLAYER
                DotsRuntime.UpdatePreFrame();
#endif

                m_World.Update();
                shouldContinue = !m_World.QuitUpdate;

#if UNITY_DOTSPLAYER
                DotsRuntime.UpdatePostFrame(shouldContinue);
#endif
            }
            else
            {
                if (m_BootPhase == BootPhase.Booting)
                {
                    UpdateBooting();
                }
                else if (m_BootPhase == BootPhase.LoadingConfig)
                {
                    UpdateLoadingConfig();
                }
                else
                {
                    throw new Exception("Invalid BootPhase specified");
                }
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

            m_ConfigScene = SceneService.LoadConfigAsync(m_World);

            m_BootPhase = BootPhase.LoadingConfig;
        }

        private void UpdateLoadingConfig()
        {
            // Tick this world specifically to ensure our load requests are handled
            m_SceneStreamingSystem.Update();

            var configStatus = SceneService.GetSceneStatus(m_World, m_ConfigScene);
            if (configStatus == SceneStatus.Loaded)
            {
                if (m_Environment.configEntity == Entity.Null)
                {
                    using (var configurationQuery = m_EntityManager.CreateEntityQuery(typeof(ConfigurationTag)))
                    {
                        if (configurationQuery.CalculateEntityCount() == 0)
                        {
                            throw new Exception($"Failed to load boot configuration scene.");
                        }

                        using (var configEntityList = configurationQuery.ToEntityArray(Allocator.Temp))
                        {
                            // Set new config entity
                            if (configEntityList.Length > 1)
                            {
                                throw new Exception(
                                    $"More than one configuration entity found in boot configuration scene.");
                            }

                            m_Environment.configEntity = configEntityList[0];
                        }
                    }
                }
            }
            else if (configStatus == SceneStatus.FailedToLoad)
            {
                throw new Exception($"Failed to load the boot configuration scene.");
            }
            else
            {
                return;
            }

            LoadStartupScenes(m_Environment);
            m_BootPhase = BootPhase.Running;
        }

        private void LoadStartupScenes(TinyEnvironment environment)
        {
            using (var startupScenes = environment.GetConfigBufferData<StartupScenes>().ToNativeArray(Allocator.Temp))
            {
                for (var i = 0; i < startupScenes.Length; ++i)
                {
                    SceneService.LoadSceneAsync(m_World, startupScenes[i].SceneReference);
                }
            }
        }
    }
}
