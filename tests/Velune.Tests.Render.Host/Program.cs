using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.UseCases;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Velune.Infrastructure.Documents;
using Velune.Infrastructure.Image;
using Velune.Infrastructure.Pdf;

namespace Velune.Tests.Render.Host;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            WriteResult(new HostResult(false, null, null, 0, 0, 0, "Usage: <file-path> [rotation-degrees]"));
            return 1;
        }

        var filePath = args[0];
        var rotation = args.Length == 2
            ? ParseRotation(args[1])
            : Rotation.Deg0;

        TestAvaloniaApp.Runner = () => RunScenarioAsync(filePath, rotation);

        return AppBuilder.Configure<TestAvaloniaApp>()
            .UsePlatformDetect()
            .StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
    }

    private static async Task<int> RunScenarioAsync(string filePath, Rotation rotation)
    {
        try
        {
            var initializer = new PdfiumInitializer();
            var sessionStore = new InMemoryDocumentSessionStore();
            var openDocumentUseCase = new OpenDocumentUseCase(
                new CompositeDocumentOpener(
                    new PdfiumDocumentOpener(initializer),
                    new AvaloniaImageDocumentOpener()),
                sessionStore,
                NoOpPerformanceMetrics.Instance);
            var renderUseCase = new RenderVisiblePageUseCase(
                sessionStore,
                new CompositeRenderService(
                    new PdfiumRenderService(initializer),
                    new ImageRenderService()));

            var openResult = await openDocumentUseCase.ExecuteAsync(new OpenDocumentRequest(filePath));
            if (openResult.IsFailure || sessionStore.Current is null)
            {
                WriteResult(new HostResult(
                    false,
                    null,
                    null,
                    0,
                    0,
                    0,
                    openResult.Error?.Message ?? "Unable to open document."));
                return 1;
            }

            var renderResult = await renderUseCase.ExecuteAsync(
                new RenderPageRequest(new PageIndex(0), 1.0, rotation));
            if (renderResult.IsFailure || renderResult.Value is null)
            {
                WriteResult(new HostResult(
                    false,
                    sessionStore.Current.Metadata.DocumentType.ToString(),
                    sessionStore.Current.Metadata.PageCount,
                    0,
                    0,
                    0,
                    renderResult.Error?.Message ?? "Unable to render document."));
                return 1;
            }

            WriteResult(new HostResult(
                true,
                sessionStore.Current.Metadata.DocumentType.ToString(),
                sessionStore.Current.Metadata.PageCount,
                renderResult.Value.Width,
                renderResult.Value.Height,
                renderResult.Value.PixelData.Length,
                null));

            if (sessionStore.Current is IDisposable disposable)
            {
                disposable.Dispose();
            }

            return 0;
        }
        catch (Exception exception)
        {
            WriteResult(new HostResult(false, null, null, 0, 0, 0, exception.ToString()));
            return 1;
        }
    }

    private static Rotation ParseRotation(string value)
    {
        return value switch
        {
            "0" => Rotation.Deg0,
            "90" => Rotation.Deg90,
            "180" => Rotation.Deg180,
            "270" => Rotation.Deg270,
            _ => throw new ArgumentOutOfRangeException(nameof(value), "Rotation must be one of 0, 90, 180 or 270.")
        };
    }

    private static void WriteResult(HostResult result)
    {
        Console.Out.Write(JsonSerializer.Serialize(result));
    }

    private sealed class TestAvaloniaApp : Avalonia.Application
    {
        public static Func<Task<int>>? Runner { get; set; }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new Window
                {
                    Width = 1,
                    Height = 1,
                    Opacity = 0,
                    ShowInTaskbar = false,
                    CanResize = false
                };

                _ = RunAndShutdownAsync(desktop);
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static async Task RunAndShutdownAsync(IClassicDesktopStyleApplicationLifetime desktop)
        {
            var exitCode = 1;

            try
            {
                if (Runner is not null)
                {
                    exitCode = await Runner();
                }
            }
            catch (Exception exception)
            {
                WriteResult(new HostResult(false, null, null, 0, 0, 0, exception.ToString()));
            }
            finally
            {
                desktop.Shutdown(exitCode);
            }
        }
    }

    private sealed class NoOpPerformanceMetrics : IPerformanceMetrics
    {
        public static readonly NoOpPerformanceMetrics Instance = new();

        public void RecordDocumentOpened(IDocumentSession session, TimeSpan duration)
        {
        }

        public void RecordViewerRenderCompleted(IDocumentSession session, RenderResult result)
        {
        }

        public void RecordThumbnailCompleted(IDocumentSession session, RenderResult result)
        {
        }

        public void Clear(DocumentId documentId)
        {
        }
    }

    private sealed record HostResult(
        bool Success,
        string? DocumentType,
        int? PageCount,
        int RenderedWidth,
        int RenderedHeight,
        int PixelBufferLength,
        string? Error);
}
