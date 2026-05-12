using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Abstractions;

/// <summary>Thread-safe in-memory implementation of <see cref="IDocumentSessionStore"/>.</summary>
public sealed class InMemoryDocumentSessionStore : IDocumentSessionStore
{
    private readonly object _gate = new();
    private readonly Dictionary<DocumentId, IDocumentSession> _sessions = [];
    private DocumentId? _activeSessionId;

    public IReadOnlyList<IDocumentSession> Sessions
    {
        get
        {
            lock (_gate)
            {
                return [.. _sessions.Values];
            }
        }
    }

    public DocumentId? ActiveSessionId
    {
        get
        {
            lock (_gate)
            {
                return _activeSessionId;
            }
        }
    }

    public IDocumentSession? Current
    {
        get
        {
            lock (_gate)
            {
                return _activeSessionId is { } activeSessionId &&
                       _sessions.TryGetValue(activeSessionId, out IDocumentSession? session)
                    ? session
                    : null;
            }
        }
    }

    public bool HasCurrent => Current is not null;

    public DocumentMetadata? CurrentMetadata => Current?.Metadata;

    public ViewportState? CurrentViewport => Current?.Viewport;

    public void SetCurrent(IDocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        lock (_gate)
        {
            _sessions.Clear();
            _sessions[session.Id] = session;
            _activeSessionId = session.Id;
        }
    }

    public void Add(IDocumentSession session, bool makeActive)
    {
        ArgumentNullException.ThrowIfNull(session);

        lock (_gate)
        {
            _sessions[session.Id] = session;
            if (makeActive || _activeSessionId is null)
            {
                _activeSessionId = session.Id;
            }
        }
    }

    public bool TryActivate(DocumentId documentId)
    {
        lock (_gate)
        {
            if (!_sessions.ContainsKey(documentId))
            {
                return false;
            }

            _activeSessionId = documentId;
            return true;
        }
    }

    public bool Remove(DocumentId documentId)
    {
        lock (_gate)
        {
            if (!_sessions.Remove(documentId))
            {
                return false;
            }

            if (_activeSessionId == documentId)
            {
                _activeSessionId = _sessions.Count > 0
                    ? _sessions.Keys.First()
                    : null;
            }

            return true;
        }
    }

    public void UpdateViewport(ViewportState viewport)
    {
        ArgumentNullException.ThrowIfNull(viewport);

        lock (_gate)
        {
            if (_activeSessionId is not { } activeSessionId ||
                !_sessions.TryGetValue(activeSessionId, out IDocumentSession? session))
            {
                throw new InvalidOperationException("No active document session.");
            }

            _sessions[activeSessionId] = session.WithViewport(viewport);
        }
    }

    public void UpdateViewport(DocumentId documentId, ViewportState viewport)
    {
        ArgumentNullException.ThrowIfNull(viewport);

        lock (_gate)
        {
            if (!_sessions.TryGetValue(documentId, out IDocumentSession? session))
            {
                throw new InvalidOperationException("The requested document session is not open.");
            }

            _sessions[documentId] = session.WithViewport(viewport);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _sessions.Clear();
            _activeSessionId = null;
        }
    }
}
