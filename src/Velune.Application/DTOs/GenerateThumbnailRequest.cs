using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

public sealed record GenerateThumbnailRequest(
    PageIndex PageIndex,
    int MaxWidth,
    int MaxHeight);
