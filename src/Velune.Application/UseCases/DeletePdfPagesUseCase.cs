using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;

namespace Velune.Application.UseCases;

/// <summary>Deletes specified pages from a PDF document.</summary>
public sealed class DeletePdfPagesUseCase
{
    private readonly IPdfDocumentStructureService _pdfDocumentStructureService;

    /// <summary>Initializes a new instance of the <see cref="DeletePdfPagesUseCase"/> class.</summary>
    /// <param name="pdfDocumentStructureService">The service responsible for PDF structural operations.</param>
    public DeletePdfPagesUseCase(IPdfDocumentStructureService pdfDocumentStructureService)
    {
        ArgumentNullException.ThrowIfNull(pdfDocumentStructureService);
        _pdfDocumentStructureService = pdfDocumentStructureService;
    }

    /// <summary>Deletes the specified pages and writes the result to the output path.</summary>
    /// <param name="request">The request containing source path, output path, and pages to delete.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The output file path on success, or a failure result.</returns>
    public Task<Result<string>> ExecuteAsync(
        DeletePdfPagesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _pdfDocumentStructureService.DeletePagesAsync(
            request.SourcePath,
            request.OutputPath,
            request.Pages,
            cancellationToken);
    }
}
