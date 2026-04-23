using Velune.Application.Configuration;

namespace Velune.Presentation.Localization;

public interface ILocalizationService
{
    string CurrentLanguageCode
    {
        get;
    }

    AppLanguagePreference CurrentLanguagePreference
    {
        get;
    }

    int Version
    {
        get;
    }

    event EventHandler? LanguageChanged;

    string GetString(string key);

    string GetString(string key, params object?[] arguments);

    bool HasKey(string key);
}
