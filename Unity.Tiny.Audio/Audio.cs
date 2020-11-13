using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Tiny;
using Unity.Transforms;
using Unity.Mathematics;
#if ENABLE_DOTSRUNTIME_PROFILER
using Unity.Development.Profiling;
#endif

namespace Unity.Tiny.Audio
{
    /// <summary>
    ///  An enum listing the possible states an audio clip can be in during loading.
    ///  Used by the AudioClip component to track loading status.
    /// </summary>
    public enum AudioClipStatus
    {
        /// <summary>The clip is not loaded in memory.</summary>
        Unloaded,

        /// <summary>The clip has begun loading but is not ready to begin playback.</summary>
        Loading,

        /// <summary>The clip is fully decoded, loaded in memory, and ready for playback.</summary>
        Loaded,

        /// <summary>The clip cannot be loaded in memory.</summary>
        LoadError
    }

    /// <summary>
    /// An enum that tells which audio clip loading strategy is being used.
    /// </summary>
    public enum AudioClipLoadType
    {
        /// <summary>When first played, this clip is fully decompressed into memory. THe uncompressed version of the clip may be de-allocated later to free up memory.</summary>
        DecompressOnPlay,

        /// <summary>The clip is always compressed in memory. When played back, it is decoded as it is being played. The uncompressed data is always de-allocated immediately after it is used.</summary>
        CompressedInMemory
    }

    /// <summary>
    /// When a 3d sound is played, it is distance-attenuated; its volume is lowered as the sound moves away from the listener.
    /// </summary>
    public enum AudioRolloffMode
    {
        /// <summary>In logarithmic rolloff mode, the volume drops logarithmically/steeply after the sound is minDistance units from the listener.</summary>
        Logarithmic = 0,
        /// <summary>In linear rolloff mode, the volume drops linearly from full volume at minDistance units (from the listener) to zero at maxDistance units.</summary>
        Linear = 1
    }

    /// <summary>
    ///  An AudioClip represents a single audio resource that can play back on
    ///  one or more AudioSource components.
    /// </summary>
    /// <remarks>
    ///  If only one AudioSource component references the AudioClip, you can attach the
    ///  AudioClip component to the same entity as that AudioSource component.
    ///
    ///  If multiple AudioSource components reference the audio clip, it's recommended
    ///  that you add the AudioClip component to a separate entity.
    ///
    ///  To perform the load of the audio resource, the AudioClip must have:
    ///  - An AudioClipLoadFromFileAudioFile to specify the location of the resource.
    ///  - An AudioClipLoadFromFile which initiates the load.
    ///
    ///  Note that this is a System, so that the actual loading will not be synchronous.
    ///
    /// <example>
    /// Minimal code load a file:
    /// <code>
    ///     mgr.AddComponentData(eClip, new AudioClip());
    ///     mgr.AddBufferFromString&lt;AudioClipLoadFromFileAudioFile&gt;(eClip, "path/to/file.wav");
    ///     mgr.AddComponent(eClip, typeof(AudioClipLoadFromFile));
    /// </code>
    /// </example>
    /// </remarks>
    public struct AudioClip : IComponentData
    {
        /// <summary>
        ///  The AudioClip load status. The AudioClipStatus enum defines the possible states.
        /// </summary>
        public AudioClipStatus status;
        /// <summary>The loading and decompression strategy used for this clip.
        public AudioClipLoadType loadType;
        /// <summary>The clip's channel count.
        public int channels;
        /// <summary>The uncompressed size of the audio clip in samples.
        public int samples;
        /// <summary>The frequence of the audio clip in samples/second.
        public int frequency;
    }

    /// <summary>
    ///  AudioClipCompressed is the audio clip's compressed data, in MP3 or Vorbis format.
    /// </summary>
    public struct AudioClipCompressed : IBufferElementData
    {
        public byte data;
    }

    /// <summary>
    ///  AudioClipUncompressed is the audio clip's uncompressed data. It is in stereo, 16-bits-per-sample format.
    ///  Audio clips are originally loaded into memory in AudioClipCompressed components. In the AudioClipLoadType is
    ///  DecompressOnPlay, the AudioClipUncompressed component will be filled in after decompression is first performed.
    ///  There is a build setting that affects the uncompressed audio memory limit. When it is exceeded, some AudioClipUncompressed
    ///  components will be cleared out to save memory based on a least-recently-used policy.
    /// </summary>
    public struct AudioClipUncompressed : IBufferElementData
    {
        public short sample;
    }

