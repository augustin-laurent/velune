using Velune.Domain.Documents;

namespace Velune.Windows.ViewModels;

internal static class WindowsDroppedMergeSourcePlanner
{
    public static WindowsDroppedMergePlan Create(
        string currentDocumentPath,
        DocumentType documentType,
        int totalPages,
        IReadOnlyList<string> droppedSourcePaths,
        int insertionIndex,
        string temporaryDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDocumentPath);
        ArgumentNullException.ThrowIfNull(droppedSourcePaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(temporaryDirectory);

        string[] dropped = droppedSourcePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray();

        if (documentType is not DocumentType.Pdf || totalPages <= 1)
        {
            return insertionIndex <= 0
                ? new WindowsDroppedMergePlan([.. dropped, currentDocumentPath], null, null)
                : new WindowsDroppedMergePlan([currentDocumentPath, .. dropped], null, null);
        }

        int normalizedIndex = Math.Clamp(insertionIndex, 0, totalPages);
        if (normalizedIndex <= 0)
        {
            return new WindowsDroppedMergePlan([.. dropped, currentDocumentPath], null, null);
        }

        if (normalizedIndex >= totalPages)
        {
            return new WindowsDroppedMergePlan([currentDocumentPath, .. dropped], null, null);
        }

        var before = new WindowsDroppedMergeExtraction(
            Path.Combine(temporaryDirectory, "current-before-drop.pdf"),
            Enumerable.Range(1, normalizedIndex).ToArray());
        var after = new WindowsDroppedMergeExtraction(
            Path.Combine(temporaryDirectory, "current-after-drop.pdf"),
            Enumerable.Range(normalizedIndex + 1, totalPages - normalizedIndex).ToArray());

        return new WindowsDroppedMergePlan([before.OutputPath, .. dropped, after.OutputPath], before, after);
    }
}

internal sealed record WindowsDroppedMergePlan(
    IReadOnlyList<string> SourcePaths,
    WindowsDroppedMergeExtraction? Before,
    WindowsDroppedMergeExtraction? After);

internal sealed record WindowsDroppedMergeExtraction(string OutputPath, IReadOnlyList<int> Pages);
