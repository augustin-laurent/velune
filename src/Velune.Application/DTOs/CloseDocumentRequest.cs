using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

/// <summary>Request to close a document; if no ID is specified, closes the active document.</summary>
public sealed record CloseDocumentRequest(DocumentId? DocumentId = null);
