using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Velune.Domain.Annotations;
using Velune.Windows.ViewModels;
using Windows.Foundation;

namespace Velune.Windows.Services;

/// <summary>
/// Helper methods used by x:Bind expressions where a converter would otherwise be needed.
/// </summary>
public static class XamlBindingHelpers
{
    private static readonly SolidColorBrush TransparentBrush = new(global::Windows.UI.Color.FromArgb(0, 0, 0, 0));
    private static readonly SolidColorBrush DefaultAccentBrush = new(global::Windows.UI.Color.FromArgb(255, 0, 120, 212));
    private static readonly SolidColorBrush DefaultCardBrush = new(global::Windows.UI.Color.FromArgb(31, 255, 255, 255));
    private static readonly SolidColorBrush PdfBrush = new(global::Windows.UI.Color.FromArgb(255, 232, 35, 46));
    private static readonly SolidColorBrush WebpBrush = new(global::Windows.UI.Color.FromArgb(255, 45, 145, 111));

    public static Visibility BoolToVisibility(object? value)
    {
        return value is true ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility InverseBoolToVisibility(object? value)
    {
        return value is true ? Visibility.Collapsed : Visibility.Visible;
    }

    public static Brush AnnotationToolBackground(object? value, string toolName)
    {
        if (!Enum.TryParse(toolName, ignoreCase: true, out AnnotationTool targetTool))
        {
            return TransparentBrush;
        }

        return value is AnnotationTool selectedTool && selectedTool == targetTool
            ? AccentBrush()
            : TransparentBrush;
    }

    public static Brush BoolToSelectedToolBackground(object? value)
    {
        return value is true ? AccentBrush() : CardBrush();
    }

    public static Visibility RecentFilePdfVisibility(string? fileName)
    {
        return IsPdf(fileName) ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility RecentFileImageVisibility(string? fileName)
    {
        return IsPdf(fileName) ? Visibility.Collapsed : Visibility.Visible;
    }

    public static Brush RecentFileBrush(string? fileName)
    {
        string extension = Path.GetExtension(fileName)?.ToLowerInvariant() ?? string.Empty;
        return extension switch
        {
            ".pdf" => PdfBrush,
            ".webp" => WebpBrush,
            _ => AccentBrush()
        };
    }

    public static double Subtract(double value, double subtract)
    {
        return value - subtract;
    }

    public static Thickness CreateThickness(double left, double top, double right, double bottom)
    {
        return new Thickness(left, top, right, bottom);
    }

    public static Thickness CreateUniformThickness(double value)
    {
        return new Thickness(value);
    }

    public static CornerRadius CreateCornerRadius(double value)
    {
        return new CornerRadius(value);
    }

    public static Brush BrushFromHex(string? hex, int alpha)
    {
        string normalized = (hex ?? string.Empty).Trim().TrimStart('#');
        if (normalized.Length != 6)
        {
            normalized = "EEF1FF";
        }

        byte clampedAlpha = (byte)Math.Clamp(alpha, 0, 255);
        return new SolidColorBrush(global::Windows.UI.Color.FromArgb(
            clampedAlpha,
            Convert.ToByte(normalized[..2], 16),
            Convert.ToByte(normalized.Substring(2, 2), 16),
            Convert.ToByte(normalized.Substring(4, 2), 16)));
    }

    public static PointCollection PointCollectionFromOverlayPoints(IReadOnlyList<AnnotationOverlayPoint>? points)
    {
        var pointCollection = new PointCollection();
        if (points is null)
        {
            return pointCollection;
        }

        foreach (AnnotationOverlayPoint point in points)
        {
            pointCollection.Add(new Point(point.X, point.Y));
        }

        return pointCollection;
    }

    public static ImageSource? ImageSourceFromPath(string? filePath)
    {
        return string.IsNullOrWhiteSpace(filePath)
            ? null
            : new BitmapImage(new Uri(filePath, UriKind.Absolute));
    }

    public static string RecentFileOpenedAt(DateTimeOffset openedAt)
    {
        if (openedAt == default)
        {
            return string.Empty;
        }

        DateTime local = openedAt.ToLocalTime().DateTime;
        DateTime today = DateTime.Today;
        CultureInfo culture = CultureInfo.CurrentUICulture;
        string languageName = culture.TwoLetterISOLanguageName;

        if (local.Date == today)
        {
            return languageName switch
            {
                "fr" => $"Aujourd'hui \u00e0 {local:HH:mm}",
                "es" => $"Hoy a las {local:HH:mm}",
                _ => $"Today at {local.ToString("h:mm tt", culture)}"
            };
        }

        if (local.Date == today.AddDays(-1))
        {
            return languageName switch
            {
                "fr" => $"Hier \u00e0 {local:HH:mm}",
                "es" => $"Ayer a las {local:HH:mm}",
                _ => $"Yesterday at {local.ToString("h:mm tt", culture)}"
            };
        }

        return languageName switch
        {
            "fr" => local.ToString("d MMM yyyy '\u00e0' HH:mm", culture),
            "es" => local.ToString("d MMM yyyy 'a las' HH:mm", culture),
            _ => local.ToString("MMM d, yyyy 'at' h:mm tt", culture)
        };
    }

    private static bool IsPdf(string? fileName)
    {
        return string.Equals(Path.GetExtension(fileName), ".pdf", StringComparison.OrdinalIgnoreCase);
    }

    private static Brush AccentBrush()
    {
        return Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("AccentBgBrush", out object? brush) && brush is Brush accentBrush
            ? accentBrush
            : DefaultAccentBrush;
    }

    private static Brush CardBrush()
    {
        return Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("CardBrush", out object? brush) && brush is Brush cardBrush
            ? cardBrush
            : DefaultCardBrush;
    }
}

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
