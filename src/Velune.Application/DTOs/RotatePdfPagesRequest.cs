using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

/// <summary>Request to rotate specific pages in a PDF file.</summary>
/// <param name="SourcePath">Path to the source PDF.</param>
/// <param name="OutputPath">Path for the output PDF.</param>
/// <param name="Pages">Zero-based indices of pages to rotate.</param>
/// <param name="Rotation">The rotation to apply.</param>
public sealed record RotatePdfPagesRequest(
    string SourcePath,
    string OutputPath,
    IReadOnlyList<int> Pages,
    Rotation Rotation);
