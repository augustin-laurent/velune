using Velune.Application.Abstractions;
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

        var result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.Null(store.Current);
        Assert.True(session.ResourcesReleased);
        Assert.Equal(session.Id, renderOrchestrator.CancelledDocumentId);
    }

    private sealed class ReleasableDocumentSession : IReleasableDocumentSession
    {
        public ReleasableDocumentSession()
        {
            Id = DocumentId.New();
            Metadata = new DocumentMetadata("test.pdf", "/tmp/test.pdf", DocumentType.Pdf, 1024, 1);
            Viewport = ViewportState.Default;
        }

        public DocumentId Id { get; }

        public DocumentMetadata Metadata { get; }

        public ViewportState Viewport { get; private set; }

        public bool ResourcesReleased { get; private set; }

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
