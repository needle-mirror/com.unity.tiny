using JetBrains.Annotations;
using Unity.Entities;

namespace Unity.Tiny.Animation.Editor
{
    [PublicAPI]
    public struct AnimatedAssetGroupingRef : IComponentData
    {
        public Entity Value;
    }

    /// <summary>
    /// Identifies an entity as a container for an ordered list of references to assets found by Tiny Animation on a pPtr curve.
    /// </summary>
    /// <remarks>
    /// Only available in the conversion world, once <see cref="AfterTinyAnimationResolution"/> has completed.
    /// </remarks>
    /// <remarks>
    /// The entity is always accompanied by a buffer of <see cref="AnimatedAssetReference"/>.
    /// </remarks>
    [PublicAPI]
    public struct AnimatedAssetGrouping : IComponentData
    {
        /// <summary>
        /// The hash for the type of the assets in this group. Calculated using <see cref="TypeHash.CalculateStableTypeHash"/>.
        /// </summary>
        public ulong AssetTypeHash;
    }

    /// <summary>
    /// Buffer containing references to the converted assets found on a pPtr curve by Tiny Animation.
    /// </summary>
    /// <remarks>
    /// Only available in the conversion world, once <see cref="AfterTinyAnimationResolution"/> has completed.
    /// </remarks>
    /// <remarks>
    /// The buffer:
    ///    • Does not contain duplicate entries
    ///    • Is in the same order as the assets on the pPtr curve they were found on
    /// <seealso cref="PPtrIndex.Value"/> will be in sync with the buffer.
    /// </remarks>
    [PublicAPI]
    public struct AnimatedAssetReference : IBufferElementData
    {
        /// <summary>
        /// A reference to the primary entity (in the destination world) generated by the conversion of the referenced asset.
        /// </summary>
        public Entity PrimaryEntity;
    }
}
