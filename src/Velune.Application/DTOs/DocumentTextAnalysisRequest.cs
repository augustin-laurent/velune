namespace Velune.Application.DTOs;

/// <summary>Request to extract and index text content from a document.</summary>
public sealed record DocumentTextAnalysisRequest(
    string JobKey,
    bool ForceOcr,
    IReadOnlyList<string>? PreferredLanguages = null);
