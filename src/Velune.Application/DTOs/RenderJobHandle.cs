namespace Velune.Application.DTOs;

public sealed record RenderJobHandle(
    Guid JobId,
    Task<RenderResult> Completion);
