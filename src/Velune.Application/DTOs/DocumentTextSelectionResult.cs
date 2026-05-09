using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

/// <summary>Result of a text selection operation on a document page.</summary>
/// <param name="PageIndex">The page the selection belongs to.</param>
/// <param name="SelectedText">The selected text content, or null if empty.</param>
/// <param name="Regions">Normalized regions covering the selected text.</param>
/// <param name="SourceKind">Whether the text came from embedded text or OCR.</param>
public sealed record DocumentTextSelectionResult(
    PageIndex PageIndex,
    string? SelectedText,
    IReadOnlyList<NormalizedTextRegion> Regions,
    TextSourceKind SourceKind)
{
    /// <summary>Gets whether the selection contains any text.</summary>
    public bool HasSelection =>
        !string.IsNullOrWhiteSpace(SelectedText) &&
        Regions.Count > 0;
}
