namespace Velune.Application.DTOs;

public sealed record ExtractPdfPagesRequest(
    string SourcePath,
    string OutputPath,
    IReadOnlyList<int> Pages);
