using Unity.Entities;
using UnityEngine;
using UnityEngine.Animations;

namespace Unity.Tiny.Animation
{
    [NotKeyable]
    [RequiresEntityConversion]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.Animation))]
    public class TinyAnimationScalePatcher : MonoBehaviour
    {
        [Tooltip("An entity whose scale is (1, 1, 1) at conversion will not get any scaling components. Tiny Animation will add it automatically if needed.\n" +
                 "By setting this value to true, you explicitly prevent the system from adding the missing components even if they are part of the animation.\n" +
                 "Note that omitting this component is equivalent to setting this value to false.")]
        public bool disableScalePatching;
    }
}
