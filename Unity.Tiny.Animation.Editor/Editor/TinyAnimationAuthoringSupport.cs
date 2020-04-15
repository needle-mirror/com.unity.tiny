using UnityEditor;
using UnityEngine;

namespace Unity.Tiny.Animation
{
    static class TinyAnimationAuthoringSupport
    {
        [MenuItem("CONTEXT/Animation/Tiny/Disable Scale Patching", false, 9001)]
        static void DisableScalePatching(MenuCommand command)
        {
            var hostGameObject = ((Component)command.context).gameObject;
            var scalePatcher = AddComponentIfNeeded<TinyAnimationScalePatcher>(hostGameObject);

            var serializedScalePatcher = new SerializedObject(scalePatcher);
            serializedScalePatcher.FindProperty("disableScalePatching").boolValue = true;
            serializedScalePatcher.ApplyModifiedProperties();
        }

        [MenuItem("CONTEXT/Animator/Tiny/Convert to Tiny Animation", false, 9001)]
        static void AddTinyAnimatorConversion(MenuCommand command)
        {
            var hostGameObject = ((Component)command.context).gameObject;
            AddComponentIfNeeded<TinyAnimationAuthoring>(hostGameObject);
        }

        static T AddComponentIfNeeded<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            if (component == null)
                component = go.AddComponent<T>();
            return component;
        }

        internal static void UpdateAdditionalAnimatorClips(this TinyAnimationAuthoring tinyAnimationAuthoring)
        {
            tinyAnimationAuthoring.additionalAnimatorClips.Clear();
            var allAnimationClips = TinyInternal.Bridge.TinyAnimationEditorBridge.GetAnimationClipsInAnimationPlayer(tinyAnimationAuthoring.gameObject);

            foreach (var clip in allAnimationClips)
            {
                if (clip != null && !tinyAnimationAuthoring.animationClips.Contains(clip) && !tinyAnimationAuthoring.additionalAnimatorClips.Contains(clip))
                    tinyAnimationAuthoring.additionalAnimatorClips.Add(clip);
            }
        }
    }
}
