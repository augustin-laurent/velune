using Velune.Application.Documents;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Velune.Windows.Services;

/// <summary>
/// Abstracts native Windows file picker operations.
/// </summary>
public interface IWindowsFileDialogService
{
    /// <summary>
    /// Shows a file picker to open a single supported document.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The selected file path, or null if cancelled.</returns>
    Task<string?> PickOpenDocumentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Shows a multi-file picker for selecting documents to merge.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of selected file paths.</returns>
    Task<IReadOnlyList<string>> PickMergeDocumentsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Shows a file picker to import a signature image.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The selected image path, or null if cancelled.</returns>
    Task<string?> PickSignatureImageAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Shows a save picker for exporting a PDF document.
    /// </summary>
    /// <param name="suggestedFileName">The default file name to suggest.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The chosen save path, or null if cancelled.</returns>
    Task<string?> PickSavePdfAsync(string suggestedFileName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implements file dialog operations using the Windows Storage Pickers API.
/// </summary>
public sealed class WindowsFileDialogService : IWindowsFileDialogService
{
    private readonly WindowsWindowContext _windowContext;
    private readonly IWindowsTextCatalog _textCatalog;

    /// <summary>
    /// Initializes the file dialog service with the window context and text catalog.
    /// </summary>
    /// <param name="windowContext">Provides the HWND for picker initialization.</param>
    /// <param name="textCatalog">Provides localized button labels for dialogs.</param>
    public WindowsFileDialogService(WindowsWindowContext windowContext, IWindowsTextCatalog textCatalog)
    {
        ArgumentNullException.ThrowIfNull(windowContext);
        ArgumentNullException.ThrowIfNull(textCatalog);

        _windowContext = windowContext;
        _textCatalog = textCatalog;
    }

    /// <inheritdoc />
    public async Task<string?> PickOpenDocumentAsync(CancellationToken cancellationToken = default)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            ViewMode = PickerViewMode.Thumbnail,
            CommitButtonText = _textCatalog.GetString("windows.dialog.open")
        };

        AddSupportedDocumentFilters(picker.FileTypeFilter);
        InitializeWithWindow.Initialize(picker, _windowContext.GetWindowHandle());

        var file = await picker.PickSingleFileAsync().AsTask(cancellationToken);
        return file?.Path;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> PickMergeDocumentsAsync(CancellationToken cancellationToken = default)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            ViewMode = PickerViewMode.List,
            CommitButtonText = _textCatalog.GetString("windows.dialog.merge")
        };

        picker.FileTypeFilter.Add(".pdf");
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".webp");
        InitializeWithWindow.Initialize(picker, _windowContext.GetWindowHandle());

        var files = await picker.PickMultipleFilesAsync().AsTask(cancellationToken);
        return files.Select(file => file.Path).Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
    }

    /// <inheritdoc />
    public async Task<string?> PickSignatureImageAsync(CancellationToken cancellationToken = default)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            ViewMode = PickerViewMode.Thumbnail,
            CommitButtonText = _textCatalog.GetString("panel.annotations.import_image")
        };

        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".webp");
        InitializeWithWindow.Initialize(picker, _windowContext.GetWindowHandle());

        var file = await picker.PickSingleFileAsync().AsTask(cancellationToken);
        return file?.Path;
    }

    /// <inheritdoc />
    public async Task<string?> PickSavePdfAsync(string suggestedFileName, CancellationToken cancellationToken = default)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = Path.GetFileNameWithoutExtension(suggestedFileName)
        };

        picker.FileTypeChoices.Add(_textCatalog.GetString("windows.dialog.pdf"), [".pdf"]);
        picker.DefaultFileExtension = ".pdf";
        InitializeWithWindow.Initialize(picker, _windowContext.GetWindowHandle());

        StorageFile? file = await picker.PickSaveFileAsync().AsTask(cancellationToken);
        return file?.Path;
    }

    private static void AddSupportedDocumentFilters(IList<string> fileTypeFilter)
    {
        foreach (var extension in SupportedDocumentFormats.AllExtensions)
        {
            fileTypeFilter.Add(extension);
        }
    }
}
