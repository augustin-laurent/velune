using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace Velune.Infrastructure.FileSystem;

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
