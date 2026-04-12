using Velune.Application.Results;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

public sealed record RenderResult(
    Guid JobId,
    DocumentId DocumentId,
    string JobKey,
    PageIndex PageIndex,
    TimeSpan Duration,
    RenderedPage? Page,
    AppError? Error,
    bool IsCanceled,
    bool IsObsolete)
{
    public bool IsSuccess => !IsCanceled && Error is null && Page is not null;
    public bool IsFailure => Error is not null;
}
