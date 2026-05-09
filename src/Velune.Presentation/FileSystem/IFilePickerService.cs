namespace Velune.Presentation.FileSystem;

/// <summary>
/// Abstraction over platform file picker dialogs.
/// </summary>
public interface IFilePickerService
{
    /// <summary>
    /// Shows an open-file dialog and returns the selected file path, or null if cancelled.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The selected file path, or null.</returns>
    Task<string?> PickOpenFileAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Shows a multi-select open-file dialog for merge source files.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of selected file paths.</returns>
    Task<IReadOnlyList<string>> PickOpenMergeSourceFilesAsync(
        string title,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Shows a save-file dialog restricted to PDF output.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="suggestedFileName">Default file name suggestion.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The chosen save path, or null if cancelled.</returns>
    Task<string?> PickSavePdfFileAsync(
        string title,
        string suggestedFileName,
        CancellationToken cancellationToken = default);
}