    /// <summary>
    /// AudioClipUsage stores data regarding if its associated AudioClip is being played and how recently it was played. This is
    /// used when determining which AudioClipUncompressed components to clear to reduce memory usage.
    /// </summary>
    public struct AudioClipUsage : IComponentData
    {
        public int playingRefCount;
        public double lastTimeUsed;
    }

    /// <summary>
    /// Location of the audio file. <seealso cref="AudioClip"/>
    /// </summary>
    public struct AudioClipLoadFromFileAudioFile : IBufferElementData
    {
        public char s;
    }

    /// <summary>
    ///  Attach this component to an entity with an AudioClip and AudioClipLoadFromFileAudioFile
    ///  component to begin loading an audio clip.
    /// </summary>
    /// <remarks>
    ///  Loading is performed by the AudioSystem.
    ///  Once loading is complete the AudioSystem removes the
    ///  AudioClipLoadFromFile component.
    /// </remarks>
    public struct AudioClipLoadFromFile : IComponentData
    {
    }

    public struct AudioSourceID : ISystemStateComponentData
    {
        public uint sourceID;
    }

    public struct EntityToStop : IBufferElementData
    {
        public Entity e;

        public static implicit operator Entity(EntityToStop entityToStop)
        {
            return entityToStop.e;
        }

        public static implicit operator EntityToStop(Entity entity)
        {
            return new EntityToStop { e = entity };
        }
    }

    public struct SourceIDToStop : IBufferElementData
    {
        public uint id;

        public static implicit operator uint(SourceIDToStop sourceIDToStop)
        {
            return sourceIDToStop.id;
        }

        public static implicit operator SourceIDToStop(uint sourceID)
        {
            return new SourceIDToStop { id = sourceID };
        }
    }

    public struct EntityPlaying : IBufferElementData
    {
        public Entity e;

        public static implicit operator Entity(EntityPlaying entityToStop)
        {
            return entityToStop.e;
        }

        public static implicit operator EntityPlaying(Entity entity)
        {
            return new EntityPlaying { e = entity };
        }
    }

    /// <summary>
    ///  Attach an AudioListener component to an entity with a LocalToWorld component. 3d
    ///  audio panning and distance-attenuation will be calculated relative to this entity's
    ///  position and orientation.
    /// </summary>
    /// <remarks>
    ///  There should only be one enabled AudioListener. If there are no listeners, all
    ///  3d sounds will be silent.
    /// </remarks>
    public struct AudioListener : IComponentData
    {
    }

    /// <summary>
    ///  Attach this component to an entity with an AudioSource component to start
    ///  playback the next time the AudioSystem updates.
    /// </summary>
    /// <remarks>
    ///  Once playback starts, the
    ///  AudioSystem removes this component.
    ///  Attaching an AudioSourceStart component to an already playing source re-starts
    ///  playback from the beginning.
    ///  To stop a playing source, use the AudioSourceStop component.
    /// <example>
    /// Minimal code to play an AudioClip:
    /// <code>
    ///     var eSource = mgr.CreateEntity();
    ///     AudioSource source = new AudioSource();
    ///     source.clip = eClip;
    ///     mgr.AddComponentData(eSource, source);
    ///     mgr.AddComponent(eSource, typeof(AudioSourceStart));
    /// </code>
    /// </example>
    /// </remarks>
    public struct AudioSourceStart : IComponentData
    {
    }

    /// <summary>
    ///  Attach this component to an entity with an AudioSource component to stop
    ///  playback the next time the AudioSystem updates.
    /// </summary>
    /// <remarks>
    ///  Once playback stops, the
    ///  AudioSystem removes this component.
    ///  Attaching an AudioSourceStop component to an already stopped source has no effect.
    ///  To start playing a source, use the AudioSourceStart component.
    /// </remarks>
    public struct AudioSourceStop : IComponentData
    {
    }

    /// <summary>
    ///  An AudioSource component plays back one audio clip at a time.
    /// </summary>
    /// <remarks>
    ///  Multiple audio sources can play at the same time.
    ///  To start playback use the AudioSourceStart component.
    ///  To stop playback use the AudioSourceStop component.
    ///
    ///  `clip`, `volume`, and `loop` are read when the audio source
    ///  starts as a result of AudioSourceStart being added. `clip` and `loop`
    ///  will not change audio that is already playing.
    ///
    ///  `isPlaying` is updated with every tick of the world.
    /// </remarks>
    public struct AudioSource : IComponentData
    {
        /// <summary>
        ///  Specifies the audio clip that plays when this source starts playing.
        /// </summary>
        //[EntityWithComponents(typeof(AudioClip))]
        public Entity clip;

