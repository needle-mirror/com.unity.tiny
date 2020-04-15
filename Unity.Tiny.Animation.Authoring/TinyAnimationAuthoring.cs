#if !UNITY_DOTSPLAYER
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;

namespace Unity.Tiny.Animation
{
    [NotKeyable]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))] //Uses Animator component as an in-editor previewer
    public class TinyAnimationAuthoring : MonoBehaviour, IAnimationClipSource
    {
        public List<AnimationClip> animationClips = new List<AnimationClip>{null};

        [Tooltip("Should this animation start playing automatically upon creation?")]
        public bool playAutomatically = true;

        [Space]
        [Tooltip("An entity whose scale is (1, 1, 1) at conversion will not get any scaling components.\n" +
                 "By setting this value to true, you allow the system to add the missing scaling components if the animation affects the scale of the Entity.")]
        public bool patchMissingScaleIfNeeded = true;

        [SerializeField]
        internal List<AnimationClip> additionalAnimatorClips = new List<AnimationClip>(8);

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