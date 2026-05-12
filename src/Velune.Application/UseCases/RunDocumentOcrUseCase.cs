using Velune.Application.Abstractions;
using Velune.Application.DTOs;

namespace Velune.Application.UseCases;

/// <summary>Submits an OCR analysis job for the active document.</summary>
public sealed class RunDocumentOcrUseCase
{
    private const string DefaultJobKey = "search:ocr";
    private readonly IDocumentTextAnalysisOrchestrator _orchestrator;

    /// <summary>Initializes a new instance of the <see cref="RunDocumentOcrUseCase"/> class.</summary>
    /// <param name="orchestrator">The text analysis orchestrator.</param>
    public RunDocumentOcrUseCase(IDocumentTextAnalysisOrchestrator orchestrator)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        _orchestrator = orchestrator;
    }

    /// <summary>Submits an OCR job with optional preferred languages.</summary>
    /// <param name="preferredLanguages">Optional list of preferred OCR languages.</param>
    /// <returns>A handle to track the submitted OCR job.</returns>
    public DocumentTextJobHandle Execute(IReadOnlyList<string>? preferredLanguages = null)
    {
        return _orchestrator.Submit(
            new DocumentTextAnalysisRequest(
                DefaultJobKey,
                ForceOcr: true,
                preferredLanguages));
    }
}
