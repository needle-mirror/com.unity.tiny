using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
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

        static readonly HashSet<Type> k_SkipVisiting = new HashSet<Type>
        {
            typeof(AnimationBinding), typeof(TinyAnimationClip), typeof(LocalToWorld)
        };

        readonly StringBuilder m_CurrentPropertyPath = new StringBuilder();
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

        public override bool IsExcluded<TProperty, TContainer, TValue>(TProperty property, ref TContainer container)
        {
            // Skip specific component types
            if (k_SkipVisiting.Contains(container.GetType()))
                return true;

            // Skip everything else once we're done
            return m_OperationComplete;
        }

        protected override VisitStatus BeginContainer<TProperty, TContainer, TValue>(
            TProperty property, ref TContainer container, ref TValue value, ref ChangeTracker changeTracker)
        {
            Append(property.GetName(), true);

            var t = value.GetType();

            // TODO: Do we support more types?
            if (typeof(IComponentData).IsAssignableFrom(t))
            {
                m_TargetComponentType = t;
                m_PropertyNameStartIndex = m_CurrentPropertyPath.Length;
            }

            return VisitStatus.Handled;
        }

        protected override void EndContainer<TProperty, TContainer, TValue>(
            TProperty property, ref TContainer container, ref TValue value, ref ChangeTracker changeTracker)
        {
            Pop(property.GetName(), true);
        }

        protected override VisitStatus Visit<TProperty, TContainer, TValue>(
            TProperty property, ref TContainer container, ref TValue value, ref ChangeTracker changeTracker)
        {
            if (!BindingUtils.IsTypeAnimatable(value.GetType()))
                return VisitStatus.Handled;

            var name = property.GetName();
            Append(name, false);

            ProcessProperty(property, ref container, ref value, ref changeTracker);

            Pop(name, false);
            return VisitStatus.Handled;
        }

        void ProcessProperty<TProperty, TContainer, TValue>(
            TProperty property, ref TContainer container, ref TValue value, ref ChangeTracker changeTracker)
        {
            if (m_CurrentPropertyPath.ToString() != m_SearchPropertyPath)
                return;

            var truncatedPropertyPath = m_CurrentPropertyPath.ToString(m_PropertyNameStartIndex, m_CurrentPropertyPath.Length - m_PropertyNameStartIndex);

            if (TryGetOffsetOfField(m_TargetComponentType, truncatedPropertyPath, out var offset))
            {
                m_Success = true;
                m_FieldOffset = (ushort)offset;
                m_FieldSize = (ushort)UnsafeUtility.SizeOf(value.GetType());
                m_StableTypeHash = TypeHash.CalculateStableTypeHash(m_TargetComponentType);
            }

            m_OperationComplete = true;
        }

        void Append(string str, bool isContainer)
        {
            m_CurrentPropertyPath.Append(str);

            if (isContainer)
                m_CurrentPropertyPath.Append('.');
        }

        void Pop(string str, bool isContainer)
        {
            m_CurrentPropertyPath.Length -= isContainer ? str.Length + 1 : str.Length;
        }

        static bool TryGetOffsetOfField(Type rootType, string propertyPath, out int offset)
        {
            offset = 0;

            if (string.IsNullOrEmpty(propertyPath))
                return false;

            var propertyPathParts = propertyPath.Split('.');
            var currentType = rootType;

            foreach (var part in propertyPathParts)
            {
                var f = currentType.GetField(part, BindingFlags.Instance | BindingFlags.Public);
                if (f == null)
                    return false;

                if (!currentType.IsValueType)
                    return false;

                offset += UnsafeUtility.GetFieldOffset(f);
                currentType = f.FieldType;
            }

            return true;
        }
    }
}
