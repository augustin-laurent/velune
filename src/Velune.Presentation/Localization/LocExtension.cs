using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;

namespace Velune.Presentation.Localization;

/// <summary>
/// XAML markup extension that provides reactive localized string bindings.
/// </summary>
public sealed class LocExtension : MarkupExtension
{
    /// <summary>
    /// Initializes a new instance with no key.
    /// </summary>
    public LocExtension()
    {
    }

    /// <summary>
    /// Initializes a new instance with the specified translation key.
    /// </summary>
    /// <param name="key">The translation key.</param>
    public LocExtension(string key)
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

        return new Binding(nameof(ILocalizationService.Version))
        {
            Source = LocalizationServiceLocator.Current,
            Converter = new LocValueConverter(Key)
        };
    }

    private sealed class LocValueConverter : IValueConverter
    {
        private readonly string _key;

        public LocValueConverter(string key)
        {
            _key = key;
        }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return LocalizationServiceLocator.Current?.GetString(_key) ?? _key;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
