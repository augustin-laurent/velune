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

    public MainWindowViewModel(
        IFilePickerService filePickerService,
        OpenDocumentUseCase openDocumentUseCase)
    {
        ArgumentNullException.ThrowIfNull(filePickerService);
        ArgumentNullException.ThrowIfNull(openDocumentUseCase);

        _filePickerService = filePickerService;
        _openDocumentUseCase = openDocumentUseCase;
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

    public bool IsEmptyStateVisible => !HasOpenDocument;
    public bool HasUserMessage => !string.IsNullOrWhiteSpace(UserMessage);
    public bool CanDismissUserMessage => HasUserMessage;

    partial void OnHasOpenDocumentChanged(bool value)
    {
        OnPropertyChanged(nameof(IsEmptyStateVisible));
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

            var session = result.Value;
            if (session is null)
            {
                UserMessage = "No document session was created.";
                StatusText = "Open failed";
                return;
            }

            HasOpenDocument = true;
            UserMessage = null;
            EmptyStateTitle = session.Metadata.FileName;
            EmptyStateDescription = $"Opened {session.Metadata.DocumentType} document.";
            StatusText = $"Opened {session.Metadata.FileName}";
        }
        catch (Exception ex)
        {
            UserMessage = ex.Message;
            StatusText = "Open failed";
        }
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
}
