using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

public sealed record DocumentTextSelectionResult(
    PageIndex PageIndex,
    string? SelectedText,
    IReadOnlyList<NormalizedTextRegion> Regions,
    TextSourceKind SourceKind)
{
    public bool HasSelection =>
        !string.IsNullOrWhiteSpace(SelectedText) &&
        Regions.Count > 0;
}
