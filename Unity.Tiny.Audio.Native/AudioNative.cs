using System;
using Unity.Entities;
using Unity.Tiny.GenericAssetLoading;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Unity.Platforms;

#if ENABLE_PLAYERCONNECTION
using UnityEngine.Networking.PlayerConnection;
#endif

#if ENABLE_DOTSRUNTIME_PROFILER
using Unity.Development.Profiling;
#endif

[assembly: InternalsVisibleTo("Unity.Tiny.Audio.Tests")]
namespace Unity.Tiny.Audio
{
    struct AudioNativeClip : ISystemStateComponentData
    {
        public uint clipID;
    }

    struct AudioNativeLoading : ISystemStateComponentData
    {
    }

    struct AudioNativeSource : ISystemStateComponentData
    {
        public uint sourceID;
    }

    static class AudioNativeCalls
    {
        private const string DLL = "lib_unity_tiny_audio_native";

        // Mixer
        [DllImport(DLL, EntryPoint = "initAudio")]
        public static extern void InitAudio();

        [DllImport(DLL, EntryPoint = "destroyAudio")]
        public static extern void DestroyAudio();

        [DllImport(DLL, EntryPoint = "reinitAudio")]
        public static extern void ReinitAudio();

        [DllImport(DLL, EntryPoint = "hasDefaultDeviceChanged")]
        [return : MarshalAs(UnmanagedType.I1)]
        public static extern bool HasDefaultDeviceChanged();

        [DllImport(DLL, EntryPoint = "getAudioOutputTimeInFrames")]
        public static extern ulong GetAudioOutputTimeInFrames();

        // Clip
        [DllImport(DLL, EntryPoint = "startLoad", CharSet = CharSet.Ansi)]
        public static extern uint StartLoad([MarshalAs(UnmanagedType.LPStr)] string imageFile);    // returns clipID

        [DllImport(DLL, EntryPoint = "freeAudio")]
        public static extern void FreeAudio(uint clipID);

        [DllImport(DLL, EntryPoint = "abortLoad")]
        public static extern void AbortLoad(uint clipID);

        [DllImport(DLL, EntryPoint = "checkLoading")]
        public static extern int CheckLoading(uint clipID);     // 0=still working, 1=ok, 2=fail

        [DllImport(DLL, EntryPoint = "finishedLoading")]
        public static extern void FinishedLoading(uint clipID);

#if ENABLE_DOTSRUNTIME_PROFILER
        [DllImport(DLL, EntryPoint = "getCpuUsage")]
        public static extern float GetCpuUsage();

        [DllImport(DLL, EntryPoint = "getRequiredMemory")]
        public static extern int GetRequiredMemory(uint clipID);
#endif
        [DllImport(DLL, EntryPoint = "clipPoolID")]
        public static extern int ClipPoolID();      // Testing: the next ID that will be assigned to a clip.

        // Source
        [DllImport(DLL, EntryPoint = "playSource")]
        public static extern uint Play(uint clipID, float volume, float pan, bool loop);    // returns sourceID (>0) or 0 or failure.

        [DllImport(DLL, EntryPoint = "isPlaying")]
        [return : MarshalAs(UnmanagedType.I1)]
        public static extern bool IsPlaying(uint sourceID);

        [DllImport(DLL, EntryPoint = "stopSource")]
        [return : MarshalAs(UnmanagedType.I1)]
        public static extern bool Stop(uint sourceID);    // returns success (or failure)

        [DllImport(DLL, EntryPoint = "pauseAudio")]
        public static extern void PauseAudio(bool doPause);    // returns success (or failure)

        [DllImport(DLL, EntryPoint = "setVolume")]
        [return : MarshalAs(UnmanagedType.I1)]
        public static extern bool SetVolume(uint sourceId, float volume);    // returns success (or failure)

        [DllImport(DLL, EntryPoint = "setPan")]
        [return : MarshalAs(UnmanagedType.I1)]
        public static extern bool SetPan(uint sourceId, float pan);    // returns success (or failure)

        [DllImport(DLL, EntryPoint = "setPitch")]
        [return : MarshalAs(UnmanagedType.I1)]
        public static extern bool SetPitch(uint sourceId, float pitch);    // returns success (or failure)

        [DllImport(DLL, EntryPoint = "setIsMuted")]
        public static extern void SetIsMuted(bool isMuted);

