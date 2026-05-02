using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Domain.Annotations;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Velune.Windows.Services;
using Velune.Windows.ViewModels;
using Windows.Foundation;

namespace Velune.Tests.Windows.Unit.Services;

public sealed class WindowsPrintCoordinatorTests
{
    [Fact]
    public void CreatePageIndices_ReturnsAllPages()
    {
        var pages = WindowsPrintPageSnapshotFactory.CreatePageIndices(3);

        Assert.Equal([0, 1, 2], pages.Select(page => page.Value));
    }

    [Fact]
    public void CaptureAnnotationsForPage_FiltersAndCopiesPageAnnotations()
    {
        var firstPageAnnotation = CreateRectangleAnnotation(0);
        var secondPageAnnotation = CreateRectangleAnnotation(1);

        var captured = WindowsPrintPageSnapshotFactory.CaptureAnnotationsForPage(
            [firstPageAnnotation, secondPageAnnotation],
            new PageIndex(1));

        var annotation = Assert.Single(captured);
        Assert.Equal(secondPageAnnotation.Id, annotation.Id);
        Assert.NotSame(secondPageAnnotation, annotation);
    }

    [Fact]
    public void CreatePrintJobSnapshot_CapturesPrintableTabState()
    {
        var sessionId = DocumentId.New();
        var tab = CreateTab(sessionId, "sample.pdf");
        tab.Title = " ";
        tab.TotalPages = 2;
        var annotation = CreateRectangleAnnotation(0);
        tab.Annotations.Add(annotation);

        var snapshot = WindowsPrintJobSnapshotFactory.Create(tab, "Velune");

        Assert.Equal(sessionId, snapshot.SessionId);
        Assert.Equal("Velune", snapshot.Title);
        Assert.Equal(2, snapshot.TotalPages);
        Assert.Equal(Rotation.Deg0, snapshot.Rotation);
        var capturedAnnotation = Assert.Single(snapshot.Annotations);
        Assert.Equal(annotation.Id, capturedAnnotation.Id);
        Assert.NotSame(annotation, capturedAnnotation);
    }

    [Fact]
    public void Calculate_CentersContentInsideImageableArea()
    {
        var layout = WindowsPrintLayoutCalculator.Calculate(
            new WindowsPrintPageDescription(
                new Size(1000, 1000),
                new Rect(100, 50, 800, 600)),
            sourceWidth: 400,
            sourceHeight: 200);

        Assert.Equal(100, layout.Left);
        Assert.Equal(150, layout.Top);
        Assert.Equal(800, layout.Width);
        Assert.Equal(400, layout.Height);
    }

    [Fact]
    public async Task PrintAsync_ReturnsMissingFile_WhenDocumentPathNoLongerExists()
    {
        var sessionId = DocumentId.New();
        var missingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        var sessionStore = new InMemoryDocumentSessionStore();
        sessionStore.Add(CreateSession(sessionId, missingPath), makeActive: true);
        using var renderOrchestrator = new StubRenderOrchestrator();
        var coordinator = new WindowsPrintCoordinator(
            new WindowsWindowContext(),
            sessionStore,
            renderOrchestrator,
            new StubWindowsTextCatalog());
        var tab = CreateTab(sessionId, missingPath);

        var result = await coordinator.PrintAsync(tab);

        Assert.True(result.IsFailure);
        Assert.Equal("print.file.missing", result.Error?.Code);
        Assert.Equal(0, renderOrchestrator.SubmitCount);
    }

    [Fact]
    public async Task PrintAsync_ReturnsMissingSession_WhenTabSessionIsNotOpen()
    {
        var sessionId = DocumentId.New();
        using var renderOrchestrator = new StubRenderOrchestrator();
        var coordinator = new WindowsPrintCoordinator(
            new WindowsWindowContext(),
            new InMemoryDocumentSessionStore(),
            renderOrchestrator,
            new StubWindowsTextCatalog());

        var filePath = Path.GetTempFileName();
        try
        {
            var tab = CreateTab(sessionId, filePath);

            var result = await coordinator.PrintAsync(tab);

            Assert.True(result.IsFailure);
            Assert.Equal("print.session.missing", result.Error?.Code);
            Assert.Equal(0, renderOrchestrator.SubmitCount);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static WindowsDocumentTabViewModel CreateTab(DocumentId sessionId, string path)
    {
        var tab = (WindowsDocumentTabViewModel)RuntimeHelpers.GetUninitializedObject(
            typeof(WindowsDocumentTabViewModel));
        tab.SessionId = sessionId;
        tab.FilePath = path;
        tab.Title = Path.GetFileName(path);
        tab.TotalPages = 1;
        tab.Rotation = Rotation.Deg0;
        SetReadOnlyProperty(tab, nameof(WindowsDocumentTabViewModel.Annotations), new ObservableCollection<DocumentAnnotation>());
        return tab;
    }

    private static void SetReadOnlyProperty<T>(WindowsDocumentTabViewModel tab, string propertyName, T value)
    {
        var backingField = typeof(WindowsDocumentTabViewModel).GetField(
            $"<{propertyName}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(backingField);
        backingField.SetValue(tab, value);
    }

    private static DocumentSession CreateSession(DocumentId sessionId, string path)
    {
        return new DocumentSession(sessionId, CreateMetadata(path), ViewportState.Default);
    }

    private static DocumentMetadata CreateMetadata(string path)
    {
        return new DocumentMetadata(
            Path.GetFileName(path),
            path,
            DocumentType.Pdf,
            fileSizeInBytes: 1,
            pageCount: 1,
            pixelWidth: 400,
            pixelHeight: 600);
    }

    private static DocumentAnnotation CreateRectangleAnnotation(int pageIndex)
    {
        return new DocumentAnnotation(
            Guid.NewGuid(),
            DocumentAnnotationKind.Rectangle,
            new PageIndex(pageIndex),
            new AnnotationAppearance("#FF0000", "#00FF00", 2),
            new NormalizedTextRegion(0.1, 0.2, 0.3, 0.4));
    }

    private sealed class StubWindowsTextCatalog : IWindowsTextCatalog
    {
        public string GetString(string key) => key;

        public string Format(string key, params object[] args) => $"{key}: {string.Join(", ", args)}";
    }

    private sealed class StubRenderOrchestrator : IRenderOrchestrator
    {
        public int SubmitCount
        {
            get; private set;
        }

        public RenderJobHandle Submit(RenderRequest request)
        {
            SubmitCount++;
            throw new InvalidOperationException("Render should not be requested by this test.");
        }

        public bool Cancel(Guid jobId) => false;

        public Task CancelDocumentJobsAsync(DocumentId documentId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}
