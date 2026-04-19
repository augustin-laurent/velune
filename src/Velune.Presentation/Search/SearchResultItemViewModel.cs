using CommunityToolkit.Mvvm.ComponentModel;
using Velune.Application.DTOs;

namespace Velune.Presentation.Search;

public partial class SearchResultItemViewModel : ObservableObject
{
    public SearchResultItemViewModel(SearchHit hit, int pageNumber)
    {
        ArgumentNullException.ThrowIfNull(hit);

        Hit = hit;
        PageNumber = pageNumber;
        Excerpt = hit.Excerpt;
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
    private bool _isSelected;
}
