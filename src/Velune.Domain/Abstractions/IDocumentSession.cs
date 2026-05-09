using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Domain.Abstractions;

/// <summary>
/// Represents an open document with its metadata and current viewport state.
/// </summary>
public interface IDocumentSession
{
    /// <summary>
    /// Unique identifier for this session.
    /// </summary>
    DocumentId Id
    {
        get;
    }

    /// <summary>
    /// Metadata about the opened document (file name, size, page count, etc.).
    /// </summary>
    DocumentMetadata Metadata
    {
        get;
    }

    /// <summary>
    /// Current viewport state (page, zoom, rotation).
    /// </summary>
    ViewportState Viewport
    {
        get;
    }

    /// <summary>
    /// Returns a new session with the specified viewport state.
    /// </summary>
    /// <param name="viewport">The new viewport state.</param>
    /// <returns>A new session instance with the updated viewport.</returns>
    IDocumentSession WithViewport(ViewportState viewport);
}
