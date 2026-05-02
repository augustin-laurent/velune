namespace Velune.Application.DTOs;

public sealed record OpenDocumentRequest(
    string FilePath,
    DocumentOpenMode OpenMode = DocumentOpenMode.ReplaceCurrent);
