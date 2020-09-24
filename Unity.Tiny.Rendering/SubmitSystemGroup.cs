using System;
using Unity.Entities;

namespace Unity.Tiny.Rendering
{
    /// <summary>
    /// Component system group during which all rendering command queue building and submission happens.
    /// Updates inside the Presentation System Group.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(UpdateWorldBoundsSystem))]
    public class SubmitSystemGroup : ComponentSystemGroup
    {
    }
}
