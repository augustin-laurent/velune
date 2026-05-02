using CommunityToolkit.Mvvm.ComponentModel;
using Velune.Domain.ValueObjects;

namespace Velune.Presentation.ViewModels;

public sealed partial class DocumentTabViewModel : ObservableObject
{
    public DocumentTabViewModel(
        DocumentId sessionId,
        string title,
        string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        _sessionId = sessionId;
        _title = title;
        _filePath = filePath;
    }

    [ObservableProperty]
    private DocumentId _sessionId;

    [ObservableProperty]
    private string _filePath;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isDirty;
}
