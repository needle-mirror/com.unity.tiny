using Unity.Entities;

namespace Unity.Tiny.Animation
{
    public struct UpdateAnimationTimeTag : IComponentData { }
    public struct ApplyAnimationResultTag : IComponentData { }
    public struct TinyAnimationTime : IComponentData
    {
        public float InternalWorkTime;
        public float Value;
    }

    public struct TinyAnimationPlaybackInfo : IComponentData
    {
        public float Duration;
        public float CycleOffset;
        public WrapMode WrapMode;
    }

    public enum WrapMode : byte
    {
        Once,
        ClampForever,
        Loop,
        PingPong,
    }
}
