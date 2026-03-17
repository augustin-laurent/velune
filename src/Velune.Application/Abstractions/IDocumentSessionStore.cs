using Velune.Domain.Abstractions;

namespace Velune.Application.Abstractions;

public interface IDocumentSessionStore
{
    IDocumentSession? Current
    {
        get;
    }

    void SetCurrent(IDocumentSession session);

    void Clear();
}
