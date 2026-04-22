using Microsoft.Extensions.Options;
using Velune.Application.Configuration;
using Velune.Application.DTOs;
using Velune.Domain.Annotations;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Velune.Infrastructure.Annotations;
using Velune.Infrastructure.Pdf;

namespace Velune.Tests.Integration.Infrastructure;

[Collection("IntegrationSerial")]
public sealed class PdfAnnotationIntegrationTests
{
    [Fact]
    public async Task ApplyAnnotationsAsync_ShouldEmbedAnnotationOverlayIntoPdfPage()
    {
        await using var workspace = await PdfAnnotationWorkspace.CreateAsync();

        var initializer = new PdfiumInitializer();
        var opener = new PdfiumDocumentOpener(initializer);
        var renderer = new PdfiumRenderService(initializer);
        var session = opener.Open(workspace.SourcePdfPath) as PdfiumDocumentSession;
        var annotatedSession = default(PdfiumDocumentSession);

        try
        {
            Assert.NotNull(session);

            var saveResult = await workspace.MarkupService.ApplyAnnotationsAsync(
                new ApplyPdfAnnotationsRequest(
                    session,
                    workspace.SourcePdfPath,
                    workspace.OutputPdfPath,
                    [
                        new DocumentAnnotation(
                            Guid.NewGuid(),
                            DocumentAnnotationKind.Highlight,
                            new PageIndex(0),
                            new AnnotationAppearance("#E2B54B", "#F9DE86", 1.5, 0.58),
                            new NormalizedTextRegion(0.10, 0.10, 0.24, 0.18))
                    ]));

            Assert.True(saveResult.IsSuccess, saveResult.Error?.Message);

            annotatedSession = opener.Open(workspace.OutputPdfPath) as PdfiumDocumentSession;
            Assert.NotNull(annotatedSession);

            initializer.EnsureInitialized();
            var pageHandle = PdfiumNative.FPDF_LoadPage(annotatedSession.Resource.Handle, 0);
            Assert.NotEqual(nint.Zero, pageHandle);
            var objectCount = PdfiumNative.FPDFPage_CountObjects(pageHandle);
            PdfiumNative.FPDF_ClosePage(pageHandle);

            var renderedPage = await renderer.RenderPageAsync(
                annotatedSession,
                new PageIndex(0),
                1.0,
                Rotation.Deg0);

            Assert.True(File.Exists(workspace.OutputPdfPath));
            Assert.True(renderedPage.Width >= 90);
            Assert.True(renderedPage.Height >= 90);

            var highlightedPixel = ReadPixel(renderedPage, 18, 18);
            var untouchedPixel = ReadPixel(renderedPage, renderedPage.Width - 10, renderedPage.Height - 10);
            var mirroredPixel = ReadPixel(renderedPage, 18, renderedPage.Height - 18);

            Assert.True(objectCount > 0, $"Expected at least one page object after saving annotations, got {objectCount}.");
            Assert.NotEqual((255, 255, 255), highlightedPixel);
            Assert.Equal((255, 255, 255), untouchedPixel);
            Assert.Equal((255, 255, 255), mirroredPixel);
        }
        finally
        {
            annotatedSession?.ReleaseResources();
            session?.ReleaseResources();
        }
    }

    private static (byte R, byte G, byte B) ReadPixel(RenderedPage renderedPage, int x, int y)
    {
        var buffer = renderedPage.PixelData.Span;
        var index = ((y * renderedPage.Width) + x) * 4;
        return (buffer[index + 2], buffer[index + 1], buffer[index]);
    }

    private sealed class PdfAnnotationWorkspace : IAsyncDisposable
    {
        private readonly string _workspacePath;

        private PdfAnnotationWorkspace(string workspacePath, SkiaPdfMarkupService markupService)
        {
            _workspacePath = workspacePath;
            MarkupService = markupService;
        }

        public string SourcePdfPath => Path.Combine(_workspacePath, "source.pdf");

        public string OutputPdfPath => Path.Combine(_workspacePath, "annotated.pdf");

        public SkiaPdfMarkupService MarkupService { get; }

        public static async Task<PdfAnnotationWorkspace> CreateAsync()
        {
            var workspacePath = Path.Combine(
                Path.GetTempPath(),
                "velune-pdf-annotation-tests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(workspacePath);
            MinimalPdfBuilder.CreateDocument(Path.Combine(workspacePath, "source.pdf"), new PdfPageSpec(100, 100));

            var appOptions = Options.Create(new AppOptions
            {
                SignatureLibraryPath = Path.Combine(workspacePath, "signature-library")
            });
            var initializer = new PdfiumInitializer();

            await Task.CompletedTask;

            return new PdfAnnotationWorkspace(
                workspacePath,
                new SkiaPdfMarkupService(
                    initializer,
                    new JsonSignatureAssetStore(appOptions)));
        }

        public ValueTask DisposeAsync()
        {
            try
            {
                if (Directory.Exists(_workspacePath))
                {
                    Directory.Delete(_workspacePath, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup for PDF annotation test artifacts.
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed record PdfPageSpec(int Width, int Height);

    private static class MinimalPdfBuilder
    {
        public static void CreateDocument(string outputPath, params PdfPageSpec[] pages)
        {
            ArgumentNullException.ThrowIfNull(outputPath);
            ArgumentNullException.ThrowIfNull(pages);

            using var stream = File.Create(outputPath);
            using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);

            writer.NewLine = "\n";
            writer.WriteLine("%PDF-1.4");
            writer.WriteLine("%VELUNE");

            var offsets = new List<long> { 0 };
            var totalObjectCount = 2 + (pages.Length * 2);

            WriteObject(writer, stream, offsets, 1, "<< /Type /Catalog /Pages 2 0 R >>");
            WriteObject(
                writer,
                stream,
                offsets,
                2,
                $"<< /Type /Pages /Kids [{string.Join(" ", Enumerable.Range(0, pages.Length).Select(index => $"{3 + (index * 2)} 0 R"))}] /Count {pages.Length} >>");

            for (var index = 0; index < pages.Length; index++)
            {
                var pageObjectNumber = 3 + (index * 2);
                var contentObjectNumber = pageObjectNumber + 1;
                var page = pages[index];

                WriteObject(
                    writer,
                    stream,
                    offsets,
                    pageObjectNumber,
                    $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {page.Width} {page.Height}] /Resources << >> /Contents {contentObjectNumber} 0 R >>");

                WriteObject(
                    writer,
                    stream,
                    offsets,
                    contentObjectNumber,
                    "<< /Length 0 >>\nstream\n\nendstream");
            }

            writer.Flush();
            var startXref = stream.Position;

            writer.WriteLine("xref");
            writer.WriteLine($"0 {totalObjectCount + 1}");
            writer.WriteLine("0000000000 65535 f ");

            for (var objectNumber = 1; objectNumber <= totalObjectCount; objectNumber++)
            {
                writer.WriteLine($"{offsets[objectNumber]:D10} 00000 n ");
            }

            writer.WriteLine("trailer");
            writer.WriteLine($"<< /Size {totalObjectCount + 1} /Root 1 0 R >>");
            writer.WriteLine("startxref");
            writer.WriteLine(startXref.ToString());
            writer.Write("%%EOF");
        }

        private static void WriteObject(
            StreamWriter writer,
            Stream stream,
            List<long> offsets,
            int objectNumber,
            string content)
        {
            writer.Flush();
            offsets.Add(stream.Position);
            writer.WriteLine($"{objectNumber} 0 obj");
            writer.WriteLine(content);
            writer.WriteLine("endobj");
            writer.Flush();
        }
    }
}
