namespace Velune.Windows.ViewModels;

/// <summary>
/// Represents the visual bounds of a text selection or search highlight overlay on a page.
/// </summary>
public sealed class TextSelectionHighlightItem
{
    /// <summary>
    /// Gets the left offset in pixels.
    /// </summary>
    public double Left
    {
        get; init;
    }

    /// <summary>
    /// Gets the top offset in pixels.
    /// </summary>
    public double Top
    {
        get; init;
    }

    /// <summary>
    /// Gets the width in pixels.
    /// </summary>
    public double Width
    {
        get; init;
    }

    /// <summary>
    /// Gets the height in pixels.
    /// </summary>
    public double Height
    {
        get; init;
    }

    /// <summary>
    /// Gets the margin used for absolute positioning in the overlay canvas.
    /// </summary>
    public Microsoft.UI.Xaml.Thickness Margin => new(Left, Top, 0, 0);
}
