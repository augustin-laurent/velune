using CommunityToolkit.Mvvm.ComponentModel;
using Velune.Application.DTOs;
using Velune.Presentation.Localization;

namespace Velune.Presentation.Search;

public partial class SearchResultItemViewModel : ObservableObject
{
    public SearchResultItemViewModel(SearchHit hit, int pageNumber, ILocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(hit);
        ArgumentNullException.ThrowIfNull(localizationService);

        Hit = hit;
        PageNumber = pageNumber;
        Excerpt = hit.Excerpt;
        UpdateLocalization(localizationService);
    }

    public SearchHit Hit
    {
        get;
    }

    public int PageNumber
    {
        get;
    }

    public string Excerpt
    {
        get;
    }

    [ObservableProperty]
    private string _pageLabel = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    public void UpdateLocalization(ILocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(localizationService);

        PageLabel = localizationService.GetString("sidebar.page", PageNumber);
    }
}
