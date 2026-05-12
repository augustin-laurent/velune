using Microsoft.UI.Xaml;
using WinRT;

namespace Velune.Windows;

/// <summary>
/// Application entry point for the WinUI desktop process.
/// </summary>
public static class Program
{
    /// <summary>
    /// Initializes COM wrappers and starts the WinUI application.
    /// </summary>
    /// <param name="args">Command-line arguments may include file paths to open.</param>
    [STAThread]
    public static void Main(string[] args)
    {
        ComWrappersSupport.InitializeComWrappers();
        Microsoft.UI.Xaml.Application.Start(_ =>
        {
            var app = new App(args);
            GC.KeepAlive(app);
        });
    }
}
