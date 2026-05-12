using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using Velune.Application.Abstractions;
using Velune.Application.Configuration;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Velune.Infrastructure.FileSystem;

namespace Velune.Infrastructure.Text;

/// <summary>
/// OCR engine implementation that invokes the Tesseract command-line tool.
/// </summary>
public sealed class TesseractOcrEngine : IOcrEngine
{
    private readonly BundledTool _tesseractTool;
    private readonly string? _tesseractDataPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="TesseractOcrEngine"/> class.
    /// </summary>
    /// <param name="options">Application options containing Tesseract executable and data paths.</param>
    public TesseractOcrEngine(IOptions<AppOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _tesseractTool = BundledToolResolver.Resolve(
            options.Value.TesseractExecutablePath,
            "tesseract",
            "tesseract");
        _tesseractDataPath = BundledToolResolver.ResolveTesseractDataPath(options.Value.TesseractDataPath);
    }

    /// <inheritdoc />
    public async Task<Result<OcrEngineInfo>> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        Result<string> versionResult = await ExecuteProcessAsync(["--version"], cancellationToken);
        if (versionResult.IsFailure)
        {
            return ResultFactory.Failure<OcrEngineInfo>(versionResult.Error!);
        }

        Result<string> languageResult = await ExecuteProcessAsync(["--list-langs"], cancellationToken);
        if (languageResult.IsFailure)
        {
            return ResultFactory.Failure<OcrEngineInfo>(languageResult.Error!);
        }

        string version = ParseVersion(versionResult.Value ?? string.Empty);
        IReadOnlyList<string> languages = ParseLanguages(languageResult.Value ?? string.Empty);

