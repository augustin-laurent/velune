using Microsoft.Extensions.DependencyInjection;
using Velune.Presentation.FileSystem;
using Velune.Presentation.ViewModels;
using Velune.Presentation.Views;

namespace Velune.Presentation.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<TopLevelProvider>();
        services.AddSingleton<IFilePickerService, AvaloniaFilePickerService>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

        return services;
    }
}
