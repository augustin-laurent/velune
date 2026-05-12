using Velune.Domain.Documents;
using Velune.Windows.ViewModels;

namespace Velune.Tests.Windows.Unit.ViewModels;

public sealed class WindowsDroppedMergeSourcePlannerTests
{
    [Fact]
    public void Create_ForPdfPrepend_PlacesDroppedSourcesBeforeCurrentDocument()
    {
        WindowsDroppedMergePlan plan = WindowsDroppedMergeSourcePlanner.Create(
            "current.pdf",
            DocumentType.Pdf,
            totalPages: 3,
            ["insert.pdf", "photo.png"],
            insertionIndex: 0,
            temporaryDirectory: Path.GetTempPath());

        Assert.Equal(["insert.pdf", "photo.png", "current.pdf"], plan.SourcePaths);
        Assert.Null(plan.Before);
        Assert.Null(plan.After);
    }

    [Fact]
    public void Create_ForPdfAppend_PlacesDroppedSourcesAfterCurrentDocument()
    {
        WindowsDroppedMergePlan plan = WindowsDroppedMergeSourcePlanner.Create(
            "current.pdf",
            DocumentType.Pdf,
            totalPages: 3,
            ["insert.pdf"],
            insertionIndex: 3,
            temporaryDirectory: Path.GetTempPath());

        Assert.Equal(["current.pdf", "insert.pdf"], plan.SourcePaths);
        Assert.Null(plan.Before);
        Assert.Null(plan.After);
    }

    [Fact]
    public void Create_ForPdfMiddleInsertion_SplitsCurrentDocumentAroundDroppedSources()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "velune-test");

        WindowsDroppedMergePlan plan = WindowsDroppedMergeSourcePlanner.Create(
            "current.pdf",
            DocumentType.Pdf,
            totalPages: 3,
            ["insert.pdf", "photo.png"],
            insertionIndex: 1,
            temporaryDirectory: tempDir);

        Assert.NotNull(plan.Before);
        Assert.NotNull(plan.After);
        Assert.Equal([1], plan.Before.Pages);
        Assert.Equal([2, 3], plan.After.Pages);
        Assert.Equal(
            [Path.Combine(tempDir, "current-before-drop.pdf"), "insert.pdf", "photo.png", Path.Combine(tempDir, "current-after-drop.pdf")],
            plan.SourcePaths);
    }

    [Fact]
    public void Create_ForImageDocument_UsesSimpleBeforeOrAfterOrdering()
    {
        WindowsDroppedMergePlan plan = WindowsDroppedMergeSourcePlanner.Create(
            "current.png",
            DocumentType.Image,
            totalPages: 1,
            ["insert.pdf"],
            insertionIndex: 1,
            temporaryDirectory: Path.GetTempPath());

        Assert.Equal(["current.png", "insert.pdf"], plan.SourcePaths);
        Assert.Null(plan.Before);
        Assert.Null(plan.After);
    }
}
