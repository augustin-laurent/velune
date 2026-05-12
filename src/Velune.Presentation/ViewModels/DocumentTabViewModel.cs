using CommunityToolkit.Mvvm.ComponentModel;
using Velune.Domain.ValueObjects;

namespace Velune.Presentation.ViewModels;

/// <summary>
/// View model representing a single open document tab in the Windows ribbon shell.
/// </summary>
public sealed partial class DocumentTabViewModel : ObservableObject
{
    /// <summary>
    /// Initializes a new document tab view model.
    /// </summary>
    /// <param name="sessionId">The document session identifier.</param>
    /// <param name="title">The tab display title.</param>
    /// <param name="filePath">The document file path.</param>
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
