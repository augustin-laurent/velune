using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Velune.Domain.Annotations;
using Windows.Foundation;

namespace Velune.Windows.Services;

/// <summary>
/// Converts a boolean value to <see cref="Visibility"/> (true = Visible).
/// </summary>
public sealed partial class BoolToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool visible = value is true;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is Visibility.Visible;
    }
}

/// <summary>
/// Converts a boolean value to <see cref="Visibility"/> (true = Collapsed).
/// </summary>
public sealed partial class InverseBoolToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool visible = value is not true;
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
public sealed partial class AnnotationToolBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (parameter is not string toolName ||
            !Enum.TryParse(toolName, ignoreCase: true, out AnnotationTool targetTool))
        {
            return new SolidColorBrush(global::Windows.UI.Color.FromArgb(0, 0, 0, 0));
        }

        if (value is AnnotationTool selectedTool && selectedTool == targetTool)
        {
            return Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("AccentBgBrush", out object? brush) && brush is SolidColorBrush accentBrush
                ? accentBrush
                : new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 0, 120, 212));
        }

        return new SolidColorBrush(global::Windows.UI.Color.FromArgb(0, 0, 0, 0));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// Converts a boolean selection state to a highlighted or default background brush.
/// </summary>
public sealed partial class BoolToSelectedToolBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is true)
        {
            return Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("AccentBgBrush", out object? brush) && brush is SolidColorBrush accentBrush
                ? accentBrush
                : new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 0, 120, 212));
        }

        return Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("CardBrush", out object? defaultBrush) && defaultBrush is SolidColorBrush cardBrush
            ? cardBrush
            : new SolidColorBrush(global::Windows.UI.Color.FromArgb(31, 255, 255, 255));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// Returns Visible when the file path has a .pdf extension.
/// </summary>
public sealed partial class RecentFilePdfVisibilityConverter : IValueConverter
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
public sealed partial class RecentFileImageVisibilityConverter : IValueConverter
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
public sealed partial class RecentFileBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush PdfBrush = new(global::Windows.UI.Color.FromArgb(255, 232, 35, 46));
    private static readonly SolidColorBrush WebpBrush = new(global::Windows.UI.Color.FromArgb(255, 45, 145, 111));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        string extension = Path.GetExtension(value as string)?.ToLowerInvariant() ?? string.Empty;
        return extension switch
        {
            ".pdf" => PdfBrush,
            ".webp" => WebpBrush,
            _ => Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("AccentBgBrush", out object? brush) && brush is SolidColorBrush accentBrush
                ? accentBrush
                : new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 0, 102, 216))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// Subtracts the parameter value from the bound double value.
/// </summary>
public sealed partial class SubtractConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double d && parameter is string s && double.TryParse(s, CultureInfo.InvariantCulture, out double subtract))
        {
            return d - subtract;
        }

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// Converts a <see cref="DateTimeOffset"/> to a localized relative date string (e.g. "Today at 10:30").
/// </summary>
public sealed partial class RecentFileOpenedAtConverter : IValueConverter
{
    public static IWindowsTextCatalog? TextCatalog { get; set; }

    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not DateTimeOffset openedAt ||
            openedAt == default)
        {
            return string.Empty;
        }

        DateTime local = openedAt.ToLocalTime().DateTime;
        DateTime today = DateTime.Today;
        CultureInfo culture = CultureInfo.CurrentUICulture;
        string languageName = culture.TwoLetterISOLanguageName;

        if (local.Date == today)
        {
            string timeStr = languageName is "en"
                ? local.ToString("h:mm tt", culture)
                : local.ToString("HH:mm", culture);

            return TextCatalog is not null
                ? TextCatalog.Format("time.today_at", timeStr)
                : $"Today at {timeStr}";
        }

        if (local.Date == today.AddDays(-1))
        {
            string timeStr = languageName is "en"
                ? local.ToString("h:mm tt", culture)
                : local.ToString("HH:mm", culture);

            return TextCatalog is not null
                ? TextCatalog.Format("time.yesterday_at", timeStr)
                : $"Yesterday at {timeStr}";
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

/// <summary>
/// Converts a hex color string (e.g. "#FF0000" or "FF0000") to a <see cref="SolidColorBrush"/>.
/// </summary>
public sealed partial class HexToSolidColorBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex))
        {
            return new SolidColorBrush(global::Windows.UI.Color.FromArgb(0, 0, 0, 0));
        }

        string normalized = hex.Trim().TrimStart('#');

        byte alpha = 255;
        if (normalized.Length == 8)
        {
            alpha = System.Convert.ToByte(normalized[..2], 16);
            normalized = normalized[2..];
        }
        else if (normalized.Length != 6)
        {
            return new SolidColorBrush(global::Windows.UI.Color.FromArgb(0, 0, 0, 0));
        }

        return new SolidColorBrush(global::Windows.UI.Color.FromArgb(
            alpha,
            System.Convert.ToByte(normalized[..2], 16),
            System.Convert.ToByte(normalized.Substring(2, 2), 16),
            System.Convert.ToByte(normalized.Substring(4, 2), 16)));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// Converts a double value to a uniform <see cref="Thickness"/>.
/// </summary>
public sealed partial class DoubleToThicknessConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double d)
        {
            return new Thickness(d);
        }

        return new Thickness(0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// Converts a double value to a uniform <see cref="CornerRadius"/>.
/// </summary>
public sealed partial class DoubleToCornerRadiusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double d)
        {
            return new CornerRadius(d);
        }

        return new CornerRadius(0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// Converts a list of (double X, double Y) tuples to a <see cref="PointCollection"/> for Polyline rendering.
/// </summary>
public sealed partial class PointListToPointCollectionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var collection = new Microsoft.UI.Xaml.Media.PointCollection();
        if (value is IReadOnlyList<(double X, double Y)> points)
        {
            foreach ((double x, double y) in points)
            {
                collection.Add(new Point(x, y));
            }
        }

        return collection;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return DependencyProperty.UnsetValue;
    }
}
