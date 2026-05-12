namespace Velune.Domain.ValueObjects;

/// <summary>
/// Determines how the zoom factor is calculated for the viewport.
/// </summary>
public enum ZoomMode
{
    /// <summary>
    /// User-specified zoom level.
    /// </summary>
    Custom = 0,

    /// <summary>
    /// Automatically zoom to fit the entire page in the viewport.
    /// </summary>
    FitToPage = 1,

    /// <summary>
    /// Automatically zoom to fit the page width to the viewport width.
    /// </summary>
    FitToWidth = 2
}
