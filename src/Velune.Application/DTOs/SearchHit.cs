using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

public sealed record SearchHit(
    PageIndex PageIndex,
    int MatchStart,
    int MatchLength,
    string Excerpt,
    IReadOnlyList<NormalizedTextRegion> Regions,
    TextSourceKind SourceKind);
