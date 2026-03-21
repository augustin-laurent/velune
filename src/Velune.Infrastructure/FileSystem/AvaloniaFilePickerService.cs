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
        var topLevel = _topLevelProvider.GetTopLevel();
        if (topLevel is null)
        {
            return null;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open a document",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Supported files")
                {
                    Patterns = ["*.pdf", "*.png", "*.jpg", "*.jpeg", "*.webp"]
                }
            ]
        });

        if (files.Count == 0)
        {
            return null;
        }

        return files[0].TryGetLocalPath();
    }
}
