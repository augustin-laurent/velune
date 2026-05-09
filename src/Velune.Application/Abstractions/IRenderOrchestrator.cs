using Velune.Application.DTOs;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Abstractions;

/// <summary>Orchestrates background page rendering jobs.</summary>
public interface IRenderOrchestrator : IDisposable
{
    /// <summary>Submits a page render request for background processing.</summary>
    /// <param name="request">The render request to process.</param>
    /// <returns>A handle to track the submitted job.</returns>
    RenderJobHandle Submit(RenderRequest request);

    /// <summary>Cancels a previously submitted render job.</summary>
    /// <param name="jobId">The identifier of the job to cancel.</param>
    /// <returns>True if the job was found and cancelled; otherwise false.</returns>
    bool Cancel(Guid jobId);

    /// <summary>Cancels all pending render jobs for the specified document.</summary>
    /// <param name="documentId">The document whose jobs should be cancelled.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CancelDocumentJobsAsync(DocumentId documentId, CancellationToken cancellationToken = default);
}
