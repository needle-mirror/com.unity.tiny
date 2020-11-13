using Unity.Entities;

namespace Unity.Tiny.Animation
{
    /// <summary>
    /// This tag component indicates that the <see cref="TinyAnimationTime"/> component should have
    /// its value updated every frame.
    /// </summary>
    public struct UpdateAnimationTimeTag : IComponentData {}

    /// <summary>
    /// This tag component indicates that an animation clip should be evaluated and the results
    /// written back into the targeted components every frame. Used in conjunction with
    /// <see cref="TinyAnimationTime" />. 
    /// </summary>
    public struct ApplyAnimationResultTag : IComponentData {}

    /// <summary>
    /// Holds the current time, in seconds, at which to evaluate the associated animation clip.
    /// </summary>
    public struct TinyAnimationTime : IComponentData
    {
        /// <summary>
        /// An internal value used to store intermediate results for different wrap modes. It
        /// should be of no interest to a majority of users as it may contain unpredicatable
        /// and arbitrarily out of bounds values.
        /// </summary>
        public float InternalWorkTime;

        /// <summary>
        /// The current time value, in seconds.
        /// </summary>
        public float Value;
    }

    /// <summary>
    /// Contains the meta information that determines the behaviour of an animation clip
    /// </summary>
    public struct TinyAnimationPlaybackInfo : IComponentData
    {
        /// <summary>
        /// The duration of the animation clip, in seconds.
        /// </summary>
        public float Duration;

        /// <summary>
        /// The normalized offset (0..1) at which to start the animation clip when
        /// playing it from the beginning.
        /// </summary>
        /// <remarks>
        /// This setting only applies to animation clips with a wrap mode of
        /// <see cref="Unity.Tiny.Animation.WrapMode.Loop"/> or <see cref="Unity.Tiny.Animation.WrapMode.PingPong"/>
        /// </remarks>
        public float CycleOffset;

        /// <summary>
        /// The behaviour of the clip, once it's evaluated beyond its duration.
        /// </summary>
        /// <seealso cref="WrapMode" />
        public WrapMode WrapMode;
    }

    /// <summary>
    /// Determines how an animation clip behaves when it's being evaluated beyond its duration.
    /// </summary>
    public enum WrapMode : byte
    {
        /// <summary>
        /// When time reaches the end of the animation clip, the clip will automatically stop playing and time will
        /// be reset to beginning of the clip.
        /// </summary>
        Once,

        /// <summary>
        /// When time reaches the end of the animation clip, the clip will keep playing the last frame and never stop.
        /// </summary>
        ClampForever,

        /// <summary>
        /// When time reaches the end of the animation clip, it will continue at the beginning.
        /// </summary>
        Loop,

        /// <summary>
        /// When time reaches the end of the animation clip, it will start going backwards until it reaches the
        /// beginning and then start going forward again.
        /// </summary>
        PingPong,
    }
}
