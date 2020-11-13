using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unity.Tiny.UI
{
    /// <summary>
    /// Takes entities with a RectangleTransform and writes out a Translation, Rotation, and Scale for them.
    /// These values are relative to the RectCanvas.
    ///
    /// Inputs are RectangleTransform + RectParent, outputs are RectSize and RectPosition.  (FIXME horrible names)
    ///
    /// </summary>

    //[WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(TransformSystemGroup))]
    [UpdateAfter(typeof(EndFrameTRSToLocalToWorldSystem))]
    [UpdateBefore(typeof(EndFrameWorldToLocalSystem))]
    public class RectangleTransformSystem : SystemBase
    {
        protected EntityQuery m_MissingResultQueryCanvas;
        protected EntityQuery m_MissingResultQueryChild;

        private NativeList<int> depthSortMultiplier;

        protected override void OnCreate()
        {
            depthSortMultiplier = new NativeList<int>(0, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            depthSortMultiplier.Dispose();
        }

        protected override void OnStartRunning()
        {
            // TODO remove
            m_MissingResultQueryCanvas = GetEntityQuery(
                ComponentType.ReadOnly<RectCanvas>(),
                ComponentType.ReadOnly<RectTransform>(),
                ComponentType.Exclude<RectTransformResult>());

            // TODO remove
            m_MissingResultQueryChild = GetEntityQuery(
                ComponentType.ReadOnly<RectParent>(),
                ComponentType.ReadOnly<RectTransform>(),
                ComponentType.Exclude<RectTransformResult>());
        }

        protected override void OnUpdate()
        {
            // TODO remove
            EntityManager.AddComponent<RectTransformResult>(m_MissingResultQueryCanvas);
            // TODO remove
            EntityManager.AddComponent<RectTransformResult>(m_MissingResultQueryChild);

            var RectParentFromEntity = GetComponentDataFromEntity<RectParent>(isReadOnly: true);
            var RectTransformFromEntity = GetComponentDataFromEntity<RectTransform>(isReadOnly: true);
            var RectTransformResultFromEntity = GetComponentDataFromEntity<RectTransformResult>(isReadOnly: true);
            var LocalToWorldFromEntity = GetComponentDataFromEntity<LocalToWorld>(isReadOnly: true);

            NativeList<int> siblingMax = new NativeList<int>(16, Allocator.Temp);
            Entities
                .WithAll<RectParent>()
                .WithoutBurst()
                .ForEach((ref Entity entity, ref RectParent rp, in RectTransform rt) =>
                {
                    var depth = GetDepth(entity, RectParentFromEntity );
                    rp.Depth = depth;
                    if (siblingMax.Length <= depth)
                        siblingMax.Resize(depth + 1, NativeArrayOptions.ClearMemory);
                    siblingMax[depth] = math.max(siblingMax[depth], rt.SiblingIndex);
                }).Run();

            if (siblingMax.IsEmpty)
                return;    // no UI

            depthSortMultiplier.Resize(siblingMax.Length, NativeArrayOptions.UninitializedMemory);
            depthSortMultiplier[siblingMax.Length - 1] = 1;
            for (int i = siblingMax.Length - 2; i >= 0; --i)
            {
                depthSortMultiplier[i] = depthSortMultiplier[i + 1] * (siblingMax[i + 1] + 2);
            }
            // The root node depth range is never added, so the maximum depth is therefore the range of the root.
            int sortRange = depthSortMultiplier[0];

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Every non-RectCanvas item must have a RectParent.
            Entities
                .WithReadOnly(RectParentFromEntity)
                .WithReadOnly(RectTransformFromEntity)
                .WithoutBurst()

                //.WithChangeFilter<RectTransform, RectParent>()
                //.WithChangeFilter<RectTransform>()
                //.WithChangeFilter<RectParent>()     //TODO: Restore & fix change filters
                .ForEach((Entity e, ref RectTransformResult rtr, in RectTransform rxform, in RectParent rparent) =>
                {
                    if (rparent.Value != Entity.Null && !EntityManager.Exists(rparent.Value))
                    {
                        // this is only true for entities that have been deleted
                        throw new ArgumentException($"RectTransform attached to entity {e} must have parent.");
                    }

                    var rxFormCopy = rxform;
                    if (float.IsNaN(rxFormCopy.AnchoredPosition.x))
                    {
                        rxFormCopy.AnchoredPosition = new float2(0, 0);
                    }

                    // compute the size
                    var size = GetRectTransformSize(rxFormCopy, rparent.Value, RectTransformFromEntity,
                        RectParentFromEntity, out var parentSize);

                    // then compute the position
                    var position =
                        GetRectTransformPosition(rxFormCopy, rparent.Value, parentSize, RectTransformFromEntity);

                    // this is the position of the pivot
                    rtr.LocalPosition = position;

                    // Todo: this is a workaround of a bigger problem of not having change filters working properly.
                    if (!rtr.Size.Equals(size))
                    {
                        rtr.Size = size;
                        if (EntityManager.HasComponent<RectangleRenderState>(e))
                        {
                            ecb.AddComponent<RectangleRendererNeedsUpdate>(e);
                        }
                    }

                    // offset from the pivot position to the lower left of the rect
                    rtr.PivotOffset = -GetPivotFromCenterRealWorldUnits(rxFormCopy.Pivot, size);
                }).Run();

            // TODO -- handle Rotation and/or Scale for entities.
            var localDepthSortMultiplier = depthSortMultiplier;
            Entities
                .WithReadOnly(RectParentFromEntity)
                .WithReadOnly(LocalToWorldFromEntity)
                .WithReadOnly(RectTransformResultFromEntity)
                .WithReadOnly(localDepthSortMultiplier)
                .WithNativeDisableContainerSafetyRestriction(LocalToWorldFromEntity)
                .WithChangeFilter<RectTransformResult>()
                .WithoutBurst()
                .ForEach((ref Entity entity, ref LocalToWorld xform,
                    in RectTransform rXForm, in RectParent rParent, in RectTransformResult rtr) =>
                {
                    // TODO: depth is used is a sorting key, which means it needs to be normalized to the camera DoF
                    GetFinalPosAndDepth(entity, rtr.LocalPosition,
                        RectTransformResultFromEntity,
                        RectParentFromEntity,
                        LocalToWorldFromEntity,
                        out float2 finalPosition, out float4x4 localToWorld);
                    int sortOrder = GetSortOrder(entity, RectTransformFromEntity, RectParentFromEntity,
                        localDepthSortMultiplier);

                    float2 pos = finalPosition + rtr.PivotOffset;
                    xform.Value = math.mul(localToWorld, float4x4.Translate(new float3(pos, -(float)sortOrder / sortRange)));
                }).Run();

            Entities
                .WithNone<RectCanvas>()
                .WithAll<RectParent>()
                .WithReadOnly(RectTransformResultFromEntity)
                .ForEach((ref Entity e, ref RectTransformResult rtr, in RectTransform rt) =>
                {
                    bool hidden = rt.Hidden;
                    Entity parent = e;
                    while (parent != Entity.Null)
                    {
                        if (RectParentFromEntity.HasComponent(parent))
                        {
                            if (RectTransformFromEntity[parent].Hidden)
                            {
                                {
                                    hidden = true;
                                    break;
                                }
                            }

                            parent = RectParentFromEntity[parent].Value;
                        }
                        else
                        {
                            parent = Entity.Null;
                        }
                    }

                    rtr.HiddenResult = hidden;
                }).Run();

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        // TODO: don't need to return depth anymore, it is written to the RectParent
        public static int GetFinalPosAndDepth(Entity e,
            float2 localPosition,
            ComponentDataFromEntity<RectTransformResult> RectTransformResultFromEntity,
            ComponentDataFromEntity<RectParent> RectParentFromEntity,
            ComponentDataFromEntity<LocalToWorld> LocalToWorldFromEntity,
            out float2 finalPosition, out float4x4 localToWorld)
        {
            finalPosition = localPosition;
            Entity parent = RectParentFromEntity[e].Value;
            Entity pparent = parent;
            int depth = 0;
            while (parent != Entity.Null)
            {
                var prr = RectTransformResultFromEntity[parent];
                depth++;
                finalPosition += prr.LocalPosition;

                pparent = parent;
                if (RectParentFromEntity.HasComponent(parent))
                    parent = RectParentFromEntity[parent].Value;
                else
                    parent = Entity.Null;
            }
            localToWorld = LocalToWorldFromEntity[pparent].Value;
            return depth;
        }

        public static int GetSortOrder(Entity entity,
            ComponentDataFromEntity<RectTransform> RectTransformFromEntity,
            ComponentDataFromEntity<RectParent> RectParentFromEntity,
            NativeList<int> depthSortMultiplier)
        {
            int sortOrder = 0;
            Entity e = entity;

            while (e != Entity.Null)
            {
                if (!RectParentFromEntity.HasComponent(e))
                    break;

                int depth = RectParentFromEntity[e].Depth;
                sortOrder += ((RectTransformFromEntity[e].SiblingIndex + 1) * depthSortMultiplier[depth]);

                e = RectParentFromEntity[e].Value;
            }
            return sortOrder;
        }

        public static int GetDepth(Entity e, ComponentDataFromEntity<RectParent> RectParentFromEntity)
        {
            if (!RectParentFromEntity.HasComponent(e))
                return 0;

            int depth = 0;
            Entity parent = RectParentFromEntity[e].Value;
            while (parent != Entity.Null)
            {
                depth++;
                if (RectParentFromEntity.HasComponent(parent))
                    parent = RectParentFromEntity[parent].Value;
                else
                    parent = Entity.Null;
            }
            return depth;
        }

        static float2 GetRectTransformSize(in RectTransform xform, Entity parent,
            ComponentDataFromEntity<RectTransform> RectTransformFromEntity,
            ComponentDataFromEntity<RectParent> RectParentFromEntity, out float2 parentSize)
        {
            parentSize = float2.zero;
            if (parent != Entity.Null)
            {
                var parentXform = RectTransformFromEntity[parent];
                var parentParent = RectParentFromEntity.HasComponent(parent) ? RectParentFromEntity[parent] : default;
                parentSize = GetRectTransformSize(parentXform, parentParent.Value, RectTransformFromEntity,
                    RectParentFromEntity, out var _);
            }

            return parentSize * (xform.AnchorMax - xform.AnchorMin) + xform.SizeDelta;
        }

        static float2 GetPivotFromCenterRealWorldUnits(float2 pivot, float2 size)
        {
            return size * pivot;
        }

        float2 GetRectTransformPosition(in RectTransform xform, Entity parent, float2 parentSize,
            ComponentDataFromEntity<RectTransform> RectTransformFromEntity)
        {
            float2 parentPivot = new float2(0.5f);
            if (parent != Entity.Null)
            {
                var parentXform = RectTransformFromEntity[parent];
                parentPivot = parentXform.Pivot;
            }
            /* The derivation of this equation is not obvious.
             * `anchorRefNorm` is the delta from the parent pivot to the child pivot in normalized
             * coordinates. In the PARENT coordinate system:
             *
             * anchorRefNorm = [Cp] - Pp
             *     where: [Cp] child pivot transformed to parent space
             *            Pp parent pivot (normalized coordinates)
             * [Cp] = Amin + (Amax - Amin) * Cp
             *
             * anchorRefNorm = Amin + (Amax - Amin) * Cp - Pp
             * re-arranging leads to the formula below
             */
            var range = (xform.AnchorMax - xform.AnchorMin);
            var anchorRefNorm = (xform.AnchorMin - parentPivot) + range * xform.Pivot;

            var anchorRefPoint = anchorRefNorm * parentSize;
            return anchorRefPoint + xform.AnchoredPosition;
        }
    }
}
