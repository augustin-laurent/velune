using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Abstractions;

/// <summary>Resolves text selection ranges within a document.</summary>
public interface IDocumentTextSelectionService
{
    /// <summary>Resolves the text content for a given selection request.</summary>
    /// <param name="request">The selection request describing the region to resolve.</param>
    /// <returns>The resolved text selection result or an error.</returns>
    Result<DocumentTextSelectionResult> Resolve(DocumentTextSelectionRequest request);

    /// <summary>Resolves a text selection by character index range.</summary>
    Result<DocumentTextSelectionResult> ResolveByRange(
        DocumentTextIndex index,
        PageIndex pageIndex,
        int startCharacterIndex,
        int endCharacterIndex);
}
