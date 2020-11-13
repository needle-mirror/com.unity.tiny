using Unity.Build;
using Unity.Properties;
using UnityEngine;

namespace Unity.Tiny.Audio.Settings
{
    public class TinyAudioSettings : IBuildComponent
    {
        [CreateProperty]
        public int MaxUncompressedAudioMemoryBytes = 50*1024*1024;
    }
}
