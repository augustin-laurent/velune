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

namespace Velune.Infrastructure.Text;

public sealed class TesseractOcrEngine : IOcrEngine
{
    private readonly string _tesseractExecutablePath;
    private readonly string? _tesseractDataPath;

    public TesseractOcrEngine(IOptions<AppOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _tesseractExecutablePath = string.IsNullOrWhiteSpace(options.Value.TesseractExecutablePath)
            ? "tesseract"
            : options.Value.TesseractExecutablePath;
        _tesseractDataPath = string.IsNullOrWhiteSpace(options.Value.TesseractDataPath)
            ? null
            : options.Value.TesseractDataPath;
    }

    public async Task<Result<OcrEngineInfo>> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        var versionResult = await ExecuteProcessAsync(["--version"], cancellationToken);
        if (versionResult.IsFailure)
        {
            return ResultFactory.Failure<OcrEngineInfo>(versionResult.Error!);
        }

        var languageResult = await ExecuteProcessAsync(["--list-langs"], cancellationToken);
        if (languageResult.IsFailure)
        {
            return ResultFactory.Failure<OcrEngineInfo>(languageResult.Error!);
        }

        var version = ParseVersion(versionResult.Value ?? string.Empty);
        var languages = ParseLanguages(languageResult.Value ?? string.Empty);

        return ResultFactory.Success(new OcrEngineInfo(
            "Tesseract",
            version,
            languages));
    }

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

        var infoResult = await GetInfoAsync(cancellationToken);
        if (infoResult.IsFailure)
        {
            return ResultFactory.Failure<OcrPageContent>(infoResult.Error!);
        }

        var languages = ResolveLanguages(
            preferredLanguages,
            infoResult.Value!.AvailableLanguages);
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "velune-tesseract",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        var outputBasePath = Path.Combine(tempDirectory, "page");

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

            var processResult = await ExecuteProcessAsync(arguments, cancellationToken);
            if (processResult.IsFailure)
            {
                return ResultFactory.Failure<OcrPageContent>(processResult.Error!);
            }

            var hocrPath = $"{outputBasePath}.hocr";
            if (!File.Exists(hocrPath))
            {
                return ResultFactory.Failure<OcrPageContent>(
                    AppError.Infrastructure(
                        "ocr.output.missing",
                        "The OCR engine did not produce any searchable output."));
            }

            var hocr = await File.ReadAllTextAsync(hocrPath, cancellationToken);
            var pageContent = ParseHocr(
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
            var startInfo = new ProcessStartInfo(_tesseractExecutablePath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            foreach (var argument in arguments)
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

            var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var standardOutput = await standardOutputTask;
            var standardError = await standardErrorTask;

            if (process.ExitCode != 0)
            {
                var message = string.IsNullOrWhiteSpace(standardError)
                    ? "The OCR engine failed to recognize text."
                    : standardError.Trim();

                return ResultFactory.Failure<string>(
                    AppError.Infrastructure(
                        "ocr.process.failed",
                        message));
            }

            return ResultFactory.Success(standardOutput);
        }
        catch (Win32Exception)
        {
            return ResultFactory.Failure<string>(
                AppError.Unsupported(
                    "ocr.tesseract.missing",
                    "Tesseract is not installed or is not available in the configured path."));
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
        foreach (var line in value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
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
            var explicitLanguages = preferredLanguages
                .Where(language => availableLanguages.Contains(language, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (explicitLanguages.Length > 0)
            {
                return explicitLanguages;
            }
        }

        var cultureLanguage = TesseractLanguageMapper.MapCulture(CultureInfo.CurrentUICulture);
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
            var fallbackWords = ExtractWords(document.Descendants());
            if (fallbackWords.Count > 0)
            {
                lineWordSets.Add(fallbackWords);
            }
        }

        var textBuilder = new System.Text.StringBuilder();
        var runs = new List<TextRun>();

        foreach (var words in lineWordSets)
        {
            if (words.Count == 0)
            {
                continue;
            }

            if (textBuilder.Length > 0)
            {
                textBuilder.Append('\n');
            }

            for (var i = 0; i < words.Count; i++)
            {
                if (i > 0)
                {
                    textBuilder.Append(' ');
                }

                var startIndex = textBuilder.Length;
                textBuilder.Append(words[i].Text);

                var bbox = words[i].Bbox!;
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
        var classes = ((string?)element.Attribute("class"))?
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

        var bboxMarker = title.IndexOf("bbox ", StringComparison.Ordinal);
        if (bboxMarker < 0)
        {
            return null;
        }

        var start = bboxMarker + 5;
        var end = title.IndexOf(';', start);
        var bboxText = end >= 0 ? title[start..end] : title[start..];
        var parts = bboxText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length != 4 ||
            !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var left) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var top) ||
            !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var right) ||
            !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var bottom) ||
            right <= left ||
            bottom <= top)
        {
            return null;
        }

        return new BoundingBox(left, top, right, bottom);
    }

    private sealed record BoundingBox(double Left, double Top, double Right, double Bottom);
}
