using System.Globalization;
using Velune.Application.Configuration;
using Velune.Application.DTOs;
using Velune.Windows.Services;
using Velune.Windows.ViewModels;

namespace Velune.Tests.Windows.Unit.ViewModels;

public sealed class WindowsRecentFileItemTests
{
    [Fact]
    public void From_FormatsTodayWithTextCatalog()
    {
        CultureInfo originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            var openedAt = new DateTimeOffset(DateTime.Today.AddHours(9).AddMinutes(30));
            var item = new RecentFileItem("document.pdf", "C:\\Temp\\document.pdf", "PDF", openedAt);

            WindowsRecentFileItem result = WindowsRecentFileItem.From(item, new StubTextCatalog());

            Assert.Equal("document.pdf", result.FileName);
            Assert.Equal("C:\\Temp\\document.pdf", result.FilePath);
            Assert.Equal("PDF", result.DocumentType);
            Assert.Equal("Today at 9:30 AM", result.OpenedAtText);
            Assert.Equal("document.pdf, PDF, Today at 9:30 AM", result.AutomationName);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    private sealed class StubTextCatalog : IWindowsTextCatalog
    {
        public event EventHandler? LanguageChanged;

        public string GetString(string key)
        {
            return key;
        }

        public string Format(string key, params object[] args)
        {
            return key switch
            {
                "recent.opened.today" => string.Format(CultureInfo.CurrentCulture, "Today at {0}", args),
                "recent.opened.yesterday" => string.Format(CultureInfo.CurrentCulture, "Yesterday at {0}", args),
                "recent.opened.date_time" => string.Format(CultureInfo.CurrentCulture, "{0} at {1}", args),
                "recent.automation.name" => string.Format(CultureInfo.CurrentCulture, "{0}, {1}, {2}", args),
                _ => key
            };
        }

        public void Reload(AppLanguagePreference preference)
        {
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
