using Avalonia.Controls;

namespace Velune.Presentation.Localization;

public static class NativeMenuLocalizer
{
    public static void LocalizeAppMenu(NativeMenu menu, ILocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(localizationService);

        SetHeader(menu, localizationService, "app.menu.about", 0);
        SetHeader(menu, localizationService, "app.menu.preferences", 1);
    }

    public static void LocalizeMainWindowMenu(NativeMenu menu, ILocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(localizationService);

        SetHeader(menu, localizationService, "app.menu.file", 0);
        SetHeader(menu, localizationService, "app.menu.open", 0, 0);
        SetHeader(menu, localizationService, "app.menu.save", 0, 1);
        SetHeader(menu, localizationService, "app.menu.print", 0, 3);
        SetHeader(menu, localizationService, "app.menu.close", 0, 5);

        SetHeader(menu, localizationService, "app.menu.page", 1);
        SetHeader(menu, localizationService, "app.menu.page.previous", 1, 0);
        SetHeader(menu, localizationService, "app.menu.page.next", 1, 1);
        SetHeader(menu, localizationService, "app.menu.page.rotate_left", 1, 3);
        SetHeader(menu, localizationService, "app.menu.page.rotate_right", 1, 4);
        SetHeader(menu, localizationService, "app.menu.page.move_earlier", 1, 6);
        SetHeader(menu, localizationService, "app.menu.page.move_later", 1, 7);
        SetHeader(menu, localizationService, "app.menu.page.extract", 1, 9);
        SetHeader(menu, localizationService, "app.menu.page.delete", 1, 10);

        SetHeader(menu, localizationService, "app.menu.view", 2);
        SetHeader(menu, localizationService, "app.menu.view.sidebar", 2, 0);
        SetHeader(menu, localizationService, "app.menu.view.search", 2, 2);
        SetHeader(menu, localizationService, "app.menu.view.info", 2, 3);
        SetHeader(menu, localizationService, "app.menu.view.preferences", 2, 4);
        SetHeader(menu, localizationService, "app.menu.view.zoom_in", 2, 6);
        SetHeader(menu, localizationService, "app.menu.view.zoom_out", 2, 7);
        SetHeader(menu, localizationService, "app.menu.view.fit_width", 2, 9);
        SetHeader(menu, localizationService, "app.menu.view.fit_page", 2, 10);

        SetHeader(menu, localizationService, "app.menu.annotate", 3);
        SetHeader(menu, localizationService, "app.menu.annotate.show", 3, 0);
        SetHeader(menu, localizationService, "app.menu.annotate.select", 3, 2);
        SetHeader(menu, localizationService, "app.menu.annotate.highlight", 3, 3);
        SetHeader(menu, localizationService, "app.menu.annotate.freehand", 3, 4);
        SetHeader(menu, localizationService, "app.menu.annotate.rectangle", 3, 5);
        SetHeader(menu, localizationService, "app.menu.annotate.text", 3, 6);
        SetHeader(menu, localizationService, "app.menu.annotate.note", 3, 7);
        SetHeader(menu, localizationService, "app.menu.annotate.stamp", 3, 8);
        SetHeader(menu, localizationService, "app.menu.annotate.signature", 3, 9);
        SetHeader(menu, localizationService, "app.menu.annotate.delete", 3, 11);
        SetHeader(menu, localizationService, "app.menu.annotate.undo", 3, 12);
        SetHeader(menu, localizationService, "app.menu.annotate.redo", 3, 13);
    }

    private static void SetHeader(
        NativeMenu menu,
        ILocalizationService localizationService,
        string key,
        params int[] path)
    {
        if (TryResolveItem(menu, path, out var item))
        {
            item.Header = localizationService.GetString(key);
        }
    }

    private static bool TryResolveItem(NativeMenu rootMenu, int[] path, out NativeMenuItem item)
    {
        ArgumentNullException.ThrowIfNull(rootMenu);
        ArgumentNullException.ThrowIfNull(path);

        item = null!;
        var currentMenu = rootMenu;

        for (var i = 0; i < path.Length; i++)
        {
            var index = path[i];
            if (index < 0 || index >= currentMenu.Items.Count || currentMenu.Items[index] is not NativeMenuItem currentItem)
            {
                return false;
            }

            if (i == path.Length - 1)
            {
                item = currentItem;
                return true;
            }

            if (currentItem.Menu is not NativeMenu childMenu)
            {
                return false;
            }

            currentMenu = childMenu;
        }

        return false;
    }
}
