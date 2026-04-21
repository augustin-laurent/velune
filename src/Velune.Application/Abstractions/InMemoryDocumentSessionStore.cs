using Velune.Domain.Abstractions;
using Velune.Domain.Documents;

namespace Velune.Application.Abstractions;

public sealed class InMemoryDocumentSessionStore : IDocumentSessionStore
{
    private readonly object _gate = new();
    private IDocumentSession? _current;

    public IDocumentSession? Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
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
            _current = session;
        }
    }

    public void UpdateViewport(ViewportState viewport)
    {
        ArgumentNullException.ThrowIfNull(viewport);

        if (Current is null)
        {
            throw new InvalidOperationException("No active document session.");
        }

        lock (_gate)
        {
            if (_current is null)
            {
                throw new InvalidOperationException("No active document session.");
            }

            _current = _current.WithViewport(viewport);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _current = null;
        }
    }
}
