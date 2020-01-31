using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Animations;

namespace Unity.Tiny.Animation
{
    [RequiresEntityConversion]
    [RequireComponent(typeof(Animator))] //Somehow, required to get IAnimationClipSource working...
    public class TinyAnimationAuthoring : MonoBehaviour, IAnimationClipSource
    {
        [NotKeyable]
        public AnimationClip animationClip;

        [NotKeyable]
        [Tooltip("Should this animation start playing automatically upon creation?")]
        public bool playAutomatically = true;

        [NotKeyable]
        [Space]
        [Tooltip("An entity whose scale is (1, 1, 1) at conversion will not get any scaling components.\n" +
                 "By setting this value to true, you allow the system to add the missing scaling components if the animation affects the scale of the Entity.")]
        public bool patchMissingScaleIfNeeded = true;

        public void GetAnimationClips(List<AnimationClip> results)
        {
            if (animationClip != null)
                results.Add(animationClip);
        }
    }
}
