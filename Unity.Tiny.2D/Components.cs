using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Tiny
{
    /// <summary>
    /// Data structure that represents a vertex in a Sprite mesh.
    /// </summary>
    public struct SpriteVertex
    {
        /// <summary>
        /// Position of the vertex.
        /// </summary>
        /// <value>-</value>
        public float3 Position;
        /// <summary>
        /// UV coordinate of the vertex.
        /// </summary>
        /// <value>-</value>
        public float2 TexCoord0;
    }

    /// <summary>
    /// Data structure that represents a Sprite's mesh.
    /// </summary>
    public struct SpriteMesh
    {
        /// <summary>
        /// Array holding all vertex data of the mesh.
        /// </summary>
        /// <value>-</value>
        public BlobArray<SpriteVertex> Vertices;

        /// <summary>
        /// Array holding all index data of the mesh.
        /// </summary>
        /// <value>-</value>
        public BlobArray<ushort> Indices;

        /// <summary>
        /// Mesh bounds.
        /// </summary>
        /// <value>-</value>
        public AABB Bounds;
    }

    /// <summary>
    /// Component that holds Sprite data.
    /// </summary>
    public struct Sprite : IComponentData
    {
        /// <summary>
        /// Reference to the Sprite's mesh.
        /// </summary>
        /// <value>-</value>
        public BlobAssetReference<SpriteMesh> Mesh;
        /// <summary>
        /// Link to the entity holding Texture data.
        /// </summary>
        /// <value>-</value>
        public Entity Texture;
    }

    /// <summary>
    /// Component that holds general 2D rendering data.
    /// </summary>
    public struct Renderer2D : IComponentData
    {
        /// <summary>
        /// The Layer the Renderer is in. Layers can be used for selective rendering from the camera.
        /// </summary>
        /// <value>The Layer needs to be within the range of [0...31].</value>
        public int RenderingLayer;
        /// <summary>
        ///  The Sorting Layer that the Renderer is set to, which determines its priority in the render queue.
        /// </summary>
        /// <value>
        /// Lower valued Renderers are rendered first, with higher valued Renderers overlapping them and appearing closer to the camera.
        /// </value>
        public short SortingLayer;
        /// <summary>
        ///  The Order in Layer value of the Renderer, which determines its render priority within its Sorting Layer.
        /// </summary>
        /// <value>
        /// Lower valued Renderers are rendered first, with higher valued Renderers overlapping them and appearing closer to the camera.
        /// </value>
        public short OrderInLayer;
        /// <summary>
        /// Local bounds of the Renderer.
        /// </summary>
        /// <value>-</value>
        public AABB Bounds;
    }

    /// <summary>
    /// Component that holds Sprite Renderer data.
    /// </summary>
    public struct SpriteRenderer : IComponentData
    {
        /// <summary>
        /// Link to the entity holding Sprite data.
        /// </summary>
        /// <value>-</value>
        public Entity Sprite;
        /// <summary>
        /// Link to the entity holding Material data.
        /// </summary>
        /// <value>-</value>
        public Entity Material;
        /// <summary>
        /// Rendering color for the Sprite graphic.
        /// </summary>
        /// <value>The default color is white.</value>
        public float4 Color;
    }

    public static class ShaderGuid
    {
        public static readonly Hash128 SpriteDefault = new Hash128("8FB548888C9446179518348C6AE7E9E0");
    }

    internal struct VisibleNode
    {
        public Entity Key;
        public ulong LayerAndOrder;
        public uint Depth;
    }
}
