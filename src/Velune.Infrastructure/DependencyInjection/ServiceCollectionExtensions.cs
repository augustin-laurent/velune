using Microsoft.Extensions.DependencyInjection;
using Velune.Application.Abstractions;
using Velune.Domain.Abstractions;
using Velune.Infrastructure.Annotations;
using Velune.Infrastructure.Documents;
using Velune.Infrastructure.FileSystem;
using Velune.Infrastructure.Image;
using Velune.Infrastructure.Preferences;
using Velune.Infrastructure.Pdf;
using Velune.Infrastructure.Text;

namespace Velune.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IUserPreferencesService, JsonUserPreferencesService>();
        services.AddSingleton<IPrintService, SystemPrintService>();
        services.AddSingleton<IOcrEngine, TesseractOcrEngine>();
        services.AddSingleton<ISignatureAssetStore, JsonSignatureAssetStore>();
        services.AddTransient<IDocumentTextService, DocumentTextService>();
        services.AddTransient<IDocumentTextSelectionService, DocumentTextSelectionService>();

        services.AddSingleton<PdfiumInitializer>();

        services.AddTransient<PdfiumDocumentOpener>();
        services.AddTransient<AvaloniaImageDocumentOpener>();

        services.AddTransient<DispatchingDocumentOpener>();
        services.AddTransient<IDocumentOpener>(sp =>
            sp.GetRequiredService<DispatchingDocumentOpener>());

        services.AddTransient<PdfiumRenderService>();
        services.AddTransient<ImageRenderService>();
        services.AddTransient<IPdfDocumentStructureService, QpdfDocumentStructureService>();
        services.AddTransient<IPdfMarkupService, SkiaPdfMarkupService>();
        services.AddTransient<IImageMarkupService, SkiaImageMarkupService>();

        services.AddTransient<DispatchingRenderService>();
        services.AddTransient<IRenderService>(sp =>
            sp.GetRequiredService<DispatchingRenderService>());

        return services;
    }
}
