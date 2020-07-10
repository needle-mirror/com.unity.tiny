using System;
using Unity.Entities;
using UnityEditor;
using Unity.Tiny;
using Unity.Tiny.Texture.Settings;
using UnityEngine;

namespace Unity.TinyConversion
{
    internal class ConvertTexture2DAsset : GameObjectConversionSystem
    {
        void CheckTextureImporter(UnityEngine.Texture2D texture, TextureImporter importer)
        {
            if(importer.crunchedCompression)
                throw new ArgumentException($"Crunch compression is not supported yet, and will result in an incorrect texture at runtime. Please uncheck the 'Use crunch compression' box on the texture {texture.name}.");
        }

        protected override void OnUpdate()
        {
            Entities.ForEach((UnityEngine.Texture2D texture) =>
            {
                var entity = GetPrimaryEntity(texture);
                string texPath = AssetDatabase.GetAssetPath(texture);

                var asset = AssetDatabase.LoadMainAssetAtPath(texPath);
                TextureImporterSettings textureImporterSettings = null;

                if (asset is Texture2D)
                {
                    // grab the TextureImporterSettings
                    var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
                    textureImporterSettings = new TextureImporterSettings();

                    if (importer != null)
                    {
                        importer.ReadTextureSettings(textureImporterSettings);
                        CheckTextureImporter(texture, importer);
                    }
                }

                DstEntityManager.AddComponentData(entity, new Image2D()
                {
                    imagePixelWidth = texture.width,
                    imagePixelHeight = texture.height,
                    status = ImageStatus.Invalid,
                    flags = Texture2DConversionUtils.GetTextureFlags(texture, textureImporterSettings)
                });

                DstEntityManager.AddComponent<Image2DLoadFromFile>(entity);

                var exportGuid = GetGuidForAssetExport(texture);

                DstEntityManager.AddComponentData(entity, new Image2DLoadFromFileGuids()
                {
                    imageAsset = exportGuid,
                    maskAsset = Guid.Empty
                });
            });
        }
    }

    [UpdateInGroup(typeof(GameObjectExportGroup))]
    internal class ExportTexture2DAsset : GameObjectConversionSystem
    {
        TinyTextureCompressionParams GetTextureCompressionParams(Texture texture)
        {
            if (!TryGetBuildConfigurationComponent<TinyTextureCompressionSettings>(out var settings))
                return new TinyTextureCompressionParams();

            if (settings.Overrides.Count > 0)
            {
                foreach (var o in settings.Overrides)
                {
                    if (o.Texture.Equals(texture))
                        return o.Parameters;
                }
            }
            return settings.Parameters;
        }

        protected override void OnUpdate()
        {
            Entities.ForEach((UnityEngine.Texture2D texture) =>
            {
                var writer = TryCreateAssetExportWriter(texture);
                if (writer != null)
                {
                    var parameters = GetTextureCompressionParams(texture);
                    switch (parameters.FormatType)
                    {
                        case TextureFormatType.WebP:
                            Texture2DExportUtils.EncodeWebP(writer, texture, parameters.Lossless, parameters.CompressionQuality);
                            break;
                        case TextureFormatType.PNG:
                        default:
                            Texture2DExportUtils.ExportPng(writer, texture);
                            break;
                    }
                }
                else
                {
                    throw new Exception($"Failed to retrieve FileInfo for texture: {texture.name}");
                }
            });
        }
    }

    internal static class Texture2DConversionUtils
    {
        internal static bool IsPowerOfTwo(UnityEngine.Texture2D texture)
        {
            return texture.width > 0 && texture.height > 0 && ((texture.width & (texture.width - 1)) == 0) && ((texture.height & (texture.height - 1)) == 0);
        }

        internal static TextureFlags GetTextureFlags(UnityEngine.Texture2D texture, TextureImporterSettings textureImporter)
        {
            TextureFlags flags = 0;

            switch (texture.filterMode)
            {
                case UnityEngine.FilterMode.Point:
                    flags |= TextureFlags.Point;
                    break;
                case UnityEngine.FilterMode.Trilinear:
                    flags |= TextureFlags.Trilinear;
                    break;
                default:
                    flags |= TextureFlags.Linear;
                    break;
            }

            switch (texture.wrapModeU)
            {
                case UnityEngine.TextureWrapMode.Clamp:
                    flags |= TextureFlags.UClamp;
                    break;
                case UnityEngine.TextureWrapMode.Mirror:
                    flags |= TextureFlags.UMirror;
                    break;
                case UnityEngine.TextureWrapMode.Repeat:
                    flags |= TextureFlags.URepeat;
                    break;
            }

            switch (texture.wrapModeV)
            {
                case UnityEngine.TextureWrapMode.Clamp:
                    flags |= TextureFlags.VClamp;
                    break;
                case UnityEngine.TextureWrapMode.Mirror:
                    flags |= TextureFlags.VMirror;
                    break;
                case UnityEngine.TextureWrapMode.Repeat:
                    flags |= TextureFlags.URepeat;
                    break;
            }

            if (texture.mipmapCount > 1)
                flags |= TextureFlags.MimapEnabled;

            if (textureImporter == null || textureImporter.sRGBTexture)
                flags |= TextureFlags.Srgb;

            if (textureImporter?.textureType == TextureImporterType.NormalMap)
                flags |= TextureFlags.IsNormalMap;

            if (!IsPowerOfTwo(texture))
            {
                if ((flags & TextureFlags.MimapEnabled) == TextureFlags.MimapEnabled)
                    throw new ArgumentException($"Mipmapping is incompatible with NPOT textures. Update texture: {texture.name} to be power of two or disable mipmaps on it.");
                else if ((flags & TextureFlags.UClamp) != TextureFlags.UClamp ||
                    (flags & TextureFlags.VClamp) != TextureFlags.VClamp)
                    throw new ArgumentException($"NPOT textures must use clamp wrap mode. Update texture: {texture.name} to be power of two or use clamp mode on it.");
            }

            return flags;
        }
    }
}
