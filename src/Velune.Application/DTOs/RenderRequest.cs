using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

public sealed record RenderRequest(
    string JobKey,
    PageIndex PageIndex,
    double ZoomFactor,
    Rotation Rotation,
    int? RequestedWidth = null,
    int? RequestedHeight = null,
    RenderPriority Priority = RenderPriority.Viewer);