        /// <summary>
        ///  Specifies the audio clip's playback volume. Values can range from 0..1.
        /// </summary>
        public float volume;

        /// <summary>
        ///  If true, replays the audio clip when it reaches end.
        /// </summary>
        public bool loop;

        /// <summary>
        ///  True if the audio clip is currently playing.
        /// </summary>
        /// <remarks>
        ///  `isPlaying` will start false, and will be false until the AudioSourceStart tag
        ///  is removed by the Audio system.
        /// </remarks>
        public bool isPlaying { get; set; }
    }

    /// <summary>
    ///  An Audio2dPanning component controls an AudioSource's 2d panning.
    /// </summary>
    /// <remarks>
    ///  `pan` is read when the associated AudioSource starts playing.
    /// <example>
    /// Minimal code to play an AudioClip with 2d panning:
    /// <code>
    ///     var eSource = mgr.CreateEntity();
    ///     AudioSource source = new AudioSource();
    ///     source.clip = eClip;
    ///     mgr.AddComponentData(eSource, source);
    ///     Audio2dPanning panning = new Audio2dPanning();
    ///     panning.pan = 0.0f;
    ///     mgr.AddComponentData(eSource, panning);
    ///     mgr.AddComponent(eSource, typeof(AudioSourceStart));
    /// </code>
    /// </example>
    /// </remarks>
    public struct Audio2dPanning : IComponentData
    {
        /// <summary>
        ///  Specifies the audio clip's playback stereo pan. Values can range from -1..1.
        /// </summary>
        public float pan;
    }

    /// <summary>
    ///  An Audio3dPanning component controls an AudioSource's 3d panning.
    /// </summary>
    /// <remarks>
    ///  The AudioSystem automatically adjusts the associated AudioSource's stereo panning
    ///  value based on the AudioSource's position relative to the AudioListener.
    /// </remarks>
    public struct Audio3dPanning : IComponentData
    {
        /// <summary>
        /// Specifies the audio clip's playback stereo pan. Values can range from -1..1.
        /// This value is set automatically by the AudioSystem.
        /// </summary>
        public float pan { get; set; }
    }

    /// <summary>
    ///  An AudioDistanceAttenuation component adjusts an AudioSource's volume.
    /// </summary>
    /// <remarks>
    ///  The AudioSystem automatically adjusts the associated AudioSource's volume
    ///  based on the AudioSource's distance from the AudioListener and the properties
    ///  in this component. When an AudioSource is less than minDistance away from the
    ///  AudioListener, the volume is not changed. When an AudioSource is further than
    ///  maxDistance away from the AudioListener, the volume is zero. The volume parameter
    ///  is set internally by the AudioSystem and is the last calculated distance-attenuation
    ///  volume.
    /// <example>
    /// Minimal code to play an AudioClip with 3d panning and distance-attenuation:
    /// <code>
    ///     var eSource = mgr.CreateEntity();
    ///     AudioSource source = new AudioSource();
    ///     source.clip = eClip;
    ///     mgr.AddComponentData(eSource, source);
    ///     Audio3dPanning panning = new Audio3dPanning();
    ///     mgr.AddComponentData(eSource, panning);
    ///     AudioDistanceAttenuation distanceAttenuation = new AudioDistanceAttenuation();
    ///     distanceAttenuation.rolloffMode = AudioRolloffMode.Logarithmic;
    ///     distanceAttenuation.minDistance = 3.0f;
    ///     distanceAttenuation.maxDistance = 50.0f;
    ///     mgr.AddComponentData(eSource, distanceAttenuation);
    ///     mgr.AddComponent(eSource, typeof(AudioSourceStart));
    /// </code>
    /// </example>
    /// </remarks>
    public struct AudioDistanceAttenuation : IComponentData
    {
        public AudioRolloffMode rolloffMode;
        public float minDistance;
        public float maxDistance;
        public float volume { get; set; }
    }

