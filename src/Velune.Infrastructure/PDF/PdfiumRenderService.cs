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

        if (session is not PdfiumDocumentSession pdfSession)
        {
            throw new NotSupportedException("The active session is not backed by PDFium.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(zoomFactor);

        _initializer.EnsureInitialized();

        var pageHandle = PdfiumNative.FPDF_LoadPage(pdfSession.Resource.Handle, pageIndex.Value);
        if (pageHandle == nint.Zero)
        {
            throw new InvalidOperationException($"Unable to load PDF page at index {pageIndex.Value}.");
        }

        nint bitmapHandle = nint.Zero;

        try
        {
            var pageWidth = Math.Max(1, (int)Math.Ceiling(PdfiumNative.FPDF_GetPageWidthF(pageHandle) * zoomFactor));
            var pageHeight = Math.Max(1, (int)Math.Ceiling(PdfiumNative.FPDF_GetPageHeightF(pageHandle) * zoomFactor));

            bitmapHandle = PdfiumNative.FPDFBitmap_CreateEx(
                pageWidth,
                pageHeight,
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
                pageWidth,
                pageHeight,
                background);

            PdfiumNative.FPDF_RenderPageBitmap(
                bitmapHandle,
                pageHandle,
                0,
                0,
                pageWidth,
                pageHeight,
                ToPdfiumRotation(rotation),
                PdfiumNative.FPDF_ANNOT);

            var stride = PdfiumNative.FPDFBitmap_GetStride(bitmapHandle);
            var sourceBuffer = PdfiumNative.FPDFBitmap_GetBuffer(bitmapHandle);

            if (sourceBuffer == nint.Zero || stride <= 0)
            {
                throw new InvalidOperationException("Unable to access PDFium bitmap buffer.");
            }

            var pixelData = new byte[pageWidth * pageHeight * 4];

            for (var y = 0; y < pageHeight; y++)
            {
                var rowSource = sourceBuffer + (y * stride);
                var rowTargetOffset = y * pageWidth * 4;
                System.Runtime.InteropServices.Marshal.Copy(
                    rowSource,
                    pixelData,
                    rowTargetOffset,
                    pageWidth * 4);
            }

            var renderedPage = new RenderedPage(
                pageIndex: pageIndex,
                pixelData: pixelData,
                width: pageWidth,
                height: pageHeight);

            return Task.FromResult(renderedPage);
        }
        finally
        {
            if (bitmapHandle != nint.Zero)
            {
                PdfiumNative.FPDFBitmap_Destroy(bitmapHandle);
            }

            PdfiumNative.FPDF_ClosePage(pageHandle);
        }
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
