using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Velune.Domain.ValueObjects;

namespace Velune.Windows.ViewModels;

/// <summary>
/// View model for a page thumbnail in the sidebar thumbnail panel.
/// </summary>
public sealed partial class WindowsPageThumbnailViewModel : ObservableObject
{
    /// <summary>
    /// Initializes a page thumbnail view model.
    /// </summary>
    /// <param name="pageNumber">The 1-based page number.</param>
    /// <param name="label">The display label (e.g. "Page 1").</param>
    /// <param name="loadingText">Text shown while the thumbnail renders.</param>
    public WindowsPageThumbnailViewModel(int pageNumber, string label, string loadingText)
    {
        PageNumber = pageNumber;
        Label = label;
        LoadingText = loadingText;
    }

    /// <summary>
    /// Gets the 1-based page number this thumbnail represents.
    /// </summary>
    public int PageNumber
    {
        get;
    }

    [ObservableProperty]
    public partial ImageSource? Image
    {
        get; set;
    }

    [ObservableProperty]
    public partial bool IsLoading
    {
        get; set;
    }

    [ObservableProperty]
    public partial bool IsSelected
    {
        get; set;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RotationAngle))]
    public partial Rotation Rotation
    {
        get; set;
    }

    [ObservableProperty]
    public partial string? ErrorText
    {
        get; set;
    }

    /// <summary>
    /// Gets the display label for this thumbnail.
    /// </summary>
    public string Label
    {
        get;
    }

    /// <summary>
    /// Gets the loading placeholder text.
    /// </summary>
    public string LoadingText
    {
        get;
    }

    public bool HasPlaceholder => Image is null && !IsLoading && !string.IsNullOrWhiteSpace(ErrorText);

    public string PlaceholderText => ErrorText ?? string.Empty;

    public int RotationAngle => (int)Rotation;

    /// <summary>
    /// Marks the thumbnail as currently rendering.
    /// </summary>
    public void BeginRender()
    {
        ErrorText = null;
        IsLoading = true;
    }

    /// <summary>
    /// Marks the thumbnail render as failed with an error message.
    /// </summary>
    /// <param name="errorText">The error description to display.</param>
    public void MarkRenderFailed(string errorText)
    {
        ErrorText = errorText;
        IsLoading = false;
    }

    partial void OnImageChanged(ImageSource? value)
    {
        if (value is not null)
        {
            ErrorText = null;
            IsLoading = false;
        }

        OnPropertyChanged(nameof(HasPlaceholder));
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(HasPlaceholder));
    }

    partial void OnErrorTextChanged(string? value)
    {
        OnPropertyChanged(nameof(PlaceholderText));
        OnPropertyChanged(nameof(HasPlaceholder));
    }
}
