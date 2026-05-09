namespace Velune.Application.DTOs;

/// <summary>Request to merge multiple PDF documents into a single output file.</summary>
/// <param name="SourcePaths">Paths to the PDF files to merge.</param>
/// <param name="OutputPath">Path for the merged output PDF.</param>
public sealed record MergePdfDocumentsRequest(
    IReadOnlyList<string> SourcePaths,
    string OutputPath);
