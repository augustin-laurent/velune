using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Application.UseCases;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Tests.Unit.Application.UseCases;

public sealed class GenerateThumbnailUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenNoSessionIsOpen()
    {
        var store = new InMemoryDocumentSessionStore();
        var service = new RecordingThumbnailService();
        var useCase = new GenerateThumbnailUseCase(store, service);

        var result = await useCase.ExecuteAsync(new GenerateThumbnailRequest(new PageIndex(0), 170, 150));

        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal("document.session.missing", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.False(service.WasCalled);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnGeneratedThumbnail_WhenSessionExists()
    {
        var store = new InMemoryDocumentSessionStore();
        store.SetCurrent(new DocumentSession(
            DocumentId.New(),
            new DocumentMetadata("test.pdf", "/tmp/test.pdf", DocumentType.Pdf, 1024, 2),
            ViewportState.Default));
        var service = new RecordingThumbnailService();
        var useCase = new GenerateThumbnailUseCase(store, service);

        var result = await useCase.ExecuteAsync(new GenerateThumbnailRequest(new PageIndex(1), 170, 150));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(service.WasCalled);
        Assert.Equal(1, service.PageIndex!.Value);
        Assert.Equal(170, service.MaxWidth);
        Assert.Equal(150, service.MaxHeight);
    }

    private sealed class RecordingThumbnailService : IThumbnailService
    {
        public bool WasCalled { get; private set; }
        public PageIndex? PageIndex { get; private set; }
        public int MaxWidth { get; private set; }
        public int MaxHeight { get; private set; }

        public Task<RenderedPage> GenerateThumbnailAsync(
            IDocumentSession session,
            PageIndex pageIndex,
            int maxWidth,
            int maxHeight,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            PageIndex = pageIndex;
            MaxWidth = maxWidth;
            MaxHeight = maxHeight;

            return Task.FromResult(new RenderedPage(pageIndex, [0, 0, 0, 255], 1, 1));
        }
    }
}
