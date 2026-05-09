using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;

namespace Velune.Application.Abstractions;

/// <summary>Loads and extracts text content from documents.</summary>
public interface IDocumentTextService
{
    /// <summary>Loads text from a document, using embedded text or OCR as needed.</summary>
    /// <param name="session">The document session to extract text from.</param>
    /// <param name="preferredLanguages">Optional language hints for OCR.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded text result or an error.</returns>
    Task<Result<DocumentTextLoadResult>> LoadAsync(
        IDocumentSession session,
        IReadOnlyList<string>? preferredLanguages,
        CancellationToken cancellationToken = default);

    /// <summary>Forces OCR on the document regardless of embedded text.</summary>
    /// <param name="session">The document session to process.</param>
    /// <param name="preferredLanguages">Optional language hints for OCR.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The OCR text index or an error.</returns>
    Task<Result<DocumentTextIndex>> RunOcrAsync(
        IDocumentSession session,
        IReadOnlyList<string>? preferredLanguages,
        CancellationToken cancellationToken = default);
}
