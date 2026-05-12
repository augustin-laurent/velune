using Velune.Application.Results;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

/// <summary>Result of a document text analysis job, containing the text index or error details.</summary>
public sealed record DocumentTextAnalysisResult(
    Guid JobId,
    DocumentId DocumentId,
    string JobKey,
    TimeSpan Duration,
    DocumentTextIndex? Index,
    AppError? Error,
    bool IsCanceled,
    bool IsObsolete,
    bool RequiresOcr)
{
    /// <summary>Indicates the analysis completed successfully with a valid index.</summary>
    public bool IsSuccess => !IsCanceled && Error is null && Index is not null;

    /// <summary>Indicates the analysis failed with an error.</summary>
    public bool IsFailure => Error is not null;
}
