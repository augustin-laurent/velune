namespace Velune.Presentation.Search;

/// <summary>
/// Represents a positioned search highlight rectangle on the document viewer.
/// </summary>
/// <param name="Left">Left offset in display coordinates.</param>
/// <param name="Top">Top offset in display coordinates.</param>
/// <param name="Width">Width of the highlight region.</param>
/// <param name="Height">Height of the highlight region.</param>
/// <param name="IsPrimary">Whether this is the currently focused search hit.</param>
public sealed record SearchHighlightItem(
    double Left,
    double Top,
    double Width,
    double Height,
    bool IsPrimary);
