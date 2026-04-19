namespace Velune.Application.DTOs;

public sealed record DocumentTextJobHandle(
    Guid JobId,
    Task<DocumentTextAnalysisResult> Completion);
