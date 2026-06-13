using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace Velune.Windows.ViewModels;

/// <summary>
/// Calculates the floating text annotation toolbar position within the page viewport.
/// </summary>
public static class TextAnnotationToolbarPlacement
{
    public static Thickness CalculateMargin(
        Rect annotationBounds,
        Size toolbarSize,
        Size viewportSize,
        double margin = 8)
    {
        double toolbarWidth = Math.Max(1, toolbarSize.Width);
        double toolbarHeight = Math.Max(1, toolbarSize.Height);
        double viewportWidth = Math.Max(1, viewportSize.Width);
        double viewportHeight = Math.Max(1, viewportSize.Height);

        double x = annotationBounds.X + annotationBounds.Width / 2 - toolbarWidth / 2;
        x = Math.Clamp(x, margin, Math.Max(margin, viewportWidth - toolbarWidth - margin));

        double above = annotationBounds.Y - toolbarHeight - margin;
        double below = annotationBounds.Bottom + margin;
        double y = above >= margin
            ? above
            : Math.Min(below, Math.Max(margin, viewportHeight - toolbarHeight - margin));

        return new Thickness(x, Math.Max(margin, y), 0, 0);
    }
}
