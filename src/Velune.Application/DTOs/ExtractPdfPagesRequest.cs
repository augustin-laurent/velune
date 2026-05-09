namespace Velune.Application.DTOs;

/// <summary>Request to extract specific pages from a PDF into a new file.</summary>
/// <param name="SourcePath">Path to the source PDF.</param>
/// <param name="OutputPath">Path for the output PDF.</param>
/// <param name="Pages">Zero-based page indices to extract.</param>
public sealed record ExtractPdfPagesRequest(
    string SourcePath,
    string OutputPath,
    IReadOnlyList<int> Pages);
