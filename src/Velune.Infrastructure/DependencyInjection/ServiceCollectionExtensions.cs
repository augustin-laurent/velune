using Microsoft.Extensions.DependencyInjection;
using Velune.Application.Abstractions;
using Velune.Domain.Abstractions;
using Velune.Infrastructure.Documents;
using Velune.Infrastructure.FileSystem;
using Velune.Infrastructure.Image;
using Velune.Infrastructure.Pdf;
using Velune.Infrastructure.Rendering;

namespace Velune.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<TopLevelProvider>();
        services.AddSingleton<IFilePickerService, AvaloniaFilePickerService>();

        services.AddSingleton<PdfiumInitializer>();

        services.AddTransient<PdfiumDocumentOpener>();
        services.AddTransient<AvaloniaImageDocumentOpener>();

        services.AddTransient<CompositeDocumentOpener>();
        services.AddTransient<IDocumentOpener>(sp =>
            sp.GetRequiredService<CompositeDocumentOpener>());

        services.AddTransient<PdfiumRenderService>();
        services.AddTransient<ImageRenderService>();

        services.AddTransient<CompositeRenderService>();
        services.AddTransient<IRenderService>(sp =>
            sp.GetRequiredService<CompositeRenderService>());

        services.AddTransient<IThumbnailService, UnsupportedThumbnailService>();

        return services;
    }
}
