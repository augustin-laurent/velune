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

        var session = CreateSession("test.pdf");

        store.SetCurrent(session);
        store.Clear();

        Assert.False(store.HasCurrent);
        Assert.Null(store.Current);
        Assert.Null(store.CurrentMetadata);
        Assert.Null(store.CurrentViewport);
    }

    [Fact]
    public void Add_ShouldKeepMultipleSessionsAndActivateRequestedSession()
    {
        var store = new InMemoryDocumentSessionStore();
        var first = CreateSession("first.pdf");
        var second = CreateSession("second.pdf");

        store.Add(first, makeActive: true);
        store.Add(second, makeActive: true);

        Assert.Equal(2, store.Sessions.Count);
        Assert.Equal(second.Id, store.ActiveSessionId);
        Assert.Same(second, store.Current);
    }

    [Fact]
    public void TryActivate_ShouldSwitchCurrentSession()
    {
        var store = new InMemoryDocumentSessionStore();
        var first = CreateSession("first.pdf");
        var second = CreateSession("second.pdf");
        store.Add(first, makeActive: true);
        store.Add(second, makeActive: false);

        var activated = store.TryActivate(second.Id);

        Assert.True(activated);
        Assert.Same(second, store.Current);
    }

    [Fact]
    public void Remove_ShouldActivateRemainingSession_WhenActiveSessionIsRemoved()
    {
        var store = new InMemoryDocumentSessionStore();
        var first = CreateSession("first.pdf");
        var second = CreateSession("second.pdf");
        store.Add(first, makeActive: true);
        store.Add(second, makeActive: true);

        var removed = store.Remove(second.Id);

        Assert.True(removed);
        Assert.Single(store.Sessions);
        Assert.Same(first, store.Current);
    }

    [Fact]
    public void UpdateViewport_ByDocumentId_ShouldUpdateInactiveSession()
    {
        var store = new InMemoryDocumentSessionStore();
        var first = CreateSession("first.pdf");
        var second = CreateSession("second.pdf");
        store.Add(first, makeActive: true);
        store.Add(second, makeActive: false);

        store.UpdateViewport(second.Id, ViewportState.Default.WithPage(new PageIndex(2)));

        Assert.Equal(2, store.Sessions.Single(session => session.Id == second.Id).Viewport.CurrentPage.Value);
        Assert.Equal(0, store.Current?.Viewport.CurrentPage.Value);
    }

    private static DocumentSession CreateSession(string fileName)
    {
        return new DocumentSession(
            DocumentId.New(),
            new DocumentMetadata(fileName, $"/tmp/{fileName}", DocumentType.Pdf, 100, 3),
            ViewportState.Default);
    }
}
