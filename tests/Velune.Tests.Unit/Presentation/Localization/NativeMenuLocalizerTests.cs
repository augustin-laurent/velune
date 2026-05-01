using Avalonia.Controls;
using Velune.Application.Configuration;
using Velune.Presentation.Localization;

namespace Velune.Tests.Unit.Presentation.Localization;

public sealed class NativeMenuLocalizerTests
{
    [Fact]
    public void LocalizeAppMenu_ShouldUpdateTopLevelEntries()
    {
        var menu = new NativeMenu();
        menu.Items.Add(new NativeMenuItem { Header = "About" });
        menu.Items.Add(new NativeMenuItem { Header = "Preferences" });

        NativeMenuLocalizer.LocalizeAppMenu(menu, new StubLocalizationService());

        Assert.Equal("About Velune", ((NativeMenuItem)menu.Items[0]).Header);
        Assert.Equal("Preferences", ((NativeMenuItem)menu.Items[1]).Header);
    }

    [Fact]
    public void LocalizeMainWindowMenu_ShouldUpdateTopLevelAndNestedEntries()
    {
        var menu = BuildMainWindowMenu();

        NativeMenuLocalizer.LocalizeMainWindowMenu(menu, new StubLocalizationService());

        Assert.Equal("File", ((NativeMenuItem)menu.Items[0]).Header);
        Assert.Equal("Page", ((NativeMenuItem)menu.Items[1]).Header);
        Assert.Equal("View", ((NativeMenuItem)menu.Items[2]).Header);
        Assert.Equal("Annotate", ((NativeMenuItem)menu.Items[3]).Header);

        var fileMenu = ((NativeMenuItem)menu.Items[0]).Menu!;
        Assert.Equal(6, fileMenu.Items.Count);
        Assert.Equal("Open", ((NativeMenuItem)fileMenu.Items[0]).Header);
        Assert.Equal("Save", ((NativeMenuItem)fileMenu.Items[1]).Header);
        Assert.Equal("Print", ((NativeMenuItem)fileMenu.Items[3]).Header);
        Assert.Equal("Close", ((NativeMenuItem)fileMenu.Items[5]).Header);

        var annotateMenu = ((NativeMenuItem)menu.Items[3]).Menu!;
        Assert.Equal("Signature", ((NativeMenuItem)annotateMenu.Items[9]).Header);
        Assert.Equal("Undo", ((NativeMenuItem)annotateMenu.Items[12]).Header);
    }

    private static NativeMenu BuildMainWindowMenu()
    {
        var fileMenu = new NativeMenu();
        fileMenu.Items.Add(new NativeMenuItem());
        fileMenu.Items.Add(new NativeMenuItem());
        fileMenu.Items.Add(new NativeMenuItemSeparator());
        fileMenu.Items.Add(new NativeMenuItem());
        fileMenu.Items.Add(new NativeMenuItemSeparator());
        fileMenu.Items.Add(new NativeMenuItem());

        var pageMenu = new NativeMenu();
        pageMenu.Items.Add(new NativeMenuItem());
        pageMenu.Items.Add(new NativeMenuItem());
        pageMenu.Items.Add(new NativeMenuItemSeparator());
        pageMenu.Items.Add(new NativeMenuItem());
        pageMenu.Items.Add(new NativeMenuItem());
        pageMenu.Items.Add(new NativeMenuItemSeparator());
        pageMenu.Items.Add(new NativeMenuItem());
        pageMenu.Items.Add(new NativeMenuItem());
        pageMenu.Items.Add(new NativeMenuItemSeparator());
        pageMenu.Items.Add(new NativeMenuItem());
        pageMenu.Items.Add(new NativeMenuItem());

        var viewMenu = new NativeMenu();
        viewMenu.Items.Add(new NativeMenuItem());
        viewMenu.Items.Add(new NativeMenuItemSeparator());
        viewMenu.Items.Add(new NativeMenuItem());
        viewMenu.Items.Add(new NativeMenuItem());
        viewMenu.Items.Add(new NativeMenuItem());
        viewMenu.Items.Add(new NativeMenuItemSeparator());
        viewMenu.Items.Add(new NativeMenuItem());
        viewMenu.Items.Add(new NativeMenuItem());
        viewMenu.Items.Add(new NativeMenuItemSeparator());
        viewMenu.Items.Add(new NativeMenuItem());
        viewMenu.Items.Add(new NativeMenuItem());

        var annotateMenu = new NativeMenu();
        annotateMenu.Items.Add(new NativeMenuItem());
        annotateMenu.Items.Add(new NativeMenuItemSeparator());
        annotateMenu.Items.Add(new NativeMenuItem());
        annotateMenu.Items.Add(new NativeMenuItem());
        annotateMenu.Items.Add(new NativeMenuItem());
        annotateMenu.Items.Add(new NativeMenuItem());
        annotateMenu.Items.Add(new NativeMenuItem());
        annotateMenu.Items.Add(new NativeMenuItem());
        annotateMenu.Items.Add(new NativeMenuItem());
        annotateMenu.Items.Add(new NativeMenuItem());
        annotateMenu.Items.Add(new NativeMenuItemSeparator());
        annotateMenu.Items.Add(new NativeMenuItem());
        annotateMenu.Items.Add(new NativeMenuItem());
        annotateMenu.Items.Add(new NativeMenuItem());

        var rootMenu = new NativeMenu();
        rootMenu.Items.Add(new NativeMenuItem { Menu = fileMenu });
        rootMenu.Items.Add(new NativeMenuItem { Menu = pageMenu });
        rootMenu.Items.Add(new NativeMenuItem { Menu = viewMenu });
        rootMenu.Items.Add(new NativeMenuItem { Menu = annotateMenu });
        return rootMenu;
    }

