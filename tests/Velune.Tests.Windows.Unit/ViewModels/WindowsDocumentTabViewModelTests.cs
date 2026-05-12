using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using Velune.Domain.Annotations;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Velune.Windows.Services;
using Velune.Windows.ViewModels;

namespace Velune.Tests.Windows.Unit.ViewModels;

public sealed class WindowsDocumentTabViewModelTests
{
    [Fact]
    public void SetPageRotation_TracksPendingRotationAndThumbnailState()
    {
        WindowsDocumentTabViewModel tab = CreateTab(pageCount: 3);

        tab.SetPageRotation(2, Rotation.Deg90);

        Assert.True(tab.HasPendingPageRotations);
        Assert.True(tab.HasPendingPageEdits);
        Assert.Equal(Rotation.Deg90, tab.GetPageRotation(2));
        Assert.Equal(Rotation.Deg90, tab.Thumbnails[1].Rotation);
        Assert.Equal([(2, Rotation.Deg90)], tab.GetPendingPageRotations());
    }

    [Fact]
    public void CurrentPageChange_SyncsTabRotationToPageRotation()
    {
        WindowsDocumentTabViewModel tab = CreateTab(pageCount: 2);
        tab.SetPageRotation(2, Rotation.Deg270);

        tab.CurrentPage = 2;

        Assert.Equal(Rotation.Deg270, tab.Rotation);
    }

    [Fact]
    public void ClearPendingPageRotations_ResetsPendingStateAndThumbnails()
    {
        WindowsDocumentTabViewModel tab = CreateTab(pageCount: 2);
        tab.SetPageRotation(1, Rotation.Deg90);
        tab.SetPageRotation(2, Rotation.Deg180);

        tab.ClearPendingPageRotations();

        Assert.False(tab.HasPendingPageRotations);
        Assert.False(tab.HasPendingPageEdits);
        Assert.All(tab.Thumbnails, thumbnail => Assert.Equal(Rotation.Deg0, thumbnail.Rotation));
        Assert.Empty(tab.GetPendingPageRotations());
    }

    private static WindowsDocumentTabViewModel CreateTab(int pageCount)
    {
        var tab = (WindowsDocumentTabViewModel)RuntimeHelpers.GetUninitializedObject(
            typeof(WindowsDocumentTabViewModel));

        SetField(tab, "_textCatalog", new StubWindowsTextCatalog());
        SetField(tab, "_pendingPageRotations", new Dictionary<int, Rotation>());
        SetField(tab, "_hiddenAnnotations", new HashSet<Guid>());
        SetField(tab, "_lockedAnnotations", new HashSet<Guid>());
        SetField(tab, "_signatureAssets", new Dictionary<string, SignatureAsset>(StringComparer.Ordinal));
        SetReadOnlyProperty(tab, nameof(WindowsDocumentTabViewModel.DocumentType), DocumentType.Pdf);
        SetReadOnlyProperty(tab, nameof(WindowsDocumentTabViewModel.Thumbnails), CreateThumbnails(pageCount));
        SetReadOnlyProperty(tab, nameof(WindowsDocumentTabViewModel.Annotations), new ObservableCollection<DocumentAnnotation>());
        SetReadOnlyProperty(tab, nameof(WindowsDocumentTabViewModel.CurrentPageAnnotationOverlays), new ObservableCollection<WindowsAnnotationOverlayViewModel>());
        SetReadOnlyProperty(tab, nameof(WindowsDocumentTabViewModel.CurrentPageCommentOverlays), new ObservableCollection<WindowsCommentOverlayViewModel>());
        SetReadOnlyProperty(tab, nameof(WindowsDocumentTabViewModel.SearchResults), new ObservableCollection<WindowsSearchResultItemViewModel>());
        SetReadOnlyProperty(tab, nameof(WindowsDocumentTabViewModel.SearchHighlights), new ObservableCollection<TextSelectionHighlightItem>());
        SetReadOnlyProperty(tab, nameof(WindowsDocumentTabViewModel.TextSelectionHighlights), new ObservableCollection<TextSelectionHighlightItem>());

        tab.TotalPages = pageCount;
        tab.CurrentPage = 1;
        tab.CurrentPagePixelWidth = 400;
        tab.CurrentPagePixelHeight = 600;
        return tab;
    }

    private static ObservableCollection<WindowsPageThumbnailViewModel> CreateThumbnails(int pageCount)
    {
        var thumbnails = new ObservableCollection<WindowsPageThumbnailViewModel>();
        for (int page = 1; page <= pageCount; page++)
        {
            thumbnails.Add(new WindowsPageThumbnailViewModel(page, $"Page {page}", "Loading"));
        }

        return thumbnails;
    }

    private static void SetField<T>(WindowsDocumentTabViewModel tab, string fieldName, T value)
    {
        FieldInfo? field = typeof(WindowsDocumentTabViewModel).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        field.SetValue(tab, value);
    }

    private static void SetReadOnlyProperty<T>(WindowsDocumentTabViewModel tab, string propertyName, T value)
    {
        FieldInfo? backingField = typeof(WindowsDocumentTabViewModel).GetField(
            $"<{propertyName}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(backingField);
        backingField.SetValue(tab, value);
    }

    private sealed class StubWindowsTextCatalog : IWindowsTextCatalog
    {
        public string GetString(string key) => key;

        public string Format(string key, params object[] args) => $"{key}: {string.Join(", ", args)}";
    }
}
