using Avalonia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Velune.Application.DependencyInjection;
using Velune.Infrastructure.DependencyInjection;
using Velune.Presentation.DependencyInjection;

namespace Velune.App;

internal static class Program
{
    private static IHost? _appHost;

    [STAThread]
    public static void Main(string[] args)
    {
        _appHost = CreateHost(args).Build();
        _appHost.Services.GetRequiredService<StartupLogger>().LogStartup();

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            if (global::Avalonia.Application.Current is App app)
            {
                app.DisposeResources();
            }

            _appHost.Dispose();
            _appHost = null;
        }
    }

    private static HostApplicationBuilder CreateHost(string[] args)
    {
        var environment =
            Environment.GetEnvironmentVariable("VELUNE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";

        var settings = new HostApplicationBuilderSettings
        {
            Args = args,
            EnvironmentName = environment
        };

        var builder = Host.CreateApplicationBuilder(settings);

        builder.Configuration.AddEnvironmentVariables(prefix: "VELUNE_");

        builder.Services
            .AddPresentation()
            .AddApplication(builder.Configuration)
            .AddInfrastructure()
            .AddSingleton<StartupLogger>();

        return builder;
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    internal static IHost AppHost =>
        _appHost ?? throw new InvalidOperationException("The host has not been initialized yet.");
}
