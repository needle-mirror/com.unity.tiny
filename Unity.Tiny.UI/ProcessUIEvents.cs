using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Tiny.Input;
using UnityEngine.Assertions;

namespace Unity.Tiny.UI
{
    struct TouchInfo
    {
        internal Touch touch;
        internal Entity e;
        internal bool inputDown;
        internal bool eligibleForClickOrPressState;
    }

    [UpdateAfter(typeof(ClearUIState))]
    [UpdateAfter(typeof(RayCaster))]
    public class ProcessUIEvents : SystemBase
    {
        NativeList<TouchInfo> cachedTouches;
        internal NativeList<HitTestEvent> hitTestEvents;

        protected override void OnCreate()
        {
            base.OnCreate();
            cachedTouches = new NativeList<TouchInfo>(10, Allocator.Persistent);
            hitTestEvents = new NativeList<HitTestEvent>(10, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            cachedTouches.Dispose();
            hitTestEvents.Dispose();
        }

        public Entity GetEntityByUIName(string name)
        {
            FixedString64 key = new FixedString64(name);
            Entity result = Entity.Null;

            var query = EntityManager.CreateEntityQuery(typeof(UIName));
            using (var entities = query.ToEntityArray(Allocator.Temp))
            {
                foreach (var e in entities)
                {
                    UIName uiName = EntityManager.GetComponentData<UIName>(e);
                    if (uiName.Name == key)
                    {
                        result = e;
                        break;
                    }
                }
            }

            query.Dispose();
            return result;
        }

        public UIState GetUIStateByUIName(string name)
        {
            var uiEntity = GetEntityByUIName(name);

            if (uiEntity == Entity.Null)
            {
               throw new ArgumentException($"Entity associated with UI element of name '{name}' could not be found.");
            }

            Assert.IsTrue(EntityManager.HasComponent<UIState>(uiEntity));
            return EntityManager.GetComponentData<UIState>(uiEntity);
        }

        void ClearOutdatedTouchCache()
        {
            // clean up the cache of hits that aren't in the current hitlist; already handled
            int cleanUpIndex = 0;
            while (cleanUpIndex < cachedTouches.Length)
            {
                if (!HitsContainsFingerId(cachedTouches[cleanUpIndex].touch.fingerId, out _))
                {
                    cachedTouches.RemoveAt(cleanUpIndex);
                }
                else
                {
                    cleanUpIndex++;
                }
            }
        }

        // State                                Color (editor UI)
        // -----                                -----------------
        // None (no hover / press / etc)        Normal
        // Hover (mouse over & up)              Highlighted color
        // Down (mouse over & down)             Pressed color      NOTE: Down->Up is a possible click event
        // (Selected - not currently supported, needs focus tracking - selected color)
        // Disabled (by Button/Toggle comp)     Disabled
        protected override void OnUpdate()
        {
            ClearOutdatedTouchCache();

            // clear the UIState (so old events don't persist...)
            Entities.ForEach((ref UIState res) => { res = default; }).Run();

            // process new and ongoing hits
            for (int i = 0; i < hitTestEvents.Length; i++)
            {
                Entity hover = default;
                Entity down = default;
                Entity clicked = default;

                var hitInfo = hitTestEvents[i];
                bool isNewTouch = !CacheContainsFingerId(hitInfo.Touch.fingerId, out int cacheIndex);

                if (hitInfo.IsFromMouse == 0)
                {
                    ProcessFingerPress(hitInfo, isNewTouch, cacheIndex, out down, out clicked);
                }
                else
                {
                    ProcessMouseInput(hitInfo, cacheIndex, out down, out clicked, out hover);
                }

                // Update state for selectable
                if (EntityManager.HasComponent<Selectable>(hitInfo.e))
                {
                    UIState state = EntityManager.GetComponentData<UIState>(hitInfo.e);
                    if (hover == hitInfo.e)
                    {
                        state.highlighted = 1;
                    }
                    if (down == hitInfo.e)
                    {
                        state.pressed = 1;
                    }
                    if (clicked == hitInfo.e)
                    {
                        state.clicked = 1;
                    }
                    EntityManager.SetComponentData(hitInfo.e, state);
                }

                if (EntityManager.HasComponent<Toggleable>(hitInfo.e) && clicked == hitInfo.e)
                {
                    var toggle = EntityManager.GetComponentData<Toggleable>(hitInfo.e);
                    toggle.IsToggled = !toggle.IsToggled;
                    EntityManager.SetComponentData(hitInfo.e, toggle);
                }
            }
        }

        void ProcessFirstMouse(HitTestEvent hitInfo, out Entity down)
        {
            cachedTouches.Add(new TouchInfo
            {
                touch = hitInfo.Touch,
                e = hitInfo.e,
                eligibleForClickOrPressState = true,
                inputDown = (hitInfo.InputDown > 0)
            });

            down = hitInfo.Down ? hitInfo.e : Entity.Null;
        }

        void ProcessMouseInput(HitTestEvent hitInfo, int cacheIndex,
            out Entity down, out Entity clicked, out Entity hover)
        {
            down = Entity.Null;
            clicked = Entity.Null;
            hover = Entity.Null;

            if (hitInfo.Up)
            {
                hover = hitInfo.e;
            }

            if (cacheIndex < 0)
            {
                ProcessFirstMouse(hitInfo, out down);
                return;
            }

            var cachedInfo = cachedTouches[cacheIndex];
            if (cachedInfo.eligibleForClickOrPressState)
            {
                if (hitInfo.Up)
                {
                    if (hitInfo.e == cachedInfo.e && cachedInfo.inputDown)
                    {
                        clicked = hitInfo.e;
                    }
                }
                else if (hitInfo.Down)
                {
                    if (hitInfo.e != cachedInfo.e && cachedInfo.inputDown)
                    {
                        cachedInfo.eligibleForClickOrPressState = false;
                        cachedTouches[cacheIndex] = cachedInfo;
                        return;
                    }

                    cachedInfo.e = hitInfo.e;
                    down = hitInfo.e;
                }
            }

            if (hitInfo.Up)
            {
                cachedInfo.eligibleForClickOrPressState = true;
            }

            cachedInfo.inputDown = (hitInfo.InputDown > 0);
            cachedTouches[cacheIndex] = cachedInfo;
        }

        void ProcessFingerPress(HitTestEvent hitInfo, bool isNewTouch, int cacheIndex,
            out Entity down, out Entity clicked)
        {
            down = Entity.Null;
            clicked = Entity.Null;

            if (isNewTouch)
            {
                // account for touch events that are within a single frame
                if (hitInfo.Touch.phase == TouchState.Ended)
                {
                    clicked = hitInfo.e;
                }
                else
                {
                    // ok to mutate array, since we are adding to the end.
                    cachedTouches.Add(new TouchInfo
                    {
                        touch = hitInfo.Touch,
                        e = hitInfo.e,
                        eligibleForClickOrPressState = true,
                        inputDown = true
                    });

                    down = hitInfo.e;
                }
            }
            else
            {
                var cachedTouchInfo = cachedTouches[cacheIndex];
                if (cachedTouchInfo.eligibleForClickOrPressState)
                {
                    // validate that finger hasn't left box
                    // if it has, current touch is no longer eligible for triggering clicked / pressed state
                    if (hitInfo.e != cachedTouchInfo.e)
                    {
                        cachedTouchInfo.eligibleForClickOrPressState = false;
                        cachedTouches[cacheIndex] = cachedTouchInfo;
                        return;
                    }

                    // haven't left original box, continue pressed state or trigger a click
                    if (hitInfo.Touch.phase == TouchState.Ended)
                    {
                        clicked = hitInfo.e;
                    }
                    else
                    {
                        down = hitInfo.e;
                    }
                }
            }
        }

        bool HitsContainsFingerId(int fingerId, out int index)
        {
            for (int i = 0; i < hitTestEvents.Length; i++)
            {
                if (hitTestEvents[i].Touch.fingerId == fingerId)
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        bool CacheContainsFingerId(int fingerId, out int index)
        {
            for (int i = 0; i < cachedTouches.Length; i++)
            {
                if (cachedTouches[i].touch.fingerId == fingerId)
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

       /**
        * Checks to see the if the entity has been pressed and the finger or mouse that clicked it is still
        * down. For example, if a user presses a button then drags their finger out of the button transform,
        * this will remain true for the button entity until the finger or mouse is released.
        */
        internal bool IsPressOnSelectedEntityActive(Entity e, out HitTestEvent outEvent)
        {
            foreach (var cachedEvent in cachedTouches)
            {
                if (e == cachedEvent.e && HitsContainsFingerId(cachedEvent.touch.fingerId, out var index))
                {
                    outEvent = hitTestEvents[index];
                    return true;
                }
            }

            outEvent = default;
            return false;
        }
    }
}
