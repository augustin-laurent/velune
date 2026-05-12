using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;

namespace Velune.Application.UseCases;

/// <summary>Changes the zoom level of the active document viewport.</summary>
public sealed class ChangeZoomUseCase
{
    private readonly IDocumentSessionStore _sessionStore;

    /// <summary>Initializes a new instance of the <see cref="ChangeZoomUseCase"/> class.</summary>
    /// <param name="sessionStore">The store holding active document sessions.</param>
    public ChangeZoomUseCase(IDocumentSessionStore sessionStore)
    {
        ArgumentNullException.ThrowIfNull(sessionStore);
        _sessionStore = sessionStore;
    }

    /// <summary>Applies the requested zoom factor and mode to the active document viewport.</summary>
    /// <param name="request">The zoom change request containing factor and mode.</param>
    /// <returns>The updated viewport state, or a failure if no session is active.</returns>
    public Result<ViewportState> Execute(ChangeZoomRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        IDocumentSession? session = _sessionStore.Current;
        if (session is null)
        {
            return ResultFactory.Failure<ViewportState>(
                AppError.NotFound(
                    "document.session.missing",
                    "No active document session."));
        }

        ViewportState updatedViewport = session.Viewport.WithZoom(request.ZoomFactor, request.ZoomMode);
        IDocumentSession updatedSession = session.WithViewport(updatedViewport);

        _sessionStore.SetCurrent(updatedSession);

        return ResultFactory.Success(updatedViewport);
    }
}
