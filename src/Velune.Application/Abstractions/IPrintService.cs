using Velune.Application.DTOs;
using Velune.Application.Results;

namespace Velune.Application.Abstractions;

/// <summary>Provides document printing capabilities.</summary>
public interface IPrintService
{
    /// <summary>Gets whether the platform supports the native print dialog.</summary>
    bool SupportsSystemPrintDialog
    {
        get;
    }

    /// <summary>Shows the system print dialog for the specified file.</summary>
    /// <param name="filePath">The path of the file to print.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> ShowSystemPrintDialogAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the list of available printers on the system.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The available printer destinations or an error.</returns>
    Task<Result<IReadOnlyList<PrintDestinationInfo>>> GetAvailablePrintersAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Prints a document using the specified request parameters.</summary>
    /// <param name="request">The print request with printer and options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> PrintAsync(
        PrintDocumentRequest request,
        CancellationToken cancellationToken = default);
}
