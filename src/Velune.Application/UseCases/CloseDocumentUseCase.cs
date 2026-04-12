using Velune.Application.Abstractions;
using Velune.Application.Results;

namespace Velune.Application.UseCases;

public sealed class CloseDocumentUseCase
{
    private readonly IPerformanceMetrics _performanceMetrics;
    private readonly IDocumentSessionStore _sessionStore;

    public CloseDocumentUseCase(
        IDocumentSessionStore sessionStore,
        IPerformanceMetrics performanceMetrics)
    {
        ArgumentNullException.ThrowIfNull(sessionStore);
        ArgumentNullException.ThrowIfNull(performanceMetrics);
        _sessionStore = sessionStore;
        _performanceMetrics = performanceMetrics;
    }

    public Result<bool> Execute()
    {
        if (_sessionStore.Current is not null)
        {
            _performanceMetrics.Clear(_sessionStore.Current.Id);
        }

        _sessionStore.Clear();
        return ResultFactory.Success(true);
    }
}
