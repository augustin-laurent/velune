using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Domain.Abstractions;

namespace Velune.Application.UseCases;

public sealed class OpenDocumentUseCase
{
    private readonly IDocumentOpener _documentOpener;
    private readonly IDocumentSessionStore _sessionStore;

    public OpenDocumentUseCase(
        IDocumentOpener documentOpener,
        IDocumentSessionStore sessionStore)
    {
        ArgumentNullException.ThrowIfNull(documentOpener);
        ArgumentNullException.ThrowIfNull(sessionStore);

        _documentOpener = documentOpener;
        _sessionStore = sessionStore;
    }

    public async Task<Result<IDocumentSession>> ExecuteAsync(
        OpenDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return Result.Failure<IDocumentSession>("File path cannot be empty.");
        }

        var session = await _documentOpener.OpenAsync(request.FilePath, cancellationToken);
        _sessionStore.SetCurrent(session);

        return Result.Success(session);
    }
}
