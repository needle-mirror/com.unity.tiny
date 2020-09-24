using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Tiny.Rendering
{
    /// <summary>
    /// Mesh renderer component containing a reference to a material to render with and submesh information
    /// </summary>
    public struct MeshRenderer : IComponentData
    {
        public Entity material;     // points to the entity with a material, must be a lit material
        public Entity mesh;         // points to the entity with the mesh, this must be a lit mesh
        public int startIndex;      // sub mesh indexing
        public int indexCount;
    }

    /// <summary>
    /// Component next to a MeshRenderer, indicating it is unlit
    /// </summary>
    public struct SimpleMeshRenderer : IComponentData
    {
    }

    /// <summary>
    /// Component next to a MeshRenderer, indicating it is lit with the basic lit shader
    /// </summary>
    public struct LitMeshRenderer : IComponentData
    {
    }

    /// <summary>
    /// Component next to a MeshRenderer, indicating it renders unlit particles
    /// </summary>
    public struct SimpleParticleRenderer : IComponentData
    {
    }

    /// <summary>
    /// Component next to a MeshRenderer, indicating it renders lit particles
    /// </summary>
    public struct LitParticleRenderer : IComponentData
    {
    }

    public struct BlitRenderer : IComponentData
    {
        public Entity texture;
        public float4 color; // linear
        public bool useExternalBlitES3;
    }
}
