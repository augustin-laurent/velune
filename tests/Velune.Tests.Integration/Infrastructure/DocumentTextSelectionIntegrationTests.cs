using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Velune.Application.Abstractions;
using Velune.Application.Configuration;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Application.Text;
using Velune.Application.UseCases;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Velune.Infrastructure.Documents;
using Velune.Infrastructure.Image;
using Velune.Infrastructure.Pdf;
using Velune.Infrastructure.Text;

namespace Velune.Tests.Integration.Infrastructure;

[Collection("IntegrationSerial")]
public sealed class DocumentTextSelectionIntegrationTests
{
    [Fact]
    public async Task SamplePdf_ShouldSelectExactEmbeddedTextFromDocumentSpace()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        string pdfPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample.pdf");
        IDocumentTextService textService = CreateTextService(temporaryDirectory.Path);
        var selectionService = new DocumentTextSelectionService();
        var searchUseCase = new SearchDocumentTextUseCase();
        IDocumentSession session = await OpenSessionAsync(pdfPath);

        try
        {
            Result<DocumentTextLoadResult> loadResult = await textService.LoadAsync(session, ["eng"]);
            Assert.True(loadResult.IsSuccess, loadResult.Error?.Message);
            Assert.NotNull(loadResult.Value?.Index);

            DocumentTextIndex index = loadResult.Value!.Index!;
            Result<IReadOnlyList<SearchHit>> hitResult = searchUseCase.Execute(
                new SearchDocumentTextRequest(
                    index,
                    new SearchQuery("Velune")));

            Assert.True(hitResult.IsSuccess);
            Assert.NotNull(hitResult.Value);
            SearchHit hit = Assert.Single(hitResult.Value);
            NormalizedTextRegion region = Assert.Single(hit.Regions);
            var request = new DocumentTextSelectionRequest(
                session,
                index,
                hit.PageIndex,
                CreatePoint(index.Pages[0], region, 0.15),
                CreatePoint(index.Pages[0], region, 0.85));

            Result<DocumentTextSelectionResult> selectionResult = selectionService.Resolve(request);

            Assert.True(selectionResult.IsSuccess, selectionResult.Error?.Message);
            Assert.NotNull(selectionResult.Value);
            Assert.Equal("Velune", NormalizeText(selectionResult.Value!.SelectedText));
            Assert.NotEmpty(selectionResult.Value.Regions);

            NormalizedTextRegion selectionBounds = ComputeBounds(selectionResult.Value.Regions);
            NormalizedTextRegion expectedBounds = ComputeBounds(hit.Regions);
            Assert.InRange(Math.Abs(selectionBounds.X - expectedBounds.X), 0, 0.02);
            Assert.InRange(Math.Abs(selectionBounds.Y - expectedBounds.Y), 0, 0.02);
            Assert.InRange(Math.Abs(selectionBounds.Width - expectedBounds.Width), 0, 0.04);
            Assert.InRange(Math.Abs(selectionBounds.Height - expectedBounds.Height), 0, 0.04);
        }
        finally
        {
            DisposeSession(session);
        }
    }

    [RequiresTesseractFact]
    public async Task OcrImage_ShouldSelectRecognizedWordFromDocumentSpace()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        string imagePath = Path.Combine(temporaryDirectory.Path, "ocr-image.pgm");
        TextRaster raster = OcrTestAssetBuilder.CreateRaster("TEST");
        OcrTestAssetBuilder.WritePgm(imagePath, raster);

        IDocumentTextService textService = CreateTextService(temporaryDirectory.Path);
        var selectionService = new DocumentTextSelectionService();
        IDocumentSession session = CreateImageSession(imagePath, raster);

        try
        {
            Result<DocumentTextIndex> ocrResult = await textService.RunOcrAsync(session, ["eng"]);
            Assert.True(ocrResult.IsSuccess, ocrResult.Error?.Message);
            Assert.NotNull(ocrResult.Value);

            PageTextContent page = ocrResult.Value!.Pages[0];
            TextRun run = Assert.Single(page.Runs);
            NormalizedTextRegion region = Assert.Single(run.Regions);
            var request = new DocumentTextSelectionRequest(
                session,
                ocrResult.Value,
                page.PageIndex,
                CreatePoint(page, region, 0.2),
                CreatePoint(page, region, 0.8));

            Result<DocumentTextSelectionResult> selectionResult = selectionService.Resolve(request);

            Assert.True(selectionResult.IsSuccess, selectionResult.Error?.Message);
            Assert.NotNull(selectionResult.Value);
            Assert.Contains("TEST", NormalizeText(selectionResult.Value!.SelectedText), StringComparison.OrdinalIgnoreCase);
            Assert.Single(selectionResult.Value.Regions);
        }
        finally
        {
            DisposeSession(session);
        }
    }

    private static DocumentTextSelectionPoint CreatePoint(
        PageTextContent page,
        NormalizedTextRegion region,
        double horizontalFactor)
    {
        return new DocumentTextSelectionPoint(
            (region.X + (region.Width * horizontalFactor)) * page.SourceWidth,
            (region.Y + (region.Height * 0.5)) * page.SourceHeight);
    }

    private static IDocumentTextService CreateTextService(string cachePath)
    {
        IOptions<AppOptions> options = Options.Create(new AppOptions
        {
            OcrCachePath = cachePath,
            TesseractExecutablePath = TesseractTestSupport.GetExecutablePath(),
            DefaultOcrLanguages = ["eng"]
        });
        return new DocumentTextService(
            new DocumentTextDiskCache(NullLogger<DocumentTextDiskCache>.Instance, options),
            new TesseractOcrEngine(options),
            new DispatchingRenderService(
                new PdfiumRenderService(IntegrationPdfium.Initializer, IntegrationPdfium.ExecutionGate),
                new ImageRenderService()),
            options);
    }

    private static async Task<IDocumentSession> OpenSessionAsync(string filePath)
    {
        if (string.Equals(Path.GetExtension(filePath), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return new PdfiumDocumentOpener(IntegrationPdfium.Initializer, IntegrationPdfium.ExecutionGate).Open(filePath);
        }

        var fileInfo = new FileInfo(filePath);
        ImageInfo imageInfo = ImageInfoReader.ReadPng(filePath);

        return new TestImageDocumentSession(
            DocumentId.New(),
            new DocumentMetadata(
                Path.GetFileName(filePath),
                filePath,
                DocumentType.Image,
                fileInfo.Length,
                1,
                pixelWidth: imageInfo.Width,
                pixelHeight: imageInfo.Height,
                formatLabel: "PNG image"),
            ViewportState.Default,
            new ImageMetadata(imageInfo.Width, imageInfo.Height));
    }

    private static IDocumentSession CreateImageSession(string filePath, TextRaster raster)
    {
        var fileInfo = new FileInfo(filePath);

        return new TestImageDocumentSession(
            DocumentId.New(),
            new DocumentMetadata(
                Path.GetFileName(filePath),
                filePath,
                DocumentType.Image,
                fileInfo.Length,
                1,
                pixelWidth: raster.Width,
                pixelHeight: raster.Height,
                formatLabel: "PGM image"),
            ViewportState.Default,
            new ImageMetadata(raster.Width, raster.Height));
    }

    private static void DisposeSession(IDocumentSession session)
    {
        if (session is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(
            ' ',
            value
                .ReplaceLineEndings(" ")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static NormalizedTextRegion ComputeBounds(IReadOnlyList<NormalizedTextRegion> regions)
    {
        double left = regions.Min(region => region.X);
        double top = regions.Min(region => region.Y);
        double right = regions.Max(region => region.X + region.Width);
        double bottom = regions.Max(region => region.Y + region.Height);

        return new NormalizedTextRegion(left, top, right - left, bottom - top);
    }

    private sealed record TestImageDocumentSession(
        DocumentId Id,
        DocumentMetadata Metadata,
        ViewportState Viewport,
        ImageMetadata ImageMetadata) : IImageDocumentSession
    {
        public IDocumentSession WithViewport(ViewportState viewport)
        {
            return this with
            {
                Viewport = viewport
            };
        }
    }
}
