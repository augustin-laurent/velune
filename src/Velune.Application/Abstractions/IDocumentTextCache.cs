using Velune.Domain.Abstractions;
using Velune.Domain.Documents;

namespace Velune.Application.Abstractions;

public interface IDocumentTextCache
{
    bool TryGet(
        IDocumentSession session,
        string engineFingerprint,
        IReadOnlyList<string> languages,
        bool forceOcr,
        out DocumentTextIndex? index);

    void Store(
        IDocumentSession session,
        string engineFingerprint,
        IReadOnlyList<string> languages,
        bool forceOcr,
        DocumentTextIndex index);
}
