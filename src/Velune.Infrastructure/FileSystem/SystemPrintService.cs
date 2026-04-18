using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Threading;
using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;

namespace Velune.Infrastructure.FileSystem;

public sealed partial class SystemPrintService : IPrintService
{
    public bool SupportsSystemPrintDialog => OperatingSystem.IsMacOS();

    public async Task<Result> ShowSystemPrintDialogAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!OperatingSystem.IsMacOS())
        {
            return ResultFactory.Failure(
                AppError.Unsupported(
                    "print.dialog.unsupported",
                    "The system print dialog is not available on this platform."));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var outcome = await ShowMacOsSystemPrintDialogAsync(filePath);

            return outcome switch
            {
                PrintDialogOutcome.Success => ResultFactory.Success(),
                PrintDialogOutcome.Cancelled => ResultFactory.Failure(
                    AppError.Validation(
                        "print.cancelled",
                        "Printing was cancelled.")),
                PrintDialogOutcome.Unsupported => ResultFactory.Failure(
                    AppError.Unsupported(
                        "print.dialog.unsupported",
                        "The system print dialog is not available on this platform.")),
                _ => ResultFactory.Failure(
                    AppError.Infrastructure(
                        "print.dialog.failed",
                        "The system print dialog could not be opened."))
            };
        }
        catch (OperationCanceledException)
        {
            return ResultFactory.Failure(
                AppError.Validation(
                    "print.cancelled",
                    "Printing was cancelled."));
        }
        catch (Exception)
        {
            return ResultFactory.Failure(
                AppError.Infrastructure(
                    "print.dialog.failed",
                    "The system print dialog could not be opened."));
        }
    }

    [SupportedOSPlatform("macos")]
    private static Task<PrintDialogOutcome> ShowMacOsSystemPrintDialogAsync(string filePath)
    {
        return Dispatcher.UIThread.InvokeAsync(
            () => MacOsPrintDialog.Show(filePath),
            DispatcherPriority.Send).GetTask();
    }

    public async Task<Result<IReadOnlyList<PrintDestinationInfo>>> GetAvailablePrintersAsync(
        CancellationToken cancellationToken = default)
    {
        if (!SupportsCupsPrinting())
        {
            return ResultFactory.Failure<IReadOnlyList<PrintDestinationInfo>>(
                AppError.Unsupported(
                    "print.platform.unsupported",
                    "Advanced printing is not available on this platform yet."));
        }

        var printerNamesResult = await TryRunCommandAsync("lpstat", ["-e"], cancellationToken);
        if (printerNamesResult.CommandMissing)
        {
            return ResultFactory.Failure<IReadOnlyList<PrintDestinationInfo>>(
                AppError.Unsupported(
                    "print.command.missing",
                    "Printing is not available because the system print tools are missing."));
        }

        if (printerNamesResult.Result.IsFailure)
        {
            return ResultFactory.Failure<IReadOnlyList<PrintDestinationInfo>>(printerNamesResult.Result.Error!);
        }

        var defaultPrinterResult = await TryRunCommandAsync("lpstat", ["-d"], cancellationToken);
        string? defaultPrinter = null;
        if (!defaultPrinterResult.CommandMissing && defaultPrinterResult.Result.IsSuccess)
        {
            defaultPrinter = ParseDefaultPrinter(defaultPrinterResult.Result.Value);
        }

        var printers = ParsePrinters(printerNamesResult.Result.Value, defaultPrinter);
        return ResultFactory.Success<IReadOnlyList<PrintDestinationInfo>>(printers);
    }

    public async Task<Result> PrintAsync(
        PrintDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!SupportsCupsPrinting())
        {
            return ResultFactory.Failure(
                AppError.Unsupported(
                    "print.platform.unsupported",
                    "Printing is not available on this platform yet."));
        }

        var arguments = BuildPrintArguments(request);

        var lpResult = await TryRunCommandAsync("lp", arguments, cancellationToken);
        if (lpResult.CommandMissing)
        {
            var lprArguments = BuildLprArguments(request);
            var lprResult = await TryRunCommandAsync("lpr", lprArguments, cancellationToken);
            if (lprResult.CommandMissing)
            {
                return ResultFactory.Failure(
                    AppError.Unsupported(
                        "print.command.missing",
                        "Printing is not available because the system print tools are missing."));
            }

            return lprResult.Result.IsSuccess
                ? ResultFactory.Success()
                : ResultFactory.Failure(lprResult.Result.Error!);
        }

        return lpResult.Result.IsSuccess
            ? ResultFactory.Success()
            : ResultFactory.Failure(lpResult.Result.Error!);
    }

    private static bool SupportsCupsPrinting() => OperatingSystem.IsMacOS() || OperatingSystem.IsLinux();

    private static PrintDestinationInfo[] ParsePrinters(string? output, string? defaultPrinter)
    {
        var printerNames = (output ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (!string.IsNullOrWhiteSpace(defaultPrinter) &&
            !printerNames.Contains(defaultPrinter, StringComparer.Ordinal))
        {
            printerNames.Insert(0, defaultPrinter);
        }

        return printerNames
            .Select(name => new PrintDestinationInfo(
                name,
                string.Equals(name, defaultPrinter, StringComparison.Ordinal)))
            .ToArray();
    }

    private static string? ParseDefaultPrinter(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        var prefix = "system default destination:";
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return line[prefix.Length..].Trim();
            }
        }

        return null;
    }

    private static List<string> BuildPrintArguments(PrintDocumentRequest request)
    {
        var arguments = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.PrinterName))
        {
            arguments.Add("-d");
            arguments.Add(request.PrinterName);
        }

        arguments.Add("-n");
        arguments.Add(request.Copies.ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(request.PageRanges))
        {
            arguments.Add("-o");
            arguments.Add($"page-ranges={request.PageRanges}");
        }

        if (request.FitToPage)
        {
            arguments.Add("-o");
            arguments.Add("fit-to-page");
        }

        switch (request.Orientation)
        {
            case PrintOrientationOption.Portrait:
                arguments.Add("-o");
                arguments.Add("orientation-requested=3");
                break;
            case PrintOrientationOption.Landscape:
                arguments.Add("-o");
                arguments.Add("orientation-requested=4");
                break;
        }

        arguments.Add("-t");
        arguments.Add(Path.GetFileName(request.FilePath));
        arguments.Add(request.FilePath);

        return arguments;
    }

    private static List<string> BuildLprArguments(PrintDocumentRequest request)
    {
        var arguments = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.PrinterName))
        {
            arguments.Add("-P");
            arguments.Add(request.PrinterName);
        }

        arguments.Add($"-#{request.Copies}");

        if (!string.IsNullOrWhiteSpace(request.PageRanges))
        {
            arguments.Add("-o");
            arguments.Add($"page-ranges={request.PageRanges}");
        }

        if (request.FitToPage)
        {
            arguments.Add("-o");
            arguments.Add("fit-to-page");
        }

        switch (request.Orientation)
        {
            case PrintOrientationOption.Portrait:
                arguments.Add("-o");
                arguments.Add("orientation-requested=3");
                break;
            case PrintOrientationOption.Landscape:
                arguments.Add("-o");
                arguments.Add("orientation-requested=4");
                break;
        }

        arguments.Add(request.FilePath);

        return arguments;
    }

    private static async Task<CommandResult> TryRunCommandAsync(
        string commandName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo(commandName)
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
                return new CommandResult(
                    false,
                    ResultFactory.Failure<string>(
                        AppError.Infrastructure(
                            "print.process.start.failed",
                            "The print request could not be started.")));
            }

            await process.WaitForExitAsync(cancellationToken);
            var standardOutput = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardError = await process.StandardError.ReadToEndAsync(cancellationToken);

            if (process.ExitCode == 0)
            {
                return new CommandResult(false, ResultFactory.Success(standardOutput));
            }

            return new CommandResult(
                false,
                ResultFactory.Failure<string>(
                    AppError.Infrastructure(
                        "print.command.failed",
                        string.IsNullOrWhiteSpace(standardError)
                            ? "The system print service could not complete the request."
                            : "The system print service reported an error while handling the request.")));
        }
        catch (Win32Exception)
        {
            return new CommandResult(
                true,
                ResultFactory.Failure<string>(
                    AppError.Unsupported(
                        "print.command.missing",
                        "The system print command is unavailable.")));
        }
        catch (OperationCanceledException)
        {
            return new CommandResult(
                false,
                ResultFactory.Failure<string>(
                    AppError.Unexpected(
                        "print.cancelled",
                        "Printing was cancelled.")));
        }
        catch (Exception)
        {
            return new CommandResult(
                false,
                ResultFactory.Failure<string>(
                    AppError.Infrastructure(
                        "print.unexpected.failure",
                        "The document could not be sent to the system print service.")));
        }
    }

    private sealed record CommandResult(
        bool CommandMissing,
        Result<string> Result);

    private enum PrintDialogOutcome
    {
        Failed = 0,
        Success = 1,
        Cancelled = 2,
        Unsupported = 3
    }

    [SupportedOSPlatform("macos")]
    private static class MacOsPrintDialog
    {
        private const nuint PdfPrintScalingModeToFit = 1;
        private const byte Yes = 1;

        private static readonly Lazy<bool> FrameworkLoader = new(LoadFrameworks);

        public static PrintDialogOutcome Show(string filePath)
        {
            if (!FrameworkLoader.Value)
            {
                return PrintDialogOutcome.Unsupported;
            }

            var autoreleasePool = CreateAutoreleasePool();
            if (autoreleasePool == IntPtr.Zero)
            {
                return PrintDialogOutcome.Failed;
            }

            try
            {
                var printableDocument = CreatePrintableDocument(filePath);
                if (printableDocument == IntPtr.Zero)
                {
                    return PrintDialogOutcome.Failed;
                }

                try
                {
                    var printInfo = ObjectiveC.SendIntPtr(
                        ObjectiveC.GetClass("NSPrintInfo"),
                        "sharedPrintInfo");
                    if (printInfo == IntPtr.Zero)
                    {
                        return PrintDialogOutcome.Failed;
                    }

                    var printOperation = ObjectiveC.SendIntPtr(
                        printableDocument,
                        "printOperationForPrintInfo:scalingMode:autoRotate:",
                        printInfo,
                        PdfPrintScalingModeToFit,
                        Yes);
                    if (printOperation == IntPtr.Zero)
                    {
                        return PrintDialogOutcome.Failed;
                    }

                    ObjectiveC.SendVoid(printOperation, "setShowsPrintPanel:", Yes);
                    ObjectiveC.SendVoid(printOperation, "setShowsProgressPanel:", Yes);

                    var jobTitle = CreateNSString(Path.GetFileName(filePath));
                    if (jobTitle != IntPtr.Zero)
                    {
                        ObjectiveC.SendVoid(printOperation, "setJobTitle:", jobTitle);
                    }

                    return ObjectiveC.SendBool(printOperation, "runOperation")
                        ? PrintDialogOutcome.Success
                        : PrintDialogOutcome.Cancelled;
                }
                finally
                {
                    ObjectiveC.SendVoid(printableDocument, "release");
                }
            }
            finally
            {
                ObjectiveC.SendVoid(autoreleasePool, "release");
            }
        }

        private static bool LoadFrameworks()
        {
            try
            {
                NativeLibrary.Load("/System/Library/Frameworks/Foundation.framework/Foundation");
                NativeLibrary.Load("/System/Library/Frameworks/AppKit.framework/AppKit");
                NativeLibrary.Load("/System/Library/Frameworks/PDFKit.framework/PDFKit");
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static IntPtr CreateAutoreleasePool()
        {
            var poolClass = ObjectiveC.GetClass("NSAutoreleasePool");
            if (poolClass == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            var allocatedPool = ObjectiveC.SendIntPtr(poolClass, "alloc");
            return allocatedPool == IntPtr.Zero
                ? IntPtr.Zero
                : ObjectiveC.SendIntPtr(allocatedPool, "init");
        }

        private static IntPtr CreatePrintableDocument(string filePath)
        {
            return string.Equals(Path.GetExtension(filePath), ".pdf", StringComparison.OrdinalIgnoreCase)
                ? CreatePdfDocumentFromPdf(filePath)
                : CreatePdfDocumentFromImage(filePath);
        }

        private static IntPtr CreatePdfDocumentFromPdf(string filePath)
        {
            var fileUrl = CreateFileUrl(filePath);
            if (fileUrl == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            var pdfDocumentClass = ObjectiveC.GetClass("PDFDocument");
            if (pdfDocumentClass == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            var allocatedDocument = ObjectiveC.SendIntPtr(pdfDocumentClass, "alloc");
            if (allocatedDocument == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            var document = ObjectiveC.SendIntPtr(allocatedDocument, "initWithURL:", fileUrl);
            if (document != IntPtr.Zero)
            {
                return document;
            }

            ObjectiveC.SendVoid(allocatedDocument, "release");
            return IntPtr.Zero;
        }

        private static IntPtr CreatePdfDocumentFromImage(string filePath)
        {
            var imagePath = CreateNSString(filePath);
            if (imagePath == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            var nsImageClass = ObjectiveC.GetClass("NSImage");
            var pdfPageClass = ObjectiveC.GetClass("PDFPage");
            var pdfDocumentClass = ObjectiveC.GetClass("PDFDocument");
            if (nsImageClass == IntPtr.Zero ||
                pdfPageClass == IntPtr.Zero ||
                pdfDocumentClass == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            var allocatedImage = ObjectiveC.SendIntPtr(nsImageClass, "alloc");
            if (allocatedImage == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            var image = ObjectiveC.SendIntPtr(allocatedImage, "initWithContentsOfFile:", imagePath);
            if (image == IntPtr.Zero)
            {
                ObjectiveC.SendVoid(allocatedImage, "release");
                return IntPtr.Zero;
            }

            try
            {
                var allocatedPage = ObjectiveC.SendIntPtr(pdfPageClass, "alloc");
                if (allocatedPage == IntPtr.Zero)
                {
                    return IntPtr.Zero;
                }

                var page = ObjectiveC.SendIntPtr(allocatedPage, "initWithImage:", image);
                if (page == IntPtr.Zero)
                {
                    ObjectiveC.SendVoid(allocatedPage, "release");
                    return IntPtr.Zero;
                }

                try
                {
                    var allocatedDocument = ObjectiveC.SendIntPtr(pdfDocumentClass, "alloc");
                    if (allocatedDocument == IntPtr.Zero)
                    {
                        return IntPtr.Zero;
                    }

                    var document = ObjectiveC.SendIntPtr(allocatedDocument, "init");
                    if (document == IntPtr.Zero)
                    {
                        ObjectiveC.SendVoid(allocatedDocument, "release");
                        return IntPtr.Zero;
                    }

                    ObjectiveC.SendVoid(document, "insertPage:atIndex:", page, 0);
                    return document;
                }
                finally
                {
                    ObjectiveC.SendVoid(page, "release");
                }
            }
            finally
            {
                ObjectiveC.SendVoid(image, "release");
            }
        }

        private static IntPtr CreateFileUrl(string filePath)
        {
            var pathString = CreateNSString(filePath);
            if (pathString == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            return ObjectiveC.SendIntPtr(
                ObjectiveC.GetClass("NSURL"),
                "fileURLWithPath:",
                pathString);
        }

        private static IntPtr CreateNSString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return IntPtr.Zero;
            }

            var utf8Value = Marshal.StringToCoTaskMemUTF8(value);
            try
            {
                return ObjectiveC.SendIntPtr(
                    ObjectiveC.GetClass("NSString"),
                    "stringWithUTF8String:",
                    utf8Value);
            }
            finally
            {
                Marshal.FreeCoTaskMem(utf8Value);
            }
        }
    }

    [SupportedOSPlatform("macos")]
    private static partial class ObjectiveC
    {
        private const string ObjCLibrary = "/usr/lib/libobjc.A.dylib";

        public static IntPtr GetClass(string className) => objc_getClass(className);

        public static IntPtr SendIntPtr(IntPtr receiver, string selector) =>
            IntPtr_objc_msgSend(receiver, GetSelector(selector));

        public static IntPtr SendIntPtr(IntPtr receiver, string selector, IntPtr arg1) =>
            IntPtr_objc_msgSend_IntPtr(receiver, GetSelector(selector), arg1);

        public static IntPtr SendIntPtr(IntPtr receiver, string selector, IntPtr arg1, nuint arg2, byte arg3) =>
            IntPtr_objc_msgSend_IntPtr_nuint_byte(receiver, GetSelector(selector), arg1, arg2, arg3);

        public static bool SendBool(IntPtr receiver, string selector) =>
            Byte_objc_msgSend(receiver, GetSelector(selector)) != 0;

        public static void SendVoid(IntPtr receiver, string selector) =>
            Void_objc_msgSend(receiver, GetSelector(selector));

        public static void SendVoid(IntPtr receiver, string selector, byte arg1) =>
            Void_objc_msgSend_byte(receiver, GetSelector(selector), arg1);

        public static void SendVoid(IntPtr receiver, string selector, IntPtr arg1) =>
            Void_objc_msgSend_IntPtr(receiver, GetSelector(selector), arg1);

        public static void SendVoid(IntPtr receiver, string selector, IntPtr arg1, nuint arg2) =>
            Void_objc_msgSend_IntPtr_nuint(receiver, GetSelector(selector), arg1, arg2);

        private static IntPtr GetSelector(string selectorName) => sel_registerName(selectorName);

        [LibraryImport(ObjCLibrary, StringMarshalling = StringMarshalling.Utf8)]
        private static partial IntPtr objc_getClass(string name);

        [LibraryImport(ObjCLibrary, StringMarshalling = StringMarshalling.Utf8)]
        private static partial IntPtr sel_registerName(string selectorName);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        private static extern IntPtr IntPtr_objc_msgSend(IntPtr receiver, IntPtr selector);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        private static extern IntPtr IntPtr_objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        private static extern IntPtr IntPtr_objc_msgSend_IntPtr_nuint_byte(
            IntPtr receiver,
            IntPtr selector,
            IntPtr arg1,
            nuint arg2,
            byte arg3);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        private static extern byte Byte_objc_msgSend(IntPtr receiver, IntPtr selector);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        private static extern void Void_objc_msgSend(IntPtr receiver, IntPtr selector);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        private static extern void Void_objc_msgSend_byte(IntPtr receiver, IntPtr selector, byte arg1);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        private static extern void Void_objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        private static extern void Void_objc_msgSend_IntPtr_nuint(
            IntPtr receiver,
            IntPtr selector,
            IntPtr arg1,
            nuint arg2);
    }
}
