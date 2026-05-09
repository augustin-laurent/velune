using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Velune.Application.Abstractions;
using Velune.Application.DependencyInjection;
using Velune.Infrastructure.DependencyInjection;
using Velune.Windows.Services;
using Velune.Windows.ViewModels;

namespace Velune.Windows;

/// <summary>
/// WinUI application class that configures DI and manages the application lifecycle.
/// </summary>
public sealed partial class App : Microsoft.UI.Xaml.Application
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
        _host = CreateHost(_args).Build();
        var startupPaths = _args.Where(File.Exists).ToArray();
        var windowCoordinator = _host.Services.GetRequiredService<WindowsWindowCoordinator>();
        if (startupPaths.Length == 0)
        {
            windowCoordinator.ShowWelcome();
            return;
        }

        await windowCoordinator.OpenWorkspaceWithFilesAsync(startupPaths);
    }

    private static HostApplicationBuilder CreateHost(string[] args)
    {
        var environment =
            Environment.GetEnvironmentVariable("VELUNE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            EnvironmentName = environment,
            ContentRootPath = AppContext.BaseDirectory
        });

        builder.Configuration.AddEnvironmentVariables(prefix: "VELUNE_");

        builder.Services
            .AddApplication(builder.Configuration)
            .AddInfrastructure()
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