        [DllImport(DLL, EntryPoint = "numSourcesAllocated")]
        public static extern int NumSourcesAllocated();          // Testing: number of SoundSources allocated.

        [DllImport(DLL, EntryPoint = "numClipsAllocated")]
        public static extern int NumClipsAllocated();            // Testing: number of SoundClips allocated.

        [DllImport(DLL, EntryPoint = "sourcePoolID")]
        public static extern int SourcePoolID();                 // Testing: the next ID that will be assigned to a source. (Useful to tell if a source changed.)
    }

    class AudioNativeSystemLoadFromFile : IGenericAssetLoader<AudioClip, AudioNativeClip, AudioClipLoadFromFile, AudioNativeLoading>
    {
        public void StartLoad(
            EntityManager entityManager,
            Entity e,
            ref AudioClip audioClip,
            ref AudioNativeClip audioNativeClip,
            ref AudioClipLoadFromFile loader,
            ref AudioNativeLoading nativeLoading)
        {
            if (audioNativeClip.clipID != 0)
                AudioNativeCalls.AbortLoad(audioNativeClip.clipID);

            if (!entityManager.HasComponent<AudioClipLoadFromFileAudioFile>(e))
            {
                audioNativeClip.clipID = 0;
                audioClip.status = AudioClipStatus.LoadError;
                return;
            }

            string path = entityManager.GetBufferAsString<AudioClipLoadFromFileAudioFile>(e);

            audioNativeClip.clipID = AudioNativeCalls.StartLoad(path);
            audioClip.status = audioNativeClip.clipID > 0 ? AudioClipStatus.Loading : AudioClipStatus.LoadError;
        }

        public LoadResult CheckLoading(IntPtr wrapper,
            EntityManager man,
            Entity e,
            ref AudioClip audioClip, ref AudioNativeClip audioNativeClip, ref AudioClipLoadFromFile param, ref AudioNativeLoading nativeLoading)
        {
            LoadResult result = (LoadResult)AudioNativeCalls.CheckLoading(audioNativeClip.clipID);

            if (result == LoadResult.success)
            {
                audioClip.status = AudioClipStatus.Loaded;
#if ENABLE_DOTSRUNTIME_PROFILER
                ProfilerStats.AccumStats.memAudioCount.Accumulate(1);
                int mem = AudioNativeCalls.GetRequiredMemory(audioNativeClip.clipID);
                ProfilerStats.AccumStats.memAudio.Accumulate(mem);
                ProfilerStats.AccumStats.memReservedAudio.Accumulate(mem);
                ProfilerStats.AccumStats.memUsedAudio.Accumulate(mem);

                // All audio clips in native Tiny audio are decompressed on load
                ProfilerStats.AccumStats.audioSampleMemory.Accumulate(mem);
#endif
            }
            else if (result == LoadResult.failed)
                audioClip.status = AudioClipStatus.LoadError;

            return result;
        }

        public void FreeNative(EntityManager man, Entity e, ref AudioNativeClip audioNativeClip)
        {
#if ENABLE_DOTSRUNTIME_PROFILER
            ProfilerStats.AccumStats.memAudioCount.Accumulate(-1);
            int mem = AudioNativeCalls.GetRequiredMemory(audioNativeClip.clipID);
            ProfilerStats.AccumStats.memAudio.Accumulate(-mem);
            ProfilerStats.AccumStats.memReservedAudio.Accumulate(-mem);
            ProfilerStats.AccumStats.memUsedAudio.Accumulate(-mem);

            ProfilerStats.AccumStats.audioSampleMemory.Accumulate(-mem);
#endif
            AudioNativeCalls.FreeAudio(audioNativeClip.clipID);
        }

