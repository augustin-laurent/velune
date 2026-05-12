using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;

namespace Velune.Application.UseCases;

/// <summary>Merges multiple PDF documents into a single output file.</summary>
public sealed class MergePdfDocumentsUseCase
{
    private readonly IPdfDocumentStructureService _pdfDocumentStructureService;

    /// <summary>Initializes a new instance of the <see cref="MergePdfDocumentsUseCase"/> class.</summary>
    /// <param name="pdfDocumentStructureService">The service responsible for PDF structural operations.</param>
    public MergePdfDocumentsUseCase(IPdfDocumentStructureService pdfDocumentStructureService)
    {
        ArgumentNullException.ThrowIfNull(pdfDocumentStructureService);
        _pdfDocumentStructureService = pdfDocumentStructureService;
    }

    /// <summary>Merges the source documents and writes the combined result to the output path.</summary>
    /// <param name="request">The request containing source paths and output path.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The output file path on success, or a failure result.</returns>
    public Task<Result<string>> ExecuteAsync(
        MergePdfDocumentsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _pdfDocumentStructureService.MergeDocumentsAsync(
            request.SourcePaths,
            request.OutputPath,
            cancellationToken);
    }
}
