using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;

namespace Velune.Application.UseCases;

/// <summary>Prints a document using the configured print service.</summary>
public sealed class PrintDocumentUseCase
{
    private readonly IPrintService _printService;

    /// <summary>Initializes a new instance of the <see cref="PrintDocumentUseCase"/> class.</summary>
    /// <param name="printService">The print service implementation.</param>
    public PrintDocumentUseCase(IPrintService printService)
    {
        ArgumentNullException.ThrowIfNull(printService);
        _printService = printService;
    }

    /// <summary>Validates and executes the print request.</summary>
    /// <param name="request">The print document request details.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure of the print operation.</returns>
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
