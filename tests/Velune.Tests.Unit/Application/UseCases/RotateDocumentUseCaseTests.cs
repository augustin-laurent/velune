using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Application.UseCases;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Tests.Unit.Application.UseCases;

public sealed class RotateDocumentUseCaseTests
{
    [Fact]
    public void Execute_ShouldReturnNotFound_WhenNoSessionIsOpen()
    {
        var store = new InMemoryDocumentSessionStore();
        var useCase = new RotateDocumentUseCase(store);

        var result = useCase.Execute(new RotateDocumentRequest(Rotation.Deg90));

        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal("document.session.missing", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public void Execute_ShouldUpdateViewportRotation_WhenSessionExists()
    {
        var store = CreateStore();
        var useCase = new RotateDocumentUseCase(store);

        var result = useCase.Execute(new RotateDocumentRequest(Rotation.Deg270));

        Assert.True(result.IsSuccess);
        Assert.NotNull(store.Current);
        Assert.Equal(Rotation.Deg270, store.Current.Viewport.Rotation);
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
