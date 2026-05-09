namespace Velune.Application.DTOs;

/// <summary>Handle to a submitted render job, providing access to its completion task.</summary>
/// <param name="JobId">Unique identifier for the render job.</param>
/// <param name="Completion">Task that completes with the render result.</param>
public sealed record RenderJobHandle(
    Guid JobId,
    Task<RenderResult> Completion);
