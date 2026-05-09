using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;

namespace Velune.Application.UseCases;

/// <summary>Extracts specified pages from a PDF into a new document.</summary>
public sealed class ExtractPdfPagesUseCase
{
    private readonly IPdfDocumentStructureService _pdfDocumentStructureService;

    /// <summary>Initializes a new instance of the <see cref="ExtractPdfPagesUseCase"/> class.</summary>
    /// <param name="pdfDocumentStructureService">The service responsible for PDF structural operations.</param>
    public ExtractPdfPagesUseCase(IPdfDocumentStructureService pdfDocumentStructureService)
    {
        ArgumentNullException.ThrowIfNull(pdfDocumentStructureService);
        _pdfDocumentStructureService = pdfDocumentStructureService;
    }

    /// <summary>Extracts the specified pages and writes them to the output path.</summary>
    /// <param name="request">The request containing source path, output path, and pages to extract.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The output file path on success, or a failure result.</returns>
    public Task<Result<string>> ExecuteAsync(
        ExtractPdfPagesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _pdfDocumentStructureService.ExtractPagesAsync(
            request.SourcePath,
            request.OutputPath,
            request.Pages,
            cancellationToken);
    }
}
