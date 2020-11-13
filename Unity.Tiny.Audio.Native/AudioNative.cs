using System;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Tiny;
using Unity.Tiny.GenericAssetLoading;
using Unity.Tiny.IO;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Unity.Platforms;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Baselib.LowLevel;
using static Unity.Baselib.LowLevel.Binding;
using Unity.Profiling;

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

        [DllImport(DLL, EntryPoint = "soundSourcePropertyMutexLock")]
        public static extern void SoundSourcePropertyMutexLock();

        [DllImport(DLL, EntryPoint = "soundSourcePropertyMutexUnlock")]
        public static extern void SoundSourcePropertyMutexUnlock();

        [DllImport(DLL, EntryPoint = "soundSourceSampleMutexLock")]
        public static extern void SoundSourceSampleMutexLock();

        [DllImport(DLL, EntryPoint = "soundSourceSampleMutexUnlock")]
        public static extern void SoundSourceSampleMutexUnlock();

        // Clip
        [DllImport(DLL, EntryPoint = "startLoadFromDisk", CharSet = CharSet.Ansi)]
        public static extern uint StartLoadFromDisk([MarshalAs(UnmanagedType.LPStr)] string imageFile);    // returns clipID

        [DllImport(DLL, EntryPoint = "startLoadFromMemory")]
        public static extern unsafe uint StartLoadFromMemory(void* compressedBuffer, int compressedBufferSize);

        [DllImport(DLL, EntryPoint = "freeAudio")]
        public static extern void FreeAudio(uint clipID);

        [DllImport(DLL, EntryPoint = "abortLoad")]
        public static extern void AbortLoad(uint clipID);

        [DllImport(DLL, EntryPoint = "checkLoading")]
        public static extern int CheckLoading(uint clipID);     // 0=still working, 1=ok, 2=fail

        [DllImport(DLL, EntryPoint = "finishedLoading")]
        public static extern void FinishedLoading(uint clipID);

        [DllImport(DLL, EntryPoint = "getCompressedMemorySize")]
        public static extern uint GetCompressedMemorySize(uint clipID);

        [DllImport(DLL, EntryPoint = "getUncompressedMemorySize")]
        public static extern uint GetUncompressedMemorySize(uint clipID);

        [DllImport(DLL, EntryPoint = "getUncompressedMemory")]
        public static extern IntPtr GetUncompressedMemory(uint clipID);        

        [DllImport(DLL, EntryPoint = "setUncompressedMemory")]
        public static extern void SetUncompressedMemory(uint clipID, IntPtr uncompressedMemory, uint uncompressedSizeFrames);

#if ENABLE_DOTSRUNTIME_PROFILER
        [DllImport(DLL, EntryPoint = "getCpuUsage")]
        public static extern float GetCpuUsage();
