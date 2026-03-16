using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

public sealed record ChangeZoomRequest(
    double ZoomFactor,
    ZoomMode ZoomMode);
