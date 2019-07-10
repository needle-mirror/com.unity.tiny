// TINY GENERATED CODE, DO NOT EDIT BY HAND


namespace Unity.Tiny.Runtime.Interpolation
{
    internal partial struct TinyStepCurveQuaternion : ITinyComponent
    {
        [TinyCachable]
        private static void RegisterCache(ICacheManagerInternal cacheManager)
        {
            cacheManager.RegisterId<TinyStepCurveQuaternion>(s_Id);
            cacheManager.RegisterComponentConstructor(Construct);
        }

        private static TinyStepCurveQuaternion Construct(TinyObject tiny) => new TinyStepCurveQuaternion(tiny);
        private static TinyId s_Id = CoreIds.Interpolation.StepCurveQuaternion;
        private static TinyType.Reference s_Ref = TypeRefs.Interpolation.StepCurveQuaternion;

        public TinyId ComponentId => s_Id;
        public TinyType.Reference TypeRef => s_Ref;

        public readonly TinyObject Tiny;

        public TinyStepCurveQuaternion(TinyObject tiny)
        {
            Tiny = tiny;
            UnityEngine.Assertions.Assert.IsNotNull(Tiny);
            UnityEngine.Assertions.Assert.AreEqual(tiny.Type.Id, ComponentId);
        }
        public TinyStepCurveQuaternion(IRegistry registry) : this(new TinyObject(registry, s_Ref))
        {
        }

        public bool IsValid => null != Tiny;

        #region Properties
        public TinyList @times
        {
            get => Tiny[nameof(@times)] as TinyList;
        }

        public TinyList @values
        {
            get => Tiny[nameof(@values)] as TinyList;
        }

        #endregion // Properties

        public void CopyFrom(TinyStepCurveQuaternion other)
        {
            CopyList(@times, other.@times);
            CopyList(@values, other.@values);
        }
        private void CopyList(TinyList lhs, TinyList rhs)
        {
            lhs.Clear();
            foreach (var item in rhs)
            {
                lhs.Add(item);
            }
        }
    }
}