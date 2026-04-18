using Microsoft.Extensions.Options;
using Velune.Application.Abstractions;
using Velune.Application.Configuration;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Application.UseCases;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Velune.Presentation.ViewModels;
using Velune.Tests.Unit.Support;

namespace Velune.Tests.Unit.Presentation.ViewModels;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void SimulateErrorCommand_ShouldShowDismissibleNonFatalNotification()
    {
        using var viewModel = CreateViewModel();

        viewModel.SimulateErrorCommand.Execute(null);

        Assert.True(viewModel.HasUserMessage);
        Assert.Equal("Non-fatal error", viewModel.UserMessageTitle);
        Assert.Equal("Unable to load the requested document.", viewModel.UserMessage);
        Assert.True(viewModel.CanDismissUserMessage);
        Assert.False(viewModel.HasNotificationPrimaryAction);
        Assert.False(viewModel.HasNotificationSecondaryAction);
    }

    [Fact]
    public void ClearRecentFilesCommand_ShouldRequestConfirmationWithoutClearingEntries()
    {
        var recentFilesService = CreateRecentFilesService();
        recentFilesService.Add(new RecentFileItem("Document.pdf", "/tmp/document.pdf", "Pdf"));
        using var viewModel = CreateViewModel(recentFilesService);

        viewModel.ClearRecentFilesCommand.Execute(null);

        Assert.True(viewModel.HasUserMessage);
        Assert.Equal("Clear recent files?", viewModel.UserMessageTitle);
        Assert.False(viewModel.CanDismissUserMessage);
        Assert.True(viewModel.HasNotificationPrimaryAction);
        Assert.True(viewModel.HasNotificationSecondaryAction);
        Assert.Single(viewModel.RecentFiles);
        Assert.Equal("Confirmation required", viewModel.StatusText);
    }

    [Fact]
    public void NotificationPrimaryActionCommand_ShouldClearRecentFilesAndShowFollowUpInfo()
    {
        var recentFilesService = CreateRecentFilesService();
        recentFilesService.Add(new RecentFileItem("Document.pdf", "/tmp/document.pdf", "Pdf"));
        using var viewModel = CreateViewModel(recentFilesService);

        viewModel.ClearRecentFilesCommand.Execute(null);
        viewModel.NotificationPrimaryActionCommand.Execute(null);

        Assert.Empty(viewModel.RecentFiles);
        Assert.True(viewModel.HasUserMessage);
        Assert.Equal("Recent files cleared", viewModel.UserMessageTitle);
        Assert.Equal("The recent files list was cleared.", viewModel.UserMessage);
        Assert.True(viewModel.CanDismissUserMessage);
        Assert.False(viewModel.HasNotificationPrimaryAction);
        Assert.False(viewModel.HasNotificationSecondaryAction);
        Assert.Equal("Recent files cleared", viewModel.StatusText);
    }

    [Fact]
    public void NotificationSecondaryActionCommand_ShouldKeepRecentFilesAndDismissConfirmation()
    {
        var recentFilesService = CreateRecentFilesService();
        recentFilesService.Add(new RecentFileItem("Document.pdf", "/tmp/document.pdf", "Pdf"));
        using var viewModel = CreateViewModel(recentFilesService);

        viewModel.ClearRecentFilesCommand.Execute(null);
        viewModel.NotificationSecondaryActionCommand.Execute(null);

        Assert.Single(viewModel.RecentFiles);
        Assert.False(viewModel.HasUserMessage);
        Assert.Equal("Action cancelled", viewModel.StatusText);
    }

    [Fact]
    public async Task OpenCommand_ShouldPopulateDocumentInfoAndShowFriendlyMetadataWarning()
    {
        using var viewModel = CreateViewModel(
            filePickerService: new StubFilePickerService("/tmp/document.pdf"),
            documentOpener: new StubDocumentOpener(
                new DocumentSession(
                    DocumentId.New(),
                    new DocumentMetadata(
                        "Document.pdf",
                        "/tmp/document.pdf",
                        DocumentType.Pdf,
                        1536000,
                        3,
                        formatLabel: "PDF document",
                        createdAt: new DateTimeOffset(2026, 04, 18, 10, 30, 0, TimeSpan.Zero),
                        modifiedAt: new DateTimeOffset(2026, 04, 18, 11, 45, 0, TimeSpan.Zero),
                        documentTitle: "Quarterly Report",
                        author: "Ada Lovelace",
                        creator: "Velune Tests",
                        detailsWarning: "Some document details are unavailable."),
                    ViewportState.Default)));

        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasOpenDocument);
        Assert.True(viewModel.HasDocumentInfo);
        Assert.True(viewModel.HasDocumentInfoWarning);
        Assert.Equal("Some document details are unavailable.", viewModel.DocumentInfoWarning);
        Assert.Equal("Some file details are unavailable", viewModel.UserMessageTitle);
        Assert.Contains(viewModel.DocumentInfoItems, item => item.Label == "Author" && item.Value == "Ada Lovelace");
        Assert.Contains(viewModel.DocumentInfoItems, item => item.Label == "Title" && item.Value == "Quarterly Report");
        Assert.Contains(viewModel.DocumentInfoItems, item => item.Label == "Pages" && item.Value == "3");
    }

    [Fact]
    public async Task ToggleInfoPanelCommand_ShouldTogglePanelWhenDocumentIsOpen()
    {
        using var viewModel = CreateViewModel(
            filePickerService: new StubFilePickerService("/tmp/image.png"),
            documentOpener: new StubDocumentOpener(
                new DocumentSession(
                    DocumentId.New(),
                    new DocumentMetadata(
                        "image.png",
                        "/tmp/image.png",
                        DocumentType.Image,
                        2048,
                        1,
                        pixelWidth: 1200,
                        pixelHeight: 800,
                        formatLabel: "PNG image"),
                    ViewportState.Default)));

        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsInfoPanelOpen);

        viewModel.ToggleInfoPanelCommand.Execute(null);

        Assert.True(viewModel.IsInfoPanelOpen);
        Assert.Equal("File information shown", viewModel.StatusText);
    }

    [Fact]
    public async Task HandleThumbnailReorderAsync_ShouldMarkPendingOrderAndMoveThumbnail()
    {
        using var viewModel = CreateViewModel(
            filePickerService: new StubFilePickerService("/tmp/document.pdf"),
            documentOpener: new StubDocumentOpener(
                new DocumentSession(
                    DocumentId.New(),
                    new DocumentMetadata("Document.pdf", "/tmp/document.pdf", DocumentType.Pdf, 1024, 3),
                    ViewportState.Default)));

        await viewModel.OpenCommand.ExecuteAsync(null);
        await viewModel.HandleThumbnailReorderAsync(1, 3);

        Assert.True(viewModel.HasPendingPageReorder);
        Assert.Equal("Page order updated", viewModel.UserMessageTitle);
        Assert.Equal([2, 3, 1], viewModel.Thumbnails.Select(item => item.SourcePageNumber).ToArray());
        Assert.Equal([1, 2, 3], viewModel.Thumbnails.Select(item => item.DisplayPageNumber).ToArray());
    }

    [Fact]
    public async Task HandleThumbnailReorderToIndexAsync_ShouldMoveThumbnailToRequestedSlot()
    {
        using var viewModel = CreateViewModel(
            filePickerService: new StubFilePickerService("/tmp/document.pdf"),
            documentOpener: new StubDocumentOpener(
                new DocumentSession(
                    DocumentId.New(),
                    new DocumentMetadata("Document.pdf", "/tmp/document.pdf", DocumentType.Pdf, 1024, 4),
                    ViewportState.Default)));

        await viewModel.OpenCommand.ExecuteAsync(null);
        await viewModel.HandleThumbnailReorderToIndexAsync(1, 3);

        Assert.True(viewModel.HasPendingPageReorder);
        Assert.Equal([2, 3, 4, 1], viewModel.Thumbnails.Select(item => item.SourcePageNumber).ToArray());
        Assert.Equal([1, 2, 3, 4], viewModel.Thumbnails.Select(item => item.DisplayPageNumber).ToArray());
    }

    [Fact]
    public async Task MoveCurrentPageLaterCommand_ShouldMoveCurrentPageByOneSlot()
    {
        using var viewModel = CreateViewModel(
            filePickerService: new StubFilePickerService("/tmp/document.pdf"),
            documentOpener: new StubDocumentOpener(
                new DocumentSession(
                    DocumentId.New(),
                    new DocumentMetadata("Document.pdf", "/tmp/document.pdf", DocumentType.Pdf, 1024, 3),
                    ViewportState.Default)));

        await viewModel.OpenCommand.ExecuteAsync(null);
        await viewModel.MoveCurrentPageLaterCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasPendingPageReorder);
        Assert.Equal([2, 1, 3], viewModel.Thumbnails.Select(item => item.SourcePageNumber).ToArray());
        Assert.Equal("Moved page 1 later", viewModel.StatusText);
    }

    [Fact]
    public async Task DeleteCurrentPageCommand_ShouldUseFilePickerAndDeleteSelectedPage()
    {
        var filePickerService = new StubFilePickerService(
            openPath: "/tmp/document.pdf",
            savePath: "/tmp/document-without-page-1.pdf");
        var pdfStructureService = new StubPdfDocumentStructureService();

        using var viewModel = CreateViewModel(
            filePickerService: filePickerService,
            documentOpener: new StubDocumentOpener(
                new DocumentSession(
                    DocumentId.New(),
                    new DocumentMetadata("Document.pdf", "/tmp/document.pdf", DocumentType.Pdf, 1024, 3),
                    ViewportState.Default)),
            pdfDocumentStructureService: pdfStructureService);

        await viewModel.OpenCommand.ExecuteAsync(null);
        await viewModel.DeleteCurrentPageCommand.ExecuteAsync(null);

        Assert.Equal("Save PDF without current page", filePickerService.LastSaveTitle);
        Assert.Equal("Document.pdf", filePickerService.LastSuggestedFileName);
        Assert.Equal([1], pdfStructureService.LastDeletedPages);
        Assert.Equal("/tmp/document-without-page-1.pdf", pdfStructureService.LastOutputPath);
    }

    [Fact]
    public async Task PersistCurrentPageRotationCommand_ShouldSaveAllPendingRotations()
    {
        var filePickerService = new StubFilePickerService(
            openPath: "/tmp/document.pdf",
            savePath: "/tmp/document-rotated.pdf");
        var pdfStructureService = new StubPdfDocumentStructureService();

        using var viewModel = CreateViewModel(
            filePickerService: filePickerService,
            documentOpener: new StubDocumentOpener(
                new DocumentSession(
                    DocumentId.New(),
                    new DocumentMetadata("Document.pdf", "/tmp/document.pdf", DocumentType.Pdf, 1024, 3),
                    ViewportState.Default)),
            pdfDocumentStructureService: pdfStructureService);

        await viewModel.OpenCommand.ExecuteAsync(null);
        await viewModel.RotateRightCommand.ExecuteAsync(null);
        await viewModel.NextPageCommand.ExecuteAsync(null);
        await viewModel.RotateLeftCommand.ExecuteAsync(null);

        await viewModel.PersistCurrentPageRotationCommand.ExecuteAsync(null);

        Assert.Equal("Save PDF with page rotations", filePickerService.LastSaveTitle);
        Assert.Equal("Document.pdf", filePickerService.LastSuggestedFileName);
        Assert.Equal(2, pdfStructureService.RotateCalls.Count);
        Assert.Equal([1], pdfStructureService.RotateCalls[0].Pages);
        Assert.Equal(Rotation.Deg90, pdfStructureService.RotateCalls[0].Rotation);
        Assert.Equal([2], pdfStructureService.RotateCalls[1].Pages);
        Assert.Equal(Rotation.Deg270, pdfStructureService.RotateCalls[1].Rotation);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The stub orchestrator is owned and disposed by MainWindowViewModel during the test lifetime.")]
    private static MainWindowViewModel CreateViewModel(
        IRecentFilesService? recentFilesService = null,
        IFilePickerService? filePickerService = null,
        IDocumentOpener? documentOpener = null,
        IRenderOrchestrator? renderOrchestrator = null,
        IPdfDocumentStructureService? pdfDocumentStructureService = null)
    {
        var sessionStore = new InMemoryDocumentSessionStore();
        var viewportStore = new InMemoryPageViewportStore();
        var orchestrator = renderOrchestrator ?? new StubRenderOrchestrator();
        var structureService = pdfDocumentStructureService ?? new StubPdfDocumentStructureService();

        return new MainWindowViewModel(
            filePickerService ?? new StubFilePickerService(),
            new OpenDocumentUseCase(documentOpener ?? new StubDocumentOpener(), sessionStore, NoOpPerformanceMetrics.Instance),
            new CloseDocumentUseCase(sessionStore, NoOpPerformanceMetrics.Instance),
            new ChangePageUseCase(sessionStore),
            new ChangeZoomUseCase(sessionStore),
            new RotateDocumentUseCase(sessionStore),
            new RotatePdfPagesUseCase(structureService),
            new DeletePdfPagesUseCase(structureService),
            new ExtractPdfPagesUseCase(structureService),
            new ReorderPdfPagesUseCase(structureService),
            orchestrator,
            sessionStore,
            recentFilesService ?? CreateRecentFilesService(),
            viewportStore);
    }

    private static IRecentFilesService CreateRecentFilesService()
    {
        return new InMemoryRecentFilesService(Options.Create(new AppOptions()));
    }

    private sealed class StubFilePickerService : IFilePickerService
    {
        private readonly string? _openPath;
        private readonly string? _savePath;

        public StubFilePickerService(string? openPath = null, string? savePath = null)
        {
            _openPath = openPath;
            _savePath = savePath;
        }

        public string? LastSaveTitle { get; private set; }

        public string? LastSuggestedFileName { get; private set; }

        public Task<string?> PickOpenFileAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_openPath);
        }

        public Task<string?> PickSavePdfFileAsync(
            string title,
            string suggestedFileName,
            CancellationToken cancellationToken = default)
        {
            LastSaveTitle = title;
            LastSuggestedFileName = suggestedFileName;
            return Task.FromResult(_savePath);
        }
    }

    private sealed class StubDocumentOpener : IDocumentOpener
    {
        private readonly IDocumentSession _documentSession;

        public StubDocumentOpener(IDocumentSession? documentSession = null)
        {
            _documentSession = documentSession ??
                new DocumentSession(
                    DocumentId.New(),
                    new DocumentMetadata("Document.pdf", "/tmp/document.pdf", DocumentType.Pdf, 1024, 2),
                    ViewportState.Default);
        }

        public Task<IDocumentSession> OpenAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_documentSession);
        }
    }

    private sealed class StubRenderOrchestrator : IRenderOrchestrator
    {
        public RenderJobHandle Submit(RenderRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var jobId = Guid.NewGuid();

            return new RenderJobHandle(
                jobId,
                Task.FromResult(
                    new RenderResult(
                        jobId,
                        DocumentId.New(),
                        request.JobKey,
                        request.PageIndex,
                        TimeSpan.Zero,
                        null,
                        null,
                        true,
                        false)));
        }

        public bool Cancel(Guid jobId)
        {
            return true;
        }

        public void Dispose()
        {
        }
    }

    private sealed class StubPdfDocumentStructureService : IPdfDocumentStructureService
    {
        public List<(string SourcePath, string OutputPath, IReadOnlyList<int> Pages, Rotation Rotation)> RotateCalls { get; } = [];

        public IReadOnlyList<int>? LastDeletedPages { get; private set; }

        public string? LastOutputPath { get; private set; }

        public bool IsAvailable() => true;

        public Task<Result<string>> RotatePagesAsync(
            string sourcePath,
            string outputPath,
            IReadOnlyList<int> pages,
            Rotation rotation,
            CancellationToken cancellationToken = default)
        {
            RotateCalls.Add((sourcePath, outputPath, pages.ToArray(), rotation));
            LastOutputPath = outputPath;
            return Task.FromResult(ResultFactory.Success(outputPath));
        }

        public Task<Result<string>> DeletePagesAsync(
            string sourcePath,
            string outputPath,
            IReadOnlyList<int> pages,
            CancellationToken cancellationToken = default)
        {
            LastDeletedPages = pages.ToArray();
            LastOutputPath = outputPath;
            return Task.FromResult(ResultFactory.Success(outputPath));
        }

        public Task<Result<string>> ExtractPagesAsync(
            string sourcePath,
            string outputPath,
            IReadOnlyList<int> pages,
            CancellationToken cancellationToken = default)
        {
            LastOutputPath = outputPath;
            return Task.FromResult(ResultFactory.Success(outputPath));
        }

        public Task<Result<string>> MergeDocumentsAsync(
            IReadOnlyList<string> sourcePaths,
            string outputPath,
            CancellationToken cancellationToken = default)
        {
            LastOutputPath = outputPath;
            return Task.FromResult(ResultFactory.Success(outputPath));
        }

        public Task<Result<string>> ReorderPagesAsync(
            string sourcePath,
            string outputPath,
            IReadOnlyList<int> orderedPages,
            CancellationToken cancellationToken = default)
        {
            LastOutputPath = outputPath;
            return Task.FromResult(ResultFactory.Success(outputPath));
        }
    }
}
