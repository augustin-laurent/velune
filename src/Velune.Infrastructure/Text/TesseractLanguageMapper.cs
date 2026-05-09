using System.Globalization;

namespace Velune.Infrastructure.Text;

/// <summary>
/// Maps .NET culture codes to Tesseract three-letter language identifiers.
/// </summary>
internal static class TesseractLanguageMapper
{
    /// <summary>
    /// Returns the Tesseract language code for the given culture, or null if unmapped.
    /// </summary>
    /// <param name="culture">The culture to map.</param>
    /// <returns>A Tesseract language code such as "fra" or "eng", or null.</returns>
    public static string? MapCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        return culture.TwoLetterISOLanguageName switch
        {
            "fr" => "fra",
            "en" => "eng",
            "de" => "deu",
            "es" => "spa",
            "it" => "ita",
            "pt" => "por",
            "nl" => "nld",
            "da" => "dan",
            "fi" => "fin",
            "sv" => "swe",
            "no" or "nb" or "nn" => "nor",
            _ => null
        };
    }
}
