using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;

namespace Velune.Application.UseCases;

public sealed class PrintDocumentUseCase
{
    private readonly IPrintService _printService;

    public PrintDocumentUseCase(IPrintService printService)
    {
        ArgumentNullException.ThrowIfNull(printService);
        _printService = printService;
    }

    public Task<Result> ExecuteAsync(
        PrintDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return Task.FromResult(ResultFactory.Failure(
                AppError.Validation(
                    "print.path.empty",
                    "A document must be open before it can be printed.")));
        }

        if (!File.Exists(request.FilePath))
        {
            return Task.FromResult(ResultFactory.Failure(
                AppError.NotFound(
                    "print.file.missing",
                    "The current document could not be found anymore.")));
        }

        if (request.Copies <= 0)
        {
            return Task.FromResult(ResultFactory.Failure(
                AppError.Validation(
                    "print.copies.invalid",
                    "The number of copies must be at least 1.")));
        }

        return _printService.PrintAsync(request, cancellationToken);
    }
}
