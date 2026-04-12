using Velune.Application.Abstractions;
using Velune.Application.UseCases;
using Velune.Tests.Unit.Support;

namespace Velune.Tests.Unit.Application.UseCases;

public sealed class CloseDocumentUseCaseTests
{
    [Fact]
    public void Execute_ShouldClearCurrentSession()
    {
        var store = new InMemoryDocumentSessionStore();
        var useCase = new CloseDocumentUseCase(store, NoOpPerformanceMetrics.Instance);

        var result = useCase.Execute();

        Assert.True(result.IsSuccess);
        Assert.Null(store.Current);
    }
}
