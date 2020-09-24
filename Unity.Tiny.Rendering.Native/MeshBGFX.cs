using Unity.Entities;
using Unity.Tiny.Assertions;
using Bgfx;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

#if ENABLE_DOTSRUNTIME_PROFILER
using Unity.Development.Profiling;
#endif

namespace Unity.Tiny.Rendering
{
    internal struct MeshBGFX : ISystemStateComponentData
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValid()
        {
            return indexBufferHandle != 0xffff && vertexBufferHandle != 0xffff;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsDynamic()
        {
            return isDynamic;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValidFor(DynamicMeshData dmd)
        {
            if (!IsValid())
                return false;
            if (dmd.UseDynamicGPUBuffer != isDynamic)
                return false;
            if (dmd.IndexCapacity != maxIndexCount)
                return false;
            if (dmd.VertexCapacity != maxVertexCount)
                return false;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bgfx.DynamicIndexBufferHandle GetDynamicIndexBufferHandle()
        {
            Assert.IsTrue(isDynamic);
            return new bgfx.DynamicIndexBufferHandle { idx = indexBufferHandle };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bgfx.DynamicVertexBufferHandle GetDynamicVertexBufferHandle()
        {
            Assert.IsTrue(isDynamic);
            return new bgfx.DynamicVertexBufferHandle { idx = vertexBufferHandle };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bgfx.IndexBufferHandle GetIndexBufferHandle()
        {
            Assert.IsTrue(!isDynamic);
            return new bgfx.IndexBufferHandle { idx = indexBufferHandle };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bgfx.VertexBufferHandle GetVertexBufferHandle()
        {
            Assert.IsTrue(!isDynamic);
            return new bgfx.VertexBufferHandle { idx = vertexBufferHandle };
        }

        public unsafe void SetForSubmit(bgfx.Encoder* encoder, int startIndex, int actualIndexCount)
        {
            if (actualIndexCount < 0)
                actualIndexCount = indexCount;
            Assert.IsTrue(startIndex >= 0 && actualIndexCount + startIndex <= indexCount);
            if (isDynamic)
            {
                bgfx.encoder_set_dynamic_index_buffer(encoder, GetDynamicIndexBufferHandle(), (uint)startIndex, (uint)actualIndexCount);
                bgfx.encoder_set_dynamic_vertex_buffer(encoder, 0, GetDynamicVertexBufferHandle(), 0, (uint)vertexCount, vertexLayoutHandle);
            }
            else
            {
                bgfx.encoder_set_index_buffer(encoder, GetIndexBufferHandle(), (uint)startIndex, (uint)actualIndexCount);
                bgfx.encoder_set_vertex_buffer(encoder, 0, GetVertexBufferHandle(), 0, (uint)vertexCount, vertexLayoutHandle);
            }
        }

        public int DebugIndex()
        {
            return (int)indexBufferHandle + ((int)vertexBufferHandle) << 16;
        }

        public static MeshBGFX CreateEmpty()
        {
#if ENABLE_DOTSRUNTIME_PROFILER
            ProfilerStats.AccumStats.memMeshCount.Accumulate(1);
#endif

            return new MeshBGFX
            {
                indexBufferHandle = 0xffff,
                vertexBufferHandle = 0xffff,
                indexCount = 0,
                vertexCount = 0,
                maxIndexCount = 0,
                maxVertexCount = 0,
                isDynamic = false,
                vertexSize = 0,
            };
        }

        public void Destroy()
        {
#if ENABLE_DOTSRUNTIME_PROFILER
            ProfilerStats.AccumStats.memMeshCount.Accumulate(-1);
            long bytesReserved = maxVertexCount * vertexSize + maxIndexCount * sizeof(ushort);
            ProfilerStats.AccumStats.memMesh.Accumulate(-bytesReserved);
            ProfilerStats.AccumStats.memReservedGFX.Accumulate(-bytesReserved);
            long bytesUsed = vertexCount * vertexSize + indexCount * sizeof(ushort);
            ProfilerStats.AccumStats.memUsedGFX.Accumulate(-bytesUsed);
#endif

            if (!isDynamic)
            {
                if (indexBufferHandle != 0xffff)
                    bgfx.destroy_index_buffer(GetIndexBufferHandle());
                if (vertexBufferHandle != 0xffff)
                    bgfx.destroy_vertex_buffer(GetVertexBufferHandle());
            }
            else
            {
                if (indexBufferHandle != 0xffff)
                    bgfx.destroy_dynamic_index_buffer(GetDynamicIndexBufferHandle());
                if (vertexBufferHandle != 0xffff)
                    bgfx.destroy_dynamic_vertex_buffer(GetDynamicVertexBufferHandle());
            }

            this = CreateEmpty();
        }

        public static unsafe MeshBGFX CreateDynamicMeshLit(RendererBGFXInstance* inst, int maxVertices, int maxIndices, bool hasSkinningData)
        {
            Assert.IsTrue(maxVertices <= 0x10000 && maxVertices > 0 && maxIndices > 0 && maxIndices <= 0xf0000);
#if ENABLE_DOTSRUNTIME_PROFILER
            ProfilerStats.AccumStats.memMeshCount.Accumulate(1);
            long bytes = maxVertices * sizeof(LitVertex) + maxIndices * sizeof(ushort);
            if (hasSkinningData)
                bytes += maxVertices * sizeof(SkinnedMeshVertex);
            ProfilerStats.AccumStats.memMesh.Accumulate(bytes);
            ProfilerStats.AccumStats.memReservedGFX.Accumulate(bytes);
#endif
            if (hasSkinningData)
            {
                return new MeshBGFX
                {
                    maxVertexCount = maxVertices,
                    maxIndexCount = maxIndices,
                    vertexCount = 0,
                    indexCount = 0,
                    vertexBufferHandle = bgfx.create_dynamic_vertex_buffer((uint)maxVertices, &inst->m_litSkinnedVertexBufferDecl, (ushort)bgfx.BufferFlags.None).idx,
                    indexBufferHandle = bgfx.create_dynamic_index_buffer((uint)maxIndices, (ushort)bgfx.BufferFlags.None).idx,
                    vertexLayoutHandle = inst->m_litSkinnedVertexBufferDeclHandle,
                    isDynamic = true,
                    vertexSize = sizeof(LitVertex) + sizeof(SkinnedMeshVertex),
                };
            }
            else
            {
                return new MeshBGFX
                {
                    maxVertexCount = maxVertices,
                    maxIndexCount = maxIndices,
                    vertexCount = 0,
                    indexCount = 0,
                    vertexBufferHandle = bgfx.create_dynamic_vertex_buffer((uint)maxVertices, &inst->m_litVertexBufferDecl, (ushort)bgfx.BufferFlags.None).idx,
                    indexBufferHandle = bgfx.create_dynamic_index_buffer((uint)maxIndices, (ushort)bgfx.BufferFlags.None).idx,
                    vertexLayoutHandle = inst->m_litVertexBufferDeclHandle,
                    isDynamic = true,
                    vertexSize = sizeof(LitVertex),
                };
            }
        }

        public static unsafe MeshBGFX CreateDynamicMeshSimple(RendererBGFXInstance* inst, int maxVertices, int maxIndices, bool hasSkinningData)
        {
            Assert.IsTrue(maxVertices <= 0x10000 && maxVertices > 0 && maxIndices > 0 && maxIndices <= 0xf0000);
#if ENABLE_DOTSRUNTIME_PROFILER
            ProfilerStats.AccumStats.memMeshCount.Accumulate(1);
            long bytes = maxVertices * sizeof(SimpleVertex) + maxIndices * sizeof(ushort);
            if (hasSkinningData)
                bytes += maxVertices * sizeof(SkinnedMeshVertex);
            ProfilerStats.AccumStats.memMesh.Accumulate(bytes);
            ProfilerStats.AccumStats.memReservedGFX.Accumulate(bytes);
#endif
            if (hasSkinningData)
            {
                return new MeshBGFX
                {
                    maxVertexCount = maxVertices,
                    maxIndexCount = maxIndices,
                    vertexCount = 0,
                    indexCount = 0,
                    vertexBufferHandle = bgfx.create_dynamic_vertex_buffer((uint)maxVertices, &inst->m_simpleSkinnedVertexBufferDecl, (ushort)bgfx.BufferFlags.None).idx,
                    indexBufferHandle = bgfx.create_dynamic_index_buffer((uint)maxIndices, (ushort)bgfx.BufferFlags.None).idx,
                    vertexLayoutHandle = inst->m_simpleSkinnedVertexBufferDeclHandle,
                    isDynamic = true,
                    vertexSize = sizeof(SimpleVertex) + sizeof(SkinnedMeshVertex),
                };
            }
            else
            {
                return new MeshBGFX
                {
                    maxVertexCount = maxVertices,
                    maxIndexCount = maxIndices,
                    vertexCount = 0,
                    indexCount = 0,
                    vertexBufferHandle = bgfx.create_dynamic_vertex_buffer((uint)maxVertices, &inst->m_simpleVertexBufferDecl, (ushort)bgfx.BufferFlags.None).idx,
                    indexBufferHandle = bgfx.create_dynamic_index_buffer((uint)maxIndices, (ushort)bgfx.BufferFlags.None).idx,
                    vertexLayoutHandle = inst->m_simpleVertexBufferDeclHandle,
                    isDynamic = true,
                    vertexSize = sizeof(SimpleVertex),
                };
            }
        }

        public static unsafe MeshBGFX CreateStaticMesh(RendererBGFXInstance* inst, ushort* indices, int nindices, SimpleVertex* vertices, int nvertices, SkinnedMeshVertex* skinningdata = null)
        {
            bool hasSkinningData = skinningdata != null;
#if ENABLE_DOTSRUNTIME_PROFILER
            ProfilerStats.AccumStats.memMeshCount.Accumulate(1);
            long bytes = nvertices * sizeof(SimpleVertex) + nindices * sizeof(ushort);
            if (hasSkinningData)
                bytes += nvertices * sizeof(SkinnedMeshVertex);
            ProfilerStats.AccumStats.memMesh.Accumulate(bytes);
            ProfilerStats.AccumStats.memReservedGFX.Accumulate(bytes);
            ProfilerStats.AccumStats.memUsedGFX.Accumulate(bytes);
#endif
            if (hasSkinningData)
            {
                int simpleVertexSize = sizeof(SimpleVertex);
                int skinningVertexSize = sizeof(SkinnedMeshVertex);
                int totalVertexSize = simpleVertexSize + skinningVertexSize;
                byte* tmpBlock = (byte*)UnsafeUtility.Malloc(totalVertexSize * nvertices, 4, Allocator.Temp);
                UnsafeUtility.MemCpyStride(tmpBlock, totalVertexSize, vertices, simpleVertexSize, simpleVertexSize, nvertices);
                UnsafeUtility.MemCpyStride(tmpBlock + simpleVertexSize, totalVertexSize, skinningdata, skinningVertexSize, skinningVertexSize, nvertices);
                bgfx.Memory* bgfxMemory = RendererBGFXStatic.CreateMemoryBlock((byte*) tmpBlock, nvertices * totalVertexSize);
                UnsafeUtility.Free(tmpBlock, Allocator.Temp);

                return new MeshBGFX
                {
                    indexBufferHandle = bgfx.create_index_buffer(RendererBGFXStatic.CreateMemoryBlock((byte*)indices, nindices * 2), (ushort)bgfx.BufferFlags.None).idx,
                    vertexBufferHandle = bgfx.create_vertex_buffer(bgfxMemory, &inst->m_simpleSkinnedVertexBufferDecl, (ushort)bgfx.BufferFlags.None).idx,
                    indexCount = nindices,
                    vertexCount = nvertices,
                    maxIndexCount = nindices,
                    maxVertexCount = nvertices,
                    vertexLayoutHandle = inst->m_simpleSkinnedVertexBufferDeclHandle,
                    isDynamic = false,
                    vertexSize = totalVertexSize,
                };
            }
            else
            {
                return new MeshBGFX
                {
                    indexBufferHandle = bgfx.create_index_buffer(RendererBGFXStatic.CreateMemoryBlock((byte*)indices, nindices * 2), (ushort)bgfx.BufferFlags.None).idx,
                    vertexBufferHandle = bgfx.create_vertex_buffer(RendererBGFXStatic.CreateMemoryBlock((byte*)vertices, nvertices * sizeof(SimpleVertex)), &inst->m_simpleVertexBufferDecl, (ushort)bgfx.BufferFlags.None).idx,
                    indexCount = nindices,
                    vertexCount = nvertices,
                    maxIndexCount = nindices,
                    maxVertexCount = nvertices,
                    vertexLayoutHandle = inst->m_simpleVertexBufferDeclHandle,
                    isDynamic = false,
                    vertexSize = sizeof(SimpleVertex),
                };
            }
        }

        public static unsafe MeshBGFX CreateStaticMeshFromBlobAsset(RendererBGFXInstance* inst, SimpleMeshRenderData meshData)
        {
            ushort* indices = (ushort*)meshData.Mesh.Value.Indices.GetUnsafePtr();
            SimpleVertex* vertices = (SimpleVertex*)meshData.Mesh.Value.Vertices.GetUnsafePtr();
            int nindices = meshData.Mesh.Value.Indices.Length;
            int nvertices = meshData.Mesh.Value.Vertices.Length;
            return CreateStaticMesh(inst, indices, nindices, vertices, nvertices);
        }

        public static unsafe MeshBGFX CreateStaticSkinnedMeshFromBlobAsset(RendererBGFXInstance* inst,
            SimpleMeshRenderData meshData, SkinnedMeshRenderData skinnedData)
        {
			ushort* indices = (ushort*)meshData.Mesh.Value.Indices.GetUnsafePtr();
            SimpleVertex* vertices = (SimpleVertex*)meshData.Mesh.Value.Vertices.GetUnsafePtr();
			SkinnedMeshVertex* skinningdata = (SkinnedMeshVertex*)skinnedData.SkinnedMeshDataRef.Value.Vertices.GetUnsafePtr();
            int nindices = meshData.Mesh.Value.Indices.Length;
            int nvertices = meshData.Mesh.Value.Vertices.Length;
            return CreateStaticMesh(inst, indices, nindices, vertices, nvertices, skinningdata);
        }

        public static unsafe MeshBGFX CreateStaticMesh(RendererBGFXInstance* inst, ushort* indices, int nindices, LitVertex* vertices, int nvertices, SkinnedMeshVertex* skinningdata = null)
        {
            Assert.IsTrue(nindices > 0 && nvertices > 0 && nvertices <= ushort.MaxValue);
            bool hasSkinningData = skinningdata != null;
#if ENABLE_DOTSRUNTIME_PROFILER
            ProfilerStats.AccumStats.memMeshCount.Accumulate(1);
            long bytes = nvertices * sizeof(LitVertex) + nindices * sizeof(ushort);
            if (hasSkinningData)
                bytes += nvertices * sizeof(SkinnedMeshVertex);
            ProfilerStats.AccumStats.memMesh.Accumulate(bytes);
            ProfilerStats.AccumStats.memReservedGFX.Accumulate(bytes);
            ProfilerStats.AccumStats.memUsedGFX.Accumulate(bytes);
#endif
            if (hasSkinningData)
            {
                int litVertexSize = sizeof(LitVertex);
                int skinningVertexSize = sizeof(SkinnedMeshVertex);
                int totalVertexSize = litVertexSize + skinningVertexSize;
                byte* tmpBlock = (byte*)UnsafeUtility.Malloc(totalVertexSize * nvertices, 4, Allocator.Temp);
                UnsafeUtility.MemCpyStride(tmpBlock, totalVertexSize, vertices, litVertexSize, litVertexSize, nvertices);
                UnsafeUtility.MemCpyStride(tmpBlock + litVertexSize, totalVertexSize, skinningdata, skinningVertexSize, skinningVertexSize, nvertices);
                bgfx.Memory* bgfxMemory = RendererBGFXStatic.CreateMemoryBlock((byte*) tmpBlock, nvertices * totalVertexSize);
                UnsafeUtility.Free(tmpBlock, Allocator.Temp);

                return new MeshBGFX
                {
                    indexBufferHandle = bgfx.create_index_buffer(RendererBGFXStatic.CreateMemoryBlock((byte*)indices, nindices * 2), (ushort)bgfx.BufferFlags.None).idx,
                    vertexBufferHandle = bgfx.create_vertex_buffer(bgfxMemory, &inst->m_litSkinnedVertexBufferDecl, (ushort)bgfx.BufferFlags.None).idx,
                    indexCount = nindices,
                    vertexCount = nvertices,
                    maxIndexCount = nindices,
                    maxVertexCount = nvertices,
                    vertexLayoutHandle = inst->m_litSkinnedVertexBufferDeclHandle,
                    isDynamic = false,
                    vertexSize = totalVertexSize,
                };
            }
            else
            {
                return new MeshBGFX
                {
                    indexCount = nindices,
                    vertexCount = nvertices,
                    maxIndexCount = nindices,
                    maxVertexCount = nvertices,
                    indexBufferHandle =
                        bgfx.create_index_buffer(RendererBGFXStatic.CreateMemoryBlock((byte*) indices, nindices * 2),
                            (ushort) bgfx.BufferFlags.None).idx,
                    vertexLayoutHandle = inst->m_litVertexBufferDeclHandle,
                    vertexBufferHandle =
                        bgfx.create_vertex_buffer(
                            RendererBGFXStatic.CreateMemoryBlock((byte*) vertices, nvertices * sizeof(LitVertex)),
                            &inst->m_litVertexBufferDecl, (ushort) bgfx.BufferFlags.None).idx,
                    isDynamic = false,
                    vertexSize = sizeof(LitVertex),
                };
            }
        }

        public static unsafe MeshBGFX CreateStaticMeshFromBlobAsset(RendererBGFXInstance* inst, LitMeshRenderData mesh)
        {
            ushort* indices = (ushort*)mesh.Mesh.Value.Indices.GetUnsafePtr();
            int nindices = mesh.Mesh.Value.Indices.Length;
            LitVertex* vertices = (LitVertex*)mesh.Mesh.Value.Vertices.GetUnsafePtr();
            int nvertices = mesh.Mesh.Value.Vertices.Length;
            return CreateStaticMesh(inst, indices, nindices, vertices, nvertices);
        }

        public static unsafe MeshBGFX CreateStaticSkinnedMeshFromBlobAsset(RendererBGFXInstance* inst,
            LitMeshRenderData meshData, SkinnedMeshRenderData skinnedData)
        {
			ushort* indices = (ushort*)meshData.Mesh.Value.Indices.GetUnsafePtr();
            int nindices = meshData.Mesh.Value.Indices.Length;
            LitVertex* vertices = (LitVertex*)meshData.Mesh.Value.Vertices.GetUnsafePtr();
            int nvertices = meshData.Mesh.Value.Vertices.Length;
			SkinnedMeshVertex* skinningdata = (SkinnedMeshVertex*)skinnedData.SkinnedMeshDataRef.Value.Vertices.GetUnsafePtr();
            return CreateStaticMesh(inst, indices, nindices, vertices, nvertices, skinningdata);
        }

        public unsafe void UpdateDynamic(ushort* indexSrc, int numIndices, byte* vertexSrc, int numVertices, int sizeofVertex)
        {
            Assert.IsTrue(isDynamic);
            Assert.IsTrue(numVertices <= maxVertexCount);
            Assert.IsTrue(numIndices <= maxIndexCount);
#if ENABLE_DOTSRUNTIME_PROFILER
            Assert.IsTrue(vertexSize == sizeofVertex);
            ProfilerStats.AccumStats.memUsedGFX.Accumulate(-(vertexCount * vertexSize + indexCount * sizeof(ushort)));
            ProfilerStats.AccumStats.memUsedGFX.Accumulate(numVertices * vertexSize + numIndices * sizeof(ushort));
#endif
            bgfx.update_dynamic_index_buffer(GetDynamicIndexBufferHandle(), 0, RendererBGFXStatic.CreateMemoryBlock((byte*)indexSrc, numIndices * 2));
            indexCount = numIndices;
            bgfx.update_dynamic_vertex_buffer(GetDynamicVertexBufferHandle(), 0, RendererBGFXStatic.CreateMemoryBlock(vertexSrc, numVertices * sizeofVertex));
            vertexCount = numVertices;
        }

        public int IndexCapacity => maxIndexCount;
        public int VertexCapacity => maxVertexCount;

        public int IndexCount => indexCount;
        public int VertexCount => vertexCount;

        private ushort indexBufferHandle;
        private ushort vertexBufferHandle;
        private int indexCount;
        private int maxIndexCount;
        private int vertexCount;
        private int maxVertexCount;
        private bgfx.VertexLayoutHandle vertexLayoutHandle;
        private bool isDynamic;
        private int vertexSize;  // For tracking memory usage
    }
}
