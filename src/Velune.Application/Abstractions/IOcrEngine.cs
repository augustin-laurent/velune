using Velune.Application.DTOs;
using Velune.Application.Results;

namespace Velune.Application.Abstractions;

public interface IOcrEngine
{
    Task<Result<OcrEngineInfo>> GetInfoAsync(CancellationToken cancellationToken = default);

    Task<Result<OcrPageContent>> RecognizePageAsync(
        OcrPageRequest request,
        IReadOnlyList<string>? preferredLanguages,
        CancellationToken cancellationToken = default);
}
