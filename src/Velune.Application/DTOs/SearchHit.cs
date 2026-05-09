using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

/// <summary>A single text search match within a document.</summary>
/// <param name="PageIndex">The page containing the match.</param>
/// <param name="MatchStart">Character offset of the match start in the page text.</param>
/// <param name="MatchLength">Length of the matched text in characters.</param>
/// <param name="Excerpt">Short text excerpt around the match.</param>
/// <param name="Regions">Normalized regions highlighting the match.</param>
/// <param name="SourceKind">Whether the text came from embedded text or OCR.</param>
public sealed record SearchHit(
    PageIndex PageIndex,
    int MatchStart,
    int MatchLength,
    string Excerpt,
    IReadOnlyList<NormalizedTextRegion> Regions,
    TextSourceKind SourceKind);
