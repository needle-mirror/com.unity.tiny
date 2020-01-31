using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Properties;
using UnityEngine;

namespace Unity.Tiny.Animation.Editor
{
    static class BindingUtils
    {
        static readonly HashSet<Type> k_AnimatableTypes = new HashSet<Type> {typeof(float) /*, typeof(double)*/}; // double is not trivially supported, skip for now

        public static bool IsTypeAnimatable(Type type)
        {
            return k_AnimatableTypes.Contains(type);
        }

        public static BindingInfo GetBindingInfo(EntityManager entityManager, Entity entity, NativeString512 propertyPath)
        {
            var path = propertyPath.ToString();
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("Could not find a proper mapping with an empty path.");
                return BindingInfo.UnsuccessfulBinding;
            }

            var container = new EntityContainer(entityManager, entity);
            var operation = new GetBindingInfoOperation(path);
            PropertyContainer.Visit(ref container, operation);

            return operation.GetResult();
        }
    }
}
