using Velune.Application.DTOs;
using Velune.Application.Results;

namespace Velune.Application.Abstractions;

public interface IImageMarkupService
{
    Task<Result<string>> FlattenAnnotationsAsync(
        ApplyImageAnnotationsRequest request,
        CancellationToken cancellationToken = default);
}
