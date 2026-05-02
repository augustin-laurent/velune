using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;

namespace Velune.Windows.ViewModels;

public sealed partial class WindowsPageThumbnailViewModel : ObservableObject
{
    public WindowsPageThumbnailViewModel(int pageNumber, string label, string loadingText)
    {
        PageNumber = pageNumber;
        Label = label;
        LoadingText = loadingText;
    }

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
    public partial string? ErrorText
    {
        get; set;
    }

    public string Label
    {
        get;
    }

    public string LoadingText
    {
        get;
    }

    public bool HasPlaceholder => Image is null && !IsLoading && !string.IsNullOrWhiteSpace(ErrorText);

    public string PlaceholderText => ErrorText ?? string.Empty;

    public void BeginRender()
    {
        ErrorText = null;
        IsLoading = true;
    }

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
