using System.Runtime.InteropServices;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Infrastructure.Pdf;

/// <summary>
/// Renders PDF pages to BGRA pixel buffers using PDFium.
/// </summary>
public sealed class PdfiumRenderService : IRenderService
{
    private readonly PdfiumInitializer _initializer;
    private readonly PdfiumExecutionGate _executionGate;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfiumRenderService"/> class.
    /// </summary>
    /// <param name="initializer">Ensures PDFium is initialized before rendering.</param>
    /// <param name="executionGate">Serializes access to the single-threaded PDFium library.</param>
    public PdfiumRenderService(PdfiumInitializer initializer, PdfiumExecutionGate executionGate)
    {
        ArgumentNullException.ThrowIfNull(initializer);
        ArgumentNullException.ThrowIfNull(executionGate);

        _initializer = initializer;
        _executionGate = executionGate;
    }

    /// <inheritdoc />
    public Task<RenderedPage> RenderPageAsync(
        IDocumentSession session,
        PageIndex pageIndex,
        double zoomFactor,
        Rotation rotation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(zoomFactor);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using IDisposable pdfiumAccess = _executionGate.Enter(cancellationToken);

            if (session is not PdfiumDocumentSession pdfSession)
            {
                throw new NotSupportedException("The active session is not backed by PDFium.");
            }

            _initializer.EnsureInitialized();

            IntPtr pageHandle = PdfiumNative.FPDF_LoadPage(pdfSession.Resource.Handle, pageIndex.Value);
            if (pageHandle == nint.Zero)
            {
                throw new InvalidOperationException($"Unable to load PDF page at index {pageIndex.Value}.");
            }

            nint bitmapHandle = nint.Zero;

            try
            {
                const int maxDimension = 16384;
                int rawWidth = Math.Clamp((int)Math.Ceiling(PdfiumNative.FPDF_GetPageWidthF(pageHandle) * zoomFactor), 1, maxDimension);
                int rawHeight = Math.Clamp((int)Math.Ceiling(PdfiumNative.FPDF_GetPageHeightF(pageHandle) * zoomFactor), 1, maxDimension);

                bool isQuarterTurn = rotation is Rotation.Deg90 or Rotation.Deg270;

                int targetWidth = isQuarterTurn ? rawHeight : rawWidth;
                int targetHeight = isQuarterTurn ? rawWidth : rawHeight;

                bitmapHandle = PdfiumNative.FPDFBitmap_CreateEx(
                    targetWidth,
                    targetHeight,
                    PdfiumNative.FPDFBitmap_BGRA,
                    nint.Zero,
                    0);

                if (bitmapHandle == nint.Zero)
                {
                    throw new InvalidOperationException("Unable to create PDFium bitmap.");
                }

                PdfiumNative.FPDFBitmap_FillRect(
                    bitmapHandle,
                    0,
                    0,
                    targetWidth,
                    targetHeight,
                    0xFFFFFFFFu);

                PdfiumNative.FPDF_RenderPageBitmap(
                    bitmapHandle,
                    pageHandle,
                    0,
                    0,
                    targetWidth,
                    targetHeight,
                    ToPdfiumRotation(rotation),
                    PdfiumNative.FPDF_ANNOT);

                int stride = PdfiumNative.FPDFBitmap_GetStride(bitmapHandle);
                IntPtr sourceBuffer = PdfiumNative.FPDFBitmap_GetBuffer(bitmapHandle);

                if (sourceBuffer == nint.Zero || stride <= 0)
                {
                    throw new InvalidOperationException("Unable to access PDFium bitmap buffer.");
                }

                int minimumStride = targetWidth * 4;
                if (stride < minimumStride)
                {
                    throw new InvalidOperationException(
                        $"Invalid PDFium bitmap stride. Expected at least {minimumStride} bytes but received {stride}.");
                }

                byte[] pixelData = new byte[targetWidth * targetHeight * 4];

                for (int y = 0; y < targetHeight; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    IntPtr rowSource = sourceBuffer + (y * stride);
                    int rowTargetOffset = y * targetWidth * 4;

                    Marshal.Copy(
                        rowSource,
                        pixelData,
                        rowTargetOffset,
                        targetWidth * 4);
                }

                return new RenderedPage(
                    pageIndex: pageIndex,
                    pixelData: pixelData,
                    width: targetWidth,
                    height: targetHeight);
            }
            finally
            {
                if (bitmapHandle != nint.Zero)
                {
                    PdfiumNative.FPDFBitmap_Destroy(bitmapHandle);
                }

                PdfiumNative.FPDF_ClosePage(pageHandle);
            }
        }, cancellationToken);
    }

    private static int ToPdfiumRotation(Rotation rotation)
    {
        return rotation switch
        {
            Rotation.Deg0 => 0,
            Rotation.Deg90 => 1,
            Rotation.Deg180 => 2,
            Rotation.Deg270 => 3,
            _ => 0
        };
    }
}
