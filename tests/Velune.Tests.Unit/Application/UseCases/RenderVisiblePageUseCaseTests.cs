using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Application.UseCases;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Tests.Unit.Application.UseCases;

public sealed class RenderVisiblePageUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnInfrastructureError_WhenRendererThrowsInvalidOperation()
    {
        var store = new InMemoryDocumentSessionStore();
        store.SetCurrent(CreateSession());

        var useCase = new RenderVisiblePageUseCase(
            store,
            new ThrowingRenderService(new InvalidOperationException("render failed")));

        var result = await useCase.ExecuteAsync(
            new RenderPageRequest(new PageIndex(0), 1.0, Rotation.Deg0));

        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal("document.render.failed", result.Error.Code);
        Assert.Equal(ErrorType.Infrastructure, result.Error.Type);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnInfrastructureError_WhenRendererUsesDisposedResource()
    {
        var store = new InMemoryDocumentSessionStore();
        store.SetCurrent(CreateSession());

        var useCase = new RenderVisiblePageUseCase(
            store,
            new ThrowingRenderService(new ObjectDisposedException("PdfiumDocumentResource")));

        var result = await useCase.ExecuteAsync(
            new RenderPageRequest(new PageIndex(0), 1.0, Rotation.Deg0));

        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal("document.render.disposed", result.Error.Code);
        Assert.Equal(ErrorType.Infrastructure, result.Error.Type);
    }

    private static DocumentSession CreateSession()
    {
        return new DocumentSession(
            DocumentId.New(),
            new DocumentMetadata("test.pdf", "/tmp/test.pdf", DocumentType.Pdf, 100, 1),
            ViewportState.Default);
    }

    private sealed class ThrowingRenderService : IRenderService
    {
        private readonly Exception _exception;

        public ThrowingRenderService(Exception exception)
        {
            _exception = exception;
        }

        public Task<RenderedPage> RenderPageAsync(
            IDocumentSession session,
            PageIndex pageIndex,
            double zoomFactor,
            Rotation rotation,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }
    }
}
