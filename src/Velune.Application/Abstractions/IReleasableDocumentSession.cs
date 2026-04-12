using Velune.Domain.Abstractions;

namespace Velune.Application.Abstractions;

public interface IReleasableDocumentSession : IDocumentSession
{
    void ReleaseResources();
}
