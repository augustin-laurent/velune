using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

public sealed record RenderPageRequest(
    PageIndex PageIndex,
    double ZoomFactor,
    Rotation Rotation
);
