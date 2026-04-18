using System.Diagnostics;
using Microsoft.Extensions.Options;
using Velune.Application.Abstractions;
using Velune.Application.Configuration;
using Velune.Application.Results;
using Velune.Domain.ValueObjects;

namespace Velune.Infrastructure.Pdf;

public sealed class QpdfDocumentStructureService : IPdfDocumentStructureService
{
    private readonly PdfiumInitializer _pdfiumInitializer;
    private readonly string _qpdfExecutablePath;
    private bool? _isAvailable;

    public QpdfDocumentStructureService(
        IOptions<AppOptions> appOptions,
        PdfiumInitializer pdfiumInitializer)
    {
        ArgumentNullException.ThrowIfNull(appOptions);
        ArgumentNullException.ThrowIfNull(pdfiumInitializer);

        _pdfiumInitializer = pdfiumInitializer;
        _qpdfExecutablePath = string.IsNullOrWhiteSpace(appOptions.Value.QpdfExecutablePath)
            ? "qpdf"
            : appOptions.Value.QpdfExecutablePath;
    }

    public bool IsAvailable()
    {
        if (_isAvailable.HasValue)
        {
            return _isAvailable.Value;
        }

        try
        {
            var startInfo = new ProcessStartInfo(_qpdfExecutablePath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("--version");

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                _isAvailable = false;
                return false;
            }

            process.WaitForExit();
            _isAvailable = process.ExitCode == 0;
        }
        catch
        {
            _isAvailable = false;
        }

        return _isAvailable.Value;
    }

    public Task<Result<string>> RotatePagesAsync(
        string sourcePath,
        string outputPath,
        IReadOnlyList<int> pages,
        Rotation rotation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);

        var validationResult = ValidatePageOperation(sourcePath, outputPath, pages, allowPartialSelection: true);
        if (validationResult.IsFailure)
        {
            return Task.FromResult(ResultFactory.Failure<string>(validationResult.Error!));
        }

        var commandArguments = CreateSingleSourceArguments(
            sourcePath,
            outputPath,
            $"--rotate={ToQpdfRotation(rotation)}:{BuildPageList(pages)}");