        public void FinishLoading(EntityManager man, Entity e, ref AudioClip audioClip, ref AudioNativeClip audioNativeClip, ref AudioNativeLoading nativeLoading)
        {
            AudioNativeCalls.FinishedLoading(audioNativeClip.clipID);
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(AudioNativeSystem))]
    class AudioIONativeSystem : GenericAssetLoader<AudioClip, AudioNativeClip, AudioClipLoadFromFile, AudioNativeLoading>
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            c = new AudioNativeSystemLoadFromFile();
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    class AudioNativeSystem : AudioSystem
    {
        private double lastWorldTimeAudioConsumed = 0.0;
        private ulong lastAudioOutputTimeInFrames = 0;

        #if ENABLE_PLAYERCONNECTION
        static bool s_Muted;
        #endif

        // for use with the toggle mute callback emitted from the editor
        static readonly Guid k_EditorMuteMessageId = new Guid("01372e16-3f1f-4b47-8d09-b48c7c8a3f4d");

        protected override void OnStartRunning()
        {
            PlatformEvents.OnSuspendResume += OnSuspendResume;
        }

        protected override void OnStopRunning()
        {
            PlatformEvents.OnSuspendResume -= OnSuspendResume;
        }


        #if ENABLE_PLAYERCONNECTION
        static void ToggleMuteFromEditor(MessageEventArgs args)
        {
            s_Muted = !s_Muted;
            AudioNativeCalls.SetIsMuted(s_Muted);
        }
        #endif

        protected override void InitAudioSystem()
        {
            AudioNativeCalls.InitAudio();

            AudioConfig ac = default;
            Entity eAudioConfig = Entity.Null;
            if (!HasSingleton<AudioConfig>())
                eAudioConfig = EntityManager.CreateEntity(typeof(AudioConfig));
            else
                eAudioConfig = GetSingletonEntity<AudioConfig>();

            #if ENABLE_PLAYERCONNECTION
            PlayerConnection.instance.Register(k_EditorMuteMessageId, ToggleMuteFromEditor);
            #endif

            ac.initialized = true;
            ac.unlocked = true;

            EntityManager.SetComponentData(eAudioConfig, ac);
        }

        protected override void DestroyAudioSystem()
        {
            #if ENABLE_PLAYERCONNECTION
            PlayerConnection.instance.Unregister(k_EditorMuteMessageId,  ToggleMuteFromEditor);
            #endif

            AudioNativeCalls.DestroyAudio();

            if (HasSingleton<AudioConfig>())
                EntityManager.DestroyEntity(GetSingletonEntity<AudioConfig>());
        }

        protected override bool PlaySource(Entity e)
        {
            var mgr = EntityManager;

            if (mgr.HasComponent<AudioSource>(e))
            {
                AudioSource audioSource = mgr.GetComponentData<AudioSource>(e);

                Entity clipEntity = audioSource.clip;
                if (mgr.HasComponent<AudioNativeClip>(clipEntity))
                {
                    AudioNativeClip clip = mgr.GetComponentData<AudioNativeClip>(clipEntity);
                    if (clip.clipID > 0)
                    {
                        // If there is an existing source, it should re-start.
                        // Do this with a Stop() and let it play below.
                        if (mgr.HasComponent<AudioNativeSource>(e))
                        {
                            AudioNativeSource ans = mgr.GetComponentData<AudioNativeSource>(e);
                            AudioNativeCalls.Stop(ans.sourceID);
                        }

                        float volume = audioSource.volume;
                        float pan = mgr.HasComponent<Audio2dPanning>(e) ? mgr.GetComponentData<Audio2dPanning>(e).pan : 0.0f;

                        // For 3d sounds, we start at volume zero because we don't know if this sound is close or far from the listener.
                        // It is much smoother to ramp up volume from zero than the alternative.
                        if (mgr.HasComponent<Audio3dPanning>(e))
                            volume = 0.0f;

                        uint sourceID = AudioNativeCalls.Play(clip.clipID, volume, pan, audioSource.loop);

                        AudioNativeSource audioNativeSource = new AudioNativeSource()
                        {
                            sourceID = sourceID
                        };
                        if (mgr.HasComponent<AudioNativeSource>(e))
                        {
                            mgr.SetComponentData(e, audioNativeSource);
                        }
                        else
                        {
                            mgr.AddComponentData(e, audioNativeSource);
                        }
                        return true;
                    }
                }
            }

            return false;
        }

        protected override void StopSource(Entity e)
        {
            if (EntityManager.HasComponent<AudioNativeSource>(e))
            {
                AudioNativeSource audioNativeSource = EntityManager.GetComponentData<AudioNativeSource>(e);
                if (audioNativeSource.sourceID > 0)
                {
                    AudioNativeCalls.Stop(audioNativeSource.sourceID);
                }
            }
        }

        protected override bool IsPlaying(Entity e)
        {
            if (EntityManager.HasComponent<AudioNativeSource>(e))
            {
                AudioNativeSource audioNativeSource = EntityManager.GetComponentData<AudioNativeSource>(e);
                if (audioNativeSource.sourceID > 0)
                {
                    return AudioNativeCalls.IsPlaying(audioNativeSource.sourceID);
                }
            }

            return false;
        }

        protected override bool SetVolume(Entity e, float volume)
        {
            if (EntityManager.HasComponent<AudioNativeSource>(e))
            {
                AudioNativeSource audioNativeSource = EntityManager.GetComponentData<AudioNativeSource>(e);
                if (audioNativeSource.sourceID > 0)
                {
                    return AudioNativeCalls.SetVolume(audioNativeSource.sourceID, volume);
                }
            }

            return false;
        }

        protected override bool SetPan(Entity e, float pan)
        {
            if (EntityManager.HasComponent<AudioNativeSource>(e))
            {
                AudioNativeSource audioNativeSource = EntityManager.GetComponentData<AudioNativeSource>(e);
                if (audioNativeSource.sourceID > 0)
                {
                    return AudioNativeCalls.SetPan(audioNativeSource.sourceID, pan);
                }
            }

            return false;
        }

        protected override bool SetPitch(Entity e, float pitch)
        {
            if (EntityManager.HasComponent<AudioNativeSource>(e))
            {
                AudioNativeSource audioNativeSource = EntityManager.GetComponentData<AudioNativeSource>(e);
                if (audioNativeSource.sourceID > 0)
                {
                    return AudioNativeCalls.SetPitch(audioNativeSource.sourceID, pitch);
                }
            }

            return false;
        }

        private void ReinitIfDefaultDeviceChanged()
        {
            if (AudioNativeCalls.HasDefaultDeviceChanged())
                AudioNativeCalls.ReinitAudio();
        }

        private void ReinitIfNoAudioConsumed(bool paused)
        {
            const double reinitTime = 0.25;
            double worldTime = World.Time.ElapsedTime;
            ulong audioOutputTimeInFrames = AudioNativeCalls.GetAudioOutputTimeInFrames();
            bool audioConsumed = audioOutputTimeInFrames != lastAudioOutputTimeInFrames;
            bool audioNeedsReinit = worldTime - lastWorldTimeAudioConsumed >= reinitTime;

            if (!audioConsumed && !paused && audioNeedsReinit)
                AudioNativeCalls.ReinitAudio();

            lastAudioOutputTimeInFrames = audioOutputTimeInFrames;
            lastWorldTimeAudioConsumed = (audioConsumed || paused || audioNeedsReinit) ? worldTime : lastWorldTimeAudioConsumed;
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            var mgr = EntityManager;
            AudioConfig ac = GetSingleton<AudioConfig>();
            AudioNativeCalls.PauseAudio(ac.paused);

            ReinitIfDefaultDeviceChanged();
            ReinitIfNoAudioConsumed(ac.paused);

            // Remove any lingering AudioNativeSources which may have had their AudioSource
            // removed, otherwise we will continue playing looping clips
            Entities
                .WithStructuralChanges()
                .WithNone<AudioSource>()
                .ForEach((Entity e, ref AudioNativeSource source) =>
                {
                    AudioNativeCalls.Stop(source.sourceID);
                    mgr.RemoveComponent<AudioNativeSource>(e);
                }).Run();

            // Remove any lingering AudioNativeClips which may have had their AudioClip
            // removed, otherwise we will continue holding on to those resources
            Entities
                .WithStructuralChanges()
                .WithNone<AudioClipLoadFromFileAudioFile>()
                .ForEach((Entity e, ref AudioNativeClip tag) =>
                {
                    AudioNativeCalls.FreeAudio(tag.clipID);
                    mgr.RemoveComponent<AudioNativeClip>(e);
                }).Run();

#if ENABLE_DOTSRUNTIME_PROFILER
            ProfilerStats.AccumStats.audioDspCPUx10.value = (long)(AudioNativeCalls.GetCpuUsage() * 10);
#endif
        }

        public void OnSuspendResume(object sender, SuspendResumeEvent evt)
        {
            AudioNativeCalls.PauseAudio(evt.Suspend);
        }
    }
}
