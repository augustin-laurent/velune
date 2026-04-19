using Velune.Application.Results;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

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
    public bool IsSuccess => !IsCanceled && Error is null && Index is not null;
    public bool IsFailure => Error is not null;
}
