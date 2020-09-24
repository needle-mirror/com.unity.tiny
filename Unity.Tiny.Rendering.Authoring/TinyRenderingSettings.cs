using Unity.Build;
using Unity.Properties;
using UnityEngine;

namespace Unity.Tiny.Rendering.Settings
{
    //TODO Need to find a way to retrieve project settings from runtime component without bringing a dependency to runtime packages
    public class TinyRenderingSettings : IBuildComponent
    {
        [CreateProperty]
        public Vector2Int WindowSize = new Vector2Int(1920, 1080);

        [CreateProperty]
        public Vector2Int RenderResolution = new Vector2Int(1920, 1080);

        [CreateProperty]
        public RenderGraphMode RenderGraphMode = RenderGraphMode.FixedRenderBuffer;

        [CreateProperty]
        public int MaxResolution = 2048;

        [CreateProperty]
        public bool AutoResizeFrame = true;

        [CreateProperty]
        public bool DisableVsync = false;

        [CreateProperty]
        public bool GPUSkinning = false;
    }
}
