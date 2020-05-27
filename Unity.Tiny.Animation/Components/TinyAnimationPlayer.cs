using Unity.Entities;

namespace Unity.Tiny.Animation
{
    public struct TinyAnimationPlayer : IComponentData
    {
        public Entity CurrentClip;
        public int CurrentIndex;
    }

    public struct TinyAnimationClipRef : IBufferElementData
    {
        public Entity Value;
    }
}
