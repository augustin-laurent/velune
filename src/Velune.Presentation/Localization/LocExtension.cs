using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using System.Globalization;

namespace Velune.Presentation.Localization;

public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key)
    {
        Key = key;
    }

    public string Key
    {
        get; set;
    } = string.Empty;

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
