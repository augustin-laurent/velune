namespace Velune.Application.DTOs;

/// <summary>Request to open a document file.</summary>
/// <param name="FilePath">Path to the document to open.</param>
/// <param name="OpenMode">How to handle the currently open document.</param>
public sealed record OpenDocumentRequest(
    string FilePath,
    DocumentOpenMode OpenMode = DocumentOpenMode.ReplaceCurrent);
