using Velune.Domain.Abstractions;
using Velune.Domain.Documents;

namespace Velune.Application.Abstractions;

/// <summary>Caches extracted text indexes for documents to avoid redundant processing.</summary>
public interface IDocumentTextCache
{
    /// <summary>Attempts to retrieve a cached text index for the given session and parameters.</summary>
    /// <param name="session">The document session to look up.</param>
    /// <param name="engineFingerprint">Identifies the OCR engine version used.</param>
    /// <param name="languages">The languages used during extraction.</param>
    /// <param name="forceOcr">Whether OCR was forced regardless of embedded text.</param>
    /// <param name="index">The cached index if found; otherwise null.</param>
    /// <returns>True if a cached index was found; otherwise false.</returns>
    bool TryGet(
        IDocumentSession session,
        string engineFingerprint,
        IReadOnlyList<string> languages,
        bool forceOcr,
        out DocumentTextIndex? index);

    /// <summary>Stores a text index in the cache for later retrieval.</summary>
    /// <param name="session">The document session to associate with.</param>
    /// <param name="engineFingerprint">Identifies the OCR engine version used.</param>
    /// <param name="languages">The languages used during extraction.</param>
    /// <param name="forceOcr">Whether OCR was forced regardless of embedded text.</param>
    /// <param name="index">The text index to cache.</param>
    void Store(
        IDocumentSession session,
        string engineFingerprint,
        IReadOnlyList<string> languages,
        bool forceOcr,
        DocumentTextIndex index);
}
