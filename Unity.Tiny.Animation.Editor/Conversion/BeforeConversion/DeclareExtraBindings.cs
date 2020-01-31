#if TINY_RENDERING_DEP
using Unity.Entities;
using Unity.Tiny.Rendering;
using Camera = Unity.Tiny.Rendering.Camera;
using Light = Unity.Tiny.Rendering.Light;

namespace Unity.Tiny.Animation.Editor
{
    [UpdateInGroup(typeof(GameObjectDeclareReferencedObjectsGroup))]
    [UpdateAfter(typeof(BeforeTinyAnimationDeclaration))]
    [UpdateBefore(typeof(AfterTinyAnimationDeclaration))]
    class DeclareExtraBindings : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            // Camera remaps
            // Those 2 *may* conflict, but animating both is asking for trouble.
            BindingsStore.CreateBindingNameRemap("Camera.field of view", $"{typeof(Camera).Name}.{nameof(Camera.fov)}");
            BindingsStore.CreateBindingNameRemap("Camera.orthographic size", $"{typeof(Camera).Name}.{nameof(Camera.fov)}");

            BindingsStore.CreateBindingNameRemap("Camera.near clip plane", $"{typeof(Camera).Name}.{nameof(Camera.clipZNear)}");
            BindingsStore.CreateBindingNameRemap("Camera.far clip plane", $"{typeof(Camera).Name}.{nameof(Camera.clipZFar)}");

            // Light remaps
            BindingsStore.CreateBindingNameRemap("Light.m_Range", $"{typeof(Light).Name}.{nameof(Light.clipZFar)}");
            BindingsStore.CreateBindingNameRemap("Light.m_Intensity", $"{typeof(Light).Name}.{nameof(Light.intensity)}");
            BindingsStore.CreateBindingNameRemap("Light.m_SpotAngle", $"{typeof(SpotLight).Name}.{nameof(SpotLight.fov)}");

            BindingsStore.CreateBindingNameRemap("Light.m_Color.r", $"{typeof(Light).Name}.{nameof(Light.color)}.x");
            BindingsStore.CreateBindingNameRemap("Light.m_Color.g", $"{typeof(Light).Name}.{nameof(Light.color)}.y");
            BindingsStore.CreateBindingNameRemap("Light.m_Color.b", $"{typeof(Light).Name}.{nameof(Light.color)}.z");
            BindingsStore.DiscardBinding("Light.m_Color.a");
        }
    }
}
#endif
