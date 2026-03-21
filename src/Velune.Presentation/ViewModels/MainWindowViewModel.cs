using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.UseCases;

namespace Velune.Presentation.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IFilePickerService _filePickerService;
    private readonly OpenDocumentUseCase _openDocumentUseCase;
    private readonly CloseDocumentUseCase _closeDocumentUseCase;
    private readonly IDocumentSessionStore _documentSessionStore;

    public MainWindowViewModel(
        IFilePickerService filePickerService,
        OpenDocumentUseCase openDocumentUseCase,
        CloseDocumentUseCase closeDocumentUseCase,
        IDocumentSessionStore documentSessionStore)
    {
        ArgumentNullException.ThrowIfNull(filePickerService);
        ArgumentNullException.ThrowIfNull(openDocumentUseCase);
        ArgumentNullException.ThrowIfNull(closeDocumentUseCase);
        ArgumentNullException.ThrowIfNull(documentSessionStore);

        _filePickerService = filePickerService;
        _openDocumentUseCase = openDocumentUseCase;
        _closeDocumentUseCase = closeDocumentUseCase;
        _documentSessionStore = documentSessionStore;
    }

    [ObservableProperty]
    private string _title = "Velune";

    [ObservableProperty]
    private string _applicationTitle = "Velune";

    [ObservableProperty]
    private string _sidebarTitle = "Pages";

    [ObservableProperty]
    private string _emptyStateTitle = "Open a document";

    [ObservableProperty]
    private string _emptyStateDescription = "Open a PDF or an image to start viewing it.";

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _hasOpenDocument;

    [ObservableProperty]
    private string? _userMessage;

    [ObservableProperty]
    private string? _currentDocumentName;

    [ObservableProperty]
    private string? _currentDocumentType;

    [ObservableProperty]
    private string? _currentDocumentPath;

    [ObservableProperty]
    private int? _currentPage;

    [ObservableProperty]
    private string _currentZoom = "100%";

    [ObservableProperty]
    private string _currentRotation = "0°";

    public bool IsEmptyStateVisible => !HasOpenDocument;

    public bool HasUserMessage => !string.IsNullOrWhiteSpace(UserMessage);

    public bool CanDismissUserMessage => HasUserMessage;

    public bool HasDocumentInfo => HasOpenDocument && !string.IsNullOrWhiteSpace(CurrentDocumentName);

    partial void OnHasOpenDocumentChanged(bool value)
    {
        OnPropertyChanged(nameof(IsEmptyStateVisible));
        OnPropertyChanged(nameof(HasDocumentInfo));
        CloseCommand.NotifyCanExecuteChanged();
    }

    partial void OnUserMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasUserMessage));
        OnPropertyChanged(nameof(CanDismissUserMessage));
        DismissMessageCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task OpenAsync()
    {
        var filePath = await _filePickerService.PickOpenFileAsync();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            StatusText = "Open cancelled";
            return;
        }

        try
        {
            var result = await _openDocumentUseCase.ExecuteAsync(new OpenDocumentRequest(filePath));

            if (result.IsFailure)
            {
                UserMessage = result.Error?.Message ?? "Unable to open the selected document.";
                StatusText = "Open failed";
                return;
            }

            RefreshFromSession();

            UserMessage = null;
            StatusText = $"Opened {CurrentDocumentName}";
        }
        catch (Exception ex)
        {
            UserMessage = ex.Message;
            StatusText = "Open failed";
        }
    }

    [RelayCommand(CanExecute = nameof(HasOpenDocument))]
    private void Close()
    {
        _closeDocumentUseCase.Execute();
        ResetDocumentState();

        UserMessage = null;
        StatusText = "Document closed";
    }

    [RelayCommand]
    private void ZoomIn()
    {
        StatusText = "Zoom in command triggered";
    }

    [RelayCommand]
    private void ZoomOut()
    {
        StatusText = "Zoom out command triggered";
    }

    [RelayCommand]
    private void RotateLeft()
    {
        StatusText = "Rotate left command triggered";
    }

    [RelayCommand]
    private void RotateRight()
    {
        StatusText = "Rotate right command triggered";
    }

    [RelayCommand(CanExecute = nameof(CanDismissUserMessage))]
    private void DismissMessage()
    {
        UserMessage = null;
        StatusText = "Message dismissed";
    }

    [RelayCommand]
    private void SimulateError()
    {
        UserMessage = "Unable to load the requested document.";
        StatusText = "An error was simulated";
    }

    private void RefreshFromSession()
    {
        var session = _documentSessionStore.Current;
        if (session is null)
        {
            ResetDocumentState();
            return;
        }

        HasOpenDocument = true;
        CurrentDocumentName = session.Metadata.FileName;
        CurrentDocumentType = session.Metadata.DocumentType.ToString();
        CurrentDocumentPath = session.Metadata.FilePath;
        CurrentPage = session.Viewport.CurrentPage.Value + 1;
        CurrentZoom = $"{session.Viewport.ZoomFactor * 100:0}%";
        CurrentRotation = $"{(int)session.Viewport.Rotation}°";

        EmptyStateTitle = session.Metadata.FileName;
        EmptyStateDescription = $"Opened {session.Metadata.DocumentType} document.";
    }

    private void ResetDocumentState()
    {
        HasOpenDocument = false;
        CurrentDocumentName = null;
        CurrentDocumentType = null;
        CurrentDocumentPath = null;
        CurrentPage = null;
        CurrentZoom = "100%";
        CurrentRotation = "0°";

        EmptyStateTitle = "Open a document";
        EmptyStateDescription = "Open a PDF or an image to start viewing it.";
    }
}
