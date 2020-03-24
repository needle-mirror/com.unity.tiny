using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Animations;

namespace Unity.Tiny.Animation
{
    [NotKeyable]
    [RequiresEntityConversion]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.Animation))]
    public class TinyAnimationMecanimSupport : MonoBehaviour, IAnimationClipSource
    {
        public List<AnimationClip> mecanimClips = new List<AnimationClip>{null};

        public void GetAnimationClips(List<AnimationClip> results)
        {
            foreach (var clip in mecanimClips)
            {
                if (clip != null)
                    results.Add(clip);
            }
        }
    }
}
