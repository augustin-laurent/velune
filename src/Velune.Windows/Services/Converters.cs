using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Velune.Domain.Annotations;

namespace Velune.Windows.Services;

/// <summary>
/// Converts a boolean value to <see cref="Visibility"/> (true = Visible).
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var visible = value is bool boolValue && boolValue;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is Visibility visibility && visibility == Visibility.Visible;
    }
}

/// <summary>
/// Converts a boolean value to <see cref="Visibility"/> (true = Collapsed).
/// </summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var visible = value is not bool boolValue || !boolValue;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is Visibility visibility && visibility != Visibility.Visible;
    }
}

/// <summary>
/// Returns a highlighted brush when the bound annotation tool matches the parameter tool.
/// </summary>
public sealed class AnnotationToolBackgroundConverter : IValueConverter
{
    private static readonly SolidColorBrush SelectedBrush = new(global::Windows.UI.Color.FromArgb(255, 0, 120, 212));
    private static readonly SolidColorBrush TransparentBrush = new(global::Windows.UI.Color.FromArgb(0, 0, 0, 0));

    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (parameter is not string toolName ||
            !Enum.TryParse(toolName, ignoreCase: true, out AnnotationTool targetTool))
        {
            return TransparentBrush;
        }

        return value is AnnotationTool selectedTool && selectedTool == targetTool
            ? SelectedBrush
            : TransparentBrush;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// Converts a boolean selection state to a highlighted or default background brush.
/// </summary>
public sealed class BoolToSelectedToolBackgroundConverter : IValueConverter
{
    private static readonly SolidColorBrush SelectedBrush = new(global::Windows.UI.Color.FromArgb(255, 0, 120, 212));
    private static readonly SolidColorBrush DefaultBrush = new(global::Windows.UI.Color.FromArgb(31, 255, 255, 255));

    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? SelectedBrush : DefaultBrush;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// Returns Visible when the file path has a .pdf extension.
/// </summary>
public sealed class RecentFilePdfVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return IsPdf(value)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return DependencyProperty.UnsetValue;
    }

    private static bool IsPdf(object value)
    {
        return string.Equals(
            Path.GetExtension(value as string),
            ".pdf",
            StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Returns Visible when the file path is a non-PDF (image) file.
/// </summary>
public sealed class RecentFileImageVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return string.Equals(
                Path.GetExtension(value as string),
                ".pdf",
                StringComparison.OrdinalIgnoreCase)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// Returns an accent brush color based on the file extension (PDF, WebP, or image).
/// </summary>
public sealed class RecentFileBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush PdfBrush = new(global::Windows.UI.Color.FromArgb(255, 232, 35, 46));
    private static readonly SolidColorBrush ImageBrush = new(global::Windows.UI.Color.FromArgb(255, 0, 102, 216));
    private static readonly SolidColorBrush WebpBrush = new(global::Windows.UI.Color.FromArgb(255, 45, 145, 111));

    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var extension = Path.GetExtension(value as string)?.ToLowerInvariant() ?? string.Empty;
        return extension switch
        {
            ".pdf" => PdfBrush,
            ".webp" => WebpBrush,
            _ => ImageBrush
        };
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// Converts a <see cref="DateTimeOffset"/> to a localized relative date string (e.g. "Today at 10:30").
/// </summary>
public sealed class RecentFileOpenedAtConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not DateTimeOffset openedAt ||
            openedAt == default)
        {
            return string.Empty;
        }

        var local = openedAt.ToLocalTime().DateTime;
        var today = DateTime.Today;
        var culture = CultureInfo.CurrentUICulture;
        var languageName = culture.TwoLetterISOLanguageName;

        if (local.Date == today)
        {
            return languageName switch
            {
                "fr" => $"Aujourd'hui à {local:HH:mm}",
                "es" => $"Hoy a las {local:HH:mm}",
                _ => $"Today at {local.ToString("h:mm tt", culture)}"
            };
        }

        if (local.Date == today.AddDays(-1))
        {
            return languageName switch
            {
                "fr" => $"Hier à {local:HH:mm}",
                "es" => $"Ayer a las {local:HH:mm}",
                _ => $"Yesterday at {local.ToString("h:mm tt", culture)}"
            };
        }

        return languageName switch
        {
            "fr" => local.ToString("d MMM yyyy 'à' HH:mm", culture),
            "es" => local.ToString("d MMM yyyy 'a las' HH:mm", culture),
            _ => local.ToString("MMM d, yyyy 'at' h:mm tt", culture)
        };
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return DependencyProperty.UnsetValue;
    }
}
