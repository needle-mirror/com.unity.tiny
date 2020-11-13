using System;
using Unity.Entities;

namespace Unity.Tiny
{
	/// <summary>
    ///  Configures the global audio state, which you can access via GetSingleton<AudioConfig>()
    ///  This component is attached to the Config entity.
    /// </summary>
    public struct AudioConfig : IComponentData
    {
        public static AudioConfig Default { get; } = new AudioConfig
        {
            initialized = false,
            paused = false,
            unlocked = false,
            maxUncompressedAudioMemoryBytes = 50*1024*1024
        };

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

		/// <summary>
        /// This is the memory limit, in bytes, for sounds that are decompress-on-demand.
        /// </summary>
        public int maxUncompressedAudioMemoryBytes;
    }
}