using Velune.Application.Results;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

/// <summary>Outcome of a page render operation.</summary>
/// <param name="JobId">Unique identifier of the render job.</param>
/// <param name="DocumentId">The document that was rendered.</param>
/// <param name="JobKey">Deduplication key for the render job.</param>
/// <param name="PageIndex">The page that was rendered.</param>
/// <param name="Duration">Time elapsed for the render.</param>
/// <param name="Page">The rendered page data, or null on failure.</param>
/// <param name="Error">Error details if the render failed.</param>
/// <param name="IsCanceled">Whether the render was canceled.</param>
/// <param name="IsObsolete">Whether the result is outdated by a newer request.</param>
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
    /// <summary>Gets whether the render completed successfully with a page.</summary>
    public bool IsSuccess => !IsCanceled && Error is null && Page is not null;

    /// <summary>Gets whether the render resulted in an error.</summary>
    public bool IsFailure => Error is not null;
}
