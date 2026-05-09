namespace Velune.Presentation.Localization;

/// <summary>
/// Static service locator for the active <see cref="ILocalizationService"/> instance.
/// </summary>
public static class LocalizationServiceLocator
{
    /// <summary>
    /// Gets or sets the current localization service instance.
    /// </summary>
    public static ILocalizationService? Current
    {
        get; set;
    }
}
