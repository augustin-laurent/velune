using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

/// <summary>Request to generate a thumbnail image for a document page.</summary>
/// <param name="PageIndex">The page to generate a thumbnail for.</param>
/// <param name="MaxWidth">Maximum thumbnail width in pixels.</param>
/// <param name="MaxHeight">Maximum thumbnail height in pixels.</param>
public sealed record GenerateThumbnailRequest(
    PageIndex PageIndex,
    int MaxWidth,
    int MaxHeight);
