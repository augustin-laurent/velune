using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

/// <summary>Request to change the viewer zoom level and mode.</summary>
public sealed record ChangeZoomRequest(
    double ZoomFactor,
    ZoomMode ZoomMode);
