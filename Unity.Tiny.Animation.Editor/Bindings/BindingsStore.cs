using System.Collections.Generic;
using JetBrains.Annotations;

namespace Unity.Tiny.Animation.Editor
{
    /// <summary>
    /// Stores a map between authoring binding names (their "MonoBehaviour" name) and runtime binding names (their "ECS" name).
    /// </summary>
    public static class BindingsStore
    {
        static readonly Dictionary<string, string> k_BindingNameRemap = new Dictionary<string, string>(64)
        {
            // Pre-filled entries for known components
            {"Transform.m_LocalPosition.x", "Translation.Value.x"},
            {"Transform.m_LocalPosition.y", "Translation.Value.y"},
            {"Transform.m_LocalPosition.z", "Translation.Value.z"},

            {"Transform.m_LocalRotation.x", "Rotation.Value.value.x"},
            {"Transform.m_LocalRotation.y", "Rotation.Value.value.y"},
            {"Transform.m_LocalRotation.z", "Rotation.Value.value.z"},
            {"Transform.m_LocalRotation.w", "Rotation.Value.value.w"},

            {"Transform.m_LocalScale.x", "NonUniformScale.Value.x"},
            {"Transform.m_LocalScale.y", "NonUniformScale.Value.y"},
            {"Transform.m_LocalScale.z", "NonUniformScale.Value.z"}
        };

        /// <summary>
        /// Use this method to create a new authoring to runtime binding name remap.
        /// </summary>
        /// <remarks>
        /// It can be very useful to use <code>typeof(MyComponentType).Name</code> and <code>nameof(MyComponentType.myAnimatedField)</code>
        /// when creating a binding name remap.
        /// </remarks>
        /// <param name="authoringName">The name of the binding in the authoring world.</param>
        /// <param name="convertedName">The name of the binding in the runtime world.</param>
        public static void CreateBindingNameRemap(string authoringName, string convertedName)
        {
            // TODO: Add safety measures in case of overwrites, since this is now user-facing?
            k_BindingNameRemap[authoringName] = convertedName;
        }
        /// <summary>
        /// Use this method to tell Tiny Animation that an authoring-time binding is not supported at runtime.
        /// </summary>
        /// <remarks>
        /// All this method does is prevent the system from displaying warnings when a binding is not handled.
        /// Useful when an RGBA value becomes RGB in DOTS, for example (you can explicitly drop the A and avoid warnings).
        /// </remarks>
        /// <param name="authoringName">The name of the binding in the authoring world.</param>
        public static void DiscardBinding(string authoringName)
        {
            // Empty strings are discarded, see: TinyAnimationBindingsResolution.
            k_BindingNameRemap[authoringName] = string.Empty;
        }

        /// <summary>
        /// Use this method to retrieve the runtime name of a binding by using its authoring name.
        /// </summary>
        /// <param name="authoringName">The name of the binding in the authoring world.</param>
        /// <param name="convertedName">A string in which to store the name of the binding in the runtime world, if found.</param>
        /// <returns><code>true</code> if a mapping exists, <code>false</code> otherwise.</returns>
        public static bool TryGetConvertedBindingName(string authoringName, out string convertedName)
        {
            return k_BindingNameRemap.TryGetValue(authoringName, out convertedName);
        }
    }
}
