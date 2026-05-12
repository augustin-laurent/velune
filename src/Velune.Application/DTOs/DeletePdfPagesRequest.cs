namespace Velune.Application.DTOs;

/// <summary>Request to delete specific pages from a PDF and save the result.</summary>
public sealed record DeletePdfPagesRequest(
    string SourcePath,
    string OutputPath,
    IReadOnlyList<int> Pages);
