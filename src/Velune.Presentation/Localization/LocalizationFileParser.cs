namespace Velune.Presentation.Localization;

internal static class LocalizationFileParser
{
    public static Dictionary<string, string> Parse(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var entries = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var rawLine in File.ReadAllLines(filePath))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = rawLine.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = rawLine[..separatorIndex].Trim();
            var value = Unescape(rawLine[(separatorIndex + 1)..].Trim());
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            entries[key] = value;
        }

        return entries;
    }

    private static string Unescape(string value)
    {
        return value
            .Replace("\\r", "\r", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\t", "\t", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);
    }
}
