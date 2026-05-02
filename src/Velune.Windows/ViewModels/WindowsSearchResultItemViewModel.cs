using CommunityToolkit.Mvvm.ComponentModel;
using Velune.Application.DTOs;
using Velune.Windows.Services;

namespace Velune.Windows.ViewModels;

public sealed partial class WindowsSearchResultItemViewModel : ObservableObject
{
    public WindowsSearchResultItemViewModel(SearchHit hit, int pageNumber, IWindowsTextCatalog textCatalog)
    {
        ArgumentNullException.ThrowIfNull(hit);
        ArgumentNullException.ThrowIfNull(textCatalog);

        Hit = hit;
        PageNumber = pageNumber;
        Excerpt = hit.Excerpt;
        PageLabel = textCatalog.Format("sidebar.page", pageNumber);
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
