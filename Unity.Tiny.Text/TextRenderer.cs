using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Unity.Tiny.Text
{
    /// <summary>
    /// A buffer containing characters to be displayed.  Should be present on an entity
    /// along with a TextRenderer that describes how it is to be rendered.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct TextRendererString : IBufferElementData
    {
        public char Value;
    }

    /// <summary>
    /// A component that describes how a TextRendererString on the same entity is to be rendered.
    /// </summary>
    public struct TextRenderer : IComponentData
    {
        /// <summary>
        /// An Entity with a BitmapFontMaterial or SDFFontMaterial that specifies the font to be used
        /// </summary>
        public Entity FontMaterial;

        /// <summary>
        /// The vertex color of the generated text mesh.  Modifying this causes the mesh to get
        /// regenerated!
        /// </summary>
        public Color MeshColor;

        /// <summary>
        /// The size in world units
        /// </summary>
        public float Size;

        /// <summary>
        /// The horizontal alignment, relative to this entity's position.
        /// </summary>
        public HorizontalAlignment HorizontalAlignment;

        /// <summary>
        /// The vertical alignment, relative to this entity's position.
        /// </summary>
        public VerticalAlignment VerticalAlignment;
    }

    /// <summary>
    /// A tag component used to indicate that you've changed something in either the string
    /// or the TextRenderer, and that the mesh should be regenerated.  Without this, no
    /// changes will be visible.
    /// </summary>
    public struct TextRendererNeedsUpdate : IComponentData
    {
    }
}
