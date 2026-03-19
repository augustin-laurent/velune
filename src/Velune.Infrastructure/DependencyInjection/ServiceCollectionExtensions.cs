using Microsoft.Extensions.DependencyInjection;
using Velune.Domain.Abstractions;
using Velune.Infrastructure.Documents;
using Velune.Infrastructure.Rendering;

namespace Velune.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient<IDocumentOpener, UnsupportedDocumentOpener>();
        services.AddTransient<IRenderService, UnsupportedRenderService>();
        services.AddTransient<IThumbnailService, UnsupportedThumbnailService>();

        return services;
    }
}
