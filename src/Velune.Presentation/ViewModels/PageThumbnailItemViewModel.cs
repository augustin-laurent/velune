using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Velune.Presentation.Localization;

namespace Velune.Presentation.ViewModels;

/// <summary>
/// View model representing a page thumbnail in the sidebar.
/// </summary>
public partial class PageThumbnailItemViewModel : ObservableObject, IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Initializes a new thumbnail item for the given source page number.
    /// </summary>
    /// <param name="sourcePageNumber">The 1-based page number.</param>
    public PageThumbnailItemViewModel(int sourcePageNumber)
    {
        SourcePageNumber = sourcePageNumber;
        DisplayPageNumber = sourcePageNumber;
    }

    /// <summary>
    /// Gets the original 1-based page number from the document.
    /// </summary>
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

    /// <summary>
    /// Updates the page label text to reflect the current language.
    /// </summary>
    /// <param name="localizationService">The localization service.</param>
    public void UpdateLocalization(ILocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(localizationService);

        PageLabel = localizationService.GetString("sidebar.page", DisplayPageNumber);
    }

    /// <inheritdoc />
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
