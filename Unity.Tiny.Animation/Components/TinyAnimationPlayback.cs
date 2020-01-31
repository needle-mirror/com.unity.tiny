using Unity.Entities;

namespace Unity.Tiny.Animation
{
    struct UpdateAnimationTimeTag : IComponentData { }
    struct ApplyAnimationResultTag : IComponentData { }
    struct TinyAnimationPlayback : IComponentData
    {
        public float time;
        public float duration;
    }
}
