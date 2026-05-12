using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;

namespace Velune.Application.UseCases;

/// <summary>Rotates specified pages within a PDF document.</summary>
public sealed class RotatePdfPagesUseCase
{
    private readonly IPdfDocumentStructureService _pdfDocumentStructureService;

    /// <summary>Initializes a new instance of the <see cref="RotatePdfPagesUseCase"/> class.</summary>
    /// <param name="pdfDocumentStructureService">The PDF structure service.</param>
    public RotatePdfPagesUseCase(IPdfDocumentStructureService pdfDocumentStructureService)
    {
        ArgumentNullException.ThrowIfNull(pdfDocumentStructureService);
        _pdfDocumentStructureService = pdfDocumentStructureService;
    }

    /// <summary>Rotates the specified pages by the requested angle.</summary>
    /// <param name="request">The rotation request containing source, output, pages, and rotation.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing the output file path or an error.</returns>
    public Task<Result<string>> ExecuteAsync(
        RotatePdfPagesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _pdfDocumentStructureService.RotatePagesAsync(
            request.SourcePath,
            request.OutputPath,
            request.Pages,
            request.Rotation,
            cancellationToken);
    }
}
