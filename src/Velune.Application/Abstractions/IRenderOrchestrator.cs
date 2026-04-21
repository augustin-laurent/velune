using Velune.Application.DTOs;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Abstractions;

public interface IRenderOrchestrator : IDisposable
{
    RenderJobHandle Submit(RenderRequest request);

    bool Cancel(Guid jobId);

    Task CancelDocumentJobsAsync(DocumentId documentId, CancellationToken cancellationToken = default);
}
