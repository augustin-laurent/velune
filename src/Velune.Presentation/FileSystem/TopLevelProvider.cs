using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace Velune.Presentation.FileSystem;

/// <summary>
/// Provides access to the current Avalonia top-level visual root.
/// </summary>
public sealed class TopLevelProvider
{
    /// <summary>
    /// Returns the active <see cref="TopLevel"/> instance, or null if unavailable.
    /// </summary>
    /// <returns>The current top-level, or null.</returns>
    public TopLevel? GetTopLevel()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return TopLevel.GetTopLevel(desktop.MainWindow);
        }

        return null;
    }
}
