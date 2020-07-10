using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Build;
using Unity.Build.DotsRuntime;
using Unity.Properties;
using Unity.TinyConversion;
using UnityEngine;
using Unity.Serialization.Json;

namespace Unity.Tiny.Texture.Settings
{
    public class TinyTextureCompressionParams
    {
        [CreateProperty]
        public TextureFormatType FormatType = TextureFormatType.WebP;

        [CreateProperty]
        public float CompressionQuality = 100.0f;

        [CreateProperty]
        public bool Lossless = true;
    }

    public class TinyTextureCompressionSettingsOverride
    {
        [CreateProperty]
        public Texture2D Texture;

        [CreateProperty]
        public TinyTextureCompressionParams Parameters = new TinyTextureCompressionParams();
    }

    public class TinyTextureCompressionSettings : IBuildComponent
    {
        [CreateProperty]
        public TinyTextureCompressionParams Parameters = new TinyTextureCompressionParams();

        [CreateProperty]
        public List<TinyTextureCompressionSettingsOverride> Overrides = new List<TinyTextureCompressionSettingsOverride>();
    }
}
