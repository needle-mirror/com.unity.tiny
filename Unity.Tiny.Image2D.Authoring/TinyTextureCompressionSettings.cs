using System;
using System.Collections.Generic;
using System.Linq;
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
        public TextureFormatType FormatType = TextureFormatType.PNG;

        [CreateProperty]
        public float CompressionQuality = 100.0f;

        [CreateProperty]
        public bool Lossless = false;
    }

    public class TinyTextureCompressionSettingsOverride
    {
        [CreateProperty]
        public Texture2D Texture;

        [CreateProperty]
        public TinyTextureCompressionParams Parameters = new TinyTextureCompressionParams();
    }

    public class TinyTextureCompressionSettings : IDotsRuntimeBuildModifier
    {
        [CreateProperty]
        public TinyTextureCompressionParams Parameters = new TinyTextureCompressionParams();

        [CreateProperty]
        public List<TinyTextureCompressionSettingsOverride> Overrides = new List<TinyTextureCompressionSettingsOverride>();

        public void Modify(JsonObject jsonObject)
        {
            if (Parameters.FormatType == TextureFormatType.WebP || Overrides.Any(v => v.Parameters.FormatType == TextureFormatType.WebP))
                jsonObject["ExportWebPFallback"] = true;
            else
                jsonObject["ExportWebPFallback"] = false;
        }
    }
}
