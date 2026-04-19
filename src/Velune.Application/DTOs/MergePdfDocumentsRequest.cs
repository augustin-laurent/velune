namespace Velune.Application.DTOs;

public sealed record MergePdfDocumentsRequest(
    IReadOnlyList<string> SourcePaths,
    string OutputPath);
