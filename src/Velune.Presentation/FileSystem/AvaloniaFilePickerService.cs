using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Velune.Application.Documents;

namespace Velune.Presentation.FileSystem;

/// <summary>
/// Avalonia-based implementation of <see cref="IFilePickerService"/> using platform storage APIs.
/// </summary>
public sealed class AvaloniaFilePickerService : IFilePickerService
{
    private readonly TopLevelProvider _topLevelProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaFilePickerService"/> class.
    /// </summary>
    /// <param name="topLevelProvider">Provider for the active top-level window.</param>
    public AvaloniaFilePickerService(TopLevelProvider topLevelProvider)
    {
        ArgumentNullException.ThrowIfNull(topLevelProvider);
        _topLevelProvider = topLevelProvider;
    }

    /// <inheritdoc />
    public async Task<string?> PickOpenFileAsync(CancellationToken cancellationToken = default)
    {
        TopLevel topLevel = _topLevelProvider.GetTopLevel()
            ?? throw new InvalidOperationException("No active TopLevel is available.");
        IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Open document",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    CreateSupportedDocumentsFileType(),
                    CreatePdfFileType(),
                    CreateImagesFileType(),
                    FilePickerFileTypes.All
                ]
            });

        if (files.Count == 0)
        {
            return null;
        }

        return files[0].TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> PickOpenMergeSourceFilesAsync(
        string title,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        TopLevel topLevel = _topLevelProvider.GetTopLevel()
            ?? throw new InvalidOperationException("No active TopLevel is available.");

        IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = true,
                FileTypeFilter =
                [
                    CreateSupportedDocumentsFileType(),
                    CreatePdfFileType(),
                    CreateImagesFileType()
                ]
            });

        return files
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<string?> PickSavePdfFileAsync(
        string title,
        string suggestedFileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedFileName);

        TopLevel topLevel = _topLevelProvider.GetTopLevel()
            ?? throw new InvalidOperationException("No active TopLevel is available.");

        IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = title,
                SuggestedFileName = suggestedFileName,
                DefaultExtension = "pdf",
                FileTypeChoices = [CreatePdfFileType("PDF document")]
            });

        return file?.TryGetLocalPath();
    }

    private static FilePickerFileType CreateSupportedDocumentsFileType()
    {
        return new FilePickerFileType("Supported documents")
        {
            Patterns = SupportedDocumentFormats.AllExtensions.Select(extension => $"*{extension}").ToArray(),
            MimeTypes =
            [
                "application/pdf",
                "image/png",
                "image/jpeg",
                "image/webp"
            ]
        };
    }

    private static FilePickerFileType CreatePdfFileType(string label = "PDF documents")
    {
        return new FilePickerFileType(label)
        {
            Patterns = SupportedDocumentFormats.PdfFileExtensions.Select(extension => $"*{extension}").ToArray(),
            MimeTypes = ["application/pdf"]
        };
    }

    private static FilePickerFileType CreateImagesFileType()
    {
        return new FilePickerFileType("Images")
        {
            Patterns = SupportedDocumentFormats.ImageFileExtensions.Select(extension => $"*{extension}").ToArray(),
            MimeTypes =
            [
                "image/png",
                "image/jpeg",
                "image/webp"
            ]
        };
    }
}
