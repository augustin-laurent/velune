using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;

namespace Velune.Application.Abstractions;

public interface IDocumentTextService
{
    Task<Result<DocumentTextLoadResult>> LoadAsync(
        IDocumentSession session,
        IReadOnlyList<string>? preferredLanguages,
        CancellationToken cancellationToken = default);

    Task<Result<DocumentTextIndex>> RunOcrAsync(
        IDocumentSession session,
        IReadOnlyList<string>? preferredLanguages,
        CancellationToken cancellationToken = default);
}
