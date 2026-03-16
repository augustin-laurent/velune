using Velune.Application.Abstractions;
using Velune.Application.UseCases;

namespace Velune.Tests.Unit.Application.UseCases;

public sealed class CloseDocumentUseCaseTests
{
    [Fact]
    public void Execute_ShouldClearCurrentSession()
    {
        var store = new InMemoryDocumentSessionStore();
        var useCase = new CloseDocumentUseCase(store);

        var result = useCase.Execute();

        Assert.True(result.IsSuccess);
        Assert.Null(store.Current);
    }
}