    /// <summary>
    ///  An AudioPitch component adjusts an AudioSource's pitch.
    /// </summary>
    /// <remarks>
    ///  The AudioSystem pushes updated pitch values down to the native audio code on each platform.
    /// <example>
    /// Minimal code to play an AudioClip with a modified pitch:
    /// <code>
    ///     var eSource = mgr.CreateEntity();
    ///     AudioSource source = new AudioSource();
    ///     source.clip = eClip;
    ///     mgr.AddComponentData(eSource, source);
    ///     AudioPitch pitchEffect = new AudioPitch();
    ///     pitchEffect.pitch = 2.0f;
    ///     mgr.AddComponentData(eSource, pitchEffect);
    ///     mgr.AddComponent(eSource, typeof(AudioSourceStart));
    /// </code>
    /// </example>
    /// </remarks>
    public struct AudioPitch : IComponentData
    {
        public float pitch;
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public abstract class AudioSystem : SystemBase
    {
        protected Entity m_audioEntity;

        protected abstract void InitAudioSystem();
        protected abstract void DestroyAudioSystem();

        protected override void OnCreate()
        {
            InitAudioSystem();

            m_audioEntity = EntityManager.CreateEntity();
            EntityManager.AddBuffer<EntityToStop>(m_audioEntity);
            EntityManager.AddBuffer<SourceIDToStop>(m_audioEntity);
            EntityManager.AddBuffer<EntityPlaying>(m_audioEntity);
        }

        protected override void OnDestroy()
        {
            DestroyAudioSystem();

            EntityManager.DestroyEntity(m_audioEntity);
        }

        protected override void OnUpdate()
        {
            var mgr = EntityManager;
            Entity audioEntity = m_audioEntity;
            double currentTime = World.Time.ElapsedTime;

            ClearAudioBuffers();

            Entities
                .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabled)
                .ForEach((Entity e, in AudioSourceID audioSourceID) =>
            {
                if (!mgr.HasComponent<AudioSource>(e) || mgr.HasComponent<AudioSourceStop>(e) || mgr.HasComponent<Disabled>(e))
                {
                    DynamicBuffer<EntityToStop> entitiesToStop = mgr.GetBuffer<EntityToStop>(audioEntity);
                    entitiesToStop.Add(e);

                    if (audioSourceID.sourceID > 0)
                    {
                        DynamicBuffer<SourceIDToStop> sourceIDsToStop = mgr.GetBuffer<SourceIDToStop>(audioEntity);
                        sourceIDsToStop.Add(audioSourceID.sourceID);
                    }
                }
            }).Run();

            int numEntitiesToStop = mgr.GetBuffer<EntityToStop>(audioEntity).Length;
            for (int i = 0; i < numEntitiesToStop; i++)
            {
                Entity e = mgr.GetBuffer<EntityToStop>(audioEntity)[i];

                if (mgr.HasComponent<AudioSourceStop>(e))
                    mgr.RemoveComponent<AudioSourceStop>(e);

                if (!mgr.HasComponent<AudioSource>(e))
                    mgr.RemoveComponent<AudioSourceID>(e);
            }

            Entities
                .ForEach((Entity e, in AudioSource audioSource) =>
                {
                    bool audioSourceStarting = mgr.HasComponent<AudioSourceStart>(e);
                    if (audioSourceStarting || audioSource.isPlaying)
                        mgr.GetBuffer<EntityPlaying>(audioEntity).Add(e);
                }).Run();

            for (int i = 0; i < mgr.GetBuffer<EntityPlaying>(audioEntity).Length; i++)
            {
                Entity e = mgr.GetBuffer<EntityPlaying>(audioEntity)[i];
                
                if (!mgr.HasComponent<AudioSourceID>(e))
                {
                    AudioSourceID audioNativeSource = new AudioSourceID() { sourceID = 0 };
                    mgr.AddComponentData(e, audioNativeSource);
                }
            }

            // Get the listener position.
            LocalToWorld listenerLocalToWorld = new LocalToWorld();
            bool foundListener = false;
            Entities
                .WithAll<AudioListener>()
                .ForEach((Entity e, in LocalToWorld localToWorld) =>
            {
                if (!foundListener)
                {
                    listenerLocalToWorld = localToWorld;
                    foundListener = true;
                }
            }).Run();

            int numEntitiesPlaying = mgr.GetBuffer<EntityPlaying>(audioEntity).Length;
            for (int i = 0; i < numEntitiesPlaying; i++)
            {
                Entity e = mgr.GetBuffer<EntityPlaying>(audioEntity)[i];
                
                if (mgr.HasComponent<AudioDistanceAttenuation>(e))
                {
                    AudioDistanceAttenuation distanceAttenuation = mgr.GetComponentData<AudioDistanceAttenuation>(e);
                    float distanceAttenuationVolume = 0.0f;

                    if (foundListener && mgr.HasComponent<LocalToWorld>(e))
                    {
                        LocalToWorld localToWorld = mgr.GetComponentData<LocalToWorld>(e);
                        float xDist = localToWorld.Position.x - listenerLocalToWorld.Position.x;
                        float yDist = localToWorld.Position.y - listenerLocalToWorld.Position.y;
                        float zDist = localToWorld.Position.z - listenerLocalToWorld.Position.z;
                        float distanceToListener = math.sqrt(xDist * xDist + yDist * yDist + zDist * zDist);

                        if (distanceToListener <= distanceAttenuation.minDistance)
                        {
                            distanceAttenuationVolume = 1.0f;
                        }
                        else if (distanceToListener > distanceAttenuation.maxDistance)
                        {
                            distanceAttenuationVolume = 0.0f;
                        }
                        else
                        {
                            // Reduce distanceToListener by minDistance because, in our simulation, we start lowering the volume after a sound is min distance away from the listener.
                            distanceToListener -= distanceAttenuation.minDistance;

                            if (distanceAttenuation.rolloffMode == AudioRolloffMode.Linear)
                            {
                                float attenuationRange = distanceAttenuation.maxDistance - distanceAttenuation.minDistance;
                                distanceAttenuationVolume = 1.0f - (distanceToListener / attenuationRange);
                            }
                            else if (distanceAttenuation.rolloffMode == AudioRolloffMode.Logarithmic)
                            {
                                // In Unity's original implementation of logarithmic attenuation, the volume is halved every minDistance units. We are copying that approach here.
                                float volumeHalfLives = distanceToListener / distanceAttenuation.minDistance;
                                distanceAttenuationVolume = 1.0f / math.pow(2.0f, volumeHalfLives);
                            }
                        }
                    }

                    distanceAttenuation.volume = distanceAttenuationVolume;
                    mgr.SetComponentData<AudioDistanceAttenuation>(e, distanceAttenuation);
                }

                if (HasComponent<Audio3dPanning>(e))
                {
                    Audio3dPanning panning = mgr.GetComponentData<Audio3dPanning>(e);
                    float pan = 0.0f;

                    if (foundListener && mgr.HasComponent<LocalToWorld>(e))
                    {
                        LocalToWorld localToWorld = mgr.GetComponentData<LocalToWorld>(e);
                        float3 listenerRight = math.normalize(listenerLocalToWorld.Right);
                        float3 listenerToSound = math.normalize(localToWorld.Position - listenerLocalToWorld.Position);
                        pan = math.dot(listenerRight, listenerToSound);
                    }

                    panning.pan = pan;
                    mgr.SetComponentData<Audio3dPanning>(e, panning);
                }
            }

#if ENABLE_DOTSRUNTIME_PROFILER
            ProfilerStats.GatheredStats |= ProfilerModes.ProfileAudio;

            ProfilerStats.AccumStats.audioPlayingSources.value = 0;
            ProfilerStats.AccumStats.audioPausedSources.value = 0;
            Entities.ForEach((Entity e, ref AudioSource source) =>
            {
                if (source.isPlaying)
                    ProfilerStats.AccumStats.audioPlayingSources.Accumulate(1);
                else
                    ProfilerStats.AccumStats.audioPausedSources.Accumulate(1);
            }).Run();

            // No concept of multiple clips playing per audio source in Tiny Audio
            ProfilerStats.AccumStats.audioNumSoundChannelInstances = ProfilerStats.AccumStats.audioPlayingSources;
#endif
        }

        void ClearAudioBuffers()
        {
            var mgr = EntityManager;

            DynamicBuffer<EntityToStop> entitiesToStop = mgr.GetBuffer<EntityToStop>(m_audioEntity);
            entitiesToStop.Length = 0;

            DynamicBuffer<SourceIDToStop> sourceIDsToStop = mgr.GetBuffer<SourceIDToStop>(m_audioEntity);
            sourceIDsToStop.Length = 0;

            DynamicBuffer<EntityPlaying> entitiesPlaying = mgr.GetBuffer<EntityPlaying>(m_audioEntity);
            entitiesPlaying.Length = 0;
        }
    }
}
