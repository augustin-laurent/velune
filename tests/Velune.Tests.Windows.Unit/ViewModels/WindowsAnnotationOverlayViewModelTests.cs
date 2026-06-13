using System.Reflection;
using System.Runtime.CompilerServices;
using Velune.Domain.Annotations;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Velune.Windows.ViewModels;

namespace Velune.Tests.Windows.Unit.ViewModels;

public sealed class WindowsAnnotationOverlayViewModelTests
{
    [Fact]
    public void AnnotationOverlayViewModels_DoNotExposePublicWinUiPropertyTypes()
    {
        Type[] viewModelTypes =
        [
            typeof(AnnotationOverlayPoint),
            typeof(WindowsAnnotationOverlayViewModel),
            typeof(WindowsCommentOverlayViewModel),
            typeof(WindowsInlineTextEditorViewModel),
            typeof(WindowsAnnotationColorItem)
        ];

        foreach (Type viewModelType in viewModelTypes)
        {
            IEnumerable<PropertyInfo> uiProperties = viewModelType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(property => IsUiType(property.PropertyType));

            Assert.Empty(uiProperties.Select(property =>
                $"{viewModelType.Name}.{property.Name}: {property.PropertyType.FullName}"));
        }
    }

    [Fact]
    public void ListItemAutomationId_UsesAnnotationId()
    {
        Guid id = Guid.Parse("1f1788c4-77ac-4b09-8b64-10cb343461b6");
        var annotation = new DocumentAnnotation(
            id,
            DocumentAnnotationKind.Rectangle,
            new PageIndex(0),
            new AnnotationAppearance("#202020", null, 0),
            new NormalizedTextRegion(0.1, 0.1, 0.2, 0.2));

        var overlay = new WindowsAnnotationOverlayViewModel(
            annotation,
            pageWidth: 500,
            pageHeight: 700,
            Rotation.Deg0,
            "Rectangle",
            "Page 1",
            "\uE9CE");

        Assert.Equal("AnnotationListItem_1f1788c477ac4b098b6410cb343461b6", overlay.ListItemAutomationId);
    }

    [Fact]
    public void CardAutomationId_UsesCommentAnnotationId()
    {
        Guid id = Guid.Parse("8ef78d31-191e-4b0f-a3ef-0ce37f29fd72");
        var overlay = (WindowsCommentOverlayViewModel)RuntimeHelpers.GetUninitializedObject(
            typeof(WindowsCommentOverlayViewModel));
        SetReadOnlyProperty(overlay, nameof(WindowsCommentOverlayViewModel.Id), id);

        Assert.Equal("CommentOverlayCard_8ef78d31191e4b0fa3ef0ce37f29fd72", overlay.CardAutomationId);
    }

    private static void SetReadOnlyProperty<T>(WindowsCommentOverlayViewModel overlay, string propertyName, T value)
    {
        FieldInfo? backingField = typeof(WindowsCommentOverlayViewModel).GetField(
            $"<{propertyName}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(backingField);
        backingField.SetValue(overlay, value);
    }

    private static bool IsUiType(Type type)
    {
        Type inspectedType = Nullable.GetUnderlyingType(type) ?? type;
        if (inspectedType.HasElementType)
        {
            return IsUiType(inspectedType.GetElementType()!);
        }

        if (inspectedType.IsGenericType)
        {
            return inspectedType.GetGenericArguments().Any(IsUiType);
        }

        string? typeNamespace = inspectedType.Namespace;
        return typeNamespace is not null &&
               (typeNamespace.StartsWith("Microsoft.UI.Xaml", StringComparison.Ordinal) ||
                typeNamespace.StartsWith("Windows.Foundation", StringComparison.Ordinal) ||
                typeNamespace.StartsWith("Windows.UI", StringComparison.Ordinal));
    }
}
