using System.Runtime.InteropServices;

namespace Velune.Infrastructure.Pdf;

internal static partial class PdfiumNative
{
    private const string LibraryName = "pdfium";

    internal const int FPDFBitmap_BGRA = 4;
    internal const int FPDF_ANNOT = 0x01;

    [DllImport(LibraryName, EntryPoint = "FPDF_InitLibrary")]
    internal static extern void FPDF_InitLibrary();

    [DllImport(LibraryName, EntryPoint = "FPDF_DestroyLibrary")]
    internal static extern void FPDF_DestroyLibrary();

    [LibraryImport(LibraryName, EntryPoint = "FPDF_LoadDocument", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint FPDF_LoadDocument(string filePath, string? password);

    [DllImport(LibraryName, EntryPoint = "FPDF_CloseDocument")]
    internal static extern void FPDF_CloseDocument(nint document);

    [DllImport(LibraryName, EntryPoint = "FPDF_GetLastError")]
    internal static extern uint FPDF_GetLastError();

    [DllImport(LibraryName, EntryPoint = "FPDF_GetPageCount")]
    internal static extern int FPDF_GetPageCount(nint document);

    [DllImport(LibraryName, EntryPoint = "FPDF_LoadPage")]
    internal static extern nint FPDF_LoadPage(nint document, int pageIndex);

    [DllImport(LibraryName, EntryPoint = "FPDF_ClosePage")]
    internal static extern void FPDF_ClosePage(nint page);

    [DllImport(LibraryName, EntryPoint = "FPDF_GetPageWidthF")]
    internal static extern float FPDF_GetPageWidthF(nint page);

    [DllImport(LibraryName, EntryPoint = "FPDF_GetPageHeightF")]
    internal static extern float FPDF_GetPageHeightF(nint page);

    [DllImport(LibraryName, EntryPoint = "FPDFPage_HasTransparency")]
    internal static extern int FPDFPage_HasTransparency(nint page);

    [DllImport(LibraryName, EntryPoint = "FPDFBitmap_CreateEx")]
    internal static extern nint FPDFBitmap_CreateEx(
        int width,
        int height,
        int format,
        nint firstScan,
        int stride);

    [DllImport(LibraryName, EntryPoint = "FPDFBitmap_Destroy")]
    internal static extern void FPDFBitmap_Destroy(nint bitmap);

    [DllImport(LibraryName, EntryPoint = "FPDFBitmap_GetBuffer")]
    internal static extern nint FPDFBitmap_GetBuffer(nint bitmap);

    [DllImport(LibraryName, EntryPoint = "FPDFBitmap_GetStride")]
    internal static extern int FPDFBitmap_GetStride(nint bitmap);

    [DllImport(LibraryName, EntryPoint = "FPDFBitmap_FillRect")]
    internal static extern void FPDFBitmap_FillRect(
        nint bitmap,
        int left,
        int top,
        int width,
        int height,
        uint color);

    [DllImport(LibraryName, EntryPoint = "FPDF_RenderPageBitmap")]
    internal static extern void FPDF_RenderPageBitmap(
        nint bitmap,
        nint page,
        int startX,
        int startY,
        int sizeX,
        int sizeY,
        int rotate,
        int flags);
}
