using Unity.Build;
using Unity.Properties;

namespace Unity.Tiny.Rendering.Settings
{
    //Optional setting
    public class TinyShaderSettings : IBuildComponent
    {
        [CreateProperty]
        public bool PackageShadersForAllPlatforms;
    }
}
