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

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The stub orchestrator is owned and disposed by MainWindowViewModel during the test lifetime.")]
    private static MainWindowViewModel CreateViewModel(IRecentFilesService? recentFilesService = null)
    {
        var sessionStore = new InMemoryDocumentSessionStore();
        var viewportStore = new InMemoryPageViewportStore();
        var renderOrchestrator = new StubRenderOrchestrator();

        return new MainWindowViewModel(
            new StubFilePickerService(),
            new OpenDocumentUseCase(new StubDocumentOpener(), sessionStore, NoOpPerformanceMetrics.Instance),
            new CloseDocumentUseCase(sessionStore, NoOpPerformanceMetrics.Instance),
            new ChangePageUseCase(sessionStore),
            new ChangeZoomUseCase(sessionStore),
            new RotateDocumentUseCase(sessionStore),
            renderOrchestrator,
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
        public Task<string?> PickOpenFileAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }
    }

    private sealed class StubDocumentOpener : IDocumentOpener
    {
        public Task<IDocumentSession> OpenAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IDocumentSession>(
                new DocumentSession(
                    DocumentId.New(),
                    new DocumentMetadata("Document.pdf", filePath, DocumentType.Pdf, 1024, 2),
                    ViewportState.Default));
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
