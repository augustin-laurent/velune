using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Application.UseCases;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Velune.Tests.Unit.Support;

namespace Velune.Tests.Unit.Application.UseCases;

public sealed class CloseDocumentUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldClearCurrentSession()
    {
        var store = new InMemoryDocumentSessionStore();
        var session = new ReleasableDocumentSession();
        store.SetCurrent(session);
        using var renderOrchestrator = new NoOpRenderOrchestrator();
        var useCase = new CloseDocumentUseCase(store, NoOpPerformanceMetrics.Instance, renderOrchestrator);

        Result<bool> result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.Null(store.Current);
        Assert.True(session.ResourcesReleased);
        Assert.Equal(session.Id, renderOrchestrator.CancelledDocumentId);
    }

    [Fact]
    public async Task ExecuteAsync_WithDocumentId_ShouldCloseOnlyRequestedSession()
    {
        var store = new InMemoryDocumentSessionStore();
        var activeSession = new ReleasableDocumentSession("active.pdf");
        var inactiveSession = new ReleasableDocumentSession("inactive.pdf");
        store.Add(activeSession, makeActive: true);
        store.Add(inactiveSession, makeActive: false);
        using var renderOrchestrator = new NoOpRenderOrchestrator();
        var useCase = new CloseDocumentUseCase(store, NoOpPerformanceMetrics.Instance, renderOrchestrator);

        Result<bool> result = await useCase.ExecuteAsync(new CloseDocumentRequest(inactiveSession.Id));

        Assert.True(result.IsSuccess);
        Assert.Single(store.Sessions);
        Assert.Same(activeSession, store.Current);
        Assert.True(inactiveSession.ResourcesReleased);
        Assert.False(activeSession.ResourcesReleased);
        Assert.Equal(inactiveSession.Id, renderOrchestrator.CancelledDocumentId);
    }

    private sealed class ReleasableDocumentSession : IReleasableDocumentSession
    {
        public ReleasableDocumentSession(string fileName = "test.pdf")
        {
            Id = DocumentId.New();
            Metadata = new DocumentMetadata(fileName, $"/tmp/{fileName}", DocumentType.Pdf, 1024, 1);
            Viewport = ViewportState.Default;
        }

        public DocumentId Id
        {
            get;
        }

        public DocumentMetadata Metadata
        {
            get;
        }

        public ViewportState Viewport
        {
            get; private set;
        }

        public bool ResourcesReleased
        {
            get; private set;
        }

        public IDocumentSession WithViewport(ViewportState viewport)
        {
            Viewport = viewport;
            return this;
        }

        public void ReleaseResources()
        {
            ResourcesReleased = true;
        }
    }
}
