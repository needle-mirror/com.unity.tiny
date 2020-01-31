using Unity.Entities;

// Note: ComponentSystemGroup are not supported for conversion systems, so we use this workaround instead.
namespace Unity.Tiny.Animation.Editor
{
    /// <summary>
    /// Before conversion, Tiny Animation will declare references to every <see cref="UnityEngine.AnimationClip"/> assets it finds and to every assets they may refer to.
    /// For example: if an animation clip has a curve updating the sprite instance, all of the sprite assets on that curve will be declared as being referenced.
    ///
    /// If your conversion system would influence what Tiny Animation should depend on, add [UpdateBefore(typeof(BeforeTinyAnimationDeclaration))] to it.
    /// </summary>
    /// <remarks>
    /// Runs in group: <see cref="GameObjectDeclareReferencedObjectsGroup"/>.
    /// </remarks>
    [UpdateInGroup(typeof(GameObjectDeclareReferencedObjectsGroup))]
    public class BeforeTinyAnimationDeclaration : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            // NOOP
        }
    }

    /// <summary>
    /// Before conversion, Tiny Animation will declare references to every <see cref="UnityEngine.AnimationClip"/> assets it finds and to every assets they may refer to.
    /// For example: if an animation clip has a curve updating the sprite instance, all of the sprite assets on that curve will be declared as being referenced.
    ///
    /// If your conversion system depends on the asset references declared by Tiny Animation, add [UpdateAfter(typeof(AfterTinyAnimationDeclaration))] to it.
    /// </summary>
    /// <remarks>
    /// Runs in group: <see cref="GameObjectDeclareReferencedObjectsGroup"/>.
    /// </remarks>
    [UpdateInGroup(typeof(GameObjectDeclareReferencedObjectsGroup))]
    public class AfterTinyAnimationDeclaration : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            // NOOP
        }
    }

    /// <summary>
    /// During conversion, Tiny Animation converts every <see cref="UnityEngine.AnimationClip"/> assets it finds.
    ///
    /// If your conversion system needs to run before animation clips are converted, add [UpdateBefore(typeof(BeforeTinyAnimationConversion))] to it.
    /// </summary>
    /// <remarks>
    /// Runs in group: <see cref="GameObjectConversionGroup"/>.
    /// </remarks>
    [UpdateInGroup(typeof(GameObjectConversionGroup))]
    public class BeforeTinyAnimationConversion : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            // NOOP
        }
    }

    /// <summary>
    /// During conversion, Tiny Animation converts every <see cref="UnityEngine.AnimationClip"/> assets it finds.
    ///
    /// If your conversion system depends on converted animation clips, add [UpdateAfter(typeof(AfterTinyAnimationConversion))] to it.
    /// </summary>
    /// <remarks>
    /// Runs in group: <see cref="GameObjectConversionGroup"/>.
    /// </remarks>
    [UpdateInGroup(typeof(GameObjectConversionGroup))]
    public class AfterTinyAnimationConversion : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            // NOOP
        }
    }


    /// <summary>
    /// After conversion, Tiny Animation resolves the bindings found in the <see cref="UnityEngine.AnimationClip"/> assets it has converted.
    ///
    /// If your conversion system needs to run before animation bindings are resolved, add [UpdateBefore(typeof(BeforeTinyAnimationResolution))] to it.
    /// </summary>
    /// <remarks>
    /// Runs in group: <see cref="GameObjectAfterConversionGroup"/>.
    /// </remarks>
    [UpdateInGroup(typeof(GameObjectAfterConversionGroup))]
    public class BeforeTinyAnimationResolution : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            // NOOP
        }
    }

    /// <summary>
    /// After conversion, Tiny Animation resolves the bindings found in the <see cref="UnityEngine.AnimationClip"/> assets it has converted.
    ///
    /// If your conversion system depends on resolved animation bindings, add [UpdateAfter(typeof(AfterTinyAnimationResolution))] to it.
    /// </summary>
    /// <remarks>
    /// Runs in group: <see cref="GameObjectAfterConversionGroup"/>.
    /// </remarks>
    [UpdateInGroup(typeof(GameObjectAfterConversionGroup))]
    public class AfterTinyAnimationResolution : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            // NOOP
        }
    }
}
