namespace Velune.Application.DTOs;

public sealed record DocumentTextAnalysisRequest(
    string JobKey,
    bool ForceOcr,
    IReadOnlyList<string>? PreferredLanguages = null);
