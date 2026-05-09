using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

/// <summary>Request to render a single page at a given zoom and rotation.</summary>
/// <param name="PageIndex">The page to render.</param>
/// <param name="ZoomFactor">Zoom multiplier (1.0 = 100%).</param>
/// <param name="Rotation">Rotation to apply to the page.</param>
public sealed record RenderPageRequest(
    PageIndex PageIndex,
    double ZoomFactor,
    Rotation Rotation
);
