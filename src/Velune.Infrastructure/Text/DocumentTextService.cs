using System.Text;
using System.IO.Compression;
using Microsoft.Extensions.Options;
using Velune.Application.Abstractions;
using Velune.Application.Configuration;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Velune.Infrastructure.Pdf;

namespace Velune.Infrastructure.Text;

public sealed class DocumentTextService : IDocumentTextService
{
    private const string EmbeddedPdfFingerprint = "pdfium-text-v1";
    private const double OcrPdfRenderZoomFactor = 2.0;
    private readonly string[] _configuredDefaultLanguages;
    private readonly IDocumentTextCache _cache;
    private readonly IOcrEngine _ocrEngine;
    private readonly IRenderService _renderService;

    public DocumentTextService(
        IDocumentTextCache cache,
        IOcrEngine ocrEngine,
        IRenderService renderService,
        IOptions<AppOptions> options)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(ocrEngine);
        ArgumentNullException.ThrowIfNull(renderService);
        ArgumentNullException.ThrowIfNull(options);

        _cache = cache;
        _ocrEngine = ocrEngine;
        _renderService = renderService;
        _configuredDefaultLanguages = options.Value.DefaultOcrLanguages
            .Where(language => !string.IsNullOrWhiteSpace(language))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<Result<DocumentTextLoadResult>> LoadAsync(
        IDocumentSession session,
        IReadOnlyList<string>? preferredLanguages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.Metadata.DocumentType is DocumentType.Pdf &&
            _cache.TryGet(session, EmbeddedPdfFingerprint, [], forceOcr: false, out var cachedEmbeddedIndex) &&
            cachedEmbeddedIndex is not null)
        {
            return ResultFactory.Success(
                new DocumentTextLoadResult(cachedEmbeddedIndex, RequiresOcr: false, UsedCache: true));
        }

        if (session.Metadata.DocumentType is DocumentType.Pdf)
        {
            var embeddedIndex = TryExtractEmbeddedPdfText(session);
            if (embeddedIndex is { HasSearchableText: true })
            {
                _cache.Store(session, EmbeddedPdfFingerprint, [], forceOcr: false, embeddedIndex);
                return ResultFactory.Success(
                    new DocumentTextLoadResult(embeddedIndex, RequiresOcr: false, UsedCache: false));
            }
        }

        var ocrFingerprint = await GetOcrFingerprintAsync(cancellationToken);
        var languages = await ResolveLanguagesAsync(preferredLanguages, cancellationToken);
        if (_cache.TryGet(session, ocrFingerprint, languages, forceOcr: true, out var cachedOcrIndex) &&
            cachedOcrIndex is not null)
        {
            return ResultFactory.Success(
                new DocumentTextLoadResult(cachedOcrIndex, RequiresOcr: false, UsedCache: true));
        }

