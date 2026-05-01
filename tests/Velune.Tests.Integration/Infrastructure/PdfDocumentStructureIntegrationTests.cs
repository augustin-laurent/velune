using System.Text;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Velune.Application.Configuration;
using Velune.Application.DTOs;
using Velune.Application.UseCases;
using Velune.Domain.ValueObjects;
using Velune.Infrastructure.Pdf;

namespace Velune.Tests.Integration.Infrastructure;

[Collection("IntegrationSerial")]
public sealed class PdfDocumentStructureIntegrationTests
{
    [RequiresQpdfFact]
    public async Task RotatePdfPages_ShouldPersistRotationOnSelectedPages()
    {
        await using var workspace = await PdfStructureTestWorkspace.CreateAsync();
        var useCase = workspace.CreateRotateUseCase();

        var outputPath = workspace.GetOutputPath("rotated.pdf");

        var result = await useCase.ExecuteAsync(
            new RotatePdfPagesRequest(workspace.SourcePdfPath, outputPath, [2], Rotation.Deg90));

        Assert.True(result.IsSuccess, result.Error?.Message);

        var documentInfo = PdfInspection.Load(outputPath);
        Assert.Equal([0, 90, 0], documentInfo.Pages.Select(page => page.RotationDegrees).ToArray());
    }

    [RequiresQpdfFact]
    public async Task ExtractPdfPages_ShouldCreateDocumentWithRequestedPagesInOrder()
    {
        await using var workspace = await PdfStructureTestWorkspace.CreateAsync();
        var useCase = workspace.CreateExtractUseCase();

        var outputPath = workspace.GetOutputPath("extracted.pdf");

        var result = await useCase.ExecuteAsync(
            new ExtractPdfPagesRequest(workspace.SourcePdfPath, outputPath, [3, 1]));

        Assert.True(result.IsSuccess, result.Error?.Message);

        var documentInfo = PdfInspection.Load(outputPath);
        Assert.Equal(2, documentInfo.PageCount);
        Assert.Equal([(90, 90), (100, 200)], documentInfo.Pages.Select(page => (page.Width, page.Height)).ToArray());
    }

    [RequiresQpdfFact]
    public async Task DeletePdfPages_ShouldRemoveRequestedPages()
    {
        await using var workspace = await PdfStructureTestWorkspace.CreateAsync();
        var useCase = workspace.CreateDeleteUseCase();

        var outputPath = workspace.GetOutputPath("trimmed.pdf");

        var result = await useCase.ExecuteAsync(
            new DeletePdfPagesRequest(workspace.SourcePdfPath, outputPath, [2]));

        Assert.True(result.IsSuccess, result.Error?.Message);

        var documentInfo = PdfInspection.Load(outputPath);
        Assert.Equal(2, documentInfo.PageCount);
        Assert.Equal([(100, 200), (90, 90)], documentInfo.Pages.Select(page => (page.Width, page.Height)).ToArray());
    }

    [RequiresQpdfFact]
    public async Task MergePdfDocuments_ShouldAppendSourceDocuments()
    {
        await using var workspace = await PdfStructureTestWorkspace.CreateAsync();
        var useCase = workspace.CreateMergeUseCase();

        var outputPath = workspace.GetOutputPath("merged.pdf");

        var result = await useCase.ExecuteAsync(
            new MergePdfDocumentsRequest([workspace.SourcePdfPath, workspace.SecondaryPdfPath], outputPath));

        Assert.True(result.IsSuccess, result.Error?.Message);

        var documentInfo = PdfInspection.Load(outputPath);
        Assert.Equal(5, documentInfo.PageCount);
        Assert.Equal(
            [(100, 200), (200, 100), (90, 90), (60, 60), (140, 80)],
            documentInfo.Pages.Select(page => (page.Width, page.Height)).ToArray());
    }

