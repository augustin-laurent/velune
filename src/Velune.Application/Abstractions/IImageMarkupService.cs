using Velune.Application.DTOs;
using Velune.Application.Results;

namespace Velune.Application.Abstractions;

/// <summary>Applies annotation markup onto image files.</summary>
public interface IImageMarkupService
{
    /// <summary>Flattens annotations into the image and saves the result.</summary>
    /// <param name="request">The request describing the image and annotations to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The output file path or an error.</returns>
    Task<Result<string>> FlattenAnnotationsAsync(
        ApplyImageAnnotationsRequest request,
        CancellationToken cancellationToken = default);
}
