using Velune.Application.DTOs;
using Velune.Application.Results;

namespace Velune.Application.Abstractions;

/// <summary>Applies annotation markup onto PDF files.</summary>
public interface IPdfMarkupService
{
    /// <summary>Flattens annotations into the PDF and saves the result.</summary>
    /// <param name="request">The request describing the PDF and annotations to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The output file path or an error.</returns>
    Task<Result<string>> ApplyAnnotationsAsync(
        ApplyPdfAnnotationsRequest request,
        CancellationToken cancellationToken = default);
}