    private sealed class StubLocalizationService : ILocalizationService
    {
        private static readonly Dictionary<string, string> Values = new(StringComparer.Ordinal)
        {
            ["app.menu.about"] = "About Velune",
            ["app.menu.preferences"] = "Preferences",
            ["app.menu.file"] = "File",
            ["app.menu.open"] = "Open",
            ["app.menu.save"] = "Save",
            ["app.menu.print"] = "Print",
            ["app.menu.close"] = "Close",
            ["app.menu.page"] = "Page",
            ["app.menu.page.previous"] = "Previous page",
            ["app.menu.page.next"] = "Next page",
            ["app.menu.page.rotate_left"] = "Rotate left",
            ["app.menu.page.rotate_right"] = "Rotate right",
            ["app.menu.page.move_earlier"] = "Move earlier",
            ["app.menu.page.move_later"] = "Move later",
            ["app.menu.page.extract"] = "Extract page",
            ["app.menu.page.delete"] = "Delete page",
            ["app.menu.view"] = "View",
            ["app.menu.view.sidebar"] = "Show pages",
            ["app.menu.view.search"] = "Search",
            ["app.menu.view.info"] = "Information",
            ["app.menu.view.preferences"] = "Preferences",
            ["app.menu.view.zoom_in"] = "Zoom in",
            ["app.menu.view.zoom_out"] = "Zoom out",
            ["app.menu.view.fit_width"] = "Fit width",
            ["app.menu.view.fit_page"] = "Fit page",
            ["app.menu.annotate"] = "Annotate",
            ["app.menu.annotate.show"] = "Show annotations",
            ["app.menu.annotate.select"] = "Select tool",
            ["app.menu.annotate.highlight"] = "Highlight",
            ["app.menu.annotate.freehand"] = "Freehand",
            ["app.menu.annotate.rectangle"] = "Rectangle",
            ["app.menu.annotate.text"] = "Text",
            ["app.menu.annotate.note"] = "Note",
            ["app.menu.annotate.stamp"] = "Stamp",
            ["app.menu.annotate.signature"] = "Signature",
            ["app.menu.annotate.delete"] = "Delete selected",
            ["app.menu.annotate.undo"] = "Undo",
            ["app.menu.annotate.redo"] = "Redo"
        };

        public string CurrentLanguageCode => "en";

        public AppLanguagePreference CurrentLanguagePreference => AppLanguagePreference.English;

        public int Version => 1;

        public event EventHandler? LanguageChanged
        {
            add { }
            remove { }
        }

        public string GetString(string key)
        {
            return Values[key];
        }

        public string GetString(string key, params object?[] arguments)
        {
            return string.Format(GetString(key), arguments);
        }

        public bool HasKey(string key)
        {
            return Values.ContainsKey(key);
        }
    }
}
