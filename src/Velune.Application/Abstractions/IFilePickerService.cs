namespace Velune.Application.Abstractions;

public interface IFilePickerService
{
    Task<string?> PickOpenFileAsync(CancellationToken cancellationToken = default);

    Task<string?> PickSavePdfFileAsync(
        string title,
        string suggestedFileName,
        CancellationToken cancellationToken = default);
}
