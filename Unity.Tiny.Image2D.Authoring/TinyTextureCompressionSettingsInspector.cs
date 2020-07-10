using Unity.Properties.UI;
using Unity.TinyConversion;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Unity.Tiny.Texture.Settings
{
    class TinyTextureCompressionSettingsInspector : Inspector<TinyTextureCompressionSettings>
    {
        public override VisualElement Build()
        {
            var root = new VisualElement();
            DoDefaultGui(root, nameof(TinyTextureCompressionSettings.Parameters));
            DoDefaultGui(root, nameof(TinyTextureCompressionSettings.Overrides));
            return root;
        }
    }

    class TinyTextureCompressionOverrideInspector : Inspector<TinyTextureCompressionSettingsOverride>
    {
        public override VisualElement Build()
        {
            var root = new VisualElement();
            DoDefaultGui(root, nameof(TinyTextureCompressionSettingsOverride.Texture));
            DoDefaultGui(root, nameof(TinyTextureCompressionSettingsOverride.Parameters));
            return root;
        }
    }

    class TinyTextureCompressionParamsInspector: Inspector<TinyTextureCompressionParams>
    {
        EnumField m_DefaultFormatType;
        FloatField m_CompressionQuality;
        Toggle m_lossless;

        public override void Update()
        {
            if (m_CompressionQuality != null)
            {
                m_CompressionQuality.SetEnabled((TextureFormatType)m_DefaultFormatType.value == TextureFormatType.WebP);
                if ((TextureFormatType)m_DefaultFormatType.value == TextureFormatType.WebP)
                {
                    if (m_CompressionQuality.value > 100)
                        m_CompressionQuality.value = 100;
                    else if (m_CompressionQuality.value < 0)
                        m_CompressionQuality.value = 0;
                }
            }

            if (m_lossless != null)
            {
                m_lossless.SetEnabled((TextureFormatType)m_DefaultFormatType.value == TextureFormatType.WebP);
                if (m_lossless.value && m_CompressionQuality != null)
                    m_CompressionQuality.SetEnabled(false);
            }
        }

        public override VisualElement Build()
        {
            var root = new VisualElement();
            DoDefaultGui(root, nameof(TinyTextureCompressionParams.FormatType));
            m_DefaultFormatType = root.Q<EnumField>(nameof(TinyTextureCompressionParams.FormatType));

            DoDefaultGui(root, nameof(TinyTextureCompressionParams.Lossless));
            m_lossless = root.Q<Toggle>(nameof(TinyTextureCompressionParams.Lossless));

            DoDefaultGui(root, nameof(TinyTextureCompressionParams.CompressionQuality));
            m_CompressionQuality = root.Q<FloatField>(nameof(TinyTextureCompressionParams.CompressionQuality));

            return root;
        }
    }
}
