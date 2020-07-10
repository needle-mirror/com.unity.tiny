using System;
using System.Collections.Generic;
using System.Reflection;
using JetBrains.Annotations;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Properties;
using Unity.Transforms;

namespace Unity.Tiny.Animation.Editor
{
    class GetBindingInfoOperation : PropertyVisitor
    {
        readonly string m_PropertyPath;

        // None of these types are worth visiting since they will never be animated but but they show up everywhere
        static readonly HashSet<Type> k_SkipVisiting = new HashSet<Type>
        {
            // Binding Data
            typeof(AnimationBinding), typeof(AnimationPPtrBinding),
            // Animation Player
            typeof(TinyAnimationTime), typeof(TinyAnimationPlayer), typeof(TinyAnimationPlaybackInfo),
            // Transform System
            typeof(LocalToWorld), typeof(LocalToParent), typeof(Parent),
            // Live Link
            typeof(EntityGuid), typeof(SceneSection)
        };

        readonly PropertyPath m_CurrentPropertyPath = new PropertyPath();
        readonly string m_SearchPropertyPath;
        bool m_OperationComplete;

        Type m_TargetComponentType;
        int m_PropertyNameStartIndex;

        bool m_Success;
        ulong m_StableTypeHash;
        ushort m_FieldOffset;
        ushort m_FieldSize;

        public GetBindingInfoOperation([NotNull] string propertyPath)
        {
            m_SearchPropertyPath = propertyPath;
            m_OperationComplete = false;
        }

        public BindingInfo GetResult()
        {
            return new BindingInfo(m_Success, m_StableTypeHash, m_FieldOffset, m_FieldSize);
        }

        protected override bool IsExcluded<TContainer, TValue>(Property<TContainer, TValue> property, ref TContainer container, ref TValue value)
        {
            return m_OperationComplete ||
                k_SkipVisiting.Contains(property.DeclaredValueType()) ||
                k_SkipVisiting.Contains(container.GetType());
        }

        protected override void VisitProperty<TContainer, TValue>(Property<TContainer, TValue> property, ref TContainer container, ref TValue value)
        {
            if (IsExcluded(property, ref container, ref value))
                return;

            m_CurrentPropertyPath.PushProperty(property);

            var t = value.GetType();

            // TODO: Do we support more types?
            if (typeof(IComponentData).IsAssignableFrom(t))
            {
                m_TargetComponentType = t;
                m_PropertyNameStartIndex = m_CurrentPropertyPath.PartsCount;
            }

            if (BindingUtils.IsDestinationTypeAnimatable(value.GetType()))
            {
                ProcessProperty(ref value);
            }
            else
            {
                property.Visit(this, ref value);
            }

            m_CurrentPropertyPath.Pop();
        }

        void ProcessProperty<TValue>(ref TValue value)
        {
            if (m_CurrentPropertyPath.ToString() != m_SearchPropertyPath)
            {
                return;
            }

            if (TryGetOffsetOfField(m_TargetComponentType, m_CurrentPropertyPath, m_PropertyNameStartIndex, out var offset))
            {
                m_Success = true;
                m_FieldOffset = (ushort)offset;
                m_FieldSize = (ushort)UnsafeUtility.SizeOf(value.GetType());
                m_StableTypeHash = TypeHash.CalculateStableTypeHash(m_TargetComponentType);
            }

            m_OperationComplete = true;
        }

        static bool TryGetOffsetOfField(Type rootType, PropertyPath propertyPath, int startIndex, out int offset)
        {
            offset = 0;

            var currentType = rootType;

            for (var i = startIndex; i < propertyPath.PartsCount; i++)
            {
                if (!currentType.IsValueType)
                    return false;

                var part = propertyPath[i];

                if (part.IsIndex || part.IsKey)
                {
                    throw new ArgumentException("TinyAnimation does not support array indexers or dictionary keys for bindings.");
                }

                var f = currentType.GetField(propertyPath[i].Name, BindingFlags.Instance | BindingFlags.Public);

                if (f == null)
                    return false;

                offset += UnsafeUtility.GetFieldOffset(f);
                currentType = f.FieldType;
            }

            return true;
        }
    }
}
