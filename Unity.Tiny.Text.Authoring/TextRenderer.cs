using System;
using UnityEngine;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using Unity.Tiny.Rendering;
using Unity.TinyConversion;
using UnityEngine.Rendering;
using MeshRenderer = UnityEngine.MeshRenderer;

namespace Unity.Tiny.Text.Authoring
{
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    [DisallowMultipleComponent]
    [ExecuteInEditMode]
    [AddComponentMenu("Text Renderer (Tiny)")]
    public class TextRenderer : MonoBehaviour
    {
        [TextArea(5, 10)]
        public string Text;
        public float Size;
        public TMP_FontAsset Font;
        public UnityEngine.Color Color;
        public HorizontalAlignment Alignment;

        private string m_PrevText;
        private float m_PrevSize;
        private UnityEngine.Color m_PrevColor;
        private HorizontalAlignment m_PrevAlignment;

        private TMP_FontAsset m_PrevFont;
        private BlobAssetReference<FontData> m_GeneratedFontData;

        private MeshRenderer m_MeshRenderer;
        private MeshFilter m_MeshFilter;

        void Awake()
        {
            m_MeshFilter = GetComponent<MeshFilter>();
            if (!m_MeshFilter)
                m_MeshFilter = gameObject.AddComponent<MeshFilter>();
            m_MeshFilter.hideFlags = HideFlags.HideAndDontSave;

            m_MeshRenderer = GetComponent<MeshRenderer>();
            if (!m_MeshRenderer)
                m_MeshRenderer = gameObject.AddComponent<MeshRenderer>();
            m_MeshRenderer.hideFlags = HideFlags.HideAndDontSave;
        }

        void OnDestroy()
        {
            if (m_GeneratedFontData.IsCreated)
                m_GeneratedFontData.Dispose();
        }

        void UpdateMesh()
        {
            if (Font == null || Size <= 0.0f || Text == null || Text.Length == 0)
            {
                // only use Text to flag this; we do want to keep the material
                // and TextData intact if the Font doesn't change
                if (m_PrevText != null)
                {
                    // Text was cleared, nothing to render any more
                    m_MeshFilter.mesh = null;
                    m_PrevText = null;
                }

                return;
            }

            var rebuildMesh = false;

            if ((!m_PrevFont || !m_PrevFont.Equals(Font)) || !m_GeneratedFontData.IsCreated)
            {
                if (m_GeneratedFontData.IsCreated)
                    m_GeneratedFontData.Dispose();
                m_GeneratedFontData = FontAssetConversion.ConvertFontData(Font);
                m_PrevFont = Font;
                m_MeshRenderer.sharedMaterial = Font.material;

                rebuildMesh = true;
            }

            if (m_PrevSize != Size || m_PrevText != Text || m_PrevAlignment != Alignment || m_PrevColor != Color)
            {
                rebuildMesh = true;
            }

            if (!rebuildMesh)
            {
                return;
            }

            // Lay out the mesh in the same way that the runtime will do it
            var vdata = new NativeList<SimpleVertex>(128, Allocator.Temp);
            var idata = new NativeList<ushort>(128, Allocator.Temp);

            unsafe
            {
                fixed (char* chars = Text)
                    TextLayout.LayoutString(chars, Text.Length, Size, Alignment, VerticalAlignment.Baseline, Color.ToTiny().AsFloat4(), ref m_GeneratedFontData.Value,
                        vdata, idata, out AABB bbox);

                // Flip vdata.TexCoord0.y for Unity's UV direction.  Do this here to make
                // it easier to have two different paths below.
                for (int i = 0; i < vdata.Length; ++i)
                {
                    ref var item = ref vdata.ElementAt(i);
                    item.TexCoord0.y = 1.0f - item.TexCoord0.y;
                }
            }

            var newMesh = new Mesh();
            newMesh.hideFlags = HideFlags.HideAndDontSave;

#if true
            // Need to split things out for Unity.
            var vbuf = new NativeArray<float3>(vdata.Length, Allocator.Temp);
            var uv0buf = new NativeArray<float2>(vdata.Length, Allocator.Temp);
            var cbuf = new NativeArray<float4>(vdata.Length, Allocator.Temp);

            for (int i = 0; i < vdata.Length; ++i)
            {
                vbuf[i] = vdata[i].Position;
                cbuf[i] = vdata[i].Color;
                uv0buf[i] = vdata[i].TexCoord0;
            }

            newMesh.SetVertices(vbuf);
            newMesh.SetColors(cbuf);
            newMesh.SetUVs(0, uv0buf);
            newMesh.SetIndices(idata.AsArray(), MeshTopology.Triangles, 0, calculateBounds: true, baseVertex: 0);
            newMesh.UploadMeshData(true);

            vbuf.Dispose();
            uv0buf.Dispose();
            cbuf.Dispose();
#else
            // This should work.  Not clear why it doesn't.
            var layout = new []
            {
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
                new VertexAttributeDescriptor(VertexAttribute.MeshColor, VertexAttributeFormat.Float32, 4),
                // this is actually BillboardPos in SimpleVertex
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 3),
            };

            newMesh.SetVertexBufferParams(vdata.Length, layout);
            newMesh.SetIndexBufferParams(idata.Length, IndexFormat.UInt16);
            newMesh.SetVertexBufferData(vdata.AsArray(), 0, 0, vdata.Length); //, 0, MeshUpdateFlags.DontValidateIndices);
            newMesh.SetIndexBufferData(idata.AsArray(), 0, 0, idata.Length); //, MeshUpdateFlags.DontValidateIndices);
            newMesh.UploadMeshData(true);
#endif

            vdata.Dispose();
            idata.Dispose();

            m_MeshFilter.mesh = newMesh;

            m_PrevSize = Size;
            m_PrevText = Text;
            m_PrevAlignment = Alignment;
            m_PrevColor = Color;
        }

        void Update()
        {
            UpdateMesh();
        }
    }
}