        return ResultFactory.Success(
            new DocumentTextLoadResult(Index: null, RequiresOcr: true, UsedCache: false));
    }

    public async Task<Result<DocumentTextIndex>> RunOcrAsync(
        IDocumentSession session,
        IReadOnlyList<string>? preferredLanguages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var languages = await ResolveLanguagesAsync(preferredLanguages, cancellationToken);
        var ocrFingerprint = await GetOcrFingerprintAsync(cancellationToken);

        if (_cache.TryGet(session, ocrFingerprint, languages, forceOcr: true, out var cachedIndex) &&
            cachedIndex is not null)
        {
            return ResultFactory.Success(cachedIndex);
        }

        var indexResult = session.Metadata.DocumentType switch
        {
            DocumentType.Pdf => await RunPdfOcrAsync(session, languages, cancellationToken),
            DocumentType.Image => await RunImageOcrAsync(session, languages, cancellationToken),
            _ => ResultFactory.Failure<DocumentTextIndex>(
                AppError.Unsupported(
                    "document.text.unsupported",
                    "This document type is not supported for text recognition."))
        };

        if (indexResult.IsSuccess && indexResult.Value is not null)
        {
            _cache.Store(session, ocrFingerprint, languages, forceOcr: true, indexResult.Value);
        }

        return indexResult;
    }

    private DocumentTextIndex? TryExtractEmbeddedPdfText(IDocumentSession session)
    {
        if (session is not PdfiumDocumentSession pdfSession)
        {
            return null;
        }

        var pageCount = pdfSession.Metadata.PageCount ?? 0;
        if (pageCount <= 0)
        {
            return null;
        }

        var pages = new List<PageTextContent>(pageCount);
        var hasAnyText = false;

        for (var pageNumber = 0; pageNumber < pageCount; pageNumber++)
        {
            var pageHandle = PdfiumNative.FPDF_LoadPage(pdfSession.Resource.Handle, pageNumber);
            if (pageHandle == nint.Zero)
            {
                continue;
            }

            try
            {
                var pageWidth = Math.Max(1, PdfiumNative.FPDF_GetPageWidthF(pageHandle));
                var pageHeight = Math.Max(1, PdfiumNative.FPDF_GetPageHeightF(pageHandle));
                var textPageHandle = PdfiumNative.FPDFText_LoadPage(pageHandle);
                if (textPageHandle == nint.Zero)
                {
                    pages.Add(new PageTextContent(
                        new PageIndex(pageNumber),
                        TextSourceKind.EmbeddedPdfText,
                        string.Empty,
                        [],
                        pageWidth,
                        pageHeight));
                    continue;
                }

                try
                {
                    var pageContent = ExtractPdfPageText(
                        new PageIndex(pageNumber),
                        textPageHandle,
                        pageWidth,
                        pageHeight);
                    pages.Add(pageContent);
                    hasAnyText |= pageContent.Runs.Count > 0;
                }
                finally
                {
                    PdfiumNative.FPDFText_ClosePage(textPageHandle);
                }
            }
            finally
            {
                PdfiumNative.FPDF_ClosePage(pageHandle);
            }
        }

        return hasAnyText
            ? new DocumentTextIndex(
                session.Metadata.FilePath,
                session.Metadata.DocumentType,
                pages,
                [])
            : null;
    }

    private static PageTextContent ExtractPdfPageText(
        PageIndex pageIndex,
        nint textPageHandle,
        double pageWidth,
        double pageHeight)
    {
        var charCount = PdfiumNative.FPDFText_CountChars(textPageHandle);
        if (charCount <= 0)
        {
            return new PageTextContent(pageIndex, TextSourceKind.EmbeddedPdfText, string.Empty, [], pageWidth, pageHeight);
        }

        var builder = new StringBuilder();
        var runs = new List<TextRun>();
        var characterRegions = new Dictionary<int, NormalizedTextRegion>();
        var currentToken = new StringBuilder();
        var currentRegions = new List<NormalizedTextRegion>();
        var lastWasWhitespace = false;

        void FlushToken()
        {
            if (currentToken.Length == 0)
            {
                return;
            }

            var startIndex = builder.Length;
            var tokenText = currentToken.ToString();
            builder.Append(tokenText);
            runs.Add(new TextRun(
                tokenText,
                startIndex,
                tokenText.Length,
                currentRegions.Count == 0 ? [] : MergeRegions(currentRegions)));
            currentToken.Clear();
            currentRegions.Clear();
            lastWasWhitespace = false;
        }

        for (var index = 0; index < charCount; index++)
        {
            var unicode = PdfiumNative.FPDFText_GetUnicode(textPageHandle, index);
            if (!TryConvertPdfUnicodeToText(unicode, out var textUnit))
            {
                continue;
            }

            var isWhitespace = textUnit.Length > 0 && char.IsWhiteSpace(textUnit, 0);

            if (isWhitespace)
            {
                FlushToken();

                if (textUnit[0] is '\n' or '\r')
                {
                    if (builder.Length > 0 && builder[^1] != '\n')
                    {
                        builder.Append('\n');
                    }

                    lastWasWhitespace = true;
                    continue;
                }

                if (builder.Length > 0 && builder[^1] is not (' ' or '\n'))
                {
                    builder.Append(' ');
                }

                lastWasWhitespace = true;
                continue;
            }

            if (lastWasWhitespace &&
                builder.Length > 0 &&
                builder[^1] is not (' ' or '\n'))
            {
                builder.Append(' ');
            }

            currentToken.Append(textUnit);

            if (PdfiumNative.FPDFText_GetCharBox(textPageHandle, index, out var left, out var right, out var bottom, out var top))
            {
                var clampedLeft = Math.Clamp(left, 0, pageWidth);
                var clampedRight = Math.Clamp(right, 0, pageWidth);
                var clampedBottom = Math.Clamp(bottom, 0, pageHeight);
                var clampedTop = Math.Clamp(top, 0, pageHeight);
                var width = Math.Max(0, clampedRight - clampedLeft);
                var height = Math.Max(0, clampedTop - clampedBottom);

                if (width > 0 && height > 0)
                {
                    var region = new NormalizedTextRegion(
                        clampedLeft / pageWidth,
                        (pageHeight - clampedTop) / pageHeight,
                        width / pageWidth,
                        height / pageHeight);
                    currentRegions.Add(region);
                    characterRegions[index] = region;
                }
            }

            lastWasWhitespace = false;
        }

        FlushToken();

        return new PageTextContent(
            pageIndex,
            TextSourceKind.EmbeddedPdfText,
            builder.ToString().Trim(),
            runs,
            pageWidth,
            pageHeight,
            characterRegions);
    }

    private static bool TryConvertPdfUnicodeToText(uint unicode, out string text)
    {
        text = string.Empty;

        if (unicode == 0)
        {
            return false;
        }

        try
        {
            text = char.ConvertFromUtf32(unchecked((int)unicode));
            return !string.IsNullOrEmpty(text);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private async Task<Result<DocumentTextIndex>> RunPdfOcrAsync(
        IDocumentSession session,
        IReadOnlyList<string> languages,
        CancellationToken cancellationToken)
    {
        var pageCount = session.Metadata.PageCount ?? 0;
        if (pageCount <= 0)
        {
            return ResultFactory.Failure<DocumentTextIndex>(
                AppError.Validation(
                    "document.text.page_count.invalid",
                    "The document has no pages to analyze."));
        }

        var pages = new List<PageTextContent>(pageCount);
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "velune-ocr-pages",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            for (var pageNumber = 0; pageNumber < pageCount; pageNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var render = await _renderService.RenderPageAsync(
                    session,
                    new PageIndex(pageNumber),
                    OcrPdfRenderZoomFactor,
                    Rotation.Deg0,
                    cancellationToken);

                var imagePath = Path.Combine(tempDirectory, $"page-{pageNumber + 1}.png");
                await BgraPngWriter.WriteAsync(
                    imagePath,
                    render.Width,
                    render.Height,
                    render.PixelData,
                    cancellationToken);

                var ocrResult = await _ocrEngine.RecognizePageAsync(
                    new OcrPageRequest(
                        new PageIndex(pageNumber),
                        imagePath,
                        render.Width,
                        render.Height),
                    languages,
                    cancellationToken);

                if (ocrResult.IsFailure)
                {
                    return ResultFactory.Failure<DocumentTextIndex>(ocrResult.Error!);
                }

                var pageContent = ocrResult.Value!;
                pages.Add(new PageTextContent(
                    pageContent.PageIndex,
                    pageContent.SourceKind,
                    pageContent.Text,
                    pageContent.Runs,
                    pageContent.SourceWidth,
                    pageContent.SourceHeight));
            }

            return ResultFactory.Success(new DocumentTextIndex(
                session.Metadata.FilePath,
                session.Metadata.DocumentType,
                pages,
                languages));
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup for OCR render files.
            }
        }
    }

    private async Task<Result<DocumentTextIndex>> RunImageOcrAsync(
        IDocumentSession session,
        IReadOnlyList<string> languages,
        CancellationToken cancellationToken)
    {
        if (session is not IImageDocumentSession imageSession)
        {
            return ResultFactory.Failure<DocumentTextIndex>(
                AppError.Unsupported(
                    "document.text.image_session.invalid",
                    "The active image session does not expose OCR metadata."));
        }

        var inputPath = session.Metadata.FilePath;
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return ResultFactory.Failure<DocumentTextIndex>(
                AppError.Validation(
                    "document.text.path.empty",
                    "The current document path is unavailable."));
        }

        var ocrResult = await _ocrEngine.RecognizePageAsync(
            new OcrPageRequest(
                new PageIndex(0),
                inputPath,
                imageSession.ImageMetadata.Width,
                imageSession.ImageMetadata.Height),
            languages,
            cancellationToken);

        if (ocrResult.IsFailure)
        {
            return ResultFactory.Failure<DocumentTextIndex>(ocrResult.Error!);
        }

        var pageContent = ocrResult.Value!;
        return ResultFactory.Success(new DocumentTextIndex(
            session.Metadata.FilePath,
            session.Metadata.DocumentType,
            [new PageTextContent(
                pageContent.PageIndex,
                pageContent.SourceKind,
                pageContent.Text,
                pageContent.Runs,
                pageContent.SourceWidth,
                pageContent.SourceHeight)],
            languages));
    }

    private async Task<string> GetOcrFingerprintAsync(CancellationToken cancellationToken)
    {
        var infoResult = await _ocrEngine.GetInfoAsync(cancellationToken);
        if (infoResult.IsSuccess && infoResult.Value is not null)
        {
            return $"tesseract:{infoResult.Value.EngineVersion}";
        }

        return "tesseract:unknown";
    }

    private async Task<IReadOnlyList<string>> ResolveLanguagesAsync(
        IReadOnlyList<string>? preferredLanguages,
        CancellationToken cancellationToken)
    {
        if (preferredLanguages is { Count: > 0 })
        {
            return preferredLanguages
                .Where(language => !string.IsNullOrWhiteSpace(language))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        if (_configuredDefaultLanguages.Length > 0)
        {
            return _configuredDefaultLanguages;
        }

        var infoResult = await _ocrEngine.GetInfoAsync(cancellationToken);
        if (infoResult.IsSuccess && infoResult.Value is not null)
        {
            var mappedLanguage = TesseractLanguageMapper.MapCulture(System.Globalization.CultureInfo.CurrentUICulture);
            if (!string.IsNullOrWhiteSpace(mappedLanguage) &&
                infoResult.Value.AvailableLanguages.Contains(mappedLanguage, StringComparer.OrdinalIgnoreCase))
            {
                return [mappedLanguage];
            }

            if (infoResult.Value.AvailableLanguages.Contains("eng", StringComparer.OrdinalIgnoreCase))
            {
                return ["eng"];
            }

            if (infoResult.Value.AvailableLanguages.Count > 0)
            {
                return [infoResult.Value.AvailableLanguages[0]];
            }
        }

        return ["eng"];
    }

    private static List<NormalizedTextRegion> MergeRegions(List<NormalizedTextRegion> regions)
    {
        if (regions.Count == 0)
        {
            return [];
        }

        var left = regions.Min(region => region.X);
        var top = regions.Min(region => region.Y);
        var right = regions.Max(region => region.X + region.Width);
        var bottom = regions.Max(region => region.Y + region.Height);

        return [new NormalizedTextRegion(left, top, right - left, bottom - top)];
    }

    private static class BgraPngWriter
    {
        private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

        public static async Task WriteAsync(
            string outputPath,
            int width,
            int height,
            ReadOnlyMemory<byte> bgra,
            CancellationToken cancellationToken)
        {
            var rgba = ConvertBgraToRgba(bgra);

            await using var stream = File.Create(outputPath);
            await stream.WriteAsync(Signature, cancellationToken);
            await WriteChunkAsync(stream, "IHDR", CreateHeader(width, height), cancellationToken);
            await WriteChunkAsync(stream, "IDAT", CreateImageData(width, height, rgba), cancellationToken);
            await WriteChunkAsync(stream, "IEND", [], cancellationToken);
        }

        private static byte[] ConvertBgraToRgba(ReadOnlyMemory<byte> bgra)
        {
            var bgraSpan = bgra.Span;
            var rgba = new byte[bgraSpan.Length];
            for (var index = 0; index < bgraSpan.Length; index += 4)
            {
                rgba[index] = bgraSpan[index + 2];
                rgba[index + 1] = bgraSpan[index + 1];
                rgba[index + 2] = bgraSpan[index];
                rgba[index + 3] = bgraSpan[index + 3];
            }

            return rgba;
        }

        private static byte[] CreateHeader(int width, int height)
        {
            using var stream = new MemoryStream();
            WriteInt32BigEndian(stream, width);
            WriteInt32BigEndian(stream, height);
            stream.WriteByte(8);
            stream.WriteByte(6);
            stream.WriteByte(0);
            stream.WriteByte(0);
            stream.WriteByte(0);
            return stream.ToArray();
        }

        private static byte[] CreateImageData(int width, int height, byte[] rgba)
        {
            using var raw = new MemoryStream((width * 4 + 1) * height);
            var stride = width * 4;

            for (var row = 0; row < height; row++)
            {
                raw.WriteByte(0);
                raw.Write(rgba, row * stride, stride);
            }

            using var compressed = new MemoryStream();
            using (var deflate = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                raw.Position = 0;
                raw.CopyTo(deflate);
            }

            return compressed.ToArray();
        }

        private static async Task WriteChunkAsync(
            Stream stream,
            string type,
            byte[] data,
            CancellationToken cancellationToken)
        {
            var typeBytes = Encoding.ASCII.GetBytes(type);
            var crc = ComputeCrc32(typeBytes, data);

            WriteInt32BigEndian(stream, data.Length);
            await stream.WriteAsync(typeBytes, cancellationToken);
            await stream.WriteAsync(data, cancellationToken);
            WriteInt32BigEndian(stream, unchecked((int)crc));
        }

        private static void WriteInt32BigEndian(Stream stream, int value)
        {
            Span<byte> bytes = stackalloc byte[4];
            bytes[0] = (byte)((value >> 24) & 0xFF);
            bytes[1] = (byte)((value >> 16) & 0xFF);
            bytes[2] = (byte)((value >> 8) & 0xFF);
            bytes[3] = (byte)(value & 0xFF);
            stream.Write(bytes);
        }

        private static uint ComputeCrc32(byte[] typeBytes, byte[] data)
        {
            var crc = 0xFFFFFFFFu;

            crc = UpdateCrc(crc, typeBytes);
            crc = UpdateCrc(crc, data);

            return crc ^ 0xFFFFFFFFu;
        }

        private static uint UpdateCrc(uint crc, byte[] data)
        {
            foreach (var current in data)
            {
                crc ^= current;
                for (var bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 1) != 0
                        ? (crc >> 1) ^ 0xEDB88320u
                        : crc >> 1;
                }
            }

            return crc;
        }
    }
}
