using System.Collections.ObjectModel;

namespace Velune.Application.Documents;

/// <summary>Defines and validates the file formats supported by the application.</summary>
public static class SupportedDocumentFormats
{
    private static readonly string[] PdfExtensions = [".pdf"];
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".webp"];

    /// <summary>Gets all supported file extensions (PDF and images combined).</summary>
    public static IReadOnlyList<string> AllExtensions
    {
        get;
    } = new ReadOnlyCollection<string>([.. PdfExtensions, .. ImageExtensions]);

    /// <summary>Gets the supported PDF file extensions.</summary>
    public static IReadOnlyList<string> PdfFileExtensions
    {
        get;
    } = new ReadOnlyCollection<string>(PdfExtensions);

    /// <summary>Gets the supported image file extensions.</summary>
    public static IReadOnlyList<string> ImageFileExtensions
    {
        get;
    } = new ReadOnlyCollection<string>(ImageExtensions);

    /// <summary>Determines whether the extension represents a PDF file.</summary>
    /// <param name="extension">The file extension to check.</param>
    /// <returns><c>true</c> if the extension is a PDF extension; otherwise <c>false</c>.</returns>
    public static bool IsPdf(string extension) =>
        Matches(extension, PdfExtensions);

    /// <summary>Determines whether the extension represents an image file.</summary>
    /// <param name="extension">The file extension to check.</param>
    /// <returns><c>true</c> if the extension is a supported image extension; otherwise <c>false</c>.</returns>
    public static bool IsImage(string extension) =>
        Matches(extension, ImageExtensions);

    /// <summary>Determines whether the extension is any supported document format.</summary>
    /// <param name="extension">The file extension to check.</param>
    /// <returns><c>true</c> if the extension is supported; otherwise <c>false</c>.</returns>
    public static bool IsSupported(string extension) =>
        IsPdf(extension) || IsImage(extension);

    /// <summary>Gets a human-readable label for the given image extension.</summary>
    /// <param name="extension">The image file extension.</param>
    /// <returns>A display label describing the image format.</returns>
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
        string normalizedExtension = Normalize(extension);
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
