using System.Globalization;

namespace Velune.Windows.Services;

public interface IWindowsTextCatalog
{
    string GetString(string key);

    string Format(string key, params object[] args);
}

public sealed class WindowsTextCatalog : IWindowsTextCatalog
{
    private readonly Dictionary<string, string> _fallbackCatalog;
    private readonly Dictionary<string, string> _activeCatalog;

    public WindowsTextCatalog()
    {
        var catalogRoot = Path.Combine(AppContext.BaseDirectory, "Localization");
        _fallbackCatalog = LoadCatalog(Path.Combine(catalogRoot, "en.lang"));

        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "fr" => "fr",
            "es" => "es",
            _ => "en"
        };

        _activeCatalog = language == "en"
            ? _fallbackCatalog
            : LoadCatalog(Path.Combine(catalogRoot, $"{language}.lang"));
    }

    public string GetString(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return _activeCatalog.TryGetValue(key, out var activeValue)
            ? activeValue
            : _fallbackCatalog.TryGetValue(key, out var fallbackValue)
                ? fallbackValue
                : key;
    }

    public string Format(string key, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, GetString(key), args);
    }

    private static Dictionary<string, string> LoadCatalog(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return values;
        }

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            values[key] = value.Replace(@"\n", Environment.NewLine, StringComparison.Ordinal);
        }

        return values;
    }
}
