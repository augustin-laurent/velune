using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Velune.Presentation.ViewModels;

public partial class PageThumbnailItemViewModel : ObservableObject, IDisposable
{
    private bool _disposed;

    public PageThumbnailItemViewModel(int pageNumber)
    {
        PageNumber = pageNumber;
    }

    public int PageNumber
    {
        get;
    }

    [ObservableProperty]
    private Bitmap? _thumbnail;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isLoading = true;

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
