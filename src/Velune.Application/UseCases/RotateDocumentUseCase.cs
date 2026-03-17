using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Domain.Documents;

namespace Velune.Application.UseCases;

public sealed class RotateDocumentUseCase
{
    private readonly IDocumentSessionStore _sessionStore;

    public RotateDocumentUseCase(IDocumentSessionStore sessionStore)
    {
        ArgumentNullException.ThrowIfNull(sessionStore);
        _sessionStore = sessionStore;
    }

    public Result<ViewportState> Execute(RotateDocumentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = _sessionStore.Current;
        if (session is null)
        {
            return ResultFactory.Failure<ViewportState>(
                AppError.NotFound(
                    "document.session.missing",
                    "No active document session."));
        }

        var updatedViewport = session.Viewport.WithRotation(request.Rotation);
        var updatedSession = session.WithViewport(updatedViewport);

        _sessionStore.SetCurrent(updatedSession);

        return ResultFactory.Success(updatedViewport);
    }
}
