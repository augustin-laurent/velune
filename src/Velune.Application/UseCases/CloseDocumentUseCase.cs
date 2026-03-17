using Velune.Application.Abstractions;
using Velune.Application.Results;

namespace Velune.Application.UseCases;

public sealed class CloseDocumentUseCase
{
    private readonly IDocumentSessionStore _sessionStore;

    public CloseDocumentUseCase(IDocumentSessionStore sessionStore)
    {
        ArgumentNullException.ThrowIfNull(sessionStore);
        _sessionStore = sessionStore;
    }

    public Result<bool> Execute()
    {
        _sessionStore.Clear();
        return ResultFactory.Success(true);
    }
}
