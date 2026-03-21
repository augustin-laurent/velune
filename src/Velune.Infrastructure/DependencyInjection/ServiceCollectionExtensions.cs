using Microsoft.Extensions.DependencyInjection;
using Velune.Application.Abstractions;
using Velune.Domain.Abstractions;
using Velune.Infrastructure.Documents;
using Velune.Infrastructure.FileSystem;
using Velune.Infrastructure.Rendering;

namespace Velune.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<TopLevelProvider>();
        services.AddSingleton<IFilePickerService, AvaloniaFilePickerService>();

        services.AddTransient<IDocumentOpener, SimpleDocumentOpener>();
        services.AddTransient<IRenderService, UnsupportedRenderService>();
        services.AddTransient<IThumbnailService, UnsupportedThumbnailService>();

        return services;
    }
}
