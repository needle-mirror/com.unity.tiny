using Unity.Entities;

namespace Unity.Tiny.Animation
{
    struct TinyAnimationPlayer : IComponentData
    {
        public Entity currentClip;
        public int currentIndex; // Might be a waste of space to use an int (limit to 255 and store in byte?)
    }

    struct TinyAnimationClipRef : IBufferElementData
    {
        public Entity value;
    }
}