        return ResultFactory.Success(new OcrEngineInfo(
            "Tesseract",
            version,
            languages));
    }

    /// <inheritdoc />
    public async Task<Result<OcrPageContent>> RecognizePageAsync(
        OcrPageRequest request,
        IReadOnlyList<string>? preferredLanguages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!File.Exists(request.InputPath))
        {
            return ResultFactory.Failure<OcrPageContent>(
                AppError.NotFound(
                    "ocr.input.missing",
                    "The OCR input file could not be found."));
        }

        Result<OcrEngineInfo> infoResult = await GetInfoAsync(cancellationToken);
        if (infoResult.IsFailure)
        {
            return ResultFactory.Failure<OcrPageContent>(infoResult.Error!);
        }

        string[] languages = ResolveLanguages(
            preferredLanguages,
            infoResult.Value!.AvailableLanguages);
        string tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "velune-tesseract",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        string outputBasePath = Path.Combine(tempDirectory, "page");

        try
        {
            var arguments = new List<string>
            {
                request.InputPath,
                outputBasePath,
                "-l",
                string.Join('+', languages),
                "--dpi",
                "300",
                "--psm",
                "6",
                "hocr"
            };

            Result<string> processResult = await ExecuteProcessAsync(arguments, cancellationToken);
            if (processResult.IsFailure)
            {
                return ResultFactory.Failure<OcrPageContent>(processResult.Error!);
            }

            string hocrPath = $"{outputBasePath}.hocr";
            if (!File.Exists(hocrPath))
            {
                return ResultFactory.Failure<OcrPageContent>(
                    AppError.Infrastructure(
                        "ocr.output.missing",
                        "The OCR engine did not produce any searchable output."));
            }

            string hocr = await File.ReadAllTextAsync(hocrPath, cancellationToken);
            OcrPageContent pageContent = ParseHocr(
                request.PageIndex,
                hocr,
                request.SourceWidth,
                request.SourceHeight,
                request.SourceKind);

            return ResultFactory.Success(pageContent);
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
                // Best-effort cleanup for OCR artifacts.
            }
        }
    }

    private async Task<Result<string>> ExecuteProcessAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            ProcessStartInfo startInfo = BundledToolResolver.CreateStartInfo(_tesseractTool);

            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            if (!string.IsNullOrWhiteSpace(_tesseractDataPath))
            {
                startInfo.Environment["TESSDATA_PREFIX"] = _tesseractDataPath;
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return ResultFactory.Failure<string>(
                    AppError.Infrastructure(
                        "ocr.process.start_failed",
                        "The OCR engine could not be started."));
            }

            Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            string standardOutput = await standardOutputTask;
            string standardError = await standardErrorTask;

            if (process.ExitCode == 0)
            {
                return ResultFactory.Success(standardOutput);
            }

            string message = string.IsNullOrWhiteSpace(standardError)
                ? "The OCR engine failed to recognize text."
                : standardError.Trim();

            return ResultFactory.Failure<string>(
                AppError.Infrastructure(
                    "ocr.process.failed",
                    message));

        }
        catch (Win32Exception)
        {
            return ResultFactory.Failure<string>(
                AppError.Unsupported(
                    "ocr.tesseract.missing",
                    "The bundled OCR engine is missing or unavailable."));
        }
        catch (InvalidOperationException ex)
        {
            return ResultFactory.Failure<string>(
                AppError.Infrastructure(
                    "ocr.process.failed",
                    ex.Message));
        }
    }

    private static string ParseVersion(string value)
    {
        foreach (string line in value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("tesseract ", StringComparison.OrdinalIgnoreCase))
            {
                return line["tesseract ".Length..].Trim();
            }
        }

        return "unknown";
    }

    private static IReadOnlyList<string> ParseLanguages(string value)
    {
        return [.. value
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith("List of available languages", StringComparison.OrdinalIgnoreCase))];
    }

    private static string[] ResolveLanguages(
        IReadOnlyList<string>? preferredLanguages,
        IReadOnlyList<string> availableLanguages)
    {
        if (preferredLanguages is { Count: > 0 })
        {
            string[] explicitLanguages = preferredLanguages
                .Where(language => availableLanguages.Contains(language, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (explicitLanguages.Length > 0)
            {
                return explicitLanguages;
            }
        }

        string? cultureLanguage = TesseractLanguageMapper.MapCulture(CultureInfo.CurrentUICulture);
        if (!string.IsNullOrWhiteSpace(cultureLanguage) &&
            availableLanguages.Contains(cultureLanguage, StringComparer.OrdinalIgnoreCase))
        {
            return [cultureLanguage];
        }

        if (availableLanguages.Contains("eng", StringComparer.OrdinalIgnoreCase))
        {
            return ["eng"];
        }

        return availableLanguages.Count > 0
            ? [availableLanguages[0]]
            : ["eng"];
    }

    private static OcrPageContent ParseHocr(
        PageIndex pageIndex,
        string hocr,
        double sourceWidth,
        double sourceHeight,
        TextSourceKind sourceKind)
    {
        var document = XDocument.Parse(hocr, LoadOptions.PreserveWhitespace);
        var lineWordSets = document.Descendants()
            .Where(element => HasCssClass(element, "ocr_line"))
            .Select(lineElement => ExtractWords(lineElement.Descendants()))
            .Where(words => words.Count > 0)
            .ToList();

        if (lineWordSets.Count == 0)
        {
            List<(string Text, BoundingBox? Bbox)> fallbackWords = ExtractWords(document.Descendants());
            if (fallbackWords.Count > 0)
            {
                lineWordSets.Add(fallbackWords);
            }
        }

        var textBuilder = new System.Text.StringBuilder();
        var runs = new List<TextRun>();

        foreach (List<(string Text, BoundingBox? Bbox)> words in lineWordSets)
        {
            if (words.Count == 0)
            {
                continue;
            }

            if (textBuilder.Length > 0)
            {
                textBuilder.Append('\n');
            }

            for (int i = 0; i < words.Count; i++)
            {
                if (i > 0)
                {
                    textBuilder.Append(' ');
                }

                int startIndex = textBuilder.Length;
                textBuilder.Append(words[i].Text);

                BoundingBox bbox = words[i].Bbox!;
                runs.Add(new TextRun(
                    words[i].Text,
                    startIndex,
                    words[i].Text.Length,
                    [new NormalizedTextRegion(
                        bbox.Left / sourceWidth,
                        bbox.Top / sourceHeight,
                        (bbox.Right - bbox.Left) / sourceWidth,
                        (bbox.Bottom - bbox.Top) / sourceHeight)]));
            }
        }

        return new OcrPageContent(
            pageIndex,
            textBuilder.Length == 0 ? string.Empty : textBuilder.ToString(),
            runs,
            sourceWidth,
            sourceHeight,
            sourceKind);
    }

    private static List<(string Text, BoundingBox? Bbox)> ExtractWords(IEnumerable<XElement> elements)
    {
        return elements
            .Where(element => HasCssClass(element, "ocrx_word") || HasCssClass(element, "ocr_word"))
            .Select(element => (
                Text: NormalizeWhitespace(element.Value),
                Bbox: TryParseBoundingBox((string?)element.Attribute("title"))))
            .Where(item => !string.IsNullOrWhiteSpace(item.Text) && item.Bbox is not null)
            .ToList();
    }

    private static bool HasCssClass(XElement element, string className)
    {
        string[]? classes = ((string?)element.Attribute("class"))?
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return classes?.Contains(className, StringComparer.Ordinal) == true;
    }

    private static string NormalizeWhitespace(string value)
    {
        return string.Join(
            ' ',
            value
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static BoundingBox? TryParseBoundingBox(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        int bboxMarker = title.IndexOf("bbox ", StringComparison.Ordinal);
        if (bboxMarker < 0)
        {
            return null;
        }

        int start = bboxMarker + 5;
        int end = title.IndexOf(';', start);
        string bboxText = end >= 0 ? title[start..end] : title[start..];
        string[] parts = bboxText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length != 4 ||
            !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double left) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double top) ||
            !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double right) ||
            !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double bottom) ||
            right <= left ||
            bottom <= top)
        {
            return null;
        }

        return new BoundingBox(left, top, right, bottom);
    }

    private sealed record BoundingBox(double Left, double Top, double Right, double Bottom);
}
