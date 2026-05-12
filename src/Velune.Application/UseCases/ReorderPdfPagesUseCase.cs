using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;

namespace Velune.Application.UseCases;

/// <summary>Reorders pages within a PDF document.</summary>
public sealed class ReorderPdfPagesUseCase
{
    private readonly IPdfDocumentStructureService _pdfDocumentStructureService;

    /// <summary>Initializes a new instance of the <see cref="ReorderPdfPagesUseCase"/> class.</summary>
    /// <param name="pdfDocumentStructureService">The PDF structure service.</param>
    public ReorderPdfPagesUseCase(IPdfDocumentStructureService pdfDocumentStructureService)
    {
        ArgumentNullException.ThrowIfNull(pdfDocumentStructureService);
        _pdfDocumentStructureService = pdfDocumentStructureService;
    }

    /// <summary>Reorders PDF pages according to the specified order.</summary>
    /// <param name="request">The reorder request containing source, output, and page order.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing the output file path or an error.</returns>
    public Task<Result<string>> ExecuteAsync(
        ReorderPdfPagesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _pdfDocumentStructureService.ReorderPagesAsync(
            request.SourcePath,
            request.OutputPath,
            request.OrderedPages,
            cancellationToken);
    }
}
