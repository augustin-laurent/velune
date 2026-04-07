using Avalonia.Platform.Storage;
using Velune.Application.Abstractions;

namespace Velune.Infrastructure.FileSystem;

public sealed class AvaloniaFilePickerService : IFilePickerService
{
    private readonly TopLevelProvider _topLevelProvider;

    public AvaloniaFilePickerService(TopLevelProvider topLevelProvider)
    {
        ArgumentNullException.ThrowIfNull(topLevelProvider);
        _topLevelProvider = topLevelProvider;
    }

    public async Task<string?> PickOpenFileAsync(CancellationToken cancellationToken = default)
    {
        var topLevel = _topLevelProvider.GetTopLevel()
            ?? throw new InvalidOperationException("No active TopLevel is available.");

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Open document",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Supported documents")
                    {
                        Patterns = ["*.pdf", "*.png", "*.jpg", "*.jpeg", "*.webp"],
                        MimeTypes =
                        [
                            "application/pdf",
                            "image/png",
                            "image/jpeg",
                            "image/webp"
                        ]
                    },
                    new FilePickerFileType("PDF documents")
                    {
                        Patterns = ["*.pdf"],
                        MimeTypes = ["application/pdf"]
                    },
                    new FilePickerFileType("Images")
                    {
                        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp"],
                        MimeTypes =
                        [
                            "image/png",
                            "image/jpeg",
                            "image/webp"
                        ]
                    },
                    FilePickerFileTypes.All
                ]
            });

        if (files.Count == 0)
        {
            return null;
        }

        return files[0].TryGetLocalPath();
    }
}
