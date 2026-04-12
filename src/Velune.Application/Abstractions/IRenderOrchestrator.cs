using Velune.Application.DTOs;

namespace Velune.Application.Abstractions;

public interface IRenderOrchestrator : IDisposable
{
    RenderJobHandle Submit(RenderRequest request);

    bool Cancel(Guid jobId);
}
