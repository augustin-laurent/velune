using Velune.Application.Abstractions;
using Velune.Application.Results;

namespace Velune.Application.UseCases;

/// <summary>Opens the operating system print dialog for a document.</summary>
public sealed class ShowSystemPrintDialogUseCase
{
    private readonly IPrintService _printService;

    /// <summary>Initializes a new instance of the <see cref="ShowSystemPrintDialogUseCase"/> class.</summary>
    /// <param name="printService">The print service implementation.</param>
    public ShowSystemPrintDialogUseCase(IPrintService printService)
    {
        ArgumentNullException.ThrowIfNull(printService);
        _printService = printService;
    }

    /// <summary>Validates the file path and shows the system print dialog.</summary>
    /// <param name="filePath">The path to the document to print.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public Task<Result> ExecuteAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Task.FromResult(ResultFactory.Failure(
                AppError.Validation(
                    "print.path.empty",
                    "A document must be open before it can be printed.")));
        }

        if (!File.Exists(filePath))
        {
            return Task.FromResult(ResultFactory.Failure(
                AppError.NotFound(
                    "print.file.missing",
                    "The current document could not be found anymore.")));
        }

        return _printService.ShowSystemPrintDialogAsync(filePath, cancellationToken);
    }
}
