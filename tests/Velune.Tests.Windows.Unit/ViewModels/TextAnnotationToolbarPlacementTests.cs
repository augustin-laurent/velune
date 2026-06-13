using Microsoft.UI.Xaml;
using Velune.Windows.ViewModels;
using Windows.Foundation;

namespace Velune.Tests.Windows.Unit.ViewModels;

public sealed class TextAnnotationToolbarPlacementTests
{
    [Fact]
    public void CalculateMargin_PrefersAboveSelectedAnnotation()
    {
        Thickness margin = TextAnnotationToolbarPlacement.CalculateMargin(
            new Rect(100, 150, 80, 40),
            new Size(120, 48),
            new Size(500, 600));

        Assert.Equal(80, margin.Left);
        Assert.Equal(94, margin.Top);
    }

    [Fact]
    public void CalculateMargin_FallsBelowWhenAboveWouldClip()
    {
        Thickness margin = TextAnnotationToolbarPlacement.CalculateMargin(
            new Rect(100, 20, 80, 40),
            new Size(120, 48),
            new Size(500, 600));

        Assert.Equal(80, margin.Left);
        Assert.Equal(68, margin.Top);
    }

    [Fact]
    public void CalculateMargin_ClampsInsideViewport()
    {
        Thickness margin = TextAnnotationToolbarPlacement.CalculateMargin(
            new Rect(10, 20, 40, 40),
            new Size(220, 48),
            new Size(240, 120));

        Assert.Equal(8, margin.Left);
        Assert.Equal(64, margin.Top);
    }
}
