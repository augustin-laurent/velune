using Velune.Windows.Services;

namespace Velune.Windows.ViewModels;

public sealed class WindowsLabels
{
    public WindowsLabels(IWindowsTextCatalog text)
    {
        ArgumentNullException.ThrowIfNull(text);

        AppName = text.GetString("app.name");
        File = text.GetString("windows.menu.file");
        Edit = text.GetString("windows.menu.edit");
        View = text.GetString("windows.menu.view");
        Annotate = text.GetString("windows.menu.annotate");
        Tools = text.GetString("windows.menu.tools");
        Open = text.GetString("toolbar.open.label");
        Merge = text.GetString("toolbar.merge.label");
        Print = text.GetString("toolbar.print.label");
        Save = text.GetString("app.menu.save");
        CloseTab = text.GetString("tabs.close");
        Search = text.GetString("panel.search.title");
        SearchDescription = text.GetString("panel.search.description");
        SearchQuery = text.GetString("panel.search.query");
        SearchQueryPlaceholder = text.GetString("panel.search.query.placeholder");
        SearchRecognize = text.GetString("panel.search.recognize");
        SearchPreviousResult = text.GetString("search.result.previous");
        SearchNextResult = text.GetString("search.result.next");
        Pages = text.GetString("sidebar.pages.title");
        Annotations = text.GetString("panel.annotations.title");
        Settings = text.GetString("panel.preferences.title");
        Info = text.GetString("panel.info.title");
        Select = text.GetString("annotation.tool.select");
        Highlight = text.GetString("annotation.tool.highlight");
        Ink = text.GetString("annotation.tool.ink");
        Text = text.GetString("annotation.tool.text");
        Note = text.GetString("annotation.tool.note");
        Rectangle = text.GetString("annotation.tool.rectangle");
        Signature = text.GetString("annotation.tool.signature");
        Stamp = text.GetString("annotation.tool.stamp");
        Erase = text.GetString("annotation.tool.erase");
        EmptyTitle = text.GetString("app.empty.title");
        EmptyDescription = text.GetString("app.empty.description");
        WindowSubtitle = text.GetString("app.window.subtitle");
        EmptyDropTitle = text.GetString("app.empty.drop_title");
        EmptyOr = text.GetString("app.empty.or");
        EmptyOpenLink = text.GetString("app.empty.open_link");
        Browse = text.GetString("app.open.button");
        MergeShort = text.GetString("app.merge.button");
        WelcomeDropTitle = text.GetString("windows.welcome.drop_title");
        WelcomeSupportedFormats = text.GetString("windows.welcome.supported_formats");
        WelcomeOpenFiles = text.GetString("windows.welcome.open_files");
        WelcomeMergePdf = text.GetString("windows.welcome.merge_pdf");
        WelcomeBrowseDevice = text.GetString("windows.welcome.browse_device");
        RecentTitle = text.GetString("app.recent.title");
        RecentClear = text.GetString("app.recent.clear");
        Loading = text.GetString("windows.thumbnail.loading");
        SearchPlaceholder = text.GetString("toolbar.search.placeholder");
        ZoomActual = text.GetString("toolbar.zoom.actual_size");
        ZoomFit = text.GetString("toolbar.zoom.fit_page");
        ZoomOut = text.GetString("toolbar.zoom.out");
        ZoomIn = text.GetString("toolbar.zoom.in");
        PreviousPage = text.GetString("toolbar.page.previous");
        NextPage = text.GetString("toolbar.page.next");
        RibbonHome = text.GetString("windows.ribbon.home");
        RibbonHelp = text.GetString("windows.ribbon.help");
        RibbonFileGroup = text.GetString("windows.ribbon.file_group");
        RibbonNavigation = text.GetString("windows.ribbon.navigation");
        RibbonZoom = text.GetString("windows.ribbon.zoom");
        RibbonDisplay = text.GetString("windows.ribbon.display");
        RibbonAnnotateGroup = text.GetString("windows.ribbon.annotate_group");
        Share = text.GetString("windows.toolbar.share");
        AnnotationDescriptionShort = text.GetString("windows.annotation.description_short");
        AnnotationToolsTab = text.GetString("panel.annotations.tab.tools");
        AnnotationCommentsTab = text.GetString("panel.annotations.tab.comments");
        AnnotationStyleTab = text.GetString("panel.annotations.tab.style");
        AnnotationActiveTool = text.GetString("windows.annotation.active_tool");
        AnnotationQuickTools = text.GetString("windows.annotation.quick_tools");
        AnnotationColor = text.GetString("panel.annotations.color");
        AnnotationOpacity = text.GetString("windows.annotation.opacity");
        AnnotationCurrent = text.GetString("windows.annotation.current");
        AnnotationRecent = text.GetString("windows.annotation.recent");
        AnnotationCurrentPage = text.GetString("panel.annotations.current_page");
        AnnotationEmptyPage = text.GetString("panel.annotations.empty_page");
        AnnotationEmptyHint = text.GetString("windows.annotation.empty_hint");
        AnnotationDeleteSelected = text.GetString("panel.annotations.delete_selected");
        PreferencesDescription = text.GetString("panel.preferences.description");
        PreferencesLanguage = text.GetString("panel.preferences.language");
        PreferencesTheme = text.GetString("panel.preferences.theme");
        PreferencesDefaultZoom = text.GetString("panel.preferences.default_zoom");
        PreferencesShowThumbnails = text.GetString("panel.preferences.show_thumbnails");
        PreferencesCacheSize = text.GetString("panel.preferences.cache_size");
        PreferencesSystem = text.GetString("preferences.theme.system");
        PreferencesLight = text.GetString("preferences.theme.light");
        PreferencesDark = text.GetString("preferences.theme.dark");
        PreferencesEnglish = text.GetString("preferences.language.english");
        PreferencesFrench = text.GetString("preferences.language.french");
        PreferencesSpanish = text.GetString("preferences.language.spanish");
        PreferencesFitPage = text.GetString("preferences.zoom.fit_page");
        PreferencesFitWidth = text.GetString("preferences.zoom.fit_width");
        PreferencesActualSize = text.GetString("preferences.zoom.actual_size");
        InfoFileSize = text.GetString("info.file_size");
        InfoDimensions = text.GetString("info.dimensions");
        InfoPages = text.GetString("info.pages");
        InfoFormat = text.GetString("info.format");
        InfoAuthor = text.GetString("info.author");
        InfoCreated = text.GetString("info.created");
        InfoModified = text.GetString("info.modified");
    }

