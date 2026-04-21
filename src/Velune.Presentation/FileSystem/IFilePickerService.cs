namespace Velune.Presentation.FileSystem;

public interface IFilePickerService
{
    Task<string?> PickOpenFileAsync(CancellationToken cancellationToken = default);

    Task<string?> PickSavePdfFileAsync(
        string title,
        string suggestedFileName,
        CancellationToken cancellationToken = default);
}
