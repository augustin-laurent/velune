using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

/// <summary>Full render request with priority and caching options.</summary>
/// <param name="JobKey">Deduplication key for the render job.</param>
/// <param name="PageIndex">The page to render.</param>
/// <param name="ZoomFactor">Zoom multiplier (1.0 = 100%).</param>
/// <param name="Rotation">Rotation to apply to the page.</param>
/// <param name="RequestedWidth">Optional target width in pixels.</param>
/// <param name="RequestedHeight">Optional target height in pixels.</param>
/// <param name="Priority">Render priority level.</param>
/// <param name="UseThumbnailDiskCache">Whether to use the thumbnail disk cache.</param>
public sealed record RenderRequest(
    string JobKey,
    PageIndex PageIndex,
    double ZoomFactor,
    Rotation Rotation,
    int? RequestedWidth = null,
    int? RequestedHeight = null,
    RenderPriority Priority = RenderPriority.Viewer,
    bool UseThumbnailDiskCache = true);
