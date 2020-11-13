using System;
using Unity.Tiny.Assertions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Input;
using Unity.Tiny.Rendering;
using Unity.Transforms;

namespace Unity.Tiny.UI
{
    struct HitTestEvent : IComponentData
    {
        public Entity e;
        public int Depth;

        public int IsFromMouse;
        public int InputDown;

        public Touch Touch;
        public float3 InputPos;
        public float2 UpperRight;
        public float2 LowLeft;
        public bool Down => InputDown > 0;
        public bool Up => InputDown == 0;
    }

    public class RayCaster : SystemBase
    {
        protected override void OnUpdate()
        {
            var inputSystem = World.GetExistingSystem<InputSystem>();
            int touchCount = inputSystem.TouchCount();
            bool mouseButtonDown = inputSystem.GetMouseButton(0);
            var hitTestEvents = World.GetExistingSystem<ProcessUIEvents>().hitTestEvents;
            hitTestEvents.Clear();

            int numHits = inputSystem.IsTouchSupported() ? touchCount : 1; // todo: could break on touchscreen pcs?
            if (numHits == 0)
                return;

            // get UI cam
            // TODO: totally broken if we aren't in overlay mode.
            Entity eCam = Entity.Null;
            Entities.WithAll<Camera>().ForEach((Entity e, UICamera cam) => { eCam = e; }).Run();
            if (eCam == Entity.Null) return;

            // populate hit location array based on either mouse location or location of each screen touch
            // populate UI hit test array with default values and hit source (mouse or touch)
            var inputLocs = new NativeList<float3>(Allocator.Temp);
            var screenToWorld = World.GetExistingSystem<ScreenToWorld>();

            if (inputSystem.IsMousePresent())
            {
                float2 rawInputPos = inputSystem.GetInputPosition();
                inputLocs.Add(screenToWorld.InputPosToWorldSpacePos(rawInputPos, 1, eCam));
                hitTestEvents.Add(new HitTestEvent
                {
                    Depth = int.MinValue,
                    e = Entity.Null,
                    InputDown = mouseButtonDown ? 1 : 0,
                    IsFromMouse = 1,
                    Touch = new Touch { fingerId = int.MaxValue }
                });
            }

            if (inputSystem.IsTouchSupported())
            {
                for (int i = 0; i < numHits; i++)
                {
                    Touch touch = inputSystem.GetTouch(i);
                    float2 rawInputPos = new float2(touch.x, touch.y);
                    inputLocs.Add(screenToWorld.InputPosToWorldSpacePos(rawInputPos, 1, eCam));
                    hitTestEvents.Add(new HitTestEvent
                    {
                        Touch = touch,
                        Depth = int.MinValue,
                        e = Entity.Null,
                        InputDown = 1,
                        IsFromMouse = 0
                    });
                }
            }

            // get component data arrays for depth traversal
            var RectParentFromEntity = GetComponentDataFromEntity<RectParent>(isReadOnly: true);
            var RectTransformFromEntity = GetComponentDataFromEntity<RectTransform>(isReadOnly: true);
            var RectTransformResultFromEntity = GetComponentDataFromEntity<RectTransformResult>(isReadOnly: true);
            var LocalToWorldFromEntity = GetComponentDataFromEntity<LocalToWorld>(isReadOnly: true);

            Assert.IsTrue(hitTestEvents.Length == inputLocs.Length);

            // populate hit info list for each touch / mouse click
            Entities
                .WithReadOnly(RectParentFromEntity)
                .WithReadOnly(RectTransformResultFromEntity)
                .ForEach((Entity e, in RectTransformResult rtr, in RectParent rParent, in Selectable selectable) =>
                {
                    if (selectable.IsInteractable && !rtr.HiddenResult)
                    {
                        for (int i = 0; i < hitTestEvents.Length; i++)
                        {
                            hitTestEvents[i] = HitTest(hitTestEvents[i], inputLocs[i], rtr,
                                RectTransformFromEntity,
                                RectTransformResultFromEntity,
                                RectParentFromEntity,
                                LocalToWorldFromEntity, e);
                        }
                    }
                }).Run();
        }

        static HitTestEvent HitTest(HitTestEvent primaryTestEvent, float3 inputPos,
            RectTransformResult rtr,
            ComponentDataFromEntity<RectTransform> RectTransformFromEntity,
            ComponentDataFromEntity<RectTransformResult> RectTransformResultFromEntity,
            ComponentDataFromEntity<RectParent> RectParentFromEntity,
            ComponentDataFromEntity<LocalToWorld> LocalToWorldFromEntity,
            Entity e)
        {
            int depth = RectangleTransformSystem.GetFinalPosAndDepth(e,
                rtr.LocalPosition,
                RectTransformResultFromEntity,
                RectParentFromEntity,
                LocalToWorldFromEntity,
                out float2 finalPosition, out float4x4 pl2w);

            // TODO This bit of math works because there is no scale/rotation; if that is supported then proper linear algebra is needed.
            float2 pos = finalPosition + rtr.PivotOffset;
            float4x4 localToWorld = math.mul(pl2w, float4x4.Translate(new float3(pos, 0)));
            float2 lowLeft = localToWorld[3].xy;
            float2 upperRight = localToWorld[3].xy + rtr.Size;

            if (inputPos.x >= lowLeft.x
                && inputPos.x <= upperRight.x
                && inputPos.y >= lowLeft.y
                && inputPos.y <= upperRight.y)
            {
                if (primaryTestEvent.e == Entity.Null || primaryTestEvent.Depth > depth)
                {
                    primaryTestEvent.e = e;
                    primaryTestEvent.Depth = depth;
                }
            }

            primaryTestEvent.LowLeft = lowLeft;
            primaryTestEvent.UpperRight = upperRight;
            primaryTestEvent.InputPos = inputPos;
            return primaryTestEvent;
        }
    }
}
