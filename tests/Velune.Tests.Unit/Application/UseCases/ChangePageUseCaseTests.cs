using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Application.UseCases;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Tests.Unit.Application.UseCases;

public sealed class ChangePageUseCaseTests
{
    [Fact]
    public void Execute_ShouldReturnNotFound_WhenNoSessionIsOpen()
    {
        var store = new InMemoryDocumentSessionStore();
        var useCase = new ChangePageUseCase(store);

        var result = useCase.Execute(new ChangePageRequest(new PageIndex(1)));

        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal("document.session.missing", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public void Execute_ShouldReturnValidationError_WhenPageIsOutOfRange()
    {
        var store = CreateStore(pageCount: 3);
        var useCase = new ChangePageUseCase(store);

        var result = useCase.Execute(new ChangePageRequest(new PageIndex(5)));

        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal("document.page.out_of_range", result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Execute_ShouldUpdateCurrentViewport_WhenPageIsValid()
    {
        var store = CreateStore(pageCount: 3);
        var useCase = new ChangePageUseCase(store);

        var result = useCase.Execute(new ChangePageRequest(new PageIndex(2)));

        Assert.True(result.IsSuccess);
        Assert.NotNull(store.Current);
        Assert.Equal(2, store.Current.Viewport.CurrentPage.Value);
    }

    private static InMemoryDocumentSessionStore CreateStore(int pageCount)
    {
        var store = new InMemoryDocumentSessionStore();
        store.SetCurrent(new DocumentSession(
            DocumentId.New(),
            new DocumentMetadata("test.pdf", "/tmp/test.pdf", DocumentType.Pdf, 1024, pageCount),
            ViewportState.Default));

        return store;
    }
}
