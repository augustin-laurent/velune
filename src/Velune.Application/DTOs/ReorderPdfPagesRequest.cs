namespace Velune.Application.DTOs;

/// <summary>Request to reorder pages in a PDF document.</summary>
/// <param name="SourcePath">Path to the source PDF.</param>
/// <param name="OutputPath">Path for the reordered output PDF.</param>
/// <param name="OrderedPages">New page order as a list of original page indices.</param>
public sealed record ReorderPdfPagesRequest(
    string SourcePath,
    string OutputPath,
    IReadOnlyList<int> OrderedPages);
