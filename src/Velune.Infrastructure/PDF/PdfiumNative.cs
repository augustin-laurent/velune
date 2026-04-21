using System.Runtime.InteropServices;

namespace Velune.Infrastructure.Pdf;

internal static partial class PdfiumNative
{
    private const string LibraryName = "pdfium";

    internal const int FPDFBitmap_BGRA = 4;
    internal const int FPDF_ANNOT = 0x01;

    [LibraryImport(LibraryName, EntryPoint = "FPDF_InitLibrary")]
    internal static partial void FPDF_InitLibrary();

    [LibraryImport(LibraryName, EntryPoint = "FPDF_DestroyLibrary")]
    internal static partial void FPDF_DestroyLibrary();

    [LibraryImport(LibraryName, EntryPoint = "FPDF_LoadDocument", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint FPDF_LoadDocument(string filePath, string? password);

    [LibraryImport(LibraryName, EntryPoint = "FPDF_CloseDocument")]
    internal static partial void FPDF_CloseDocument(nint document);

    [LibraryImport(LibraryName, EntryPoint = "FPDF_GetLastError")]
    internal static partial uint FPDF_GetLastError();

    [LibraryImport(LibraryName, EntryPoint = "FPDF_GetPageCount")]
    internal static partial int FPDF_GetPageCount(nint document);

    [LibraryImport(LibraryName, EntryPoint = "FPDF_GetMetaText", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial uint FPDF_GetMetaText(
        nint document,
        string tag,
        byte[] buffer,
        uint buflen);

    [LibraryImport(LibraryName, EntryPoint = "FPDF_LoadPage")]
    internal static partial nint FPDF_LoadPage(nint document, int pageIndex);

    [LibraryImport(LibraryName, EntryPoint = "FPDF_ClosePage")]
    internal static partial void FPDF_ClosePage(nint page);

    [LibraryImport(LibraryName, EntryPoint = "FPDF_GetPageWidthF")]
    internal static partial float FPDF_GetPageWidthF(nint page);

    [LibraryImport(LibraryName, EntryPoint = "FPDF_GetPageHeightF")]
    internal static partial float FPDF_GetPageHeightF(nint page);

    [LibraryImport(LibraryName, EntryPoint = "FPDFText_LoadPage")]
    internal static partial nint FPDFText_LoadPage(nint page);

    [LibraryImport(LibraryName, EntryPoint = "FPDFText_ClosePage")]
    internal static partial void FPDFText_ClosePage(nint textPage);

    [LibraryImport(LibraryName, EntryPoint = "FPDFText_CountChars")]
    internal static partial int FPDFText_CountChars(nint textPage);

    [LibraryImport(LibraryName, EntryPoint = "FPDFText_GetUnicode")]
    internal static partial uint FPDFText_GetUnicode(nint textPage, int index);

    [LibraryImport(LibraryName, EntryPoint = "FPDFText_GetText")]
    internal static partial int FPDFText_GetText(
        nint textPage,
        int startIndex,
        int count,
        [Out] ushort[] result);

    [LibraryImport(LibraryName, EntryPoint = "FPDFText_GetCharBox")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFText_GetCharBox(
        nint textPage,
        int index,
        out double left,
        out double right,
        out double bottom,
        out double top);

    [LibraryImport(LibraryName, EntryPoint = "FPDFText_GetCharIndexAtPos")]
    internal static partial int FPDFText_GetCharIndexAtPos(
        nint textPage,
        double x,
        double y,
        double xTolerance,
        double yTolerance);

    [LibraryImport(LibraryName, EntryPoint = "FPDFText_CountRects")]
    internal static partial int FPDFText_CountRects(
        nint textPage,
        int startIndex,
        int count);

    [LibraryImport(LibraryName, EntryPoint = "FPDFText_GetRect")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFText_GetRect(
        nint textPage,
        int rectIndex,
        out double left,
        out double top,
        out double right,
        out double bottom);

    [LibraryImport(LibraryName, EntryPoint = "FPDFPage_HasTransparency")]
    internal static partial int FPDFPage_HasTransparency(nint page);

    [LibraryImport(LibraryName, EntryPoint = "FPDFBitmap_CreateEx")]
    internal static partial nint FPDFBitmap_CreateEx(
        int width,
        int height,
        int format,
        nint firstScan,
        int stride);

    [LibraryImport(LibraryName, EntryPoint = "FPDFBitmap_Destroy")]
    internal static partial void FPDFBitmap_Destroy(nint bitmap);

    [LibraryImport(LibraryName, EntryPoint = "FPDFBitmap_GetBuffer")]
    internal static partial nint FPDFBitmap_GetBuffer(nint bitmap);

    [LibraryImport(LibraryName, EntryPoint = "FPDFBitmap_GetStride")]
    internal static partial int FPDFBitmap_GetStride(nint bitmap);

    [LibraryImport(LibraryName, EntryPoint = "FPDFBitmap_FillRect")]
    internal static partial void FPDFBitmap_FillRect(
        nint bitmap,
        int left,
        int top,
        int width,
        int height,
        uint color);

    [LibraryImport(LibraryName, EntryPoint = "FPDF_RenderPageBitmap")]
    internal static partial void FPDF_RenderPageBitmap(
        nint bitmap,
        nint page,
        int startX,
        int startY,
        int sizeX,
        int sizeY,
        int rotate,
        int flags);
}
