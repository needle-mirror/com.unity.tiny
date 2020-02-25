using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace Unity.Tiny.Animation.Editor
{
    [UpdateInGroup(typeof(GameObjectDeclareReferencedObjectsGroup))]
    [UpdateAfter(typeof(BeforeTinyAnimationDeclaration))]
    [UpdateBefore(typeof(AfterTinyAnimationDeclaration))]
    class AnimationClipReferenceDeclaration : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((TinyAnimationAuthoring tinyAnimationAuthoring) =>
            {
                var animationClips = tinyAnimationAuthoring.animationClips;
                if (animationClips == null || animationClips.Count == 0)
                    return;

                foreach (var clip in animationClips)
                {
                    if (clip != null)
                        DeclareAnimationClipReferencedAssets(clip);
                }
            });
        }

        void DeclareAnimationClipReferencedAssets(AnimationClip clip)
        {
            DeclareReferencedAsset(clip);

            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            if (bindings.Length == 0)
                return;

            foreach (var binding in bindings)
            {
                var references = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                foreach (var reference in references)
                {
                    DeclareReferencedAsset(reference.value);
                }
            }
        }
    }
}
