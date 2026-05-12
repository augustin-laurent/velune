using Velune.Domain.Documents;

namespace Velune.Application.DTOs;

/// <summary>Request to search for text within a document's text index.</summary>
/// <param name="Index">The text index to search.</param>
/// <param name="Query">The search query to execute.</param>
public sealed record SearchDocumentTextRequest(
    DocumentTextIndex Index,
    SearchQuery Query);
