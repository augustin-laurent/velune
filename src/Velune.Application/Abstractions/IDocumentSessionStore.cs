using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Abstractions;

/// <summary>Manages open document sessions and tracks which session is active.</summary>
public interface IDocumentSessionStore
{
    /// <summary>Gets all currently open document sessions.</summary>
    IReadOnlyList<IDocumentSession> Sessions
    {
        get;
    }

    /// <summary>Gets the identifier of the currently active session, or null if none.</summary>
    DocumentId? ActiveSessionId
    {
        get;
    }

    /// <summary>Gets the currently active document session, or null if none.</summary>
    IDocumentSession? Current
    {
        get;
    }

    /// <summary>Gets whether an active session exists.</summary>
    bool HasCurrent
    {
        get;
    }

    /// <summary>Gets the metadata of the active session, or null if none.</summary>
    DocumentMetadata? CurrentMetadata
    {
        get;
    }

    /// <summary>Gets the viewport state of the active session, or null if none.</summary>
    ViewportState? CurrentViewport
    {
        get;
    }

    /// <summary>Replaces all sessions with the specified session and makes it active.</summary>
    /// <param name="session">The session to set as the sole active session.</param>
    void SetCurrent(IDocumentSession session);

    /// <summary>Adds a session to the store.</summary>
    /// <param name="session">The session to add.</param>
    /// <param name="makeActive">Whether to activate the session immediately.</param>
    void Add(IDocumentSession session, bool makeActive);

    /// <summary>Attempts to activate the session with the specified identifier.</summary>
    /// <param name="documentId">The document identifier to activate.</param>
    /// <returns>True if the session was found and activated; otherwise false.</returns>
    bool TryActivate(DocumentId documentId);

    /// <summary>Removes the session with the specified identifier.</summary>
    /// <param name="documentId">The document identifier to remove.</param>
    /// <returns>True if the session was found and removed; otherwise false.</returns>
    bool Remove(DocumentId documentId);

    /// <summary>Updates the viewport state of the currently active session.</summary>
    /// <param name="viewport">The new viewport state.</param>
    void UpdateViewport(ViewportState viewport);

    /// <summary>Updates the viewport state of the specified session.</summary>
    /// <param name="documentId">The target document identifier.</param>
    /// <param name="viewport">The new viewport state.</param>
    void UpdateViewport(DocumentId documentId, ViewportState viewport);

    /// <summary>Removes all sessions from the store.</summary>
    void Clear();
}
