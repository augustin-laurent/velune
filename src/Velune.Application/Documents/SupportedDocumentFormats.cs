using System.Collections.ObjectModel;

namespace Velune.Application.Documents;

public static class SupportedDocumentFormats
{
    private static readonly string[] PdfExtensions = [".pdf"];
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".webp"];

    public static IReadOnlyList<string> AllExtensions
    {
        get;
    } = new ReadOnlyCollection<string>([.. PdfExtensions, .. ImageExtensions]);

    public static IReadOnlyList<string> PdfFileExtensions
    {
        get;
    } = new ReadOnlyCollection<string>(PdfExtensions);

    public static IReadOnlyList<string> ImageFileExtensions
    {
        get;
    } = new ReadOnlyCollection<string>(ImageExtensions);

    public static bool IsPdf(string extension) =>
        Matches(extension, PdfExtensions);

    public static bool IsImage(string extension) =>
        Matches(extension, ImageExtensions);

    public static bool IsSupported(string extension) =>
        IsPdf(extension) || IsImage(extension);

    public static string GetImageFormatLabel(string extension)
    {
        return Normalize(extension) switch
        {
            ".jpg" or ".jpeg" => "JPEG image",
            ".png" => "PNG image",
            ".webp" => "WebP image",
            _ => "Image"
        };
    }

    private static bool Matches(string extension, IReadOnlyCollection<string> supportedExtensions)
    {
        var normalizedExtension = Normalize(extension);
        return supportedExtensions.Contains(normalizedExtension, StringComparer.Ordinal);
    }

    private static string Normalize(string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        return extension[0] == '.'
            ? extension.ToLowerInvariant()
            : $".{extension.ToLowerInvariant()}";
    }
}
