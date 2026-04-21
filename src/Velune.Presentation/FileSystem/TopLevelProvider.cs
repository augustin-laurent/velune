using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace Velune.Presentation.FileSystem;

public sealed class TopLevelProvider
{
    public TopLevel? GetTopLevel()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return TopLevel.GetTopLevel(desktop.MainWindow);
        }

        return null;
    }
}
