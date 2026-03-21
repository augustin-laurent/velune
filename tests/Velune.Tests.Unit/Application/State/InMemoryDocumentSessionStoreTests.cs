using Velune.Application.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Tests.Unit.Application.State;

public sealed class InMemoryDocumentSessionStoreTests
{
    [Fact]
    public void SetCurrent_ShouldExposeCurrentSession()
    {
        var store = new InMemoryDocumentSessionStore();

        var session = new DocumentSession(
            DocumentId.New(),
            new DocumentMetadata("test.pdf", "/tmp/test.pdf", DocumentType.Pdf, 100, 1),
            ViewportState.Default);

        store.SetCurrent(session);

        Assert.True(store.HasCurrent);
        Assert.NotNull(store.Current);
        Assert.Equal("test.pdf", store.CurrentMetadata?.FileName);
    }

    [Fact]
    public void Clear_ShouldResetState()
    {
        var store = new InMemoryDocumentSessionStore();

        var session = new DocumentSession(
            DocumentId.New(),
            new DocumentMetadata("test.pdf", "/tmp/test.pdf", DocumentType.Pdf, 100, 1),
            ViewportState.Default);

        store.SetCurrent(session);
        store.Clear();

        Assert.False(store.HasCurrent);
        Assert.Null(store.Current);
        Assert.Null(store.CurrentMetadata);
        Assert.Null(store.CurrentViewport);
    }
}
