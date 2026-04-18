namespace Velune.Application.DTOs;

public sealed record ReorderPdfPagesRequest(
    string SourcePath,
    string OutputPath,
    IReadOnlyList<int> OrderedPages);
