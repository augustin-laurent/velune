using Microsoft.Extensions.Options;
using Velune.Application.Abstractions;
using Velune.Application.Configuration;
using Velune.Application.DTOs;
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The stub orchestrator is owned and disposed by MainWindowViewModel during the test lifetime.")]
    private static MainWindowViewModel CreateViewModel(
        IRecentFilesService? recentFilesService = null,
        IFilePickerService? filePickerService = null,
        IDocumentOpener? documentOpener = null,
        IRenderOrchestrator? renderOrchestrator = null)
    {
        var sessionStore = new InMemoryDocumentSessionStore();
        var viewportStore = new InMemoryPageViewportStore();
        var orchestrator = renderOrchestrator ?? new StubRenderOrchestrator();

        return new MainWindowViewModel(
            filePickerService ?? new StubFilePickerService(),
            new OpenDocumentUseCase(documentOpener ?? new StubDocumentOpener(), sessionStore, NoOpPerformanceMetrics.Instance),
            new CloseDocumentUseCase(sessionStore, NoOpPerformanceMetrics.Instance),
            new ChangePageUseCase(sessionStore),
            new ChangeZoomUseCase(sessionStore),
            new RotateDocumentUseCase(sessionStore),
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
        private readonly string? _selectedPath;

        public StubFilePickerService(string? selectedPath = null)
        {
            _selectedPath = selectedPath;
        }

        public Task<string?> PickOpenFileAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_selectedPath);
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
}
