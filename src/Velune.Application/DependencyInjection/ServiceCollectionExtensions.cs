using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Velune.Application.Abstractions;
using Velune.Application.Configuration;
using Velune.Application.Instrumentation;
using Velune.Application.Rendering;
using Velune.Application.Text;
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
        services.AddSingleton<IPerformanceMetrics, DevelopmentPerformanceMetrics>();
        services.AddSingleton<IRenderMemoryCache, RenderMemoryCache>();
        services.AddSingleton<IThumbnailDiskCache, ThumbnailDiskCache>();
        services.AddSingleton<IRenderOrchestrator, RenderOrchestrator>();
        services.AddSingleton<IDocumentTextCache, DocumentTextDiskCache>();
        services.AddSingleton<IDocumentTextAnalysisOrchestrator, DocumentTextAnalysisOrchestrator>();

        services.AddTransient<OpenDocumentUseCase>();
        services.AddTransient<CloseDocumentUseCase>();
        services.AddTransient<RenderVisiblePageUseCase>();
        services.AddTransient<GenerateThumbnailUseCase>();
        services.AddTransient<LoadDocumentTextUseCase>();
        services.AddTransient<RunDocumentOcrUseCase>();
        services.AddTransient<CancelDocumentTextAnalysisUseCase>();
        services.AddTransient<SearchDocumentTextUseCase>();
        services.AddTransient<ResolveDocumentTextSelectionUseCase>();
        services.AddTransient<PrintDocumentUseCase>();
        services.AddTransient<ShowSystemPrintDialogUseCase>();
        services.AddTransient<ChangeZoomUseCase>();
        services.AddTransient<RotateDocumentUseCase>();
        services.AddTransient<RotatePdfPagesUseCase>();
        services.AddTransient<DeletePdfPagesUseCase>();
        services.AddTransient<ExtractPdfPagesUseCase>();
        services.AddTransient<MergePdfDocumentsUseCase>();
        services.AddTransient<ReorderPdfPagesUseCase>();
        services.AddTransient<ChangePageUseCase>();

        return services;
    }
}
