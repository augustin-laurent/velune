using Velune.Application.Results;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Abstractions;

/// <summary>Provides structural PDF manipulation operations such as page rotation, deletion, and merging.</summary>
public interface IPdfDocumentStructureService
{
    /// <summary>Gets whether the service is available on the current platform.</summary>
    /// <returns>True if the service can be used; otherwise false.</returns>
    bool IsAvailable();

    /// <summary>Rotates the specified pages and writes the result to the output path.</summary>
    /// <param name="sourcePath">The source PDF file path.</param>
    /// <param name="outputPath">The destination file path for the modified PDF.</param>
    /// <param name="pages">The zero-based page indices to rotate.</param>
    /// <param name="rotation">The rotation to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The output file path or an error.</returns>
    Task<Result<string>> RotatePagesAsync(
        string sourcePath,
        string outputPath,
        IReadOnlyList<int> pages,
        Rotation rotation,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes the specified pages and writes the result to the output path.</summary>
    /// <param name="sourcePath">The source PDF file path.</param>
    /// <param name="outputPath">The destination file path for the modified PDF.</param>
    /// <param name="pages">The zero-based page indices to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The output file path or an error.</returns>
    Task<Result<string>> DeletePagesAsync(
        string sourcePath,
        string outputPath,
        IReadOnlyList<int> pages,
        CancellationToken cancellationToken = default);

    /// <summary>Extracts the specified pages into a new PDF at the output path.</summary>
    /// <param name="sourcePath">The source PDF file path.</param>
    /// <param name="outputPath">The destination file path for the extracted PDF.</param>
    /// <param name="pages">The zero-based page indices to extract.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The output file path or an error.</returns>
    Task<Result<string>> ExtractPagesAsync(
        string sourcePath,
        string outputPath,
        IReadOnlyList<int> pages,
        CancellationToken cancellationToken = default);

    /// <summary>Merges multiple PDF documents into a single output file.</summary>
    /// <param name="sourcePaths">The PDF file paths to merge in order.</param>
    /// <param name="outputPath">The destination file path for the merged PDF.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The output file path or an error.</returns>
    Task<Result<string>> MergeDocumentsAsync(
        IReadOnlyList<string> sourcePaths,
        string outputPath,
        CancellationToken cancellationToken = default);

    /// <summary>Reorders pages according to the specified sequence and writes to the output path.</summary>
    /// <param name="sourcePath">The source PDF file path.</param>
    /// <param name="outputPath">The destination file path for the reordered PDF.</param>
    /// <param name="orderedPages">The desired page order as zero-based indices.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The output file path or an error.</returns>
    Task<Result<string>> ReorderPagesAsync(
        string sourcePath,
        string outputPath,
        IReadOnlyList<int> orderedPages,
        CancellationToken cancellationToken = default);
}
