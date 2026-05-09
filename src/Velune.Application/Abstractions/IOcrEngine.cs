using Velune.Application.DTOs;
using Velune.Application.Results;

namespace Velune.Application.Abstractions;

/// <summary>Provides optical character recognition capabilities.</summary>
public interface IOcrEngine
{
    /// <summary>Gets information about the OCR engine and its capabilities.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Engine information or an error.</returns>
    Task<Result<OcrEngineInfo>> GetInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>Recognizes text on a single page.</summary>
    /// <param name="request">The page render data to perform OCR on.</param>
    /// <param name="preferredLanguages">Optional language hints for recognition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The recognized page content or an error.</returns>
    Task<Result<OcrPageContent>> RecognizePageAsync(
        OcrPageRequest request,
        IReadOnlyList<string>? preferredLanguages,
        CancellationToken cancellationToken = default);
}
