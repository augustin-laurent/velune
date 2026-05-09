using Avalonia.Markup.Xaml;

namespace Velune.Presentation.Localization;

/// <summary>
/// XAML markup extension that provides a statically-resolved localized string for native menus.
/// </summary>
public sealed class NativeLocExtension : MarkupExtension
{
    /// <summary>
    /// Initializes a new instance with no key.
    /// </summary>
    public NativeLocExtension()
    {
    }

    /// <summary>
    /// Initializes a new instance with the specified translation key.
    /// </summary>
    /// <param name="key">The translation key.</param>
    public NativeLocExtension(string key)
    {
        Key = key;
    }

    /// <summary>
    /// Gets or sets the translation key to resolve.
    /// </summary>
    public string Key
    {
        get; set;
    } = string.Empty;

    /// <inheritdoc />
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (LocalizationServiceLocator.Current is null || string.IsNullOrWhiteSpace(Key))
        {
            return Key;
        }

        return LocalizationServiceLocator.Current.GetString(Key);
    }
}
