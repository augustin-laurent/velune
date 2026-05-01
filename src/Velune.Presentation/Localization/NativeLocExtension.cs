using Avalonia.Markup.Xaml;

namespace Velune.Presentation.Localization;

public sealed class NativeLocExtension : MarkupExtension
{
    public NativeLocExtension()
    {
    }

    public NativeLocExtension(string key)
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

        return LocalizationServiceLocator.Current.GetString(Key);
    }
}
