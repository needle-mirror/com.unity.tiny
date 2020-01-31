using Unity.Entities;
using Unity.Transforms;

namespace Unity.Tiny.Animation
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public class TinyAnimationSystemGroup : ComponentSystemGroup { }
}