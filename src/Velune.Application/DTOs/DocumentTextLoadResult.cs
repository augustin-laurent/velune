using Velune.Domain.Documents;

namespace Velune.Application.DTOs;

/// <summary>Result of loading document text, indicating whether OCR is needed or cache was used.</summary>
/// <param name="Index">The loaded text index, or null if text is unavailable.</param>
/// <param name="RequiresOcr">Whether OCR is needed to extract text.</param>
/// <param name="UsedCache">Whether the result was served from cache.</param>
public sealed record DocumentTextLoadResult(
    DocumentTextIndex? Index,
    bool RequiresOcr,
    bool UsedCache);
