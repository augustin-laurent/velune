using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

public sealed record CloseDocumentRequest(DocumentId? DocumentId = null);
