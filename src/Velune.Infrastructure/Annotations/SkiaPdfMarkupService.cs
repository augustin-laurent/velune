using System.Runtime.InteropServices;
using SkiaSharp;
using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Domain.Annotations;
using Velune.Infrastructure.Pdf;

namespace Velune.Infrastructure.Annotations;

public sealed class SkiaPdfMarkupService : IPdfMarkupService
{
    private const float OverlayRenderScale = 2.25f;
    private readonly PdfiumInitializer _pdfiumInitializer;
    private readonly ISignatureAssetStore _signatureAssetStore;

    public SkiaPdfMarkupService(
        PdfiumInitializer pdfiumInitializer,
        ISignatureAssetStore signatureAssetStore)
    {
        ArgumentNullException.ThrowIfNull(pdfiumInitializer);
        ArgumentNullException.ThrowIfNull(signatureAssetStore);

        _pdfiumInitializer = pdfiumInitializer;
        _signatureAssetStore = signatureAssetStore;
    }

    public async Task<Result<string>> ApplyAnnotationsAsync(
        ApplyPdfAnnotationsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Session is not PdfiumDocumentSession)
        {
            return ResultFactory.Failure<string>(
                AppError.Validation("document.annotation.session.invalid", "The current document session is not a PDF session."));
        }

        if (string.IsNullOrWhiteSpace(request.InputPath))
        {
            return ResultFactory.Failure<string>(
                AppError.Validation("document.annotation.input.empty", "The input path is required."));
        }

        if (string.IsNullOrWhiteSpace(request.OutputPath))
        {
            return ResultFactory.Failure<string>(
                AppError.Validation("document.annotation.output.empty", "The output path is required."));
        }

        try
        {
            _pdfiumInitializer.EnsureInitialized();

            var signatureAssets = _signatureAssetStore.GetAll().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
            using var document = new PdfiumEditableDocument(request.InputPath);

            foreach (var pageAnnotations in request.Annotations
                         .GroupBy(annotation => annotation.PageIndex.Value)
                         .OrderBy(group => group.Key))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (pageAnnotations.Key < 0 || pageAnnotations.Key >= document.PageCount)
                {
                    return ResultFactory.Failure<string>(
                        AppError.Validation(
                            "document.annotation.page.invalid",
                            $"Annotation page index {pageAnnotations.Key + 1} is outside the document bounds."));
                }

                PdfiumEditablePage? page = null;
                SKBitmap? overlayBitmap = null;

                try
                {
#pragma warning disable CA2000
                    page = document.LoadPage(pageAnnotations.Key);
#pragma warning restore CA2000
                    var pageWidth = PdfiumNative.FPDF_GetPageWidthF(page.Handle);
                    var pageHeight = PdfiumNative.FPDF_GetPageHeightF(page.Handle);
#pragma warning disable CA2000
                    overlayBitmap = BuildOverlayBitmap(
                        pageWidth,
                        pageHeight,
                        [.. pageAnnotations.Select(annotation => annotation.DeepCopy())],
                        signatureAssets);
#pragma warning restore CA2000

                    if (overlayBitmap is null)
                    {
                        continue;
                    }

                    using var pdfiumBitmap = PdfiumBitmap.FromSkiaBitmap(overlayBitmap);
                    var imageObject = PdfiumNative.FPDFPageObj_NewImageObj(document.Handle);
                    if (imageObject == nint.Zero)
                    {
                        throw new InvalidOperationException("Unable to create a PDF image object for annotations.");
                    }

                    try
                    {
                        if (!PdfiumNative.FPDFImageObj_SetBitmap([page.Handle], 1, imageObject, pdfiumBitmap.Handle))
                        {
                            throw new InvalidOperationException("Unable to assign the annotation overlay bitmap to the PDF image object.");
                        }

                        if (!PdfiumNative.FPDFImageObj_SetMatrix(
                                imageObject,
                                pageWidth,
                                0,
                                0,
                                pageHeight,
                                0,
                                0))
                        {
                            throw new InvalidOperationException("Unable to position the annotation overlay inside the PDF page.");
                        }

                        PdfiumNative.FPDFPage_InsertObject(page.Handle, imageObject);
                        imageObject = nint.Zero;

                        if (!PdfiumNative.FPDFPage_GenerateContent(page.Handle))
                        {
                            throw new InvalidOperationException("Unable to persist the PDF annotation overlay content.");
                        }
                    }
                    finally
                    {
                        if (imageObject != nint.Zero)
                        {
                            PdfiumNative.FPDFPageObj_Destroy(imageObject);
                        }
                    }
                }
                finally
                {
                    overlayBitmap?.Dispose();
                    page?.Dispose();
                }
            }

            using var writer = new PdfiumFileWriter(new FileStream(
                request.OutputPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None));

            if (!PdfiumNative.FPDF_SaveAsCopy(document.Handle, writer.Handle, 0))
            {
                throw new InvalidOperationException("Unable to save the updated PDF document.");
            }

            return ResultFactory.Success(request.OutputPath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or FileNotFoundException)
        {
            return ResultFactory.Failure<string>(
                AppError.Infrastructure(
                    "document.annotation.pdf_failed",
                    exception.Message));
        }
    }

    private static SKBitmap? BuildOverlayBitmap(
        float pageWidth,
        float pageHeight,
        IReadOnlyList<DocumentAnnotation> annotations,
        IReadOnlyDictionary<string, SignatureAsset> signatureAssets)
    {
        if (annotations.Count == 0)
        {
            return null;
        }

        var pixelWidth = Math.Max(1, (int)Math.Ceiling(pageWidth * OverlayRenderScale));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(pageHeight * OverlayRenderScale));
