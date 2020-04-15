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
            Entities.ForEach((UnityEngine.Animation animationComponent) =>
            {
                if (!TinyAnimationConversionState.ValidateGameObjectAndWarn(animationComponent.gameObject))
                    return;

                var animationClips = TinyAnimationConversionState.GetAllAnimationClips(animationComponent);
                if (animationClips.Length == 0)
                    return;

                foreach (var clip in animationClips)
                {
                    DeclareAnimationClipReferencedAssets(clip);
                }
            });

            Entities.ForEach((TinyAnimationAuthoring animationComponent) =>
            {
                if (!TinyAnimationConversionState.ValidateGameObjectAndWarn(animationComponent.gameObject))
                    return;

                var animationClips = TinyAnimationConversionState.GetAllAnimationClips(animationComponent);
                if (animationClips.Length == 0)
                    return;

                foreach (var clip in animationClips)
                {
                    DeclareAnimationClipReferencedAssets(clip);
                }
            });

            UserBindingsRemapper.FillMap();
        }

        void DeclareAnimationClipReferencedAssets(AnimationClip clip)
        {
            DeclareReferencedAsset(clip);

            // Animation clip may have references to other assets through pPtr curves (like Sprites, Materials, etc.)
            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            if (bindings.Length == 0)
                return;

            foreach (var binding in bindings)
            {
                var references = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                foreach (var reference in references)
                {
                    DeclareReferencedAsset(reference.value);
                    TinyAnimationConversionState.RegisterDeclaredAsset(reference.value);
                }
            }
        }
    }
}
