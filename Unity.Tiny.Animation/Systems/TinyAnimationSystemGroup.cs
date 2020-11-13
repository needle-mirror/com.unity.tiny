using Unity.Entities;
using Unity.Transforms;

namespace Unity.Tiny.Animation
{
    /// <summary>
    /// All of the systems related to TinyAnimation are updated in this group.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public class TinyAnimationSystemGroup : ComponentSystemGroup {}
}
