namespace Velune.Application.DTOs;

/// <summary>Handle to a running text analysis job, providing its ID and completion task.</summary>
public sealed record DocumentTextJobHandle(
    Guid JobId,
    Task<DocumentTextAnalysisResult> Completion);
