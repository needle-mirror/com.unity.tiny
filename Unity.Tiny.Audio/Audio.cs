using Unity.Collections;
using Unity.Entities;
using Unity.Tiny;
using Unity.Transforms;
using Unity.Mathematics;

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

    public enum AudioRolloffMode
    {
        Logarithmic = 0,
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
    }

    /// <summary>
    /// Location of the audio file. <seealso cref="AudioClip"/>
    /// </summary>
    public struct AudioClipLoadFromFileAudioFile: IBufferElementData
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


    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public abstract class AudioSystem : SystemBase
    {
        protected abstract void InitAudioSystem();
        protected abstract void DestroyAudioSystem();

        protected abstract bool PlaySource(Entity e);
        protected abstract void StopSource(Entity e);
        protected abstract bool IsPlaying(Entity e);
        protected abstract bool SetVolume(Entity e, float volume);
        protected abstract bool SetPan(Entity e, float pan);
        protected abstract bool SetPitch(Entity e, float pitch);

        protected override void OnCreate()
        {
            InitAudioSystem();
        }

        protected override void OnDestroy()
        {
            DestroyAudioSystem();
        }

        protected override void OnUpdate()
        {
            var mgr = EntityManager;
            LocalToWorld listenerLocalToWorld = new LocalToWorld();
            bool foundListener = false;

            // Get the listener position.
            Entities.ForEach((Entity e, ref LocalToWorld localToWorld, ref AudioListener listener) =>
            {
                if (!foundListener)
                {
                    listenerLocalToWorld = localToWorld;
                    foundListener = true;
                }
            }).Run();

            // Stop sounds.
            {
                Entities
                    .WithStructuralChanges()
                    .WithoutBurst()
                    .WithAll<AudioSource>()
                    .ForEach((Entity e, ref AudioSourceStop tag) =>
                    {
                        StopSource(e);
                        mgr.RemoveComponent<AudioSourceStop>(e);
                    }).Run();
                Entities
                    .WithoutBurst()
                    .WithAll<AudioSource>()
                    .ForEach((Entity e, ref Disabled tag) =>
                    {
                        StopSource(e);
                    }).Run();
            }

            // Play sounds.
            {
                Entities
                    .WithStructuralChanges()
                    .WithAll<AudioSource>()
                    .ForEach((Entity e, ref AudioSourceStart tag) =>
                    {
                        if(PlaySource(e))
                            mgr.RemoveComponent<AudioSourceStart>(e);
                    }).Run();
            }

            // Update isPlaying.
            Entities
                .WithoutBurst()
                .ForEach((Entity e, ref AudioSource source) =>
            {
                source.isPlaying = IsPlaying(e);
            }).Run();

            // Update volume for sources that are not distance-attenuated.
            Entities
                .WithoutBurst()
                .WithNone<AudioDistanceAttenuation>()
                .ForEach((Entity e, ref AudioSource source) =>
            {
                SetVolume(e, source.volume);
            }).Run();

            // Update volume for sources that are distance-attenuated.
            Entities
                .WithoutBurst()
                .ForEach((Entity e, ref AudioSource source, ref AudioDistanceAttenuation distanceAttenuation) =>
            {
                float distanceAttenuationVolume = 0.0f;

                if (foundListener && HasComponent<LocalToWorld>(e))
                {
                    LocalToWorld localToWorld = GetComponent<LocalToWorld>(e);
                    float xDist = localToWorld.Position.x - listenerLocalToWorld.Position.x;
                    float yDist = localToWorld.Position.y - listenerLocalToWorld.Position.y;
                    float zDist = localToWorld.Position.z - listenerLocalToWorld.Position.z;
                    float distanceToListener = math.sqrt(xDist*xDist + yDist*yDist + zDist*zDist);

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
                SetVolume(e, source.volume * distanceAttenuationVolume);
            }).Run();

            // Update 2d panning.
            Entities
                .WithoutBurst()
                .ForEach((Entity e, ref AudioSource source, ref Audio2dPanning panning) =>
            {
                SetPan(e, panning.pan);
            }).Run();

            // Update 3d panning.
            Entities
                .WithoutBurst()
                .ForEach((Entity e, ref AudioSource source, ref Audio3dPanning panning) =>
            {
                float pan = 0.0f;

                if (foundListener && HasComponent<LocalToWorld>(e))
                {
                    LocalToWorld localToWorld = GetComponent<LocalToWorld>(e);
                    float3 listenerRight = math.normalize(listenerLocalToWorld.Right);
                    float3 listenerToSound = math.normalize(localToWorld.Position - listenerLocalToWorld.Position);
                    pan = math.dot(listenerRight, listenerToSound);
                }

                panning.pan = pan;
                SetPan(e, pan);
            }).Run();

            // Update pitch.
            Entities
                .WithoutBurst()
                .ForEach((Entity e, ref AudioSource source, ref AudioPitch pitchEffect) =>
            {
                if (pitchEffect.pitch > 0.0f)
                    SetPitch(e, pitchEffect.pitch);
                else
                    SetPitch(e, 1.0f);
            }).Run();
        }
    }

    /// <summary>
    ///  Configures the global audio state, which you can access via TinyEnvironment.GetConfigData
    ///  This component is attached to the Config entity.
    /// </summary>
    public struct AudioConfig : IComponentData
    {
        /// <summary>
        ///  True if the audio context is initialized.
        /// </summary>
        /// <remarks>
        ///  After you export and launch the project, and the AudioSystem updates
        ///  for the first time, the AudioConfig component attempts to initialize
        ///  audio. If successful, it sets this value to true.
        ///
        ///  Once audio is initialized successfully the AudioConfig component does
        ///  not re-attempt to initialize it on subsequent AudioSystem updates.
        /// </remarks>
        public bool initialized;

        /// <summary>
        ///  If true, pauses the audio context. Set this at any time to pause or
        ///  resume audio.
        /// </summary>
        public bool paused;

        /// <summary>
        ///  True if the audio context is unlocked in the browser.
        /// </summary>
        /// <remarks>
        ///  Some browsers require a user interaction, for example a touch interaction
        ///  or key input, to unlock the audio context. If the context is locked
        ///  no audio plays.
        /// </remarks>
        public bool unlocked;
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
        public bool isPlaying { get; internal set; }
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
    public struct Audio2dPanning: IComponentData
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
        public float pan { get; internal set; }
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
        public float volume { get; internal set; }
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
}
