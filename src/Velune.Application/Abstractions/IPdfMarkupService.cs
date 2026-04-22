using Velune.Application.DTOs;
using Velune.Application.Results;

namespace Velune.Application.Abstractions;

public interface IPdfMarkupService
{
    Task<Result<string>> ApplyAnnotationsAsync(
        ApplyPdfAnnotationsRequest request,
        CancellationToken cancellationToken = default);
}
