using Velune.Domain.Abstractions;
using Velune.Domain.Documents;

namespace Velune.Application.Abstractions;

public interface IDocumentSessionStore
{
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

    void UpdateViewport(ViewportState viewport);

    void Clear();
}
