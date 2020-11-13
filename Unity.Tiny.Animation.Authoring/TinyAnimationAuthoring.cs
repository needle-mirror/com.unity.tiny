#if !UNITY_DOTSRUNTIME
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;

namespace Unity.Tiny.Animation
{
    /// <summary>
    /// Add a TinyAnimationAuthoring component to a <see cref="GameObject"/> to play [Mecanim](xref:MecanimFAQ) clips
    /// in Tiny.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Add the [Mecanim](xref:MecanimFAQ) clips you want to use to the
    /// <see cref="TinyAnimationAuthoring.animationClips"/> list. They will be converted and
    /// available on the Entity resulting from the conversion of the GameObject with this component.
    /// </para>
    /// <para>
    /// When you add a TinyAnimationAuthoring component to a GameObject, Unity also adds an <see cref="Animator"/>
    /// component. This Animator component supports previewing animations in the Editor. TinyAnimation does not
    /// use the component directly and you can safely leave it empty. It doesn't even require
    /// a controller. However, if your Animator does have a controller and some clips attached to it,
    /// TinyAnimationAuthoring detects them, converts them, and makes them available at runtime.
    /// </para>
    /// </remarks>
    [NotKeyable]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))] //Uses Animator component as an in-editor previewer
    public class TinyAnimationAuthoring : MonoBehaviour, IAnimationClipSource
    {
        /// <summary>
        /// The list of animations to be converted and made available at runtime.
        /// </summary>
        public List<AnimationClip> animationClips = new List<AnimationClip> {null};

        /// <summary>
        /// Whether to start playing an animation automatically upon instantiation or not.
        /// </summary>
        [Tooltip("Should this animation start playing automatically upon creation?")]
        public bool playAutomatically = true;

        /// <summary>
        /// Forces the addition of runtime Scale components to the TinyAnimation entity during conversion.
        /// </summary>
        /// <para>
        /// Set this value to true, if an animation affects the scale of an Entity. Setting patchMissingScaleIfNeeded
        /// to true, forces the Transform conversion system to add scale components even when they would normally be
        /// omitted.
        /// </para>
        /// <para>
        /// By default, the ECS Transform systems do not add Scale components to <see cref="GameObject"/>
        /// instances whose scale is set to (1, 1, 1), which is the default value. This optimization prevents
        /// needless scale-related calculations, but can prevent an animation clip that changes the scale
        /// from playing properly.
        /// </para>
        [Space]
        [Tooltip("An entity whose scale is (1, 1, 1) at conversion will not get any scaling components.\n" +
            "By setting this value to true, you allow the system to add the missing scaling components if the animation affects the scale of the Entity.")]
        public bool patchMissingScaleIfNeeded = true;

        [SerializeField]
        internal List<AnimationClip> additionalAnimatorClips = new List<AnimationClip>(8);

        /// <inheritdoc cref="IAnimationClipSource.GetAnimationClips" />
        public void GetAnimationClips(List<AnimationClip> results)
        {
            foreach (var clip in animationClips)
            {
                if (clip != null)
                    results.Add(clip);
            }
        }
    }
}
#endif
