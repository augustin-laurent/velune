using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;

namespace Velune.Presentation.Localization;

internal static class NativeMenuRefreshHelper
{
    internal static void Reapply(AvaloniaObject owner, NativeMenu menu)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(menu);

        if (owner is TopLevel topLevel &&
            TryRefreshThroughNativeExporter(topLevel, menu))
        {
            return;
        }

        if (!ReferenceEquals(NativeMenu.GetMenu(owner), menu))
        {
            NativeMenu.SetMenu(owner, menu);
            return;
        }

        // Avalonia refreshes exported native menus through the attached menu property and
        // NativeMenu.NeedsUpdate. Reapplying the menu nudges the platform exporter to pick
        // up live localization changes, but macOS may still defer repainting the system
        // menu bar until the next native refresh cycle.
        NativeMenu.SetMenu(owner, null!);
        NativeMenu.SetMenu(owner, menu);
    }

    private static bool TryRefreshThroughNativeExporter(TopLevel topLevel, NativeMenu menu)
    {
        try
        {
            var getInfo = typeof(NativeMenu).GetMethod(
                "GetInfo",
                BindingFlags.Static | BindingFlags.NonPublic);
            var nativeMenuInfo = getInfo?.Invoke(null, new object[] { topLevel });
            if (nativeMenuInfo is null)
            {
                return false;
            }

            var exporter = nativeMenuInfo
                .GetType()
                .GetProperty("Exporter", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
                .GetValue(nativeMenuInfo);
            if (exporter is not INativeMenuExporter nativeMenuExporter)
            {
                return false;
            }

            nativeMenuExporter.SetNativeMenu(menu);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
