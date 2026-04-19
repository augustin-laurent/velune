using Velune.Application.Results;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Abstractions;

public interface IPdfDocumentStructureService
{
    bool IsAvailable();

    Task<Result<string>> RotatePagesAsync(
        string sourcePath,
        string outputPath,
        IReadOnlyList<int> pages,
        Rotation rotation,
        CancellationToken cancellationToken = default);

    Task<Result<string>> DeletePagesAsync(
        string sourcePath,
        string outputPath,
        IReadOnlyList<int> pages,
        CancellationToken cancellationToken = default);

    Task<Result<string>> ExtractPagesAsync(
        string sourcePath,
        string outputPath,
        IReadOnlyList<int> pages,
        CancellationToken cancellationToken = default);

    Task<Result<string>> MergeDocumentsAsync(
        IReadOnlyList<string> sourcePaths,
        string outputPath,
        CancellationToken cancellationToken = default);

    Task<Result<string>> ReorderPagesAsync(
        string sourcePath,
        string outputPath,
        IReadOnlyList<int> orderedPages,
        CancellationToken cancellationToken = default);
}
