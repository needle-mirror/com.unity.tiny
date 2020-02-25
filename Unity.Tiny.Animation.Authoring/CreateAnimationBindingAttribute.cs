using System;

namespace Unity.Tiny.Animation
{
    /// <summary>
    /// Use this attribute on an animatable field of a MonoBehaviour that acts as a proxy to a field in an IComponentData.
    /// The general use-case is when we have an authoring component that doesn't match 1-to-1 with the generated component(s)
    /// but we still want to animate fields on the resulting component(s).
    /// </summary>
    ///
    /// <remarks>
    /// We consider a field animatable on a *MonoBehaviour* when it is:
    /// - a float or a Vector type
    /// - serialized either through [SerializeField] or because it is public
    /// - not explicitly marked with [NotKeyable]
    /// - not in a type marked with [NotKeyable]
    ///
    /// We consider a field animatable on an *IComponentData* when it is:
    /// - a float or a floatX type
    /// - public
    /// - not explicitly marked with [NotKeyable]
    /// - not in a type marked with [NotKeyable]
    ///
    /// Note that conversion between VectorX and floatX is handled implicitly.
    /// </remarks>
    ///
    /// <example>
    /// In this example, we want to animate the value of <code>FloatValue</code> so we add the serializable field <code>float m_Value</code>
    /// on the authoring component <code>FloatValueAuthoring</code> on which we apply the <code>CreateAnimationBinding</code> attribute.
    /// <code>
    /// struct FloatValue : IComponentData { public float value; } // We want to be able to animate value
    ///
    /// class FloatValueAuthoring : MonoBehaviour, IConvertGameObjectToEntity
    /// {
    ///     [SerializeField]
    ///     [CreateAnimationBinding(typeof(FloatValue).Name, nameof(FloatValue.value))] // We use the attribute to tell the system what IComponentData field it should match
    ///     float m_Value;
    ///     public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    ///     {
    ///         dstManager.AddComponentData(entity, new FloatValue { value = m_Value } );
    ///     }
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class CreateAnimationBindingAttribute : Attribute
    {
        // These 2 fields are used for external validation
        internal Type RequestedComponentType { get; }
        internal string RequestedPropertyPath { get; }

        public string BindsTo { get; }

        /// <summary>
        /// Constructor for CreateAnimationBindingAttribute
        /// </summary>
        /// <param name="componentType">The type of the IComponentData struct containing the field we want to animate.</param>
        /// <param name="propertyPath">
        /// The path to the field we want to animate. For simple cases, it is possible to use <code>nameof(MyType.myField)</code>.
        /// For more complex cases, use the format "field.subField.subField".
        /// </param>
        public CreateAnimationBindingAttribute(Type componentType, string propertyPath)
        {
            RequestedComponentType = componentType;
            RequestedPropertyPath = propertyPath;

            if (componentType != null && !string.IsNullOrEmpty(propertyPath))
                BindsTo = $"{componentType.Name}.{propertyPath}";
        }
    }
}