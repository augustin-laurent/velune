using System.Reflection;
using Velune.Domain.Annotations;
using Velune.Infrastructure.Annotations;

namespace Velune.Tests.Windows.Unit.Infrastructure;

public sealed class PdfAttachmentAnnotationStoreCompatibilityTests
{
    [Fact]
    public void DeserializeAnnotation_TextAnnotationPreservesZeroBorderWidth()
    {
        object dto = CreateAnnotationDto();
        Set(dto, "Kind", DocumentAnnotationKind.Text);
        Set(dto, "StrokeThickness", 0d);

        DocumentAnnotation annotation = Deserialize(dto);

        Assert.Equal(DocumentAnnotationKind.Text, annotation.Kind);
        Assert.Equal(0, annotation.Appearance.StrokeThickness);
        Assert.Null(annotation.Appearance.BorderHex);
        Assert.False(annotation.Appearance.IsBold);
        Assert.False(annotation.Appearance.IsItalic);
        Assert.False(annotation.Appearance.IsUnderline);
        Assert.Equal(TextAnnotationAlignment.Left, annotation.Appearance.TextAlignment);
    }

    [Fact]
    public void DeserializeAnnotation_LegacyNonTextAnnotationDefaultsZeroStrokeWidth()
    {
        object dto = CreateAnnotationDto();
        Set(dto, "Kind", DocumentAnnotationKind.Rectangle);
        Set(dto, "StrokeThickness", 0d);

        DocumentAnnotation annotation = Deserialize(dto);

        Assert.Equal(DocumentAnnotationKind.Rectangle, annotation.Kind);
        Assert.Equal(2, annotation.Appearance.StrokeThickness);
    }

    [Fact]
    public void SerializeAnnotation_WritesTextSpecificAppearanceFields()
    {
        var annotation = new DocumentAnnotation(
            Guid.NewGuid(),
            DocumentAnnotationKind.Text,
            new(0),
            new AnnotationAppearance(
                "#121212",
                "#F6F6F6",
                1,
                0.75,
                16,
                "Segoe UI",
                borderHex: "#454545",
                isBold: true,
                isItalic: true,
                isUnderline: true,
                textAlignment: TextAnnotationAlignment.Right),
            new(0.1, 0.2, 0.3, 0.4),
            text: "Styled");

        object dto = Serialize(annotation);

        Assert.Equal("#454545", Get<string?>(dto, "BorderHex"));
        Assert.True(Get<bool>(dto, "IsBold"));
        Assert.True(Get<bool>(dto, "IsItalic"));
        Assert.True(Get<bool>(dto, "IsUnderline"));
        Assert.Equal(TextAnnotationAlignment.Right, Get<TextAnnotationAlignment>(dto, "TextAlignment"));
    }

    private static object CreateAnnotationDto()
    {
        Type dtoType = GetAnnotationDtoType();
        object dto = Activator.CreateInstance(dtoType)!;
        Set(dto, "Id", Guid.NewGuid());
        Set(dto, "PageIndex", 0);
        Set(dto, "StrokeHex", "#202020");
        Set(dto, "Opacity", 1d);
        Set(dto, "FontSize", 14d);
        Set(dto, "BoundsX", 0.1d);
        Set(dto, "BoundsY", 0.2d);
        Set(dto, "BoundsW", 0.3d);
        Set(dto, "BoundsH", 0.4d);
        return dto;
    }

    private static object Serialize(DocumentAnnotation annotation)
    {
        MethodInfo method = typeof(PdfAttachmentAnnotationStore).GetMethod(
            "SerializeAnnotation",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        return method.Invoke(null, [annotation])!;
    }

    private static DocumentAnnotation Deserialize(object dto)
    {
        MethodInfo method = typeof(PdfAttachmentAnnotationStore).GetMethod(
            "DeserializeAnnotation",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        return Assert.IsType<DocumentAnnotation>(method.Invoke(null, [dto]));
    }

    private static Type GetAnnotationDtoType()
    {
        return typeof(PdfAttachmentAnnotationStore).GetNestedType(
            "AnnotationDto",
            BindingFlags.NonPublic)!;
    }

    private static T Get<T>(object instance, string propertyName)
    {
        return (T)GetAnnotationDtoType().GetProperty(propertyName)!.GetValue(instance)!;
    }

    private static void Set<T>(object instance, string propertyName, T value)
    {
        GetAnnotationDtoType().GetProperty(propertyName)!.SetValue(instance, value);
    }
}