    [RequiresQpdfFact]
    public async Task MergePdfDocuments_ShouldAppendImageSourcesAsPages()
    {
        await using var workspace = await PdfStructureTestWorkspace.CreateAsync();
        var useCase = workspace.CreateMergeUseCase();

        var outputPath = workspace.GetOutputPath("merged-with-image.pdf");

        var result = await useCase.ExecuteAsync(
            new MergePdfDocumentsRequest([workspace.SourcePdfPath, workspace.ImagePath, workspace.SecondaryPdfPath], outputPath));

        Assert.True(result.IsSuccess, result.Error?.Message);

        var documentInfo = PdfInspection.Load(outputPath);
        Assert.Equal(6, documentInfo.PageCount);
        Assert.Equal(
            [(100, 200), (200, 100), (90, 90), (32, 48), (60, 60), (140, 80)],
            documentInfo.Pages.Select(page => (page.Width, page.Height)).ToArray());
    }

    [RequiresQpdfFact]
    public async Task ReorderPdfPages_ShouldPersistRequestedOrder()
    {
        await using var workspace = await PdfStructureTestWorkspace.CreateAsync();
        var useCase = workspace.CreateReorderUseCase();

        var outputPath = workspace.GetOutputPath("reordered.pdf");

        var result = await useCase.ExecuteAsync(
            new ReorderPdfPagesRequest(workspace.SourcePdfPath, outputPath, [3, 1, 2]));

        Assert.True(result.IsSuccess, result.Error?.Message);

        var documentInfo = PdfInspection.Load(outputPath);
        Assert.Equal(
            [(90, 90), (100, 200), (200, 100)],
            documentInfo.Pages.Select(page => (page.Width, page.Height)).ToArray());
    }

    private sealed class PdfStructureTestWorkspace : IAsyncDisposable
    {
        private readonly string _workspacePath;
        private readonly QpdfDocumentStructureService _service;

        private PdfStructureTestWorkspace(string workspacePath, QpdfDocumentStructureService service)
        {
            _workspacePath = workspacePath;
            _service = service;
        }

        public string SourcePdfPath => Path.Combine(_workspacePath, "source.pdf");
        public string SecondaryPdfPath => Path.Combine(_workspacePath, "secondary.pdf");
        public string ImagePath => Path.Combine(_workspacePath, "photo.png");

        public static async Task<PdfStructureTestWorkspace> CreateAsync()
        {
            var workspacePath = Path.Combine(
                Path.GetTempPath(),
                "velune-qpdf-tests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(workspacePath);

            MinimalPdfBuilder.CreateDocument(
                Path.Combine(workspacePath, "source.pdf"),
                new PdfPageSpec(100, 200),
                new PdfPageSpec(200, 100),
                new PdfPageSpec(90, 90));

            MinimalPdfBuilder.CreateDocument(
                Path.Combine(workspacePath, "secondary.pdf"),
                new PdfPageSpec(60, 60),
                new PdfPageSpec(140, 80));

            CreateImage(Path.Combine(workspacePath, "photo.png"), 32, 48);

            await Task.CompletedTask;

            return new PdfStructureTestWorkspace(
                workspacePath,
                new QpdfDocumentStructureService(
                    Options.Create(new AppOptions
                    {
                        QpdfExecutablePath = QpdfTestSupport.GetExecutablePath()
                    }),
                    IntegrationPdfium.Initializer));
        }

        public RotatePdfPagesUseCase CreateRotateUseCase() => new(_service);

        public DeletePdfPagesUseCase CreateDeleteUseCase() => new(_service);

        public ExtractPdfPagesUseCase CreateExtractUseCase() => new(_service);

        public MergePdfDocumentsUseCase CreateMergeUseCase() => new(_service);

        public ReorderPdfPagesUseCase CreateReorderUseCase() => new(_service);

        public string GetOutputPath(string fileName) => Path.Combine(_workspacePath, fileName);

        private static void CreateImage(string outputPath, int width, int height)
        {
            using var bitmap = new SKBitmap(width, height);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.CornflowerBlue);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(outputPath);
            data.SaveTo(stream);
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
                // Best-effort cleanup for test artifacts.
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed record PdfPageSpec(int Width, int Height);

    private sealed record PdfPageInfo(int Width, int Height, int RotationDegrees);

    private sealed record PdfDocumentInfo(int PageCount, IReadOnlyList<PdfPageInfo> Pages);

    private static class MinimalPdfBuilder
    {
        public static void CreateDocument(string outputPath, params PdfPageSpec[] pages)
        {
            ArgumentNullException.ThrowIfNull(outputPath);
            ArgumentNullException.ThrowIfNull(pages);

            using var stream = File.Create(outputPath);
            using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);

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
        }
    }

