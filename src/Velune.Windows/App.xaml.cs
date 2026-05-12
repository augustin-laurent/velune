using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Velune.Application.DependencyInjection;
using Velune.Application.Documents;
using Velune.Infrastructure.DependencyInjection;
using Velune.Windows.Services;
using Velune.Windows.ViewModels;
using Velune.Windows.ViewModels.UndoSystem;

namespace Velune.Windows;

/// <summary>
/// WinUI application class that configures DI and manages the application lifecycle.
/// </summary>
public sealed partial class App
{
    private readonly string[] _args;
    private IHost? _host;

    /// <summary>
    /// Initializes the application with the given command-line arguments.
    /// </summary>
    /// <param name="args">Command-line arguments passed from the entry point.</param>
    public App(string[] args)
    {
        _args = args;
        InitializeComponent();
    }

    /// <summary>
    /// Gets the DI service provider from the built host.
    /// </summary>
    public IServiceProvider Services => _host?.Services
        ?? throw new InvalidOperationException("Host not initialized.");

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        VeluneTempDirectory.CleanupStale();
        _host = CreateHost(_args).Build();
        string[] startupPaths = _args.Where(File.Exists).ToArray();
        WindowsWindowCoordinator windowCoordinator = _host.Services.GetRequiredService<WindowsWindowCoordinator>();
        if (startupPaths.Length == 0)
        {
            windowCoordinator.ShowWelcome();
            return;
        }

        await windowCoordinator.OpenWorkspaceWithFilesAsync(startupPaths);
    }

    private static HostApplicationBuilder CreateHost(string[] args)
    {
        string environment =
            Environment.GetEnvironmentVariable("VELUNE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";

        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            EnvironmentName = environment,
            ContentRootPath = AppContext.BaseDirectory
        });

        builder.Configuration.AddEnvironmentVariables(prefix: "VELUNE_");

        builder.Services
            .AddApplication(builder.Configuration)
            .AddInfrastructure()
            .AddSingleton<UndoRedoManager>()
            .AddSingleton<WindowsWindowContext>()
            .AddSingleton<WindowsWindowCoordinator>()
            .AddSingleton<IWindowsTextCatalog, WindowsTextCatalog>()
            .AddSingleton<IWindowsFileDialogService, WindowsFileDialogService>()
            .AddSingleton<IWindowsPrintCoordinator, WindowsPrintCoordinator>()
            .AddSingleton<WindowsMainViewModel>()
            .AddTransient<PageOrganizerViewModel>()
            .AddTransient<MainWindow>()
            .AddTransient<WelcomeWindow>();

        return builder;
    }

}
