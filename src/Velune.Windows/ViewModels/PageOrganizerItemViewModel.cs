using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Velune.Domain.ValueObjects;

namespace Velune.Windows.ViewModels;

/// <summary>
/// Represents a single page item within the page organizer grid.
/// </summary>
public sealed partial class PageOrganizerItemViewModel : ObservableObject
{
    /// <summary>
    /// Initializes a page organizer item with its page number and optional rotation.
    /// </summary>
    /// <param name="pageNumber">The 1-based page number.</param>
    /// <param name="rotation">Initial page rotation.</param>
    public PageOrganizerItemViewModel(int pageNumber, Rotation rotation = Rotation.Deg0)
    {
        PageNumber = pageNumber;
        OriginalPageNumber = pageNumber;
        Rotation = rotation;
    }

    /// <summary>
    /// Gets the original page number before any reordering.
    /// </summary>
    public int OriginalPageNumber
    {
        get;
    }

    [ObservableProperty]
    public partial int PageNumber
    {
        get; set;
    }

    [ObservableProperty]
    public partial ImageSource? Thumbnail
    {
        get; set;
    }

    [ObservableProperty]
    public partial bool IsSelected
    {
        get; set;
    }

    [ObservableProperty]
    public partial bool IsLoading
    {
        get; set;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RotationAngle))]
    [NotifyPropertyChangedFor(nameof(IsLandscapeRotation))]
    public partial Rotation Rotation
    {
        get; set;
    }

    /// <summary>
    /// Gets the rotation angle in degrees for binding.
    /// </summary>
    public int RotationAngle => (int)Rotation;

    /// <summary>
    /// Gets whether the current rotation results in landscape orientation.
    /// </summary>
    public bool IsLandscapeRotation => Rotation is Rotation.Deg90 or Rotation.Deg270;
}
