using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Velune.Application.Abstractions;
using Velune.Application.Configuration;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Application.UseCases;
using Velune.Domain.Abstractions;
using Velune.Domain.Annotations;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Velune.Presentation.FileSystem;
using Velune.Presentation.Localization;
using Velune.Presentation.Platform;
using Velune.Presentation.ViewModels;
using Velune.Tests.Unit.Support;
using AppResult = Velune.Application.Results.Result;

namespace Velune.Tests.Unit.Presentation.ViewModels;

public sealed class MainWindowViewModelTests
{
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
    public void LanguagePreferenceChange_OnMacOs_ShouldShowRestartNoticeInSelectedLanguage()
    {
        var previousDetector = PresentationPlatform.IsMacOSDetector;
        PresentationPlatform.IsMacOSDetector = static () => true;

        try
        {
            using var viewModel = CreateViewModel();

            viewModel.SelectedLanguagePreference = viewModel.LanguagePreferenceOptions
                .Single(option => option.Value == AppLanguagePreference.French);

            Assert.True(viewModel.HasUserMessage);
            Assert.Equal("Un redémarrage peut être nécessaire", viewModel.UserMessageTitle);
            Assert.Equal(
                "Certains libellés du menu natif macOS peuvent rester dans l’ancienne langue jusqu’au redémarrage de Velune.",
                viewModel.UserMessage);
            Assert.Equal("Préférences mises à jour", viewModel.StatusText);
        }
        finally
        {
            PresentationPlatform.IsMacOSDetector = previousDetector;
        }
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
                new StubImageDocumentSession(
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
                    ViewportState.Default,
                    new ImageMetadata(1200, 800))));

        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsInfoPanelOpen);

        viewModel.ToggleInfoPanelCommand.Execute(null);

        Assert.True(viewModel.IsInfoPanelOpen);
        Assert.Equal("File information shown", viewModel.StatusText);
    }

    [Fact]
    public async Task TogglePreferencesPanelCommand_ShouldTogglePanelWhenDocumentIsOpen()
    {
        using var viewModel = CreateViewModel(
            filePickerService: new StubFilePickerService("/tmp/document.pdf"),
            documentOpener: new StubDocumentOpener(
                new DocumentSession(
                    DocumentId.New(),
                    new DocumentMetadata("Document.pdf", "/tmp/document.pdf", DocumentType.Pdf, 1024, 1),
                    ViewportState.Default)));

        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsPreferencesPanelOpen);

        viewModel.TogglePreferencesPanelCommand.Execute(null);

        Assert.True(viewModel.IsPreferencesPanelOpen);
        Assert.Equal("Preferences shown", viewModel.StatusText);
    }

    [Fact]
    public async Task ShowThumbnailsPanelPreference_ShouldHideSidebarAndPersistPreference()
    {
        var preferencesService = new StubUserPreferencesService();
        using var viewModel = CreateViewModel(
            filePickerService: new StubFilePickerService("/tmp/document.pdf"),
            documentOpener: new StubDocumentOpener(
                new DocumentSession(
                    DocumentId.New(),
                    new DocumentMetadata("Document.pdf", "/tmp/document.pdf", DocumentType.Pdf, 1024, 2),
                    ViewportState.Default)),
            userPreferencesService: preferencesService);

        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsSidebarVisible);

        viewModel.ShowThumbnailsPanelPreference = false;

        Assert.False(viewModel.IsSidebarVisible);
        Assert.False(preferencesService.Current.ShowThumbnailsPanel);
    }

    [Fact]
    public async Task OpenCommand_ShouldApplyPreferredDefaultZoomForImageDocuments()
    {
        var preferencesService = new StubUserPreferencesService(
            UserPreferences.CreateDefault(64) with
            {
                DefaultZoom = DefaultZoomPreference.FitToWidth
            });

        using var viewModel = CreateViewModel(
            filePickerService: new StubFilePickerService("/tmp/image.png"),
            documentOpener: new StubDocumentOpener(
                new StubImageDocumentSession(
                    DocumentId.New(),
                    new DocumentMetadata(
                        "image.png",
                        "/tmp/image.png",
                        DocumentType.Image,
                        2048,
                        1,
                        pixelWidth: 2400,
                        pixelHeight: 1200,
                        formatLabel: "PNG image"),
                    ViewportState.Default,
                    new ImageMetadata(2400, 1200))),
            userPreferencesService: preferencesService);

        await viewModel.UpdateDocumentViewportAsync(1200, 900);
        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.Equal("49%", viewModel.CurrentZoom);
    }

    [Fact]
    public async Task ZoomInCommand_ShouldKeepZoomWhenNavigatingToNextPage()
    {
        using var viewModel = CreateViewModel(
            filePickerService: new StubFilePickerService("/tmp/document.pdf"),
            documentOpener: new StubDocumentOpener(
                new DocumentSession(
                    DocumentId.New(),
                    new DocumentMetadata("Document.pdf", "/tmp/document.pdf", DocumentType.Pdf, 1024, 3),
                    ViewportState.Default)));

        await viewModel.OpenCommand.ExecuteAsync(null);
        await viewModel.ZoomInCommand.ExecuteAsync(null);
        await viewModel.NextPageCommand.ExecuteAsync(null);

        Assert.Equal(2, viewModel.CurrentPage);
        Assert.Equal("110%", viewModel.CurrentZoom);
    }

    [Fact]
    public async Task SearchTextCommand_ShouldPopulateResults_WhenSearchableTextIsAvailable()
    {
        using var textAnalysisOrchestrator = new StubDocumentTextAnalysisOrchestrator(request =>
            CreateTextAnalysisResult(
                request,
                index: CreateDocumentTextIndex("Velune integration sample")));

        using var viewModel = CreateViewModel(
            filePickerService: new StubFilePickerService("/tmp/document.pdf"),
            documentOpener: new StubDocumentOpener(
                new DocumentSession(
                    DocumentId.New(),
                    new DocumentMetadata("Document.pdf", "/tmp/document.pdf", DocumentType.Pdf, 1024, 1),
                    ViewportState.Default)),
            textAnalysisOrchestrator: textAnalysisOrchestrator);

        await viewModel.OpenCommand.ExecuteAsync(null);
        await viewModel.ToggleSearchPanelCommand.ExecuteAsync(null);

        viewModel.SearchQueryInput = "velune";
        await viewModel.SearchTextCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsSearchPanelOpen);
        Assert.Single(viewModel.SearchResults);
        Assert.Equal("1 result", viewModel.SearchResultSummary);
        Assert.True(viewModel.SearchResults[0].IsSelected);
        Assert.Contains("Velune integration sample", viewModel.SearchResults[0].Excerpt, StringComparison.OrdinalIgnoreCase);
        Assert.Null(viewModel.SearchPanelNotice);
        Assert.Equal("Search result on page 1", viewModel.StatusText);
        Assert.Single(textAnalysisOrchestrator.Requests);
        Assert.False(textAnalysisOrchestrator.Requests[0].ForceOcr);
    }

    [Fact]
    public async Task ImageDocuments_ShouldDisableSearchUi()
    {
        using var textAnalysisOrchestrator = new StubDocumentTextAnalysisOrchestrator(request =>
            request.ForceOcr
                ? CreateTextAnalysisResult(
                    request,
                    index: CreateDocumentTextIndex("Scanned invoice"))
                : CreateTextAnalysisResult(
                    request,
                    requiresOcr: true));

        using var viewModel = CreateViewModel(
            filePickerService: new StubFilePickerService("/tmp/image.png"),
            documentOpener: new StubDocumentOpener(
                new StubImageDocumentSession(
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
                    ViewportState.Default,
                    new ImageMetadata(1200, 800))),
            textAnalysisOrchestrator: textAnalysisOrchestrator);

        await viewModel.OpenCommand.ExecuteAsync(null);
        Assert.False(viewModel.IsSearchAvailableForCurrentDocument);
        Assert.False(viewModel.ToggleSearchPanelCommand.CanExecute(null));
        Assert.False(viewModel.OpenSearchCommand.CanExecute(null));
        Assert.False(viewModel.UseInlineHeaderSearch);
        Assert.False(viewModel.UseCollapsedHeaderSearchButton);
        Assert.False(viewModel.IsSearchPanelOpen);
        Assert.Empty(textAnalysisOrchestrator.Requests);
    }

    [Fact]
    public async Task ImageDocuments_ShouldHideSidebarControls()
    {
        using var viewModel = CreateViewModel(
            filePickerService: new StubFilePickerService("/tmp/image.png"),
            documentOpener: new StubDocumentOpener(
                new StubImageDocumentSession(
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
                    ViewportState.Default,
                    new ImageMetadata(1200, 800))));

        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.False(viewModel.SidebarHostVisible);
        Assert.False(viewModel.IsSidebarVisible);
        Assert.False(viewModel.CanToggleSidebar);
        Assert.False(viewModel.ToggleSidebarCommand.CanExecute(null));
    }

    [Fact]
    public async Task Annotations_ShouldEnableSaveForImageDocuments()
    {
        using var viewModel = CreateViewModel(
            filePickerService: new StubFilePickerService("/tmp/image.png"),
            documentOpener: new StubDocumentOpener(
                new StubImageDocumentSession(
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
                    ViewportState.Default,
                    new ImageMetadata(1200, 800))));

        await viewModel.OpenCommand.ExecuteAsync(null);
        viewModel.ToggleAnnotationsPanelCommand.Execute(null);
        viewModel.SelectAnnotationToolCommand.Execute("Rectangle");

        var began = viewModel.BeginAnnotationInteraction(20, 30, 200, 200);
        Assert.True(began);

        viewModel.UpdateAnnotationInteraction(120, 140, 200, 200);
        viewModel.CompleteAnnotationInteraction(120, 140, 200, 200);

        Assert.True(viewModel.HasPendingAnnotationChanges);
        Assert.True(viewModel.CanSaveDocument);
        Assert.True(viewModel.HasCurrentPageAnnotations);
    }

    [Fact]
    public async Task Annotations_ShouldSupportUndoAndRedo()
    {
        using var viewModel = CreateViewModel(
            filePickerService: new StubFilePickerService("/tmp/document.pdf"),
            documentOpener: new StubDocumentOpener(
                new DocumentSession(
                    DocumentId.New(),
                    new DocumentMetadata("Document.pdf", "/tmp/document.pdf", DocumentType.Pdf, 1024, 1),
                    ViewportState.Default)));

        await viewModel.OpenCommand.ExecuteAsync(null);
        viewModel.ToggleAnnotationsPanelCommand.Execute(null);
        viewModel.SelectAnnotationToolCommand.Execute("Rectangle");

        Assert.True(viewModel.BeginAnnotationInteraction(20, 30, 200, 200));

        viewModel.UpdateAnnotationInteraction(120, 140, 200, 200);
        viewModel.CompleteAnnotationInteraction(120, 140, 200, 200);

        Assert.Single(viewModel.CurrentPageAnnotations);
        Assert.True(viewModel.CanUndoAnnotations);

        viewModel.UndoAnnotationsCommand.Execute(null);

        Assert.Empty(viewModel.CurrentPageAnnotations);
        Assert.True(viewModel.CanRedoAnnotations);

        viewModel.RedoAnnotationsCommand.Execute(null);

        Assert.Single(viewModel.CurrentPageAnnotations);
        Assert.Equal(DocumentAnnotationKind.Rectangle, viewModel.CurrentPageAnnotations[0].Kind);
    }

    [Fact]
    public async Task DrawnSignature_ShouldBecomeAvailableAndPlaceable()
    {
        using var viewModel = CreateViewModel(
            filePickerService: new StubFilePickerService("/tmp/document.pdf"),
            documentOpener: new StubDocumentOpener(
                new DocumentSession(
                    DocumentId.New(),
                    new DocumentMetadata("Document.pdf", "/tmp/document.pdf", DocumentType.Pdf, 1024, 1),
                    ViewportState.Default)));

        await viewModel.OpenCommand.ExecuteAsync(null);
        viewModel.ToggleAnnotationsPanelCommand.Execute(null);

        viewModel.BeginSignatureCapture(20, 20, 200, 100);
        viewModel.UpdateSignatureCapture(140, 58, 200, 100);
        viewModel.CompleteSignatureCapture();
        viewModel.SaveDrawnSignatureAssetCommand.Execute(null);

        Assert.True(viewModel.HasSignatureAssets);
        Assert.True(viewModel.CanUseSignaturePlacement);
        Assert.NotNull(viewModel.SelectedSignatureAssetId);

        viewModel.SelectAnnotationToolCommand.Execute("Signature");

        Assert.True(viewModel.BeginAnnotationInteraction(50, 50, 200, 200));

        viewModel.UpdateAnnotationInteraction(150, 110, 200, 200);
        viewModel.CompleteAnnotationInteraction(150, 110, 200, 200);

        Assert.Contains(
            viewModel.CurrentPageAnnotations,
            annotation => annotation.Kind == DocumentAnnotationKind.Signature &&
                          annotation.AssetId == viewModel.SelectedSignatureAssetId);
    }

    [Fact]
    public async Task DeleteSelectedSignatureAsset_ShouldRemoveItFromLibrary()
    {
        using var viewModel = CreateViewModel(
            filePickerService: new StubFilePickerService("/tmp/document.pdf"),
            documentOpener: new StubDocumentOpener(
                new DocumentSession(
                    DocumentId.New(),
                    new DocumentMetadata("Document.pdf", "/tmp/document.pdf", DocumentType.Pdf, 1024, 1),
                    ViewportState.Default)));

        await viewModel.OpenCommand.ExecuteAsync(null);
        viewModel.ToggleAnnotationsPanelCommand.Execute(null);

        viewModel.BeginSignatureCapture(20, 20, 200, 100);
        viewModel.UpdateSignatureCapture(140, 58, 200, 100);
        viewModel.CompleteSignatureCapture();
        viewModel.SaveDrawnSignatureAssetCommand.Execute(null);

        Assert.True(viewModel.HasSignatureAssets);

        viewModel.DeleteSelectedSignatureAssetCommand.Execute(viewModel.SelectedSignatureAssetId);

        Assert.False(viewModel.HasSignatureAssets);
        Assert.Null(viewModel.SelectedSignatureAssetId);
    }

    [Fact]
    public async Task ToggleAnnotationsPanel_ShouldCloseSearchPanel()
    {
        using var textAnalysisOrchestrator = new StubDocumentTextAnalysisOrchestrator(request =>
            CreateTextAnalysisResult(
                request,
                index: CreateDocumentTextIndex("Velune integration sample")));

        using var viewModel = CreateViewModel(
            filePickerService: new StubFilePickerService("/tmp/document.pdf"),
            documentOpener: new StubDocumentOpener(
                new DocumentSession(
                    DocumentId.New(),
                    new DocumentMetadata("Document.pdf", "/tmp/document.pdf", DocumentType.Pdf, 1024, 1),
                    ViewportState.Default)),
            textAnalysisOrchestrator: textAnalysisOrchestrator);

        await viewModel.OpenCommand.ExecuteAsync(null);
        await viewModel.ToggleSearchPanelCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsSearchPanelOpen);

        viewModel.ToggleAnnotationsPanelCommand.Execute(null);

        Assert.True(viewModel.IsAnnotationsPanelOpen);
        Assert.False(viewModel.IsSearchPanelOpen);
    }

    [Fact]
    public async Task PrintDocumentCommand_ShouldUseSystemPrintDialog_WhenAvailable()
    {
        using var temporaryFile = new TemporaryFile(".pdf");
        var printService = new StubPrintService(
            ResultFactory.Success(),
            supportsSystemPrintDialog: true);

        using var viewModel = CreateViewModel(
            filePickerService: new StubFilePickerService(temporaryFile.Path),
            documentOpener: new StubDocumentOpener(
                new DocumentSession(
                    DocumentId.New(),
                    new DocumentMetadata(Path.GetFileName(temporaryFile.Path), temporaryFile.Path, DocumentType.Pdf, 1024, 1),
                    ViewportState.Default)),
            printService: printService);

        await viewModel.OpenCommand.ExecuteAsync(null);
        await viewModel.PrintDocumentCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsPrintPanelOpen);
        Assert.Equal("System print dialog shown", viewModel.StatusText);
        Assert.Equal(temporaryFile.Path, printService.LastSystemDialogFilePath);
        Assert.Equal(0, printService.GetAvailablePrintersCallCount);
    }

    [Fact]
    public async Task PrintDocumentCommand_ShouldOpenPrintPanelAndLoadPrinters_WhenSystemDialogIsUnavailable()
    {
        using var temporaryFile = new TemporaryFile(".pdf");
        var printService = new StubPrintService(
            ResultFactory.Success(),
            [new PrintDestinationInfo("Office Printer", true), new PrintDestinationInfo("Label Printer", false)],
            supportsSystemPrintDialog: false);

        using var viewModel = CreateViewModel(
            filePickerService: new StubFilePickerService(temporaryFile.Path),
            documentOpener: new StubDocumentOpener(
                new DocumentSession(
                    DocumentId.New(),
                    new DocumentMetadata(Path.GetFileName(temporaryFile.Path), temporaryFile.Path, DocumentType.Pdf, 1024, 1),
                    ViewportState.Default)),
            printService: printService);

        await viewModel.OpenCommand.ExecuteAsync(null);
        await viewModel.PrintDocumentCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsPrintPanelOpen);
        Assert.Equal("Print panel shown", viewModel.StatusText);
        Assert.Equal(2, viewModel.PrintDestinations.Count);
        Assert.Equal("Office Printer", viewModel.SelectedPrintDestination?.Name);
        Assert.Equal(1, printService.GetAvailablePrintersCallCount);
    }

    [Fact]
    public async Task PrintDocumentCommand_ShouldKeepSessionClean_WhenSystemDialogIsCancelled()
    {
        using var temporaryFile = new TemporaryFile(".pdf");

        using var viewModel = CreateViewModel(
            filePickerService: new StubFilePickerService(temporaryFile.Path),
            documentOpener: new StubDocumentOpener(
                new DocumentSession(
                    DocumentId.New(),
                    new DocumentMetadata(Path.GetFileName(temporaryFile.Path), temporaryFile.Path, DocumentType.Pdf, 1024, 1),
                    ViewportState.Default)),
            printService: new StubPrintService(
                ResultFactory.Success(),
                supportsSystemPrintDialog: true,
                systemDialogResult: ResultFactory.Failure(
                    AppError.Validation(
                        "print.cancelled",
                        "Printing was cancelled."))));

        await viewModel.OpenCommand.ExecuteAsync(null);
        await viewModel.PrintDocumentCommand.ExecuteAsync(null);

        Assert.Equal("Print cancelled", viewModel.StatusText);
        Assert.False(viewModel.HasUserMessage);
        Assert.False(viewModel.IsPrintPanelOpen);
    }

    [Fact]
    public async Task SubmitPrintJobCommand_ShouldShowInfoNotification_WhenPrintStarts()
    {
        using var temporaryFile = new TemporaryFile(".pdf");
        var printService = new StubPrintService(
            ResultFactory.Success(),
            [new PrintDestinationInfo("Office Printer", true)],
            supportsSystemPrintDialog: false);

        using var viewModel = CreateViewModel(
            filePickerService: new StubFilePickerService(temporaryFile.Path),
            documentOpener: new StubDocumentOpener(
                new DocumentSession(
                    DocumentId.New(),
                    new DocumentMetadata(Path.GetFileName(temporaryFile.Path), temporaryFile.Path, DocumentType.Pdf, 1024, 1),
                    ViewportState.Default)),
            printService: printService);

        await viewModel.OpenCommand.ExecuteAsync(null);
        await viewModel.PrintDocumentCommand.ExecuteAsync(null);

        viewModel.PrintCopiesInput = "2";
        viewModel.SelectedPrintPageRangeOption = viewModel.PrintPageRangeOptions
            .Single(option => option.Value == PrintPageRangeChoice.CurrentPage);
        viewModel.SelectedPrintOrientationOption = viewModel.PrintOrientationOptions
            .Single(option => option.Value == PrintOrientationOption.Landscape);
        viewModel.PrintFitToPage = true;

        await viewModel.SubmitPrintJobCommand.ExecuteAsync(null);

        Assert.Equal("Print started", viewModel.UserMessageTitle);
        Assert.Equal("Print started", viewModel.StatusText);
        Assert.False(viewModel.IsPrintPanelOpen);
        Assert.NotNull(printService.LastRequest);
        Assert.Equal(temporaryFile.Path, printService.LastRequest!.FilePath);
        Assert.Equal("Office Printer", printService.LastRequest.PrinterName);
        Assert.Equal(2, printService.LastRequest.Copies);
        Assert.Equal("1", printService.LastRequest.PageRanges);
        Assert.Equal(PrintOrientationOption.Landscape, printService.LastRequest.Orientation);
        Assert.True(printService.LastRequest.FitToPage);
    }

    [Fact]
    public async Task SubmitPrintJobCommand_ShouldShowFriendlyError_WhenPrintFails()
    {
        using var temporaryFile = new TemporaryFile(".pdf");

        using var viewModel = CreateViewModel(
            filePickerService: new StubFilePickerService(temporaryFile.Path),
            documentOpener: new StubDocumentOpener(
                new DocumentSession(
                    DocumentId.New(),
                    new DocumentMetadata(Path.GetFileName(temporaryFile.Path), temporaryFile.Path, DocumentType.Pdf, 1024, 1),
                    ViewportState.Default)),
            printService: new StubPrintService(
                ResultFactory.Failure(
                    AppError.Unsupported(
                        "print.platform.unsupported",
                        "Printing is not available on this platform yet.")),
                [new PrintDestinationInfo("Office Printer", true)],
                supportsSystemPrintDialog: false));

        await viewModel.OpenCommand.ExecuteAsync(null);
        await viewModel.PrintDocumentCommand.ExecuteAsync(null);
        await viewModel.SubmitPrintJobCommand.ExecuteAsync(null);

        const string ExpectedMessage =
            "The document could not be printed.\n\nTechnical details: Printing is not available on this platform yet.";

        Assert.Equal("Print failed", viewModel.UserMessageTitle);
        Assert.Equal("Print failed", viewModel.StatusText);
        Assert.Equal(ExpectedMessage, viewModel.UserMessage);
        Assert.True(viewModel.IsPrintPanelOpen);
        Assert.Equal(ExpectedMessage, viewModel.PrintPanelNotice);
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

        Assert.Equal("Save PDF without page", filePickerService.LastSaveTitle);
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
        IPdfDocumentStructureService? pdfDocumentStructureService = null,
        IUserPreferencesService? userPreferencesService = null,
        IPrintService? printService = null,
        IDocumentTextAnalysisOrchestrator? textAnalysisOrchestrator = null,
        IDocumentTextSelectionService? textSelectionService = null)
    {
        var sessionStore = new InMemoryDocumentSessionStore();
        var viewportStore = new InMemoryPageViewportStore();
        var orchestrator = renderOrchestrator ?? new StubRenderOrchestrator();
        var structureService = pdfDocumentStructureService ?? new StubPdfDocumentStructureService();
        var activePrintService = printService ?? new StubPrintService(ResultFactory.Success());
        var activeTextAnalysisOrchestrator = textAnalysisOrchestrator ?? new StubDocumentTextAnalysisOrchestrator();
        var activeTextSelectionService = textSelectionService ?? new StubDocumentTextSelectionService();
        var activeUserPreferencesService = userPreferencesService ?? new StubUserPreferencesService();
        var localizationService = CreateLocalizationService(activeUserPreferencesService);

        return new MainWindowViewModel(
            filePickerService ?? new StubFilePickerService(),
            activePrintService,
            new OpenDocumentUseCase(documentOpener ?? new StubDocumentOpener(), sessionStore, NoOpPerformanceMetrics.Instance, orchestrator),
            new CloseDocumentUseCase(sessionStore, NoOpPerformanceMetrics.Instance, orchestrator),
            new PrintDocumentUseCase(activePrintService),
            new ShowSystemPrintDialogUseCase(activePrintService),
            new LoadDocumentTextUseCase(activeTextAnalysisOrchestrator),
            new RunDocumentOcrUseCase(activeTextAnalysisOrchestrator),
            new CancelDocumentTextAnalysisUseCase(activeTextAnalysisOrchestrator),
            new SearchDocumentTextUseCase(),
            new ResolveDocumentTextSelectionUseCase(activeTextSelectionService),
            new ChangePageUseCase(sessionStore),
            new ChangeZoomUseCase(sessionStore),
            new RotateDocumentUseCase(sessionStore),
            new RotatePdfPagesUseCase(structureService),
            new DeletePdfPagesUseCase(structureService),
            new ExtractPdfPagesUseCase(structureService),
            new ReorderPdfPagesUseCase(structureService),
            new StubPdfMarkupService(),
            new StubImageMarkupService(),
            new StubSignatureAssetStore(),
            orchestrator,
            sessionStore,
            recentFilesService ?? CreateRecentFilesService(),
            viewportStore,
            activeUserPreferencesService,
            localizationService,
            new LocalizedErrorFormatter(localizationService));
    }

    private static IRecentFilesService CreateRecentFilesService()
    {
        return new InMemoryRecentFilesService(Options.Create(new AppOptions()));
    }

    private static FileLocalizationService CreateLocalizationService(IUserPreferencesService userPreferencesService)
    {
        return new FileLocalizationService(
            NullLogger<FileLocalizationService>.Instance,
            userPreferencesService,
            Options.Create(new AppOptions
            {
                LocalizationPath = ResolveLocalizationCatalogRoot()
            }));
    }

    private static string ResolveLocalizationCatalogRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "Velune.Presentation",
                "Resources",
                "Localization");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the localization catalog root for unit tests.");
    }

    private static DocumentTextIndex CreateDocumentTextIndex(string text)
    {
        return new DocumentTextIndex(
            "/tmp/document.pdf",
            DocumentType.Pdf,
            [
                new PageTextContent(
                    new PageIndex(0),
                    TextSourceKind.EmbeddedPdfText,
                    text,
                    [new TextRun(
                        text,
                        0,
                        text.Length,
                        [new NormalizedTextRegion(0.1, 0.1, 0.4, 0.08)])],
                    1000,
                    1400)
            ],
            ["eng"]);
    }

    private static DocumentTextAnalysisResult CreateTextAnalysisResult(
        DocumentTextAnalysisRequest request,
        DocumentTextIndex? index = null,
        AppError? error = null,
        bool requiresOcr = false,
        bool isCanceled = false)
    {
        return new DocumentTextAnalysisResult(
            Guid.NewGuid(),
            DocumentId.New(),
            request.JobKey,
            TimeSpan.Zero,
            index,
            error,
            isCanceled,
            IsObsolete: false,
            requiresOcr);
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

    private sealed record StubImageDocumentSession(
        DocumentId Id,
        DocumentMetadata Metadata,
        ViewportState Viewport,
        ImageMetadata ImageMetadata) : IImageDocumentSession
    {
        public IDocumentSession WithViewport(ViewportState viewport)
        {
            return this with
            {
                Viewport = viewport
            };
        }
    }

    private sealed class StubRenderOrchestrator : IRenderOrchestrator
    {
        private readonly RenderedPage? _renderedPage;
        private readonly bool _isCanceled;

        public StubRenderOrchestrator(RenderedPage? renderedPage = null, bool isCanceled = true)
        {
            _renderedPage = renderedPage;
            _isCanceled = isCanceled;
        }

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
                        _renderedPage is null
                            ? null
                            : new RenderedPage(
                                request.PageIndex,
                                _renderedPage.PixelData.ToArray(),
                                _renderedPage.Width,
                                _renderedPage.Height),
                        null,
                        _isCanceled,
                        false)));
        }

        public bool Cancel(Guid jobId)
        {
            return true;
        }

        public Task CancelDocumentJobsAsync(DocumentId documentId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }

    private sealed class StubPdfMarkupService : IPdfMarkupService
    {
        public Task<Result<string>> ApplyAnnotationsAsync(
            ApplyPdfAnnotationsRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ResultFactory.Success(request.OutputPath));
        }
    }

    private sealed class StubImageMarkupService : IImageMarkupService
    {
        public Task<Result<string>> FlattenAnnotationsAsync(
            ApplyImageAnnotationsRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ResultFactory.Success(request.OutputPath));
        }
    }

    private sealed class StubSignatureAssetStore : ISignatureAssetStore
    {
        private readonly List<SignatureAsset> _assets = [];

        public IReadOnlyList<SignatureAsset> GetAll()
        {
            return _assets.ToArray();
        }

        public Result<SignatureAsset> Import(string sourceImagePath)
        {
            var asset = new SignatureAsset(
                Guid.NewGuid().ToString("N"),
                Path.GetFileNameWithoutExtension(sourceImagePath),
                sourceImagePath,
                120,
                60,
                DateTimeOffset.UtcNow);
            _assets.Add(asset);
            return ResultFactory.Success(asset);
        }

        public AppResult Delete(string assetId)
        {
            var removed = _assets.RemoveAll(asset => string.Equals(asset.Id, assetId, StringComparison.Ordinal));
            return removed > 0
                ? ResultFactory.Success()
                : ResultFactory.Failure(AppError.NotFound("signature.asset.not_found", "The test signature was not found."));
        }

        public Result<SignatureAsset> SaveInkSignature(string displayName, IReadOnlyList<NormalizedPoint> points)
        {
            var asset = new SignatureAsset(
                Guid.NewGuid().ToString("N"),
                displayName,
                "/tmp/signature.png",
                120,
                60,
                DateTimeOffset.UtcNow);
            _assets.Add(asset);
            return ResultFactory.Success(asset);
        }
    }

    private sealed class StubDocumentTextAnalysisOrchestrator : IDocumentTextAnalysisOrchestrator
    {
        private readonly Func<DocumentTextAnalysisRequest, DocumentTextAnalysisResult> _resultFactory;

        public StubDocumentTextAnalysisOrchestrator(
            Func<DocumentTextAnalysisRequest, DocumentTextAnalysisResult>? resultFactory = null)
        {
            _resultFactory = resultFactory ?? (request => CreateTextAnalysisResult(
                request,
                index: CreateDocumentTextIndex("Velune default text")));
        }

        public List<DocumentTextAnalysisRequest> Requests { get; } = [];

        public List<Guid> CancelledJobIds { get; } = [];

        public DocumentTextJobHandle Submit(DocumentTextAnalysisRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            Requests.Add(request);

            var jobId = Guid.NewGuid();
            var result = _resultFactory(request) with
            {
                JobId = jobId
            };

            return new DocumentTextJobHandle(jobId, Task.FromResult(result));
        }

        public bool Cancel(Guid jobId)
        {
            CancelledJobIds.Add(jobId);
            return true;
        }

        public void Dispose()
        {
        }
    }

    private sealed class StubDocumentTextSelectionService : IDocumentTextSelectionService
    {
        public Result<DocumentTextSelectionResult> Resolve(DocumentTextSelectionRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            return ResultFactory.Success(
                new DocumentTextSelectionResult(
                    request.PageIndex,
                    null,
                    [],
                    TextSourceKind.Ocr));
        }
    }

    private sealed class StubUserPreferencesService : IUserPreferencesService
    {
        public StubUserPreferencesService(UserPreferences? current = null)
        {
            Current = current ?? UserPreferences.CreateDefault(64) with
            {
                Language = AppLanguagePreference.English
            };
        }

        public UserPreferences Current { get; private set; }

        public event EventHandler? PreferencesChanged;

        public Task SaveAsync(UserPreferences preferences, CancellationToken cancellationToken = default)
        {
            Current = preferences;
            PreferencesChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }

    private sealed class StubPrintService : IPrintService
    {
        private readonly AppResult _result;
        private readonly IReadOnlyList<PrintDestinationInfo> _printers;
        private readonly AppResult _systemDialogResult;

        public StubPrintService(
            AppResult result,
            IReadOnlyList<PrintDestinationInfo>? printers = null,
            bool supportsSystemPrintDialog = false,
            AppResult? systemDialogResult = null)
        {
            _result = result;
            _printers = printers ?? [];
            SupportsSystemPrintDialog = supportsSystemPrintDialog;
            _systemDialogResult = systemDialogResult ?? ResultFactory.Success();
        }

        public bool SupportsSystemPrintDialog { get; }

        public int GetAvailablePrintersCallCount { get; private set; }

        public PrintDocumentRequest? LastRequest { get; private set; }

        public string? LastSystemDialogFilePath { get; private set; }

        public Task<AppResult> ShowSystemPrintDialogAsync(string filePath, CancellationToken cancellationToken = default)
        {
            LastSystemDialogFilePath = filePath;
            return Task.FromResult(_systemDialogResult);
        }

        public Task<Result<IReadOnlyList<PrintDestinationInfo>>> GetAvailablePrintersAsync(CancellationToken cancellationToken = default)
        {
            GetAvailablePrintersCallCount++;
            return Task.FromResult(ResultFactory.Success(_printers));
        }

        public Task<AppResult> PrintAsync(PrintDocumentRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(_result);
        }
    }

    private sealed class TemporaryFile : IDisposable
    {
        public TemporaryFile(string extension)
        {
            Path = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), extension);
            File.WriteAllText(Path, "temporary");
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (File.Exists(Path))
                {
                    File.Delete(Path);
                }
            }
            catch
            {
                // Best-effort cleanup for temporary test files.
            }
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
