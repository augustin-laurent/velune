namespace Velune.Presentation.ViewModels;

/// <summary>
/// Specifies the page range selection mode for printing.
/// </summary>
public enum PrintPageRangeChoice
{
    /// <summary>
    /// Print all pages.
    /// </summary>
    AllPages,

    /// <summary>
    /// Print only the current page.
    /// </summary>
    CurrentPage,

    /// <summary>
    /// Print a user-specified custom range.
    /// </summary>
    CustomRange
}
