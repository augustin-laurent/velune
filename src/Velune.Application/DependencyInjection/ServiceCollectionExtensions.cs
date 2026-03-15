using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Velune.Application.Configuration;

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

        return services;
    }
}
