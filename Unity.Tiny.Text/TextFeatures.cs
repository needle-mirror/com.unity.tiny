namespace Unity.Tiny.Text
{
    /// <summary>
    /// Horizontal alignment for layout purposes.
    /// </summary>
    public enum HorizontalAlignment
    {
        Left,
        Center,
        Right
    }

    public enum VerticalAlignment
    {
        Top,        // The text is positioned from the top down (pivot at top)
        Center,   // Text is centered at the visual middle of the font
        Baseline,   // Normal mode - the text is positioned on the baseline
        Bottom      // The tex is positioned above the bottom
    }
}
