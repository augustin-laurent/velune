using System.Globalization;

namespace Velune.Infrastructure.Text;

internal static class TesseractLanguageMapper
{
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