        return RunQpdfAsync(commandArguments, outputPath, cancellationToken);
    }

    public Task<Result<string>> DeletePagesAsync(
        string sourcePath,
        string outputPath,
        IReadOnlyList<int> pages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);

        var validationResult = ValidatePageOperation(sourcePath, outputPath, pages, allowPartialSelection: true);
        if (validationResult.IsFailure)
        {
            return Task.FromResult(ResultFactory.Failure<string>(validationResult.Error!));
        }

        var pageCount = GetPageCount(sourcePath);
        if (pageCount.IsFailure)
        {
            return Task.FromResult(ResultFactory.Failure<string>(pageCount.Error!));
        }

        var deletedPages = pages.ToHashSet();
        var keptPages = Enumerable.Range(1, pageCount.Value!)
            .Where(pageNumber => !deletedPages.Contains(pageNumber))
            .ToArray();

        if (keptPages.Length == 0)
        {
            return Task.FromResult(ResultFactory.Failure<string>(
                AppError.Validation(
                    "pdf.structure.delete.all-pages",
                    "At least one page must remain in the PDF.")));
        }

        var commandArguments = CreatePageSelectionArguments(sourcePath, outputPath, BuildPageList(keptPages));
        return RunQpdfAsync(commandArguments, outputPath, cancellationToken);
    }

    public Task<Result<string>> ExtractPagesAsync(
        string sourcePath,
        string outputPath,
        IReadOnlyList<int> pages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);

        var validationResult = ValidatePageOperation(sourcePath, outputPath, pages, allowPartialSelection: true);
        if (validationResult.IsFailure)
        {
            return Task.FromResult(ResultFactory.Failure<string>(validationResult.Error!));
        }

        var commandArguments = CreatePageSelectionArguments(sourcePath, outputPath, BuildPageList(pages));
        return RunQpdfAsync(commandArguments, outputPath, cancellationToken);
    }

    public Task<Result<string>> MergeDocumentsAsync(
        IReadOnlyList<string> sourcePaths,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);

        if (!IsAvailable())
        {
            return Task.FromResult(ResultFactory.Failure<string>(CreateUnavailableError()));
        }

        if (sourcePaths.Count < 2)
        {
            return Task.FromResult(ResultFactory.Failure<string>(
                AppError.Validation(
                    "pdf.structure.merge.sources.invalid",
                    "At least two PDF documents are required for a merge.")));
        }

        foreach (var sourcePath in sourcePaths)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                return Task.FromResult(ResultFactory.Failure<string>(
                    AppError.NotFound(
                        "pdf.structure.merge.source.missing",
                        "One of the source PDF documents could not be found.")));
            }
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return Task.FromResult(ResultFactory.Failure<string>(
                AppError.Validation(
                    "pdf.structure.output.empty",
                    "An output path is required.")));
        }

        EnsureOutputDirectory(outputPath);

        var commandArguments = new List<string>
        {
            "--empty",
            "--warning-exit-0",
            "--pages"
        };

        foreach (var sourcePath in sourcePaths)
        {
            commandArguments.Add(sourcePath);
        }

        commandArguments.Add("--");
        commandArguments.Add(outputPath);

        return RunQpdfAsync(commandArguments, outputPath, cancellationToken);
    }

    public Task<Result<string>> ReorderPagesAsync(
        string sourcePath,
        string outputPath,
        IReadOnlyList<int> orderedPages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orderedPages);

        var validationResult = ValidatePageOperation(sourcePath, outputPath, orderedPages, allowPartialSelection: false);
        if (validationResult.IsFailure)
        {
            return Task.FromResult(ResultFactory.Failure<string>(validationResult.Error!));
        }

        var pageCount = GetPageCount(sourcePath);
        if (pageCount.IsFailure)
        {
            return Task.FromResult(ResultFactory.Failure<string>(pageCount.Error!));
        }

        if (orderedPages.Count != pageCount.Value ||
            orderedPages.Distinct().Count() != pageCount.Value ||
            orderedPages.Min() != 1 ||
            orderedPages.Max() != pageCount.Value)
        {
            return Task.FromResult(ResultFactory.Failure<string>(
                AppError.Validation(
                    "pdf.structure.reorder.pages.invalid",
                    "The reordered page list must contain each page exactly once.")));
        }

        var commandArguments = CreatePageSelectionArguments(sourcePath, outputPath, BuildPageList(orderedPages));
        return RunQpdfAsync(commandArguments, outputPath, cancellationToken);
    }

    private Result ValidatePageOperation(
        string sourcePath,
        string outputPath,
        IReadOnlyList<int> pages,
        bool allowPartialSelection)
    {
        if (!IsAvailable())
        {
            return ResultFactory.Failure(CreateUnavailableError());
        }

        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return ResultFactory.Failure(
                AppError.NotFound(
                    "pdf.structure.source.missing",
                    "The source PDF document could not be found."));
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return ResultFactory.Failure(
                AppError.Validation(
                    "pdf.structure.output.empty",
                    "An output path is required."));
        }

        if (pages is null || pages.Count == 0)
        {
            return ResultFactory.Failure(
                AppError.Validation(
                    "pdf.structure.pages.empty",
                    "At least one page must be selected."));
        }

        if (pages.Any(page => page <= 0))
        {
            return ResultFactory.Failure(
                AppError.Validation(
                    "pdf.structure.pages.invalid",
                    "Page numbers must be greater than zero."));
        }

        if (pages.Distinct().Count() != pages.Count)
        {
            return ResultFactory.Failure(
                AppError.Validation(
                    "pdf.structure.pages.duplicate",
                    "Page selections cannot contain duplicates."));
        }

        var pageCount = GetPageCount(sourcePath);
        if (pageCount.IsFailure)
        {
            return ResultFactory.Failure(pageCount.Error!);
        }

        if (pages.Any(page => page > pageCount.Value))
        {
            return ResultFactory.Failure(
                AppError.Validation(
                    "pdf.structure.pages.out-of-range",
                    $"Page selections must stay between 1 and {pageCount.Value}."));
        }

        if (!allowPartialSelection && pages.Count != pageCount.Value)
        {
            return ResultFactory.Failure(
                AppError.Validation(
                    "pdf.structure.pages.incomplete",
                    "The page selection must contain the whole document."));
        }

        EnsureOutputDirectory(outputPath);
        return ResultFactory.Success();
    }

    private Result<int> GetPageCount(string sourcePath)
    {
        try
        {
            _pdfiumInitializer.EnsureInitialized();

            var documentHandle = PdfiumNative.FPDF_LoadDocument(sourcePath, null);
            if (documentHandle == nint.Zero)
            {
                return ResultFactory.Failure<int>(
                    AppError.Infrastructure(
                        "pdf.structure.source.open-failed",
                        "The source PDF could not be opened for structural editing."));
            }

            try
            {
                return ResultFactory.Success(PdfiumNative.FPDF_GetPageCount(documentHandle));
            }
            finally
            {
                PdfiumNative.FPDF_CloseDocument(documentHandle);
            }
        }
        catch (Exception)
        {
            return ResultFactory.Failure<int>(
                AppError.Infrastructure(
                    "pdf.structure.source.inspect-failed",
                    "The source PDF could not be inspected."));
        }
    }

    private static List<string> CreateSingleSourceArguments(
        string sourcePath,
        string outputPath,
        string operationArgument)
    {
        var arguments = new List<string>
        {
            sourcePath,
            "--warning-exit-0",
            operationArgument
        };

        if (Path.GetFullPath(sourcePath).Equals(Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
        {
            arguments.Add("--replace-input");
        }
        else
        {
            arguments.Add(outputPath);
        }

        return arguments;
    }

    private static List<string> CreatePageSelectionArguments(
        string sourcePath,
        string outputPath,
        string pageSelection)
    {
        var arguments = new List<string>
        {
            sourcePath,
            "--warning-exit-0",
            "--pages",
            ".",
            pageSelection,
            "--"
        };

        if (Path.GetFullPath(sourcePath).Equals(Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
        {
            arguments.Add("--replace-input");
        }
        else
        {
            arguments.Add(outputPath);
        }

        return arguments;
    }

    private async Task<Result<string>> RunQpdfAsync(
        IReadOnlyList<string> arguments,
        string outputPath,
        CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo(_qpdfExecutablePath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return ResultFactory.Failure<string>(
                    AppError.Infrastructure(
                        "pdf.structure.process.start-failed",
                        "The PDF structure tool could not be started."));
            }

            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // Best-effort cancellation.
                }
            });

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(outputTask, errorTask);

            if (process.ExitCode == 0)
            {
                return ResultFactory.Success(outputPath);
            }

            var errorDetails = string.IsNullOrWhiteSpace(errorTask.Result)
                ? outputTask.Result
                : errorTask.Result;

            return ResultFactory.Failure<string>(
                AppError.Infrastructure(
                    "pdf.structure.process.failed",
                    BuildProcessFailureMessage(errorDetails)));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return ResultFactory.Failure<string>(
                AppError.Infrastructure(
                    "pdf.structure.process.error",
                    "The PDF structure operation failed."));
        }
    }

    private static string BuildPageList(IEnumerable<int> pages) =>
        string.Join(",", pages);

    private static string ToQpdfRotation(Rotation rotation)
    {
        return rotation switch
        {
            Rotation.Deg90 => "+90",
            Rotation.Deg180 => "+180",
            Rotation.Deg270 => "+270",
            _ => "+0"
        };
    }

    private static string BuildProcessFailureMessage(string? errorDetails)
    {
        if (string.IsNullOrWhiteSpace(errorDetails))
        {
            return "The PDF structure operation failed.";
        }

        var sanitizedMessage = string.Join(
            " ",
            errorDetails
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(2));

        return string.IsNullOrWhiteSpace(sanitizedMessage)
            ? "The PDF structure operation failed."
            : $"The PDF structure operation failed: {sanitizedMessage}";
    }

    private static void EnsureOutputDirectory(string outputPath)
    {
        var directoryPath = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }

    private static AppError CreateUnavailableError() =>
        AppError.Infrastructure(
            "pdf.structure.tool.missing",
            "PDF structural editing requires qpdf to be installed.");
}
