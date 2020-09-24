using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Tiny.Rendering;
using Unity.Transforms;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Tiny
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(UpdateCameraMatricesSystem))]
    internal class RenderNodeSystem : SystemBase
    {
        private Dictionary<Entity, NativeHashMap<Entity, VisibleNode>> m_CameraVisibleList;
        private EntityQuery m_CameraQuery;
        private EntityQuery m_RendererQuery;

        private struct RenderNodeComparer : IComparer<VisibleNode>
        {
            public int Compare(VisibleNode lhs, VisibleNode rhs)
            {
                if (lhs.LayerAndOrder != rhs.LayerAndOrder)
                    return lhs.LayerAndOrder < rhs.LayerAndOrder ? -1 : 1;

                return lhs.Key.Index - rhs.Key.Index;
            }
        }

        [BurstCompile]
        private struct SortRenderNodeJob : IJob
        {
            [NativeDisableContainerSafetyRestriction] public NativeHashMap<Entity, VisibleNode> RenderNodes;

            public void Execute()
            {
                var renderItemArray = RenderNodes.GetValueArray(Allocator.Temp);

                renderItemArray.Sort(new RenderNodeComparer());
                // now assign the sort order
                for(var i = 0; i < renderItemArray.Length; i++)
                {
                    var r = renderItemArray[i];
                    r.Depth = (uint)i;
                    // put it back???
                    RenderNodes[r.Key] = r;
                }
            }
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            m_CameraVisibleList = new Dictionary<Entity, NativeHashMap<Entity, VisibleNode>>();
            m_CameraQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new []
                {
                    ComponentType.ReadOnly<Camera>(),
                    ComponentType.ReadOnly<CameraMask>(),
                    ComponentType.ReadOnly<CameraSettings2D>(),
                    ComponentType.ReadOnly<CameraMatrices>(),
                }
            });

            m_RendererQuery = GetEntityQuery(new EntityQueryDesc()
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Renderer2D>(),
                    ComponentType.ReadOnly<LocalToWorld>()
                },
            });

            // Requiring DisplayInfo to only run the system in DotsRuntime
            RequireSingletonForUpdate<Unity.Tiny.DisplayInfo>();
        }

        protected override void OnDestroy()
        {
            foreach (var hashMap in m_CameraVisibleList.Values)
            {
                if (hashMap.IsCreated)
                    hashMap.Dispose();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsCulled(float3 center, float radius, ref CameraMatrices cameraMatrices)
        {
            ref var frustum = ref cameraMatrices.frustum;

            // Iterate all the frustum planes.
            for (var i = 0; i < frustum.PlanesCount; ++i)
            {
                // Fetch the plane.
                var plane = frustum.GetPlane(i);

                // Check if the sphere is outside the frustum plane.
                // If so then early-out as culled.
                var distance = math.dot(center, plane.xyz) + plane.w;
                if (distance < -radius)
                    return true;
            }

            // Not culled.
            return false;
        }

        protected override void OnUpdate()
        {
            ClearVisibleMaps();

            // here we cull and order in 1 move
            using (var cameraEntities = m_CameraQuery.ToEntityArray(Allocator.TempJob))
            using (var cameraMasks = m_CameraQuery.ToComponentDataArray<CameraMask>(Allocator.TempJob))
            using (var cameraSettings2D = m_CameraQuery.ToComponentDataArray<CameraSettings2D>(Allocator.TempJob))
            using (var cameraMatrices = m_CameraQuery.ToComponentDataArray<CameraMatrices>(Allocator.TempJob))
            using (var jobHandles = new NativeList<JobHandle>(cameraEntities.Length, Allocator.TempJob))
            {
                for (var i = 0; i < cameraEntities.Length; i++)
                {
                    var renderEntityCount =  m_RendererQuery.CalculateEntityCount();
                    var visibleList = AddVisibleMap(cameraEntities[i], renderEntityCount);

                    var renderNodes = visibleList.AsParallelWriter();
                    var matrices = cameraMatrices[i];
                    var settings2D = cameraSettings2D[i];
                    var cameraMask = cameraMasks[i];

                    var cullJobHandle = Entities
                        .WithName("CullRenderer2D")
                        .ForEach((
                            Entity e,
                            in Renderer2D renderer,
                            in LocalToWorld localToWorld) =>
                        {
                            var layerMask = 1 << renderer.RenderingLayer;
                            if ((cameraMask.mask & (ulong)layerMask) == 0)
                                return;

                            var center = localToWorld.Position;
                            var radius = math.cmax(renderer.Bounds.Extents);

                            // Early-out if the renderer needs culling.
                            if (IsCulled(center, radius, ref matrices))
                                return;

                            var sortingDistance = -math.dot(settings2D.customSortAxis, center);
                            renderNodes.TryAdd(e, new VisibleNode
                            {
                                Key = e,
                                LayerAndOrder = MergeLayerAndOrder(renderer.SortingLayer, renderer.OrderInLayer, sortingDistance),
                            });
                        }).ScheduleParallel(Dependency);

                    var sortJob = new SortRenderNodeJob
                    {
                        RenderNodes = visibleList
                    }.Schedule(cullJobHandle);

                    jobHandles.Add(sortJob);
                }

                Dependency = JobHandle.CombineDependencies(jobHandles.AsArray());
            }
        }

        private NativeHashMap<Entity, VisibleNode> AddVisibleMap(Entity camera, int size)
        {
            if (m_CameraVisibleList.ContainsKey(camera))
                throw new System.InvalidOperationException($"Ordered Visible Map for {camera.ToString()} already exist.");

            var visibleList = new NativeHashMap<Entity, VisibleNode>(size, Allocator.TempJob);
            m_CameraVisibleList[camera] = visibleList;

            return visibleList;
        }

        public NativeHashMap<Entity, VisibleNode> GetOrderedVisibleMap(Entity camera)
        {
            if (!m_CameraVisibleList.TryGetValue(camera, out var visibleList))
                throw new System.InvalidOperationException($"Ordered Visible Map for {camera.ToString()} does not exist.");

            return visibleList;
        }

        private void ClearVisibleMaps()
        {
            foreach (var hashMap in m_CameraVisibleList.Values)
                hashMap.Dispose();

            m_CameraVisibleList.Clear();
        }

        private static ulong MergeLayerAndOrder(short layer, short order, float z)
        {
            // Make sure negative values are capped as shorts
            var ulLayer = (ulong)(ushort)layer;
            var ulOrder = (ulong)(ushort)order;

            var unsignedZ = math.asuint(z);

            // Fix up twos complement for negative floats
            unsignedZ ^= (uint)((int)unsignedZ >> 31) >> 1;

            // Pack and fixup signed values for sort
            var packed = ulLayer << 48 | ulOrder << 32 | (ulong)unsignedZ;
            return packed ^ 0x8000_8000_80000000ul;
        }
    }
}
