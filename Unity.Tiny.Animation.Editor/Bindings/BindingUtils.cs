using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Properties;
using UnityEngine;

using UnityObject = UnityEngine.Object;

namespace Unity.Tiny.Animation.Editor
{
    static class BindingUtils
    {
        static readonly HashSet<Type> k_AnimatableSourceTypes = new HashSet<Type> {typeof(float), typeof(Entity), typeof(UnityObject)};
        static readonly HashSet<Type> k_AnimatableDestinationTypes = new HashSet<Type> {typeof(float), typeof(Entity)};

        public static bool IsSourceTypeAnimatable(Type type)
        {
            foreach (var sourceType in k_AnimatableSourceTypes)
            {
                if (sourceType.IsAssignableFrom(type))
                    return true;
            }

            return false;
        }

        public static bool IsDestinationTypeAnimatable(Type type)
        {
            return k_AnimatableDestinationTypes.Contains(type);
        }

        public static BindingInfo GetBindingInfo(EntityManager entityManager, Entity entity, FixedString512 propertyPath)
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
