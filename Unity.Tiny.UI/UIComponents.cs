using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Tiny.UI
{
    public struct Slider : IComponentData
    {
        byte m_WholeNumbers;
        byte m_Interactable;
        byte m_Initialized;
        internal float oldValue;
        float m_Value;

        public SliderDirection Direction;
        public Entity FillRect;
        public Entity HandleRect;
        public float2 Range;

        public float Value
        {
            get => m_Value;
            set
            {
                if (m_Initialized == 0)
                {
                    oldValue = value;
                    m_Initialized = 1;
                }

                m_Value = value;
            }
        }

        public bool UseWholeNumbers
        {
            get => m_WholeNumbers > 0;
            set => m_WholeNumbers = (byte)(value ? 1 : 0);
        }

        public bool IsInteractable
        {
            get => m_Interactable > 0;
            set => m_Interactable = (byte)(value ? 1 : 0);
        }
    }

    public enum SliderDirection
    {
        /// <summary>
        /// From the left to the right
        /// </summary>
        LeftToRight,

        /// <summary>
        /// From the right to the left
        /// </summary>
        RightToLeft,

        /// <summary>
        /// From the bottom to the top.
        /// </summary>
        BottomToTop,

        /// <summary>
        /// From the top to the bottom.
        /// </summary>
        TopToBottom,
    }

    public struct Selectable : IComponentData
    {
        byte m_Interactable;

        public Entity Graphic;
        public Color NormalColor;
        public Color HighlightedColor;
        public Color PressedColor;
        public Color SelectedColor;
        public Color DisabledColor;

        public bool IsInteractable
        {
            get => m_Interactable > 0;
            set => m_Interactable = (byte)(value ? 1 : 0);
        }
    }

    public struct UIState : IComponentData
    {
        internal byte highlighted;
        internal byte pressed;
        internal byte clicked;
        internal byte valueChanged;

        public bool IsHighlight => highlighted > 0;

        public bool IsPressed => pressed > 0;

        public bool IsClicked => clicked > 0;

        public bool ValueChanged => valueChanged > 0;
    }

    public struct Toggleable : IComponentData
    {
        public Entity ToggledGraphic;

        byte m_IsToggled;

        public bool IsToggled
        {
            get => m_IsToggled > 0;
            set => m_IsToggled = (byte)(value ? 1 : 0);
        }
    }

    public struct UIName : IComponentData
    {
        public FixedString64 Name;
    }

    /// <summary>
    /// A RectCanvas, the root of a RectTransform hierarchy.
    /// </summary>
    public struct RectCanvas : IComponentData
    {
        /// <summary>
        /// Size of the canvas.  Used as the root size to compute all child sizes and positions.
        /// If there is a RectCanvasScaleWithCamera component on the same entity, this will
        /// be updated based on the referenced camera settings.
        /// </summary>
        public float2 ReferenceResolution;

        public RenderMode RenderMode { get; set; }
    }

    public enum RenderMode
    {
        /// <summary>
        ///   <para>Render at the end of the Scene using a 2D Canvas.</para>
        /// </summary>
        ScreenSpaceOverlay,

        /// <summary>
        ///   <para>Render using the Camera configured on the Canvas.</para>
        /// </summary>
        ScreenSpaceCamera,

        /// <summary>
        ///   <para>Render using any Camera in the Scene that can render the layer.</para>
        /// </summary>
        WorldSpace,
    }

    public struct RectCanvasScaleWithCamera : IComponentData
    {
        public Entity Camera;
    }

    public struct UICamera : IComponentData { }

    public struct RectTransform : IComponentData
    {
        /// <summary>
        /// The normalized position in the parent RectTransform or UICanvas's space of
        /// the lower-left corner of the rectangle.
        /// </summary>
        public float2 AnchorMin;

        /// <summary>
        /// The normalized position in the parent RectTransform or UICanvas's space of
        /// the upper-right corner of the rectangle.
        /// </summary>
        public float2 AnchorMax;

        /// <summary>
        /// The size of this RectTransform relative to the distances between the
        /// anchors.
        /// If the anchors are together, the SizeDelta is the element's exact size.
        ///
        /// If the anchors are separated, the SizeDelta is the delta from the size of
        /// anchor rectangle (i.e. negative if it is contained within the anchor rectangle).
        /// The SizeDelta and AnchoredPosition work together to determine the final size of the
        /// Rect.
        /// </summary>
        public float2 SizeDelta;

        /// <summary>
        /// The position in the parent RectTransform relative to the corresponding X or Y anchor's
        /// position.  If the min and max coordinate of the anchor direction is the same, then the
        /// AnchoredPosition determines the offset from the parent's corresponding anchor position to the
        /// Pivot, and the SizeDelta determines the size in that direction (centered on the pivot).
        /// </summary>
        public float2 AnchoredPosition;

        /// <summary>
        /// The point around which this RectTransform rotates and expands outwards in size from,
        /// defined as a normalized coordinate of the rectangle itself.  0,0 is the lower-left corner, while 1,1
        /// is the upper-right corner.
        /// </summary>
        public float2 Pivot;

        public int SiblingIndex;

        /// <summary>
        /// If set, will hide this entity and all it's children. Useful for
        /// toggling a UI (or a part of the UI) on or off.
        /// </summary>
        byte m_Hidden;

        public bool Hidden
        {
            get => m_Hidden > 0;
            set => m_Hidden = (byte)(value ? 1 : 0);
        }
    }

    public struct RectParent : IComponentData
    {
        /// <summary>
        /// Parent of this RectTransform.  Every RectTransform must have a parent, and the root
        /// of the hierarchy must be an entity with a RectCanvas.
        /// </summary>
        public Entity Value;

        /// <summary>
        /// Depth in the tree (written during transformation.)
        /// </summary>
        internal int Depth;
    }

    /// <summary>
    /// The computed size and local position of a RectTransform element
    /// </summary>
    [WriteGroup(typeof(Unity.Transforms.LocalToWorld))]
    public struct RectTransformResult : IComponentData
    {
        /// <summary>
        /// The position of this rect's pivot point, in world units relative to its parent's LocalPosition.
        /// </summary>
        public float2 LocalPosition;

        /// <summary>
        /// The size of this rectangle in world units.
        /// </summary>
        public float2 Size;

        /// <summary>
        /// Offset from the LocalPosition (pivot point) to the lower left of the rectangle. In world units.
        /// </summary>
        public float2 PivotOffset;

        byte m_HiddenResult;

        public bool HiddenResult
        {
            get => m_HiddenResult > 0;
            set => m_HiddenResult = (byte)(value ? 1 : 0);
        }
    }
}
