using System;
using System.IO;
using System.Runtime.InteropServices;
using WebP;
using UnityEditor;
using UnityEngine;

namespace Unity.TinyConversion
{
    public enum TextureFormatType
    {
        PNG,
        WebP
    }

   internal static class Texture2DExportUtils
    {
        internal static bool HasColor(UnityEngine.Texture2D texture)
        {
            return texture.format != UnityEngine.TextureFormat.Alpha8;
        }

        internal static bool HasAlpha(UnityEngine.Texture2D texture)
        {
            if (!HasAlpha(texture.format))
            {
                return false;
            }

            if (texture.format == UnityEngine.TextureFormat.ARGB4444 ||
                texture.format == UnityEngine.TextureFormat.ARGB32 ||
                texture.format == UnityEngine.TextureFormat.RGBA32)
            {
                var tmp = BlitTexture(texture, UnityEngine.TextureFormat.ARGB32);
                UnityEngine.Color32[] pix = tmp.GetPixels32();
                for (int i = 0; i < pix.Length; ++i)
                {
                    if (pix[i].a != byte.MaxValue)
                    {
                        return true;
                    }
                }

                // image has alpha channel, but every alpha value is opaque
                return false;
            }
            return true;
        }

        internal static bool HasAlpha(UnityEngine.TextureFormat format)
        {
            return format == UnityEngine.TextureFormat.Alpha8 ||
                format == UnityEngine.TextureFormat.ARGB4444 ||
                format == UnityEngine.TextureFormat.ARGB32 ||
                format == UnityEngine.TextureFormat.RGBA32 ||
                format == UnityEngine.TextureFormat.DXT5 ||
                format == UnityEngine.TextureFormat.PVRTC_RGBA2 ||
                format == UnityEngine.TextureFormat.PVRTC_RGBA4 ||
                format == UnityEngine.TextureFormat.ETC2_RGBA8 ||
                format == UnityEngine.TextureFormat.RGBAHalf ||
                format == UnityEngine.TextureFormat.DXT5Crunched;
        }

        internal static UnityEngine.Texture2D BlitTexture(UnityEngine.Texture2D texture, UnityEngine.TextureFormat format, bool alphaOnly = false)
        {
            var texPath = AssetDatabase.GetAssetPath(texture);
            var asset = AssetDatabase.LoadMainAssetAtPath(texPath);
            RenderTextureReadWrite rtReadWrite = UnityEngine.RenderTextureReadWrite.sRGB;

            if (asset is Texture2D)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
                if (importer != null)
                    rtReadWrite = importer.sRGBTexture ? UnityEngine.RenderTextureReadWrite.sRGB : UnityEngine.RenderTextureReadWrite.Linear;
            }

            // hack to support text mesh pro
            var fontAssetType = Type.GetType("TMPro.TMP_FontAsset, Unity.TextMeshPro, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            if (fontAssetType != null && fontAssetType.IsInstanceOfType(asset))
            {
                // TMPro texture atlases are always Linear space
                rtReadWrite = UnityEngine.RenderTextureReadWrite.Linear;
            }

            // Create a temporary RenderTexture of the same size as the texture
            var tmp = UnityEngine.RenderTexture.GetTemporary(
                texture.width,
                texture.height,
                0,
                UnityEngine.RenderTextureFormat.Default,
                rtReadWrite);

            // Blit the pixels on texture to the RenderTexture
            UnityEngine.Graphics.Blit(texture, tmp);

            // Backup the currently set RenderTexture
            var previous = UnityEngine.RenderTexture.active;

            // Set the current RenderTexture to the temporary one we created
            UnityEngine.RenderTexture.active = tmp;

            // Create a new readable Texture2D to copy the pixels to it
            var result = new UnityEngine.Texture2D(texture.width, texture.height, format, false);

            // Copy the pixels from the RenderTexture to the new Texture
            result.ReadPixels(new UnityEngine.Rect(0, 0, tmp.width, tmp.height), 0, 0);
            result.Apply();

            // Broadcast alpha to color
            if (alphaOnly || !HasColor(texture))
            {
                var pixels = result.GetPixels();
                for (var i = 0; i < pixels.Length; i++)
                {
                    pixels[i].r = pixels[i].a;
                    pixels[i].g = pixels[i].a;
                    pixels[i].b = pixels[i].a;
                }

                result.SetPixels(pixels);
                result.Apply();
            }

            // Reset the active RenderTexture
            UnityEngine.RenderTexture.active = previous;

            // Release the temporary RenderTexture
            UnityEngine.RenderTexture.ReleaseTemporary(tmp);
            return result;
        }

