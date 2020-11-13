using Unity.Entities;

namespace Unity.Tiny.Animation
{
    /// <summary>
    /// Identifies which clip to play from the list contained in <see cref="TinyAnimationClipRef"/>.
    /// </summary>
    /// <remarks>
    /// You can get or set the current clip using the functions defined by <see cref="TinyAnimation"/>.
    /// </remarks>
    public struct TinyAnimationPlayer : IComponentData
    {
        /// <summary>
        /// A reference to the Entity representing the clip currently selected for playback.
        /// </summary>
        public Entity CurrentClip;

        /// <summary>
        /// The index in the <see cref="TinyAnimationClipRef"/> buffer for the clip currently selected
        /// for playback.
        /// </summary>
        public int CurrentIndex;
    }

    /// <summary>
    /// A list of entities representing animation clips.
    /// </summary>
    /// <remarks>
    /// Select which clip to play using the functions defined by <see cref="TinyAnimation"/>.
    /// The current clip is stored in the <see cref="TinyAnimationPlayer"/> component. 
    /// </remarks>
    public struct TinyAnimationClipRef : IBufferElementData
    {
        /// <summary>
        /// A reference to an Entity representing an animation clip.
        /// </summary>
        public Entity Value;

        /// <summary>
        /// An identifier for the animation clip. Created using <see cref="TinyAnimation.StringToHash"/>
        /// on the original animation clip asset's name.
        /// </summary>
        public uint Hash;
    }
}