#pragma warning disable CA2000
        var bitmap = new SKBitmap(new SKImageInfo(pixelWidth, pixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul));
#pragma warning restore CA2000

        try
        {
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            SkiaAnnotationRenderer.DrawAnnotations(
                canvas,
                pixelWidth,
                pixelHeight,
                Domain.ValueObjects.Rotation.Deg0,
                annotations,
                signatureAssets);
            canvas.Flush();
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private sealed class PdfiumEditableDocument : IDisposable
    {
        private nint _handle;

        public PdfiumEditableDocument(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("The source PDF document does not exist.", filePath);
            }

            _handle = PdfiumNative.FPDF_LoadDocument(filePath, null);
            if (_handle == nint.Zero)
            {
                throw new InvalidOperationException($"Unable to open PDF document for annotation save. Error: {PdfiumNative.FPDF_GetLastError()}.");
            }

            PageCount = PdfiumNative.FPDF_GetPageCount(_handle);
        }

        public nint Handle => _handle;

        public int PageCount
        {
            get;
        }

        public PdfiumEditablePage LoadPage(int pageIndex)
        {
            var pageHandle = PdfiumNative.FPDF_LoadPage(_handle, pageIndex);
            if (pageHandle == nint.Zero)
            {
                throw new InvalidOperationException($"Unable to load PDF page {pageIndex + 1}.");
            }

            return new PdfiumEditablePage(pageHandle);
        }

        public void Dispose()
        {
            var handle = Interlocked.Exchange(ref _handle, nint.Zero);
            if (handle != nint.Zero)
            {
                PdfiumNative.FPDF_CloseDocument(handle);
            }
        }
    }

    private sealed class PdfiumEditablePage : IDisposable
    {
        private nint _handle;

        public PdfiumEditablePage(nint handle)
        {
            _handle = handle;
        }

        public nint Handle => _handle;

        public void Dispose()
        {
            var handle = Interlocked.Exchange(ref _handle, nint.Zero);
            if (handle != nint.Zero)
            {
                PdfiumNative.FPDF_ClosePage(handle);
            }
        }
    }

    private sealed class PdfiumBitmap : IDisposable
    {
        private nint _handle;

        private PdfiumBitmap(nint handle)
        {
            _handle = handle;
        }

        public nint Handle => _handle;

        public static PdfiumBitmap FromSkiaBitmap(SKBitmap source)
        {
            var handle = PdfiumNative.FPDFBitmap_CreateEx(
                source.Width,
                source.Height,
                PdfiumNative.FPDFBitmap_BGRA,
                nint.Zero,
                0);

            if (handle == nint.Zero)
            {
                throw new InvalidOperationException("Unable to allocate a PDFium bitmap for annotations.");
            }

            var targetBuffer = PdfiumNative.FPDFBitmap_GetBuffer(handle);
            var targetStride = PdfiumNative.FPDFBitmap_GetStride(handle);
            var sourceStride = source.RowBytes;
            var rowLength = source.Width * 4;
            var rowBuffer = new byte[rowLength];

            for (var row = 0; row < source.Height; row++)
            {
                Marshal.Copy(
                    nint.Add(source.GetPixels(), row * sourceStride),
                    rowBuffer,
                    0,
                    rowLength);
                Marshal.Copy(
                    rowBuffer,
                    0,
                    nint.Add(targetBuffer, row * targetStride),
                    rowLength);
            }

            return new PdfiumBitmap(handle);
        }

        public void Dispose()
        {
            var handle = Interlocked.Exchange(ref _handle, nint.Zero);
            if (handle != nint.Zero)
            {
                PdfiumNative.FPDFBitmap_Destroy(handle);
            }
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int PdfiumWriteBlockCallback(nint self, nint data, nuint size);

    private sealed class PdfiumFileWriter : IDisposable
    {
        private static readonly Dictionary<nint, PdfiumFileWriter> Writers = [];
        private static readonly Lock WritersGate = new();
        private static readonly PdfiumWriteBlockCallback WriteBlockCallbackInstance = WriteBlock;

        private readonly Stream _stream;
        private readonly nint _nativeHandle;
        private readonly GCHandle _callbackHandle;
        private bool _disposed;

        public PdfiumFileWriter(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            _stream = stream;
            _callbackHandle = GCHandle.Alloc(WriteBlockCallbackInstance);
            var nativeStruct = new PdfiumNative.FpdfFileWrite
            {
                Version = 1,
                WriteBlock = Marshal.GetFunctionPointerForDelegate(WriteBlockCallbackInstance)
            };

            _nativeHandle = Marshal.AllocHGlobal(Marshal.SizeOf<PdfiumNative.FpdfFileWrite>());
            Marshal.StructureToPtr(nativeStruct, _nativeHandle, fDeleteOld: false);

            lock (WritersGate)
            {
                Writers[_nativeHandle] = this;
            }
        }

        public nint Handle => _nativeHandle;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            lock (WritersGate)
            {
                Writers.Remove(_nativeHandle);
            }

            if (_nativeHandle != nint.Zero)
            {
                Marshal.FreeHGlobal(_nativeHandle);
            }

            if (_callbackHandle.IsAllocated)
            {
                _callbackHandle.Free();
            }

            _stream.Dispose();
        }

        private static int WriteBlock(nint self, nint data, nuint size)
        {
            PdfiumFileWriter? writer;
            lock (WritersGate)
            {
                Writers.TryGetValue(self, out writer);
            }

            if (writer is null)
            {
                return 0;
            }

            try
            {
                checked
                {
                    var length = (int)size;
                    var buffer = new byte[length];
                    Marshal.Copy(data, buffer, 0, length);
                    writer._stream.Write(buffer, 0, length);
                    return 1;
                }
            }
            catch
            {
                return 0;
            }
        }
    }
}
