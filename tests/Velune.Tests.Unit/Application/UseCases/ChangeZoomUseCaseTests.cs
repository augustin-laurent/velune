using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Application.UseCases;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Tests.Unit.Application.UseCases;

public sealed class ChangeZoomUseCaseTests
{
    [Fact]
    public void Execute_ShouldReturnNotFound_WhenNoSessionIsOpen()
    {
        var store = new InMemoryDocumentSessionStore();
        var useCase = new ChangeZoomUseCase(store);

        var result = useCase.Execute(new ChangeZoomRequest(1.5, ZoomMode.FitToWidth));

        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal("document.session.missing", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public void Execute_ShouldUpdateViewportZoom_WhenSessionExists()
    {
        var store = CreateStore();
        var useCase = new ChangeZoomUseCase(store);

        var result = useCase.Execute(new ChangeZoomRequest(1.75, ZoomMode.FitToPage));

        Assert.True(result.IsSuccess);
        Assert.NotNull(store.Current);
        Assert.Equal(1.75, store.Current.Viewport.ZoomFactor);
        Assert.Equal(ZoomMode.FitToPage, store.Current.Viewport.ZoomMode);
    }

    private static InMemoryDocumentSessionStore CreateStore()
    {
        var store = new InMemoryDocumentSessionStore();
        store.SetCurrent(new DocumentSession(
            DocumentId.New(),
            new DocumentMetadata("test.pdf", "/tmp/test.pdf", DocumentType.Pdf, 1024, 3),
            ViewportState.Default));

        return store;
    }
}
