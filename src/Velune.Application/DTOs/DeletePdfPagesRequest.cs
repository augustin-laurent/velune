namespace Velune.Application.DTOs;

public sealed record DeletePdfPagesRequest(
    string SourcePath,
    string OutputPath,
    IReadOnlyList<int> Pages);
