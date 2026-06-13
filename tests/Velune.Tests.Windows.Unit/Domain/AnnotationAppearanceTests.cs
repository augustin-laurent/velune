using Velune.Domain.Annotations;

namespace Velune.Tests.Windows.Unit.Domain;

public sealed class AnnotationAppearanceTests
{
    [Fact]
    public void Constructor_AllowsZeroStrokeThicknessForTextBorders()
    {
        var appearance = new AnnotationAppearance("#202020", null, 0);

        Assert.Equal(0, appearance.StrokeThickness);
    }

    [Fact]
    public void DeepCopy_PreservesTextSpecificAppearance()
    {
        var annotation = new DocumentAnnotation(
            Guid.NewGuid(),
            DocumentAnnotationKind.Text,
            new(0),
            new AnnotationAppearance(
                "#101010",
                "#FFFFFF",
                0,
                0.8,
                18,
                "Segoe UI",
                borderHex: "#303030",
                isBold: true,
                isItalic: true,
                isUnderline: true,
                textAlignment: TextAnnotationAlignment.Center),
            new(0.1, 0.2, 0.3, 0.4),
            text: "Sample");

        DocumentAnnotation copy = annotation.DeepCopy();

        Assert.NotSame(annotation, copy);
        Assert.Equal("#303030", copy.Appearance.BorderHex);
        Assert.True(copy.Appearance.IsBold);
        Assert.True(copy.Appearance.IsItalic);
        Assert.True(copy.Appearance.IsUnderline);
        Assert.Equal(TextAnnotationAlignment.Center, copy.Appearance.TextAlignment);
        Assert.Equal(0, copy.Appearance.StrokeThickness);
    }
}
