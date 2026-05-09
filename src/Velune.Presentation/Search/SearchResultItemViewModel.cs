using CommunityToolkit.Mvvm.ComponentModel;
using Velune.Application.DTOs;
using Velune.Presentation.Localization;

namespace Velune.Presentation.Search;

/// <summary>
/// View model representing a single search result entry in the search panel.
/// </summary>
public partial class SearchResultItemViewModel : ObservableObject
{
    /// <summary>
    /// Initializes a new search result item from a search hit.
    /// </summary>
    /// <param name="hit">The search hit data.</param>
    /// <param name="pageNumber">The 1-based page number of the hit.</param>
    /// <param name="localizationService">Localization service for label formatting.</param>
    public SearchResultItemViewModel(SearchHit hit, int pageNumber, ILocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(hit);
        ArgumentNullException.ThrowIfNull(localizationService);

        Hit = hit;
        PageNumber = pageNumber;
        Excerpt = hit.Excerpt;
        UpdateLocalization(localizationService);
    }

    /// <summary>
    /// Gets the underlying search hit data.
    /// </summary>
    public SearchHit Hit
    {
        get;
    }

    /// <summary>
    /// Gets the 1-based page number where the hit was found.
    /// </summary>
    public int PageNumber
    {
        get;
    }

    /// <summary>
    /// Gets the text excerpt surrounding the match.
    /// </summary>
    public string Excerpt
    {
        get;
    }

    [ObservableProperty]
    private string _pageLabel = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Updates the page label text to reflect the current language.
    /// </summary>
    /// <param name="localizationService">The localization service.</param>
    public void UpdateLocalization(ILocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(localizationService);

        PageLabel = localizationService.GetString("sidebar.page", PageNumber);
    }
}
