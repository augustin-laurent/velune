using System.Runtime.InteropServices;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Infrastructure.Pdf;

public sealed class PdfiumRenderService : IRenderService
{
    private readonly PdfiumInitializer _initializer;

    public PdfiumRenderService(PdfiumInitializer initializer)
    {
        ArgumentNullException.ThrowIfNull(initializer);
        _initializer = initializer;
    }

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

            if (session is not PdfiumDocumentSession pdfSession)
            {
                throw new NotSupportedException("The active session is not backed by PDFium.");
            }

            _initializer.EnsureInitialized();

            var pageHandle = PdfiumNative.FPDF_LoadPage(pdfSession.Resource.Handle, pageIndex.Value);
            if (pageHandle == nint.Zero)
            {
                throw new InvalidOperationException($"Unable to load PDF page at index {pageIndex.Value}.");
            }

            nint bitmapHandle = nint.Zero;

            try
            {
                var rawWidth = Math.Max(1, (int)Math.Ceiling(PdfiumNative.FPDF_GetPageWidthF(pageHandle) * zoomFactor));
                var rawHeight = Math.Max(1, (int)Math.Ceiling(PdfiumNative.FPDF_GetPageHeightF(pageHandle) * zoomFactor));

                var isQuarterTurn = rotation is Rotation.Deg90 or Rotation.Deg270;

                var targetWidth = isQuarterTurn ? rawHeight : rawWidth;
                var targetHeight = isQuarterTurn ? rawWidth : rawHeight;

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

                var hasTransparency = PdfiumNative.FPDFPage_HasTransparency(pageHandle) != 0;
                var background = hasTransparency ? 0x00000000u : 0xFFFFFFFFu;

                PdfiumNative.FPDFBitmap_FillRect(
                    bitmapHandle,
                    0,
                    0,
                    targetWidth,
                    targetHeight,
                    background);

                PdfiumNative.FPDF_RenderPageBitmap(
                    bitmapHandle,
                    pageHandle,
                    0,
                    0,
                    targetWidth,
                    targetHeight,
                    ToPdfiumRotation(rotation),
                    PdfiumNative.FPDF_ANNOT);

                var stride = PdfiumNative.FPDFBitmap_GetStride(bitmapHandle);
                var sourceBuffer = PdfiumNative.FPDFBitmap_GetBuffer(bitmapHandle);

                if (sourceBuffer == nint.Zero || stride <= 0)
                {
                    throw new InvalidOperationException("Unable to access PDFium bitmap buffer.");
                }

                var pixelData = new byte[targetWidth * targetHeight * 4];

                for (var y = 0; y < targetHeight; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var rowSource = sourceBuffer + (y * stride);
                    var rowTargetOffset = y * targetWidth * 4;

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
