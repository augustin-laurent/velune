using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Domain.ValueObjects;

namespace Velune.Tests.Unit.Support;

public sealed class NoOpRenderOrchestrator : IRenderOrchestrator
{
    public DocumentId? CancelledDocumentId { get; private set; }

    public RenderJobHandle Submit(RenderRequest request)
    {
        throw new NotSupportedException("No-op orchestrator does not execute render jobs.");
    }

    public bool Cancel(Guid jobId)
    {
        return false;
    }

    public Task CancelDocumentJobsAsync(DocumentId documentId, CancellationToken cancellationToken = default)
    {
        CancelledDocumentId = documentId;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }
}
