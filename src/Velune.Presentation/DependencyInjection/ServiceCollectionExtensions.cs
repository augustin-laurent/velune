using Microsoft.Extensions.DependencyInjection;
using Velune.Presentation.FileSystem;
using Velune.Presentation.Localization;
using Velune.Presentation.ViewModels;
using Velune.Presentation.Views;

namespace Velune.Presentation.DependencyInjection;

/// <summary>
/// Extension methods for registering presentation layer services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all presentation layer services into the container.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<TopLevelProvider>();
        services.AddSingleton<IFilePickerService, AvaloniaFilePickerService>();
        services.AddSingleton<ILocalizationService, FileLocalizationService>();
        services.AddSingleton<ILocalizedErrorFormatter, LocalizedErrorFormatter>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

        return services;
    }
}
