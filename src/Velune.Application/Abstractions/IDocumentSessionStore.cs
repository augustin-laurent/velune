using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Abstractions;

public interface IDocumentSessionStore
{
    IReadOnlyList<IDocumentSession> Sessions
    {
        get;
    }

    DocumentId? ActiveSessionId
    {
        get;
    }

    IDocumentSession? Current
    {
        get;
    }

    bool HasCurrent
    {
        get;
    }

    DocumentMetadata? CurrentMetadata
    {
        get;
    }

    ViewportState? CurrentViewport
    {
        get;
    }

    void SetCurrent(IDocumentSession session);

    void Add(IDocumentSession session, bool makeActive);

    bool TryActivate(DocumentId documentId);

    bool Remove(DocumentId documentId);

    void UpdateViewport(ViewportState viewport);

    void UpdateViewport(DocumentId documentId, ViewportState viewport);

    void Clear();
}