    private static class PdfInspection
    {
        private static int _isInitialized;

        public static PdfDocumentInfo Load(string pdfPath)
        {
            EnsureInitialized();

            var documentHandle = TestPdfiumNative.FPDF_LoadDocument(pdfPath, null);
            Assert.NotEqual(nint.Zero, documentHandle);

            try
            {
                var pageCount = TestPdfiumNative.FPDF_GetPageCount(documentHandle);
                var pages = new List<PdfPageInfo>(pageCount);

                for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
                {
                    var pageHandle = TestPdfiumNative.FPDF_LoadPage(documentHandle, pageIndex);
                    Assert.NotEqual(nint.Zero, pageHandle);

                    try
                    {
                        pages.Add(new PdfPageInfo(
                            (int)Math.Round(TestPdfiumNative.FPDF_GetPageWidthF(pageHandle)),
                            (int)Math.Round(TestPdfiumNative.FPDF_GetPageHeightF(pageHandle)),
                            TestPdfiumNative.FPDFPage_GetRotation(pageHandle) * 90));
                    }
                    finally
                    {
                        TestPdfiumNative.FPDF_ClosePage(pageHandle);
                    }
                }

                return new PdfDocumentInfo(pageCount, pages);
            }
            finally
            {
                TestPdfiumNative.FPDF_CloseDocument(documentHandle);
            }
        }

        private static void EnsureInitialized()
        {
            if (Interlocked.Exchange(ref _isInitialized, 1) == 1)
            {
                return;
            }

            TestPdfiumNative.FPDF_InitLibrary();
        }
    }

    private static partial class TestPdfiumNative
    {
        private const string LibraryName = "pdfium";

        [System.Runtime.InteropServices.DllImport(LibraryName, EntryPoint = "FPDF_InitLibrary")]
        internal static extern void FPDF_InitLibrary();

        [System.Runtime.InteropServices.DllImport(LibraryName, EntryPoint = "FPDF_LoadDocument", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.SysInt)]
        internal static extern nint FPDF_LoadDocument(
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string filePath,
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string? password);

        [System.Runtime.InteropServices.DllImport(LibraryName, EntryPoint = "FPDF_CloseDocument")]
        internal static extern void FPDF_CloseDocument(nint document);

        [System.Runtime.InteropServices.DllImport(LibraryName, EntryPoint = "FPDF_GetPageCount")]
        internal static extern int FPDF_GetPageCount(nint document);

        [System.Runtime.InteropServices.DllImport(LibraryName, EntryPoint = "FPDF_LoadPage")]
        internal static extern nint FPDF_LoadPage(nint document, int pageIndex);

        [System.Runtime.InteropServices.DllImport(LibraryName, EntryPoint = "FPDF_ClosePage")]
        internal static extern void FPDF_ClosePage(nint page);

        [System.Runtime.InteropServices.DllImport(LibraryName, EntryPoint = "FPDF_GetPageWidthF")]
        internal static extern float FPDF_GetPageWidthF(nint page);

        [System.Runtime.InteropServices.DllImport(LibraryName, EntryPoint = "FPDF_GetPageHeightF")]
        internal static extern float FPDF_GetPageHeightF(nint page);

        [System.Runtime.InteropServices.DllImport(LibraryName, EntryPoint = "FPDFPage_GetRotation")]
        internal static extern int FPDFPage_GetRotation(nint page);
    }
}
