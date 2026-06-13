using System.Globalization;
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
    /// <param name="automationNameFormat">Localized page label format.</param>
    public PageOrganizerItemViewModel(
        int pageNumber,
        Rotation rotation = Rotation.Deg0,
        string automationNameFormat = "Page {0}")
    {
        PageNumber = pageNumber;
        OriginalPageNumber = pageNumber;
        Rotation = rotation;
        AutomationNameFormat = automationNameFormat;
    }

    /// <summary>
    /// Gets the original page number before any reordering.
    /// </summary>
    public int OriginalPageNumber
    {
        get;
    }

    public string AutomationNameFormat
    {
        get;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AutomationId))]
    [NotifyPropertyChangedFor(nameof(AutomationName))]
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

    public string AutomationId => $"PageOrganizerPageItem_{PageNumber}";

    public string AutomationName => string.Format(CultureInfo.CurrentCulture, AutomationNameFormat, PageNumber);

    /// <summary>
    /// Gets whether the current rotation results in landscape orientation.
    /// </summary>
    public bool IsLandscapeRotation => Rotation is Rotation.Deg90 or Rotation.Deg270;
}
