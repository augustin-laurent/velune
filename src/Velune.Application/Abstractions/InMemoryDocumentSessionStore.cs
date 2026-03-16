using Velune.Domain.Abstractions;

namespace Velune.Application.Abstractions;

public sealed class InMemoryDocumentSessionStore : IDocumentSessionStore
{
    public IDocumentSession? Current
    {
        get; private set;
    }

    public void SetCurrent(IDocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        Current = session;
    }

    public void Clear()
    {
        Current = null;
    }
}
