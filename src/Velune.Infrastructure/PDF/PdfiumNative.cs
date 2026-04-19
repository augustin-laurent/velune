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

    [LibraryImport(LibraryName, EntryPoint = "FPDF_GetMetaText", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial uint FPDF_GetMetaText(
        nint document,
        string tag,
        byte[] buffer,
        uint buflen);

    [DllImport(LibraryName, EntryPoint = "FPDF_LoadPage")]
    internal static extern nint FPDF_LoadPage(nint document, int pageIndex);

    [DllImport(LibraryName, EntryPoint = "FPDF_ClosePage")]
    internal static extern void FPDF_ClosePage(nint page);

    [DllImport(LibraryName, EntryPoint = "FPDF_GetPageWidthF")]
    internal static extern float FPDF_GetPageWidthF(nint page);

    [DllImport(LibraryName, EntryPoint = "FPDF_GetPageHeightF")]
    internal static extern float FPDF_GetPageHeightF(nint page);

    [DllImport(LibraryName, EntryPoint = "FPDFText_LoadPage")]
    internal static extern nint FPDFText_LoadPage(nint page);

    [DllImport(LibraryName, EntryPoint = "FPDFText_ClosePage")]
    internal static extern void FPDFText_ClosePage(nint textPage);

    [DllImport(LibraryName, EntryPoint = "FPDFText_CountChars")]
    internal static extern int FPDFText_CountChars(nint textPage);

    [DllImport(LibraryName, EntryPoint = "FPDFText_GetUnicode")]
    internal static extern uint FPDFText_GetUnicode(nint textPage, int index);

    [DllImport(LibraryName, EntryPoint = "FPDFText_GetText")]
    internal static extern int FPDFText_GetText(
        nint textPage,
        int startIndex,
        int count,
        [Out] ushort[] result);

    [DllImport(LibraryName, EntryPoint = "FPDFText_GetCharBox")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool FPDFText_GetCharBox(
        nint textPage,
        int index,
        out double left,
        out double right,
        out double bottom,
        out double top);

    [DllImport(LibraryName, EntryPoint = "FPDFText_GetCharIndexAtPos")]
    internal static extern int FPDFText_GetCharIndexAtPos(
        nint textPage,
        double x,
        double y,
        double xTolerance,
        double yTolerance);

    [DllImport(LibraryName, EntryPoint = "FPDFText_CountRects")]
    internal static extern int FPDFText_CountRects(
        nint textPage,
        int startIndex,
        int count);

    [DllImport(LibraryName, EntryPoint = "FPDFText_GetRect")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool FPDFText_GetRect(
        nint textPage,
        int rectIndex,
        out double left,
        out double top,
        out double right,
        out double bottom);

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
