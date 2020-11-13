# Events

Tiny.UI tries to balance an efficient, DOTS-oriented approach to event handling 
with a usable API. The essential functionality for the client is "get me
the event for a given UI element." On the other hand, there is certainly the
expectation of performance and reasonable memory usage.

## Stages

A note on sorting: Tiny.UI doesn't support overlapping siblings. Depth (both 
rendering and events) is supported for child UI elements, which is almost 
certainly what is intended. Sibling elements overlapping will get sorted in an 
unspecified order.

### 1st Stage: RayCast
The RayCaster System gathers up all the events available from the Input system: 
0-1 mouse events, and 0-N finger events. It does the necessary coordinate 
transforms, walks the RectTransformResults and (if there is a hit) chooses the 
element displayed on top. Hit is at the resolution of an entity; 
the RayCaster detects whether an Entity (or Null) is hit by an event.

All the input events (whether they hit a RectTransform or not) are then 
written to the `hitTestEvents` of the `ProcessUIEvents` System.

(Design aside: originally this was passed via a DynamicBuffer component in
special entity. While this seemed more DOTS-y, it was also harder to use
and less performant, so after discussion we switched back to writing to an
array.)

### 2nd Stage: ProcessUIEvents
`ProcessUIEvents` runs after the `RayCast` System. The input to ProcessUIEVents
is a NativeArray of hit test information. Additionally, ProcessUIEvents caches 
information about the previous hits stored in the `cachedTouches`.

The purpose of ProcessUIEvents is to compare the previous and current 
touch/mouse data and generated the correct UI state.

This is fiddly and complex code. We tried to simplify as much as possible, 
but it's an essentially complex bit of logic. Some events have a current and 
previous state, some just previous, some just current. It is even possible to
have in-frame touch click events. All that has to be reconciled to infer 
the current state of Highlights, Presses, and Clicks.

The good news is that ProcessUIEvents is well isolated by its inputs and 
outputs. We can - and do - test it in the unit tests, and add unit tests as 
we identify behaviors and bugs.

In the end, for every Entity with a Selectable component (which is a UI 
element that can be interacted with) ProcessUIEvents writes a UIState. 
The UIState is the simple set of flags for Highlight, Pressed, and Clicked.

### 3rd Stage: Using Events
Finally, a client program needs to use the events. On the one hand, this is as
simple as a ForEach through the UIStates and getting the results you need. 
There are typically very few UIResults so this should be fast.

The challenge is this: which Entity is associated with which UGUI element? 
For this we provide GetEntityByUIName() and GetUIStateByUIName()
which returns the Entity associated with the name of the UI in the editor. 
It will run through the available names and return the first match. The
somewhat cumbersome "UIName" moniker is used to remind developers that this is
the name specified in the editor UI; not an entity name or some other name.
