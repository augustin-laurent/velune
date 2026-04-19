namespace Velune.Presentation.Search;

public sealed record SearchHighlightItem(
    double Left,
    double Top,
    double Width,
    double Height,
    bool IsPrimary);
