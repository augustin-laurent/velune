using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Velune.Presentation.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
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

    public bool IsEmptyStateVisible => !HasOpenDocument;

    partial void OnHasOpenDocumentChanged(bool value)
    {
        OnPropertyChanged(nameof(IsEmptyStateVisible));
    }

    [RelayCommand]
    private void Open()
    {
        StatusText = "Open command triggered";
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
}
