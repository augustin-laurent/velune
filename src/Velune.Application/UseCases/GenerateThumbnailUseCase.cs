using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;

namespace Velune.Application.UseCases;

public sealed class GenerateThumbnailUseCase
{
    private readonly IDocumentSessionStore _sessionStore;
    private readonly IThumbnailService _thumbnailService;

    public GenerateThumbnailUseCase(
        IDocumentSessionStore sessionStore,
        IThumbnailService thumbnailService)
    {
        ArgumentNullException.ThrowIfNull(sessionStore);
        ArgumentNullException.ThrowIfNull(thumbnailService);

        _sessionStore = sessionStore;
        _thumbnailService = thumbnailService;
    }

    public async Task<Result<RenderedPage>> ExecuteAsync(
        GenerateThumbnailRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = _sessionStore.Current;
        if (session is null)
        {
            return ResultFactory.Failure<RenderedPage>(
                AppError.NotFound(
                    "document.session.missing",
                    "No active document session."));
        }

        var thumbnail = await _thumbnailService.GenerateThumbnailAsync(
            session,
            request.PageIndex,
            request.MaxWidth,
            request.MaxHeight,
            cancellationToken);

        return ResultFactory.Success(thumbnail);
    }
}
