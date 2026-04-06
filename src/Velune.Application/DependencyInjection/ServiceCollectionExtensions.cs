using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Velune.Application.Abstractions;
using Velune.Application.Configuration;
using Velune.Application.UseCases;

namespace Velune.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<AppOptions>(
            configuration.GetSection(AppOptions.SectionName));

        services.AddSingleton<IDocumentSessionStore, InMemoryDocumentSessionStore>();
        services.AddSingleton<IRecentFilesService, InMemoryRecentFilesService>();
        services.AddSingleton<IPageViewportStore, InMemoryPageViewportStore>();

        services.AddTransient<OpenDocumentUseCase>();
        services.AddTransient<CloseDocumentUseCase>();
        services.AddTransient<RenderVisiblePageUseCase>();
        services.AddTransient<GenerateThumbnailUseCase>();
        services.AddTransient<ChangeZoomUseCase>();
        services.AddTransient<RotateDocumentUseCase>();
        services.AddTransient<ChangePageUseCase>();

        return services;
    }
}