#endif

        [DllImport(DLL, EntryPoint = "clipPoolID")]
        public static extern int ClipPoolID();      // Testing: the next ID that will be assigned to a clip.

        // Source 
        [DllImport(DLL, EntryPoint = "playSource")]
        public static extern uint Play(uint clipID, float volume, float pan, int loop);    // returns sourceID (>0) or 0 for failure.

        [DllImport(DLL, EntryPoint = "isPlaying")]
        public static extern int IsPlaying(uint sourceID);

        [DllImport(DLL, EntryPoint = "stopSource")]
        public static extern int Stop(uint sourceID);    // returns success (or failure)

        [DllImport(DLL, EntryPoint = "pauseAudio")]
        public static extern void PauseAudio(bool doPause);    // returns success (or failure)

        [DllImport(DLL, EntryPoint = "setVolume")]
        public static extern void SetVolume(uint sourceId, float volume);    // returns success (or failure)

        [DllImport(DLL, EntryPoint = "setPan")]
        public static extern void SetPan(uint sourceId, float pan);    // returns success (or failure)

        [DllImport(DLL, EntryPoint = "setPitch")]
        public static extern void SetPitch(uint sourceId, float pitch);    // returns success (or failure)

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
        public unsafe void StartLoad(
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

            DynamicBuffer<AudioClipUncompressed> audioClipUncompressed = entityManager.GetBuffer<AudioClipUncompressed>(e);
            if (audioClipUncompressed.Length > 0)
                return;

            string path = entityManager.GetBufferAsString<AudioClipLoadFromFileAudioFile>(e);
            if (path[0] == '!')
            {
                // This is a special path name that is used to load up a fake audio asset for our automated tests.
                audioNativeClip.clipID = AudioNativeCalls.StartLoadFromDisk(path);
                audioClip.status = audioNativeClip.clipID > 0 ? AudioClipStatus.Loading : AudioClipStatus.LoadError;
            }
            else
            {
                // Read the audio clip from disk into an AudioClipCompressed component.
                LoadSoundClipFromDisk(entityManager, e, path);
                DynamicBuffer<AudioClipCompressed> audioClipCompressed = entityManager.GetBuffer<AudioClipCompressed>(e);

                audioNativeClip.clipID = AudioNativeCalls.StartLoadFromMemory(audioClipCompressed.GetUnsafeReadOnlyPtr(), audioClipCompressed.Length);
                audioClip.status = audioNativeClip.clipID > 0 ? AudioClipStatus.Loading : AudioClipStatus.LoadError;
            }
        }

        public unsafe LoadResult CheckLoading(IntPtr wrapper,
            EntityManager man,
            Entity e,
            ref AudioClip audioClip, ref AudioNativeClip audioNativeClip, ref AudioClipLoadFromFile param, ref AudioNativeLoading nativeLoading)
        {
            if (audioClip.status == AudioClipStatus.Loading)
                return LoadResult.success;
            else
                return LoadResult.failed;
        }

        public void FreeNative(EntityManager man, Entity e, ref AudioNativeClip audioNativeClip)
        {
            if (!man.HasComponent<AudioClipUsage>(e))
                return;
            AudioClipUsage audioClipUsage = man.GetComponentData<AudioClipUsage>(e);
            bool clipIsPlaying = audioClipUsage.playingRefCount > 0;

            if (clipIsPlaying)
            {
                AudioNativeCalls.SoundSourcePropertyMutexLock();
                AudioNativeCalls.SoundSourceSampleMutexLock();
            }

            AudioNativeCalls.FreeAudio(audioNativeClip.clipID);
            DynamicBuffer<AudioClipCompressed> audioClipCompressed = man.GetBuffer<AudioClipCompressed>(e);
            audioClipCompressed.ResizeUninitialized(0);

            if (clipIsPlaying)
            {
                AudioNativeCalls.SoundSourcePropertyMutexUnlock();
                AudioNativeCalls.SoundSourceSampleMutexUnlock();
            }
        }

        public void FinishLoading(EntityManager man, Entity e, ref AudioClip audioClip, ref AudioNativeClip audioNativeClip, ref AudioNativeLoading nativeLoading)
        {
            AudioNativeCalls.FinishedLoading(audioNativeClip.clipID);
        }

        public unsafe void LoadSoundClipFromDisk(EntityManager mgr, Entity e, string filePath)
        {
            DynamicBuffer<AudioClipCompressed> audioClipCompressed = mgr.GetBuffer<AudioClipCompressed>(e);
            if (audioClipCompressed.Length > 0)
                return;

#if UNITY_ANDROID
            var op = IOService.RequestAsyncRead(filePath);
            while (op.GetStatus() <= AsyncOp.Status.InProgress);

            op.GetData(out byte* data, out int sizeInBytes);
            audioClipCompressed.ResizeUninitialized(sizeInBytes);
            byte* audioClipCompressedBytes = (byte*)audioClipCompressed.GetUnsafePtr();
            for (int i = 0; i < sizeInBytes; i++)
                audioClipCompressedBytes[i] = data[i];

            op.Dispose();
#else
            FixedString512 filePathFixedString = new FixedString512(filePath);
            Baselib_ErrorState errorState = new Baselib_ErrorState();
            Baselib_FileIO_SyncFile fileHandle = Baselib_FileIO_SyncOpen(filePathFixedString.GetUnsafePtr(), Baselib_FileIO_OpenFlags.Read, &errorState);
            if (errorState.code != Baselib_ErrorCode.Success)
                return;

            UInt64 fileSize = Baselib_FileIO_SyncGetFileSize(fileHandle, &errorState);
            if (fileSize > Int32.MaxValue)
            {
                Baselib_FileIO_SyncClose(fileHandle, &errorState);
                return;
            }
            
            audioClipCompressed.ResizeUninitialized((int)fileSize);
            UInt64 bytesRead = Baselib_FileIO_SyncRead(fileHandle, 0, (IntPtr)audioClipCompressed.GetUnsafePtr(), (ulong)audioClipCompressed.Length, &errorState);
            Baselib_FileIO_SyncClose(fileHandle, &errorState);
#endif
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
        private int m_uncompressedAudioMemoryBytes = 0;
        private double m_lastWorldTimeAudioConsumed = 0.0;
        private ulong m_lastAudioOutputTimeInFrames = 0;

        #if ENABLE_PLAYERCONNECTION
        static bool s_Muted;
        #endif

        // for use with the toggle mute callback emitted from the editor
        static readonly Guid k_EditorMuteMessageId = new Guid("01372e16-3f1f-4b47-8d09-b48c7c8a3f4d");

        protected override void OnStartRunning()
        {
            PlatformEvents.OnSuspendResume += OnSuspendResume;
            m_uncompressedAudioMemoryBytes = 0;

            AudioConfig ac = GetSingleton<AudioConfig>();
            ac.initialized = true;
            ac.unlocked = true;
            SetSingleton<AudioConfig>(ac);           
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

            #if ENABLE_PLAYERCONNECTION
            PlayerConnection.instance.Register(k_EditorMuteMessageId, ToggleMuteFromEditor);
            #endif
        }

        protected override void DestroyAudioSystem()
        {
            #if ENABLE_PLAYERCONNECTION
            PlayerConnection.instance.Unregister(k_EditorMuteMessageId,  ToggleMuteFromEditor);
            #endif

            AudioNativeCalls.DestroyAudio();
        }

        [BurstCompile]
        protected static unsafe uint PlaySource(EntityManager mgr, Entity e)
        {
            if (mgr.HasComponent<AudioSource>(e))
            {
                AudioSource audioSource = mgr.GetComponentData<AudioSource>(e);
                Entity clipEntity = audioSource.clip;
                DynamicBuffer<AudioClipUncompressed> audioClipUncompressed = mgr.GetBuffer<AudioClipUncompressed>(clipEntity);

                if (mgr.HasComponent<AudioNativeClip>(clipEntity))
                {
                    AudioNativeClip audioNativeClip = mgr.GetComponentData<AudioNativeClip>(clipEntity);
                    if (audioNativeClip.clipID > 0)
                    {
                        bool decompressOnPlay = false;
                        if (mgr.HasComponent<AudioClip>(clipEntity))
                        {
                            AudioClip audioClip = mgr.GetComponentData<AudioClip>(clipEntity);
                            decompressOnPlay = (audioClip.loadType == AudioClipLoadType.DecompressOnPlay);

                            if (decompressOnPlay && (audioClipUncompressed.Length == 0))
                                return 0;

                            audioClip.status = AudioClipStatus.Loaded;
                            mgr.SetComponentData<AudioClip>(clipEntity, audioClip);
                        }

                        // If there is an existing source, it should re-start.
                        // Do this with a Stop() and let it play below.
                        if (mgr.HasComponent<AudioSourceID>(e))
                        {
                            AudioSourceID ans = mgr.GetComponentData<AudioSourceID>(e);
                            AudioNativeCalls.Stop(ans.sourceID);
                        }

                        float volume = audioSource.volume;
                        float pan = mgr.HasComponent<Audio2dPanning>(e) ? mgr.GetComponentData<Audio2dPanning>(e).pan : 0.0f;

                        // For 3d sounds, we start at volume zero because we don't know if this sound is close or far from the listener.
                        // It is much smoother to ramp up volume from zero than the alternative.
                        if (mgr.HasComponent<Audio3dPanning>(e))
                            volume = 0.0f;

                        uint sourceID = AudioNativeCalls.Play(audioNativeClip.clipID, volume, pan, audioSource.loop ? 1 : 0);
                        return sourceID;
                    }
                }
            }

            return 0;
        }

        [BurstCompile]
        protected static void StopSource(EntityManager mgr, Entity e)
        {
            if (mgr.HasComponent<AudioSourceID>(e))
            {
                AudioSourceID audioSourceID = mgr.GetComponentData<AudioSourceID>(e);
                if (audioSourceID.sourceID > 0)
                {
                    AudioNativeCalls.Stop(audioSourceID.sourceID);
                }
            }
        }

        [BurstCompile]
        protected static int IsPlaying(EntityManager mgr, Entity e)
        {
            if (mgr.HasComponent<AudioSourceID>(e))
            {
                AudioSourceID audioSourceID = mgr.GetComponentData<AudioSourceID>(e);
                if (audioSourceID.sourceID > 0)
                {
                    return AudioNativeCalls.IsPlaying(audioSourceID.sourceID);
                }
            }

            return 0;
        }

        [BurstCompile]
        protected static void SetVolume(EntityManager mgr, Entity e, float volume)
        {
            if (mgr.HasComponent<AudioSourceID>(e))
            {
                AudioSourceID audioSourceID = mgr.GetComponentData<AudioSourceID>(e);
                if (audioSourceID.sourceID > 0)
                {
                    AudioNativeCalls.SetVolume(audioSourceID.sourceID, volume);
                }
            }
        }

        [BurstCompile]
        protected static void SetPan(EntityManager mgr, Entity e, float pan)
        {
            if (mgr.HasComponent<AudioSourceID>(e))
            {
                AudioSourceID audioSourceID = mgr.GetComponentData<AudioSourceID>(e);
                if (audioSourceID.sourceID > 0)
                {
                    AudioNativeCalls.SetPan(audioSourceID.sourceID, pan);
                }
            }
        }

        [BurstCompile]
        protected static void SetPitch(EntityManager mgr, Entity e, float pitch)
        {
            if (mgr.HasComponent<AudioSourceID>(e))
            {
                AudioSourceID audioSourceID = mgr.GetComponentData<AudioSourceID>(e);
                if (audioSourceID.sourceID > 0)
                {
                    AudioNativeCalls.SetPitch(audioSourceID.sourceID, pitch);
                }
            }
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
            bool audioConsumed = audioOutputTimeInFrames != m_lastAudioOutputTimeInFrames;
            bool audioNeedsReinit = worldTime - m_lastWorldTimeAudioConsumed >= reinitTime;

            if (!audioConsumed && !paused && audioNeedsReinit)
                AudioNativeCalls.ReinitAudio();

            m_lastAudioOutputTimeInFrames = audioOutputTimeInFrames;
            m_lastWorldTimeAudioConsumed = (audioConsumed || paused || audioNeedsReinit) ? worldTime : m_lastWorldTimeAudioConsumed;
        }

        protected override unsafe void OnUpdate()
        {
            var mgr = EntityManager;
            Entity audioEntity = m_audioEntity;
            double currentTime = World.Time.ElapsedTime;
            int uncompressedAudioMemoryBytes = m_uncompressedAudioMemoryBytes;
            double worldElapsedTime = World.Time.ElapsedTime;
            AudioConfig ac = GetSingleton<AudioConfig>();

            NativeList<Entity> entitiesPlayed = new NativeList<Entity>(Allocator.Temp);
            
            base.OnUpdate();            

            AudioNativeCalls.PauseAudio(ac.paused);
            ReinitIfDefaultDeviceChanged();
            ReinitIfNoAudioConsumed(ac.paused);

            // We are starting to make AudioSource play/stop and property changes, so block the audio mixer thread from doing any work
            // on this state until we are done.
            AudioNativeCalls.SoundSourcePropertyMutexLock();

            for (int i = 0; i < mgr.GetBuffer<SourceIDToStop>(audioEntity).Length; i++)
            {
                uint id = mgr.GetBuffer<SourceIDToStop>(audioEntity)[i];
                AudioNativeCalls.Stop(id);
            }

            // Play sounds.
            Entities
                .WithAll<AudioSource, AudioSourceStart>()
                .ForEach((Entity e) =>
                {
                    uint sourceID = PlaySource(mgr, e);
                    if (sourceID > 0)
                    {
                        AudioSourceID audioSourceID = mgr.GetComponentData<AudioSourceID>(e);
                        audioSourceID.sourceID = sourceID;
                        mgr.SetComponentData<AudioSourceID>(e, audioSourceID);

                        entitiesPlayed.Add(e);
                    }
                }).Run();

            for (int i = 0; i < entitiesPlayed.Length; i++)
                mgr.RemoveComponent<AudioSourceStart>(entitiesPlayed[i]);
            
            Entities
                .ForEach((Entity e, ref AudioClipUsage audioClipUsage) =>
                {
                    audioClipUsage.playingRefCount = 0;
                }).Run();

            // Re-calculate the playing ref count for each audio clip. Also, update AudioSource's isPlaying bool and remove
            // any AudioSource entities from the list if they are no longer playing.
            Entities
                .ForEach((Entity e, in AudioSource audioSource) =>
                {
                    bool audioSourceStarting = mgr.HasComponent<AudioSourceStart>(e);
                    if (audioSourceStarting || audioSource.isPlaying)
                    {
                        Entity clipEntity = audioSource.clip;
                        AudioClipUsage audioClipUsage = mgr.GetComponentData<AudioClipUsage>(clipEntity);
                        audioClipUsage.playingRefCount++;
                        audioClipUsage.lastTimeUsed = currentTime;
                        mgr.SetComponentData<AudioClipUsage>(clipEntity, audioClipUsage);
                    }
                }).Run();

            if (uncompressedAudioMemoryBytes > ac.maxUncompressedAudioMemoryBytes)
            {
                Entities
                    .ForEach((Entity e, ref DynamicBuffer<AudioClipUncompressed> audioClipUncompressed) =>
                    {
                        if (audioClipUncompressed.Length > 0)
                        {
                            AudioClipUsage audioClipUsage = mgr.GetComponentData<AudioClipUsage>(e);

                            bool notRecentlyUsed = (audioClipUsage.lastTimeUsed + 15.0f < currentTime);
                            bool largeAudioAsset = audioClipUncompressed.Length > 2*1024*1024;

                            if ((uncompressedAudioMemoryBytes > ac.maxUncompressedAudioMemoryBytes) &&
                                (audioClipUsage.playingRefCount <= 0) &&
                                (notRecentlyUsed || largeAudioAsset))
                            {
                                int clipUncompressedAudioMemoryBytes = audioClipUncompressed.Length * sizeof(short);

                                AudioNativeClip audioNativeClip = mgr.GetComponentData<AudioNativeClip>(e);
                                AudioNativeCalls.SetUncompressedMemory(audioNativeClip.clipID, (IntPtr)null, 0);

                                AudioClip audioClip = mgr.GetComponentData<AudioClip>(e);
                                audioClip.status = AudioClipStatus.Loading;
                                mgr.SetComponentData<AudioClip>(e, audioClip);

                                audioClipUncompressed.ResizeUninitialized(0);
                                uncompressedAudioMemoryBytes -= clipUncompressedAudioMemoryBytes;                     
                            }
                        }
                    }).Run();

                Entities
                    .ForEach((Entity e, ref DynamicBuffer<AudioClipUncompressed> audioClipUncompressed) =>
                    {
                        if (audioClipUncompressed.Length > 0)
                        {
                            AudioClipUsage audioClipUsage = mgr.GetComponentData<AudioClipUsage>(e);
                            
                            if ((uncompressedAudioMemoryBytes > ac.maxUncompressedAudioMemoryBytes) &&
                                (audioClipUsage.playingRefCount <= 0))
                            {
                                int clipUncompressedAudioMemoryBytes = audioClipUncompressed.Length * sizeof(short);

                                AudioNativeClip audioNativeClip = mgr.GetComponentData<AudioNativeClip>(e);
                                AudioNativeCalls.SetUncompressedMemory(audioNativeClip.clipID, (IntPtr)null, 0);

                                AudioClip audioClip = mgr.GetComponentData<AudioClip>(e);
                                audioClip.status = AudioClipStatus.Loading;
                                mgr.SetComponentData<AudioClip>(e, audioClip);

                                audioClipUncompressed.ResizeUninitialized(0);
                                uncompressedAudioMemoryBytes -= clipUncompressedAudioMemoryBytes;                       
                            }
                        }
                    }).Run();
            }
            
            DynamicBuffer<EntityPlaying> entitiesPlaying = mgr.GetBuffer<EntityPlaying>(m_audioEntity);
            for (int i = 0; i < entitiesPlaying.Length; i++)
            {
                Entity e = entitiesPlaying[i];
                AudioSource audioSource = mgr.GetComponentData<AudioSource>(e);

                audioSource.isPlaying = (IsPlaying(mgr, e) == 1) ? true : false;
                mgr.SetComponentData<AudioSource>(e, audioSource);

                if (audioSource.isPlaying)
                {
                    float volume = audioSource.volume;
                    if (mgr.HasComponent<AudioDistanceAttenuation>(e))
                    {
                        AudioDistanceAttenuation distanceAttenuation = mgr.GetComponentData<AudioDistanceAttenuation>(e);
                        volume *= distanceAttenuation.volume;
                    }
                    SetVolume(mgr, e, volume);

                    if (mgr.HasComponent<Audio3dPanning>(e))
                    {
                        Audio3dPanning panning = mgr.GetComponentData<Audio3dPanning>(e);
                        SetPan(mgr, e, panning.pan);
                    }
                    else if (mgr.HasComponent<Audio2dPanning>(e))
                    {
                        Audio2dPanning panning = mgr.GetComponentData<Audio2dPanning>(e);
                        SetPan(mgr, e, panning.pan);
                    }

                    if (mgr.HasComponent<AudioPitch>(e))
                    {
                        AudioPitch pitchEffect = mgr.GetComponentData<AudioPitch>(e);
                        float pitch = (pitchEffect.pitch > 0.0f) ? pitchEffect.pitch : 1.0f;
                        SetPitch(mgr, e, pitch);
                    }
                } 
            }

            // We are done making AudioSource property changes, so unblock the audio mixer thread.
            AudioNativeCalls.SoundSourcePropertyMutexUnlock();

#if ENABLE_DOTSRUNTIME_PROFILER
            ProfilerStats.GatheredStats |= ProfilerModes.ProfileAudio;
            ProfilerStats.AccumStats.audioDspCPUx10.value = (long)(AudioNativeCalls.GetCpuUsage() * 10);
            
            ProfilerStats.AccumStats.memAudioCount.value = 0;
            ProfilerStats.AccumStats.memAudio.value = 0;
            ProfilerStats.AccumStats.memReservedAudio.value = 0;
            ProfilerStats.AccumStats.memUsedAudio.value = 0;
            ProfilerStats.AccumStats.audioStreamFileMemory.value = 0;
            ProfilerStats.AccumStats.audioSampleMemory.value = 0;

            Entities
                .ForEach((Entity e, in DynamicBuffer<AudioClipCompressed> audioClipCompressed, in DynamicBuffer<AudioClipUncompressed> audioClipUncompressed) =>
                {
                    int audioClipCompressedBytes = audioClipCompressed.Length;
                    int audioClipUncompressedBytes = audioClipUncompressed.Length * sizeof(short);
                    int audioClipTotalBytes = audioClipCompressedBytes + audioClipUncompressedBytes;

                    ProfilerStats.AccumStats.memAudioCount.Accumulate(1);
                    ProfilerStats.AccumStats.memAudio.Accumulate(audioClipTotalBytes);
                    ProfilerStats.AccumStats.memReservedAudio.Accumulate(audioClipTotalBytes);
                    ProfilerStats.AccumStats.memUsedAudio.Accumulate(audioClipTotalBytes);
                    ProfilerStats.AccumStats.audioSampleMemory.Accumulate(audioClipTotalBytes);
                }).Run();
#endif

            Entities
                .WithStructuralChanges()
                .WithNone<AudioClipLoadFromFileAudioFile>()
                .ForEach((Entity e, ref AudioNativeClip audioNativeClip) =>
                {
                    AudioClipUsage audioClipUsage = mgr.GetComponentData<AudioClipUsage>(e);
                    bool clipIsPlaying = audioClipUsage.playingRefCount > 0;

                    if (!clipIsPlaying)
                    {
                        AudioNativeCalls.FreeAudio(audioNativeClip.clipID);
                        DynamicBuffer<AudioClipCompressed> audioClipCompressed = mgr.GetBuffer<AudioClipCompressed>(e);
                        audioClipCompressed.ResizeUninitialized(0);
                        mgr.RemoveComponent<AudioNativeClip>(e);
                    }
                }).Run();
            
            uncompressedAudioMemoryBytes = 0;
            Entities
                .ForEach((Entity e, in DynamicBuffer<AudioClipUncompressed> audioClipUncompressed) =>
                {
                    uncompressedAudioMemoryBytes += audioClipUncompressed.Length * sizeof(short);
                }).Run();
            m_uncompressedAudioMemoryBytes = uncompressedAudioMemoryBytes;

            Entities
                .ForEach((Entity e, ref DynamicBuffer<AudioClipUncompressed> audioClipUncompressed, in AudioClip audioClip, in AudioNativeClip audioNativeClip, in AudioClipUsage audioClipUsage) =>
                {
                    // For playing sounds that are not yet uncompressed, get their uncompressed size, and allocate a memory block for the decompression step.
                    if ((audioClip.loadType == AudioClipLoadType.DecompressOnPlay) && 
                        (audioClipUsage.playingRefCount > 0) && 
                        (audioNativeClip.clipID > 0) && 
                        (audioClipUncompressed.Length == 0))
                    {   
                        int uncompressedSizeInFrames = audioClip.samples / audioClip.channels;
                        int uncompressedSizeInSamples = uncompressedSizeInFrames * 2;

                        // Tiny native audio currently always decodes audio clips to 44.1KHz. While this constraint exists, we have to adjust the sample count here.
                        const int uncompressedFrequency = 44100;
                        if (audioClip.frequency != uncompressedFrequency)
                        {
                            float audioClipSeconds = (float)audioClip.samples / (float)audioClip.frequency;
                            uncompressedSizeInFrames = (int)(audioClipSeconds * (float)uncompressedFrequency) + 1;
                            uncompressedSizeInSamples = uncompressedSizeInFrames * 2;
                        }

                        audioClipUncompressed.ResizeUninitialized(uncompressedSizeInSamples);
                        short* audioClipUncompressedBuffer = (short*)audioClipUncompressed.GetUnsafePtr();

                        UnsafeUtility.MemSet(audioClipUncompressedBuffer, 0, audioClipUncompressed.Length*sizeof(short));
                        AudioNativeCalls.SetUncompressedMemory(audioNativeClip.clipID, (IntPtr)audioClipUncompressedBuffer, (uint)uncompressedSizeInFrames);
                    }
                }).Run();

            // Decompress sounds. 
            Entities
                .WithAll<AudioClipUncompressed>()
                .ForEach((Entity e, in AudioClip audioClip, in AudioNativeClip audioNativeClip, in AudioClipUsage audioClipUsage) =>
                {
                    if ((audioClip.loadType == AudioClipLoadType.DecompressOnPlay) &&
                        (audioClipUsage.playingRefCount > 0) &&
                        (audioNativeClip.clipID > 0) &&
                        (audioClip.status != AudioClipStatus.Loaded))
                    {
                        AudioNativeCalls.CheckLoading(audioNativeClip.clipID);
                    }
                }).ScheduleParallel();
        }

        public void OnSuspendResume(object sender, SuspendResumeEvent evt)
        {
            AudioNativeCalls.PauseAudio(evt.Suspend);
        }
    }
}