    public string AppName
    {
        get;
    }
    public string File
    {
        get;
    }
    public string Edit
    {
        get;
    }
    public string View
    {
        get;
    }
    public string Annotate
    {
        get;
    }
    public string Tools
    {
        get;
    }
    public string Open
    {
        get;
    }
    public string Merge
    {
        get;
    }
    public string Print
    {
        get;
    }
    public string Save
    {
        get;
    }
    public string CloseTab
    {
        get;
    }
    public string Search
    {
        get;
    }
    public string SearchDescription
    {
        get;
    }
    public string SearchQuery
    {
        get;
    }
    public string SearchQueryPlaceholder
    {
        get;
    }
    public string SearchRecognize
    {
        get;
    }
    public string SearchPreviousResult
    {
        get;
    }
    public string SearchNextResult
    {
        get;
    }
    public string Pages
    {
        get;
    }
    public string Annotations
    {
        get;
    }
    public string Settings
    {
        get;
    }
    public string Info
    {
        get;
    }
    public string Select
    {
        get;
    }
    public string Highlight
    {
        get;
    }
    public string Ink
    {
        get;
    }
    public string Text
    {
        get;
    }
    public string Note
    {
        get;
    }
    public string Rectangle
    {
        get;
    }
    public string Signature
    {
        get;
    }
    public string Stamp
    {
        get;
    }
    public string Erase
    {
        get;
    }
    public string EmptyTitle
    {
        get;
    }
    public string EmptyDescription
    {
        get;
    }
    public string WindowSubtitle
    {
        get;
    }
    public string EmptyDropTitle
    {
        get;
    }
    public string EmptyOr
    {
        get;
    }
    public string EmptyOpenLink
    {
        get;
    }
    public string Browse
    {
        get;
    }
    public string MergeShort
    {
        get;
    }
    public string WelcomeDropTitle
    {
        get;
    }
    public string WelcomeSupportedFormats
    {
        get;
    }
    public string WelcomeOpenFiles
    {
        get;
    }
    public string WelcomeMergePdf
    {
        get;
    }
    public string WelcomeBrowseDevice
    {
        get;
    }
    public string RecentTitle
    {
        get;
    }
    public string RecentClear
    {
        get;
    }
    public string Loading
    {
        get;
    }
    public string SearchPlaceholder
    {
        get;
    }
    public string ZoomActual
    {
        get;
    }
    public string ZoomFit
    {
        get;
    }
    public string ZoomOut
    {
        get;
    }
    public string ZoomIn
    {
        get;
    }
    public string PreviousPage
    {
        get;
    }
    public string NextPage
    {
        get;
    }
    public string RibbonHome
    {
        get;
    }
    public string RibbonHelp
    {
        get;
    }
    public string RibbonFileGroup
    {
        get;
    }
    public string RibbonNavigation
    {
        get;
    }
    public string RibbonZoom
    {
        get;
    }
    public string RibbonDisplay
    {
        get;
    }
    public string RibbonAnnotateGroup
    {
        get;
    }
    public string Share
    {
        get;
    }
    public string AnnotationDescriptionShort
    {
        get;
    }
    public string AnnotationToolsTab
    {
        get;
    }
    public string AnnotationCommentsTab
    {
        get;
    }
    public string AnnotationStyleTab
    {
        get;
    }
    public string AnnotationActiveTool
    {
        get;
    }
    public string AnnotationQuickTools
    {
        get;
    }
    public string AnnotationColor
    {
        get;
    }
    public string AnnotationOpacity
    {
        get;
    }
    public string AnnotationCurrent
    {
        get;
    }
    public string AnnotationRecent
    {
        get;
    }
    public string AnnotationCurrentPage
    {
        get;
    }
    public string AnnotationEmptyPage
    {
        get;
    }
    public string AnnotationEmptyHint
    {
        get;
    }
    public string AnnotationDeleteSelected
    {
        get;
    }
    public string PreferencesDescription
    {
        get;
    }
    public string PreferencesLanguage
    {
        get;
    }
    public string PreferencesTheme
    {
        get;
    }
    public string PreferencesDefaultZoom
    {
        get;
    }
    public string PreferencesShowThumbnails
    {
        get;
    }
    public string PreferencesCacheSize
    {
        get;
    }
    public string PreferencesSystem
    {
        get;
    }
    public string PreferencesLight
    {
        get;
    }
    public string PreferencesDark
    {
        get;
    }
    public string PreferencesEnglish
    {
        get;
    }
    public string PreferencesFrench
    {
        get;
    }
    public string PreferencesSpanish
    {
        get;
    }
    public string PreferencesFitPage
    {
        get;
    }
    public string PreferencesFitWidth
    {
        get;
    }
    public string PreferencesActualSize
    {
        get;
    }
    public string InfoFileSize
    {
        get;
    }
    public string InfoDimensions
    {
        get;
    }
    public string InfoPages
    {
        get;
    }
    public string InfoFormat
    {
        get;
    }
    public string InfoAuthor
    {
        get;
    }
    public string InfoCreated
    {
        get;
    }
    public string InfoModified
    {
        get;
    }
}
