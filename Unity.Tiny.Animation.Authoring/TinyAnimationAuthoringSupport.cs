using TinyInternal.Bridge;
using UnityEditor;
using UnityEngine;

namespace Unity.Tiny.Animation
{
    static class TinyAnimationAuthoringSupport
    {
        [MenuItem("CONTEXT/Animation/Add Tiny.Animation support components", false, 9001)]
        static void AddSupportComponents(MenuCommand command)
        {
            var hostGameObject = ((UnityEngine.Animation)command.context).gameObject;
            AddComponentIfNeeded<TinyAnimationMecanimSupport>(hostGameObject);
            AddComponentIfNeeded<TinyAnimationScalePatcher>(hostGameObject);
        }

        static void AddComponentIfNeeded<T>(GameObject go) where T : Component
        {
            if (go.GetComponent<T>() == null)
                go.AddComponent<T>();
        }

        // Using this engine code as reference for menu entry placement:
        //     MenuController::AddMenuItem("Assets/Create/Animator Controller", "", "67", menu, 401);
        //     MenuController::AddMenuItem("Assets/Create/Animation", "", "52", menu, 402);
        //     MenuController::AddMenuItem("Assets/Create/Animator Override Controller", "", "72", menu, 403);
        //     MenuController::AddMenuItem("Assets/Create/Avatar Mask", "", "71", menu, 404);
        const int k_TinyAnimationClipMenuIndex = 402; // Between `Animation` and `Animator Override Controller`

        [MenuItem("Assets/Create/Tiny Animation Clip", false, k_TinyAnimationClipMenuIndex)]
        static void CreateAnimationClip()
        {
            TinyAnimationEditorBridge.CreateLegacyClip("New Tiny Animation");
        }
    }
}
