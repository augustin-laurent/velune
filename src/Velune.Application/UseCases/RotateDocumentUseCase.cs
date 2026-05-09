using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Domain.Documents;

namespace Velune.Application.UseCases;

/// <summary>Rotates the currently open document viewport.</summary>
public sealed class RotateDocumentUseCase
{
    private readonly IDocumentSessionStore _sessionStore;

    /// <summary>Initializes a new instance of the <see cref="RotateDocumentUseCase"/> class.</summary>
    /// <param name="sessionStore">The document session store.</param>
    public RotateDocumentUseCase(IDocumentSessionStore sessionStore)
    {
        ArgumentNullException.ThrowIfNull(sessionStore);
        _sessionStore = sessionStore;
    }

    /// <summary>Applies the requested rotation to the active document viewport.</summary>
    /// <param name="request">The rotation request.</param>
    /// <returns>A result containing the updated viewport state or an error.</returns>
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
