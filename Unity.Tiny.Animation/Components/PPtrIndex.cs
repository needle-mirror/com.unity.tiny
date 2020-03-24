using JetBrains.Annotations;
using Unity.Entities;

namespace Unity.Tiny.Animation
{
    [PublicAPI]
    public struct PPtrIndex : IComponentData
    {
        // Updated using type index. Not consumed by this package.
        [UsedImplicitly]
        public ushort Value;
    }
}
