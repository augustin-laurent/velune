using CommunityToolkit.Mvvm.ComponentModel;
using Velune.Application.DTOs;
using Velune.Windows.Services;

namespace Velune.Windows.ViewModels;

/// <summary>
/// View model for a single text search result displayed in the search panel.
/// </summary>
public sealed partial class WindowsSearchResultItemViewModel : ObservableObject
{
    /// <summary>
    /// Initializes a search result item from a search hit.
    /// </summary>
    /// <param name="hit">The underlying search hit data.</param>
    /// <param name="pageNumber">The 1-based page number of the hit.</param>
    /// <param name="textCatalog">Provides localized labels.</param>
    public WindowsSearchResultItemViewModel(SearchHit hit, int pageNumber, IWindowsTextCatalog textCatalog)
    {
        ArgumentNullException.ThrowIfNull(hit);
        ArgumentNullException.ThrowIfNull(textCatalog);

        Hit = hit;
        PageNumber = pageNumber;
        Excerpt = hit.Excerpt;
        PageLabel = textCatalog.Format("sidebar.page", pageNumber);
    }

    /// <summary>
    /// Gets the underlying search hit data including regions.
    /// </summary>
    public SearchHit Hit
    {
        get;
    }

    /// <summary>
    /// Gets the 1-based page number where this result was found.
    /// </summary>
    public int PageNumber
    {
        get;
    }

    /// <summary>
    /// Gets the text excerpt for display in the result list.
    /// </summary>
    public string Excerpt
    {
        get;
    }

    [ObservableProperty]
    public partial string PageLabel
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial bool IsSelected
    {
        get;
        set;
    }
}
