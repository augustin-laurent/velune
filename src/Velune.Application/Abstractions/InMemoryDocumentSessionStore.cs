using Velune.Domain.Abstractions;
using Velune.Domain.Documents;

namespace Velune.Application.Abstractions;

public sealed class InMemoryDocumentSessionStore : IDocumentSessionStore
{
    public IDocumentSession? Current
    {
        get; private set;
    }

    public bool HasCurrent => Current is not null;

    public DocumentMetadata? CurrentMetadata => Current?.Metadata;

    public ViewportState? CurrentViewport => Current?.Viewport;

    public void SetCurrent(IDocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        Current = session;
    }

    public void UpdateViewport(ViewportState viewport)
    {
        ArgumentNullException.ThrowIfNull(viewport);

        if (Current is null)
        {
            throw new InvalidOperationException("No active document session.");
        }

        Current = Current.WithViewport(viewport);
    }

    public void Clear()
    {
        Current = null;
    }
}
