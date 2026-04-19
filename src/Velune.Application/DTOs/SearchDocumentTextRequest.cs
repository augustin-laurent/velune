using Velune.Domain.Documents;

namespace Velune.Application.DTOs;

public sealed record SearchDocumentTextRequest(
    DocumentTextIndex Index,
    SearchQuery Query);
