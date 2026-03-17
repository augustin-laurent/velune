using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Application.UseCases;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Tests.Unit.Application.UseCases;

public sealed class OpenDocumentUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldFail_WhenFilePathIsEmpty()
    {
        var opener = new FakeDocumentOpener();
        var store = new InMemoryDocumentSessionStore();
        var useCase = new OpenDocumentUseCase(opener, store);

        var result = await useCase.ExecuteAsync(new OpenDocumentRequest(string.Empty));

        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal("document.path.empty", result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStoreSession_WhenOpenSucceeds()
    {
        var opener = new FakeDocumentOpener();
        var store = new InMemoryDocumentSessionStore();
        var useCase = new OpenDocumentUseCase(opener, store);

        var result = await useCase.ExecuteAsync(new OpenDocumentRequest("/tmp/test.pdf"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(store.Current);
    }

    private sealed class FakeDocumentOpener : IDocumentOpener
    {
        public Task<IDocumentSession> OpenAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            IDocumentSession session = new FakeDocumentSession(
                DocumentId.New(),
                new DocumentMetadata("test.pdf", filePath, DocumentType.Pdf, 100, 1),
                ViewportState.Default);

            return Task.FromResult(session);
        }
    }

    private sealed class FakeDocumentSession : IDocumentSession
    {
        public FakeDocumentSession(
            DocumentId id,
            DocumentMetadata metadata,
            ViewportState viewport)
        {
            Id = id;
            Metadata = metadata;
            Viewport = viewport;
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
            get;
        }

        public IDocumentSession WithViewport(ViewportState viewport) =>
            new FakeDocumentSession(Id, Metadata, viewport);
    }
}
