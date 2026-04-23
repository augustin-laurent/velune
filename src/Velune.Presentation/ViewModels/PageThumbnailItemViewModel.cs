using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Velune.Presentation.Localization;

namespace Velune.Presentation.ViewModels;

public partial class PageThumbnailItemViewModel : ObservableObject, IDisposable
{
    private bool _disposed;

    public PageThumbnailItemViewModel(int sourcePageNumber)
    {
        SourcePageNumber = sourcePageNumber;
        DisplayPageNumber = sourcePageNumber;
    }

    public int SourcePageNumber
    {
        get;
    }

    [ObservableProperty]
    private int _displayPageNumber;

    [ObservableProperty]
    private Bitmap? _thumbnail;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private bool _isDragging;

    [ObservableProperty]
    private string _pageLabel = string.Empty;

    public void UpdateLocalization(ILocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(localizationService);

        PageLabel = localizationService.GetString("sidebar.page", DisplayPageNumber);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            Thumbnail?.Dispose();
            Thumbnail = null;
        }

        _disposed = true;
    }
}
