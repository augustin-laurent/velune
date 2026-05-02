using Velune.Application.Documents;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Velune.Windows.Services;

public interface IWindowsFileDialogService
{
    Task<string?> PickOpenDocumentAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> PickMergeDocumentsAsync(CancellationToken cancellationToken = default);

    Task<string?> PickSavePdfAsync(string suggestedFileName, CancellationToken cancellationToken = default);
}

public sealed class WindowsFileDialogService : IWindowsFileDialogService
{
    private readonly WindowsWindowContext _windowContext;
    private readonly IWindowsTextCatalog _textCatalog;

    public WindowsFileDialogService(WindowsWindowContext windowContext, IWindowsTextCatalog textCatalog)
    {
        ArgumentNullException.ThrowIfNull(windowContext);
        ArgumentNullException.ThrowIfNull(textCatalog);

        _windowContext = windowContext;
        _textCatalog = textCatalog;
    }

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
