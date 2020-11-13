#if !UNITY_DOTSRUNTIME
using UnityEngine;
using UnityEngine.Animations;

namespace Unity.Tiny.Animation
{
    /// <summary>
    /// Specifies whether to prevent TinyAnimation conversion systems from adding runtime Scale components to the
    /// TinyAnimation entity during the conversion of <see cref="UnityEngine.Animation"/> components.
    /// </summary>
    /// <remarks>
    /// Use a TinyAnimationScalePatcher component in conjunction an Animation component.
    /// This component is optional. If omitted, the TinyAnimation conversion systems default to adding
    /// the missing components where needed.
    /// </remarks>
    [NotKeyable]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.Animation))]
    public class TinyAnimationScalePatcher : MonoBehaviour
    {
        /// <summary>
        /// Normally, the TinyAnimation conversion systems adds runtime Scale components when needed. 
        /// Set the <see cref="disableScalePatching"/> value to false, to override the default behaviour.
        /// </summary>
        /// <remarks>
        /// <para>
        /// An entity whose scale is (1, 1, 1) at conversion will not get any scaling components which
        /// could prevent an animation clip from playing properly.
        /// </para>
        /// <para>
        /// By setting this value to true, you are opting out of the system generating those components
        /// automatically for you.
        /// </para>
        /// </remarks>
        [Tooltip("An entity whose scale is (1, 1, 1) at conversion will not get any scaling components. Tiny Animation will add it automatically if needed.\n" +
            "By setting this value to true, you explicitly prevent the system from adding the missing components even if they are part of the animation.\n" +
            "Note that omitting this component is equivalent to setting this value to false.")]
        public bool disableScalePatching;
    }
}
#endif
