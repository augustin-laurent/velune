using System.Globalization;
using Velune.Application.DTOs;
using Velune.Windows.Services;

namespace Velune.Windows.ViewModels;

/// <summary>
/// Windows-specific display model for a recently opened file.
/// </summary>
public sealed record WindowsRecentFileItem
{
    public WindowsRecentFileItem(
        string fileName,
        string filePath,
        string documentType,
        DateTimeOffset openedAt,
        string openedAtText,
        string automationName)
    {
        FileName = fileName;
        FilePath = filePath;
        DocumentType = documentType;
        OpenedAt = openedAt;
        OpenedAtText = openedAtText;
        AutomationName = automationName;
    }

    public string FileName
    {
        get;
    }

    public string FilePath
    {
        get;
    }

    public string DocumentType
    {
        get;
    }

    public DateTimeOffset OpenedAt
    {
        get;
    }

    public string OpenedAtText
    {
        get;
    }

    public string AutomationName
    {
        get;
    }

    public static WindowsRecentFileItem From(RecentFileItem item, IWindowsTextCatalog textCatalog)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(textCatalog);

        string openedAtText = FormatOpenedAt(item.OpenedAt, textCatalog);

        return new WindowsRecentFileItem(
            item.FileName,
            item.FilePath,
            item.DocumentType,
            item.OpenedAt,
            openedAtText,
            textCatalog.Format("recent.automation.name", item.FileName, item.DocumentType, openedAtText));
    }

    private static string FormatOpenedAt(DateTimeOffset openedAt, IWindowsTextCatalog textCatalog)
    {
        if (openedAt == default)
        {
            return string.Empty;
        }

        DateTime local = openedAt.ToLocalTime().DateTime;
        DateTime today = DateTime.Today;
        CultureInfo culture = CultureInfo.CurrentUICulture;
        bool usesTwentyFourHourClock = culture.TwoLetterISOLanguageName is "fr" or "es";
        string timeText = local.ToString(usesTwentyFourHourClock ? "HH:mm" : "h:mm tt", culture);

        if (local.Date == today)
        {
            return textCatalog.Format("recent.opened.today", timeText);
        }

        if (local.Date == today.AddDays(-1))
        {
            return textCatalog.Format("recent.opened.yesterday", timeText);
        }

        string dateText = local.ToString(usesTwentyFourHourClock ? "d MMM yyyy" : "MMM d, yyyy", culture);
        return textCatalog.Format("recent.opened.date_time", dateText, timeText);
    }
}
