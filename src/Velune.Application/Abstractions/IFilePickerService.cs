namespace Velune.Application.Abstractions;

public interface IFilePickerService
{
    Task<string?> PickOpenFileAsync(CancellationToken cancellationToken = default);
}
