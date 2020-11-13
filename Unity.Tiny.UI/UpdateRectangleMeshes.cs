using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Rendering;
using Unity.Transforms;
using UnityEngine.Assertions;

namespace Unity.Tiny.UI
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(SubmitSystemGroup))]
    public class UpdateRectangleMeshes : SystemBase
    {
        const int kMaxVertex = 16;
        const int kMaxIndex = 54;

        void AddMeshRendererComponents(Entity e, int nVertex, int nIndex, NativeArray<SimpleVertex> vertices)
        {
            MeshRenderer mr = EntityManager.GetComponentData<MeshRenderer>(e);
            mr.startIndex = 0;
            mr.indexCount = nIndex;
            EntityManager.SetComponentData(e, mr);

            DynamicMeshData dmd = EntityManager.GetComponentData<DynamicMeshData>(e);
            dmd.Dirty = true;
            dmd.NumIndices = nIndex;
            dmd.NumVertices = nVertex;

            MeshBounds mb = default;
            mb.Bounds = MeshHelper.ComputeBounds(vertices);

            EntityManager.SetComponentData<DynamicMeshData>(e, dmd);
            EntityManager.SetComponentData(e, mb);
        }

        public void CreateQuad(Entity e, float3 org, RectangleRenderState rectangleRenderState, RectTransformResult rRes)
        {
            var indices = EntityManager.GetBuffer<DynamicIndex>(e);
            var vertices = EntityManager.GetBuffer<DynamicSimpleVertex>(e);

            vertices.ResizeUninitialized(4);
            indices.ResizeUninitialized(6);

            float2 size = rRes.Size;
            float4 outer = rectangleRenderState.Outer;

            for (int j = 0; j < 2; ++j)
            {
                for (int i = 0; i < 2; ++i)
                {
                    float x = size.x * i;
                    float y = size.y * j;

                    SimpleVertex sv = new SimpleVertex
                    {
                        Position = new float3(x, y, org.z),
                        Color = new float4(1, 1, 1, 1),
                        TexCoord0 = new float2(i == 0 ? outer.x : outer.z, j == 0 ? outer.w : outer.y)
                    };
                    vertices[j * 2 + i] = new DynamicSimpleVertex()
                    {
                        Value = sv
                    };
                }
            }

            indices[0] = new DynamicIndex() {Value = 0};
            indices[1] = new DynamicIndex() {Value = 3};
            indices[2] = new DynamicIndex() {Value = 1};
            indices[3] = new DynamicIndex() {Value = 0};
            indices[4] = new DynamicIndex() {Value = 2};
            indices[5] = new DynamicIndex() {Value = 3};

            AddMeshRendererComponents(e, vertices.Length, indices.Length,
                vertices.AsNativeArray().Reinterpret<DynamicSimpleVertex, SimpleVertex>());
        }

        public void Create9Slice(Entity e, float3 org, RectangleRenderState rectangleRenderState, RectTransformResult rRes)
        {
            var indices = EntityManager.GetBuffer<DynamicIndex>(e);
            var vertices = EntityManager.GetBuffer<DynamicSimpleVertex>(e);

            vertices.ResizeUninitialized(kMaxVertex);
            indices.ResizeUninitialized(kMaxIndex);

            float2 size = rectangleRenderState.BaseSize;
            size = size / (rectangleRenderState.PixelsPerUnit * rectangleRenderState.PixelsPerUnitMultiplier);

            float4 border = rectangleRenderState.Border;
            float4 outer = rectangleRenderState.Outer;

            float2 split0 = new float2(border.x, border.y);
            float2 split1 = new float2(border.z, border.w);

            float2[] xy = new float2[4];
            xy[0] = new float2(0);
            xy[1] = size * split0;
            xy[2] = rRes.Size - split1 * size;
            xy[3] = rRes.Size;

            float2[] uv = new float2[4];
            uv[0] = outer.xy;
            uv[1] = outer.xy + (outer.zw - outer.xy) * split0;
            uv[2] = outer.zw - (outer.zw - outer.xy) * split1;
            uv[3] = outer.zw;

            for (int j = 0; j < 4; ++j)
            {
                for (int i = 0; i < 4; ++i)
                {
                    float x = org.x + xy[i].x;
                    float y = org.y + xy[j].y;

                    SimpleVertex sv = new SimpleVertex
                    {
                        Position = new float3(x, y, org.z),
                        Color = new float4(1, 1, 1, 1),
                        TexCoord0 = new float2(uv[i].x, 1.0f - uv[j].y)
                    };
                    vertices[j * 4 + i] = new DynamicSimpleVertex()
                    {
                        Value = sv
                    };
                }
            }

            for (int j = 0; j < 3; ++j)
            {
                for (int i = 0; i < 3; ++i)
                {
                    const int P0 = 0;
                    const int P1 = 1;
                    const int P2 = 5;
                    const int P3 = 4;

                    int iB = j * 18 + i * 6;
                    int vB = j * 4 + i;

                    indices[iB + 0] = new DynamicIndex() {Value = (ushort) (vB + P0)};
                    indices[iB + 1] = new DynamicIndex() {Value = (ushort) (vB + P2)};
                    indices[iB + 2] = new DynamicIndex() {Value = (ushort) (vB + P1)};
                    indices[iB + 3] = new DynamicIndex() {Value = (ushort) (vB + P3)};
                    indices[iB + 4] = new DynamicIndex() {Value = (ushort) (vB + P2)};
                    indices[iB + 5] = new DynamicIndex() {Value = (ushort) (vB + P0)};
                }
            }

            AddMeshRendererComponents(e, vertices.Length, indices.Length,
                vertices.AsNativeArray().Reinterpret<DynamicSimpleVertex, SimpleVertex>());
        }

        void CreateSimpleRenderer(Entity eMesh, quaternion rot, float3 pos, float3 scale)
        {
            EntityManager.AddComponentData(eMesh, new MeshRenderer // renderer -> maps to shader to use
            {
                material = eMesh,
                mesh = eMesh,
                startIndex = 0,
                indexCount = 0
            });

            EntityManager.AddComponentData(eMesh, new LocalToWorld
            {
                Value = float4x4.identity
            });
            EntityManager.AddComponentData(eMesh, new Translation
            {
                Value = pos
            });
            EntityManager.AddComponentData(eMesh, new Rotation
            {
                Value = rot
            });
            if (scale.x != scale.y || scale.y != scale.z)
            {
                EntityManager.AddComponentData(eMesh, new NonUniformScale
                {
                    Value = scale
                });
            }
            else if (scale.x != 1.0f)
            {
                EntityManager.AddComponentData(eMesh, new Scale
                {
                    Value = scale.x
                });
            }

            EntityManager.AddComponentData(eMesh, new WorldBounds());

            EntityManager.AddBuffer<DynamicSimpleVertex>(eMesh);
            EntityManager.AddBuffer<DynamicIndex>(eMesh);

            var iBuffer = EntityManager.GetBuffer<DynamicIndex>(eMesh);
            var vBuffer = EntityManager.GetBuffer<DynamicSimpleVertex>(eMesh);

            vBuffer.Capacity = kMaxVertex;
            vBuffer.ResizeUninitialized(kMaxVertex);
            iBuffer.Capacity = kMaxIndex;
            iBuffer.ResizeUninitialized(kMaxIndex);

            DynamicMeshData dmd = new DynamicMeshData
            {
                Dirty = true,
                IndexCapacity = iBuffer.Capacity,
                VertexCapacity = vBuffer.Capacity,
                NumIndices = iBuffer.Length,
                NumVertices = vBuffer.Length,
                UseDynamicGPUBuffer = false
            };
            EntityManager.AddComponentData<DynamicMeshData>(eMesh, dmd);
        }

        protected override unsafe void OnUpdate()
        {
            CompleteDependency();

            // ForEach RectangleRenderState, initialize with needed structures, and flag to have location computed below.
            // In conversion, we already added SimpleMaterial to this entity.
            Entities
                .WithNone<DynamicMeshData, MeshBounds>()
                .WithStructuralChanges()
                .ForEach((ref Entity entity, in RectangleRenderState rectangleRenderer) =>
                {
                    if (!EntityManager.HasComponent<SimpleMeshRenderer>(entity))
                        EntityManager.AddComponentData(entity, new SimpleMeshRenderer());

                    EntityManager.AddComponent<MeshBounds>(entity);

                    // for new things force the update
                    EntityManager.AddComponent<RectangleRendererNeedsUpdate>(entity);

                    float3 origin = float3.zero;
                    CreateSimpleRenderer(entity, quaternion.identity, origin, new float3(1));

                })
                .Run();

            // TODO restore color, and srgb color
            //var srgbColors = GetSingleton<DisplayInfo>().colorSpace == ColorSpace.Gamma;

            // Run through each RectangleRenderState, and re-generated (or generate) a mesh as needed.
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            Entities
                .WithAll<RectangleRendererNeedsUpdate>()
                .WithoutBurst()
                .WithStructuralChanges()
                .ForEach((Entity meshEntity, ref RectangleRenderState rRenderer, ref DynamicMeshData dmd, in RectTransformResult rRes) =>
                {
                    float3 origin = new float3(0);
                    switch (rRenderer.ImageRenderType)
                    {
                        case ImageRenderType.Sliced:
                        {
                            Create9Slice(meshEntity, origin, rRenderer, rRes);
                            break;
                        }

                        case ImageRenderType.Simple:
                        {
                            CreateQuad(meshEntity, origin, rRenderer, rRes);
                            break;
                        }

                        case ImageRenderType.Tiled:
                            throw new NotImplementedException();
                        case ImageRenderType.Filled:
                            throw new NotImplementedException();
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    dmd.Dirty = true;
                    ecb.RemoveComponent<RectangleRendererNeedsUpdate>(meshEntity);
                })
                .Run();
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
