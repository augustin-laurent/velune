using Velune.Domain.Abstractions;

namespace Velune.Application.Abstractions;

/// <summary>A document session whose native resources can be explicitly released.</summary>
public interface IReleasableDocumentSession : IDocumentSession
{
    /// <summary>Releases native or unmanaged resources held by the session.</summary>
    void ReleaseResources();
}
