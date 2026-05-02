using Microsoft.UI.Xaml;
using WinRT;

namespace Velune.Windows;

public static class Program
{
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