        public static void ExportPng(Stream writer, UnityEngine.Texture2D texture)
        {
            var format = HasAlpha(texture) ? UnityEngine.TextureFormat.RGBA32 : UnityEngine.TextureFormat.RGB24;
            var outputTexture = BlitTexture(texture, format);
            var bytes = UnityEngine.ImageConversion.EncodeToPNG(outputTexture);
            writer.Write(bytes, 0, bytes.Length);
            writer.Dispose();
        }

        const int WEBP_ENCODER_ABI_VERSION = 527;

        internal static unsafe void EncodeWebP(Stream writer, Texture2D texture, bool lossless, float quality)
        {
            bool hasAlpha = HasAlpha(texture);
           // Stream stream = fileInfo.Create();
            var format = hasAlpha ? UnityEngine.TextureFormat.RGBA32 : UnityEngine.TextureFormat.RGB24;
            var outputTexture = BlitTexture(texture, format);

            WebPConfig config = new WebPConfig();
            WebPPicture picture = new WebPPicture();

            if (WebpEncoderNativeCalls.WebPConfigInitInternal(ref config, WebPPreset.WEBP_PRESET_DEFAULT, quality, WEBP_ENCODER_ABI_VERSION) == 0)
                throw new Exception("Failed to initialize WebPConfig.");
            if (lossless)
            {
                //TODO: refine the level value [0..9] we want to set. 6 seems a good default value
                WebpEncoderNativeCalls.WebPConfigLosslessPreset(ref config, 6);
            }

            if (WebpEncoderNativeCalls.WebPValidateConfig(ref config) == 0)
                throw new Exception("Failed to validate WebPConfig.");

            if (WebpEncoderNativeCalls.WebPPictureInitInternal(ref picture, WEBP_ENCODER_ABI_VERSION) == 0)
                throw new Exception("Failed to initialize WebPPicture, version mismatch.");  // version mismatch error
            picture.width = outputTexture.width;
            picture.height = outputTexture.height;
            if (WebpEncoderNativeCalls.WebPPictureAlloc(ref picture) == 0)
                throw new Exception("Failed to allocate WebPPicture");

            GCHandle lPinnedArray = GCHandle.Alloc(outputTexture.GetRawTextureData(), GCHandleType.Pinned);
            fixed (byte* buffer = outputTexture.GetRawTextureData())
            {
                if (hasAlpha)
                {
                    int stride = picture.width * 4;
                    byte* lTmpDataPtr = buffer + (picture.height - 1) * stride;
                    WebpEncoderNativeCalls.WebPPictureImportRGBA(ref picture, lTmpDataPtr, -stride);
                }
                else
                {
                    int stride = picture.width * 3;
                    byte* lTmpDataPtr = buffer + (picture.height - 1) * stride;
                    WebpEncoderNativeCalls.WebPPictureImportRGB(ref picture, lTmpDataPtr, -stride);
                }
            }

            //Write the compressed data in WebPWriterFunction
            picture.writer = (IntPtr data, UIntPtr size, ref WebPPicture pPicture) =>
            {
                byte[] res = new byte[size.ToUInt32()];
                Marshal.Copy(data, res, 0, (int)size.ToUInt32());
                try
                {
                    writer.Write(res, 0, res.Length);
                }
                catch (Exception e)
                {
                    throw new Exception("Failed in writing a WebP compressed image to a stream." + e.Message );
                }
                return 1;
            };

            if (WebpEncoderNativeCalls.WebPEncode(ref config, ref picture) == 0) {
                WebpEncoderNativeCalls.WebPPictureFree(ref picture);
                throw new Exception($"Failed to encode webp image error: {picture.error_code.ToString()}");
            }

            WebpEncoderNativeCalls.WebPPictureFree(ref picture);

            writer.Dispose();
            lPinnedArray.Free();
        }
    }
}
