using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Rendering;

namespace Unity.Tiny.Text
{
    /// <summary>
    /// Creates and updates meshes for all TextRendererString + TextRenderer components
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(SubmitSystemGroup))]
    public class UpdateTextMeshes : SystemBase
    {
        protected override unsafe void OnUpdate()
        {
            CompleteDependency();

            // If someone is just creating text, we'll take care of the material and mesh
            // TODO -- we really need an efficient way to one-step transform an entity into
            // the shape that we need
            Entities
                .WithAll<TextRendererString>()
                .WithNone<MeshRenderer, DynamicMeshData>()
                .WithNone<MeshBounds, DynamicSimpleVertex, DynamicIndex>()
                .WithStructuralChanges()
                .ForEach((ref Entity entity, in TextRenderer font) =>
                {
                    // will fill in startIndex/indexCount later
                    if (!EntityManager.HasComponent<MeshRenderer>(entity))
                        EntityManager.AddComponentData(entity, new MeshRenderer
                        {
                            mesh = entity,
                            material = font.FontMaterial,
                            startIndex = 0,
                            indexCount = 0
                        });

                    // may as well, these are likely not there
                    EntityManager.AddComponent<DynamicMeshData>(entity);
                    EntityManager.AddComponent<MeshBounds>(entity);
                    EntityManager.AddBuffer<DynamicSimpleVertex>(entity);
                    EntityManager.AddBuffer<DynamicIndex>(entity);

                    // for new things force the update
                    EntityManager.AddComponent<TextRendererNeedsUpdate>(entity);
                })
                .Run();

            var textMaterialFromEntity = GetComponentDataFromEntity<BitmapFontMaterial>();
            var textSDFMaterialFromEntity = GetComponentDataFromEntity<SDFFontMaterial>();
            var dsvFromEntity = GetBufferFromEntity<DynamicSimpleVertex>();
            var diFromEntity = GetBufferFromEntity<DynamicIndex>();

            var srgbColors = GetSingleton<DisplayInfo>().colorSpace == ColorSpace.Gamma;

            // we're going to do fine-grained change tracking, because mesh generation is costly since it'll
            // cause a re-upload to graphics
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            Entities
                .WithAll<TextRendererNeedsUpdate>()
                .ForEach((Entity entity, ref MeshRenderer meshRenderer,
                    ref TextRenderer fontRef,
                    ref DynamicBuffer<TextRendererString> text) =>
                    {
                        if (fontRef.FontMaterial == Entity.Null)
                            return;

                        //Console.WriteLine($"[{entity.Index}:{entity.Version}] match");

                        // TODO support static text meshes too.  For our initial impl the mesh is always dynamic
                        var meshEntity = meshRenderer.mesh;

                        // This meshEntity will often be the same as the renderer entity (e.g. if setup was done
                        // as above, next to TextRendererString component).  But it doesn't need to be; there could be
                        // a shared mesh.  In that case though, this code will end up modifying every text string
                        // that uses that mesh, which is probably not what's desired!
                        // We also don't really need a dozen meshes for the identical string/font.

                        /**All of this can be SIGNIFICANTLY OPTIMIZED!!!.*/
                        var vertexBuffer = dsvFromEntity[meshEntity];
                        var indexBuffer = diFromEntity[meshEntity];

                        var vertexColor = srgbColors ? Color.LinearToSRGB(fontRef.MeshColor.AsFloat4()) : fontRef.MeshColor.AsFloat4();

                        //string s = new String((char*)UnsafeUtility.AddressOf(ref text.ElementAt(0)), 0, text.Length);
                        //Console.WriteLine($"[{entity.Index}:{entity.Version}] Generating mesh for {s}");

                        AABB bounds;
                        BlobAssetReference<FontData> fontData;

                        if (textMaterialFromEntity.HasComponent(fontRef.FontMaterial))
                        {
                            var material = textMaterialFromEntity[fontRef.FontMaterial];
                            fontData = material.FontData;
                        }
                        else if (textSDFMaterialFromEntity.HasComponent(fontRef.FontMaterial))
                        {
                            var material = textSDFMaterialFromEntity[fontRef.FontMaterial];
                            fontData = material.FontData;
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }

                        TextLayout.LayoutString((char*) text.GetUnsafePtr(), text.Length,
                            fontRef.Size, fontRef.HorizontalAlignment, fontRef.VerticalAlignment,
                            vertexColor,
                            ref fontData.Value,
                            vertexBuffer, indexBuffer, out bounds);

                        meshRenderer.startIndex = 0;
                        meshRenderer.indexCount = indexBuffer.Length;

                        var dmd = new DynamicMeshData
                        {
                            Dirty = true,
                            IndexCapacity = indexBuffer.Capacity,
                            VertexCapacity = vertexBuffer.Capacity,
                            NumIndices = indexBuffer.Length,
                            NumVertices = vertexBuffer.Length,
                            UseDynamicGPUBuffer = true
                        };

                        ecb.SetComponent(meshEntity, dmd);
                        ecb.SetComponent(meshEntity, new MeshBounds { Bounds = bounds });

                        ecb.RemoveComponent<TextRendererNeedsUpdate>(entity);
                    })
                .Run();
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
