using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

public sealed record RotatePdfPagesRequest(
    string SourcePath,
    string OutputPath,
    IReadOnlyList<int> Pages,
    Rotation Rotation);
