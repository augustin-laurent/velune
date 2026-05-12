using Velune.Windows.Services;

namespace Velune.Windows.ViewModels;

/// <summary>
/// Provides all localized UI label strings used by the Windows presentation layer.
/// </summary>
public sealed class WindowsLabels
{
    /// <summary>
    /// Initializes all labels from the text catalog.
    /// </summary>
    /// <param name="text">The text catalog providing localized strings.</param>
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
        SaveAs = text.GetString("app.menu.save_as");
        Close = text.GetString("app.close");
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
        PageOrganizer = text.GetString("windows.menu.page_organizer");
        MenuSelection = text.GetString("windows.menu.selection");
        PageRotateLeft = text.GetString("windows.page.rotate_left");
        PageRotateRight = text.GetString("windows.page.rotate_right");
        PageMoveUp = text.GetString("windows.page.move_up");
        PageMoveDown = text.GetString("windows.page.move_down");
        PageDelete = text.GetString("windows.page.delete");
        Insert = text.GetString("windows.page.insert");
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
        AnnotationFill = text.GetString("panel.annotations.fill");
        AnnotationFillColor = text.GetString("panel.annotations.fill_color");
        AnnotationText = text.GetString("panel.annotations.text");
        AnnotationTextPlaceholder = text.GetString("panel.annotations.text.placeholder");
        AnnotationOpacity = text.GetString("windows.annotation.opacity");
        AnnotationCurrent = text.GetString("windows.annotation.current");
        AnnotationRecent = text.GetString("windows.annotation.recent");
        AnnotationCurrentPage = text.GetString("panel.annotations.current_page");
        AnnotationEmptyPage = text.GetString("panel.annotations.empty_page");
        AnnotationEmptyHint = text.GetString("windows.annotation.empty_hint");
        AnnotationDeleteSelected = text.GetString("panel.annotations.delete_selected");
        AnnotationMenuDelete = text.GetString("panel.annotations.menu.delete");
        AnnotationMenuLock = text.GetString("panel.annotations.menu.lock");
        AnnotationMenuUnlock = text.GetString("panel.annotations.menu.unlock");
        AnnotationMenuHide = text.GetString("panel.annotations.menu.hide");
        AnnotationMenuShow = text.GetString("panel.annotations.menu.show");
        AnnotationMenuEdit = text.GetString("panel.annotations.menu.edit");
        AnnotationMenuRotate90 = text.GetString("panel.annotations.menu.rotate90");
        AnnotationMenuResetRotation = text.GetString("panel.annotations.menu.reset_rotation");
        AnnotationMenuFlipH = text.GetString("panel.annotations.menu.flip_h");
        AnnotationMenuFlipV = text.GetString("panel.annotations.menu.flip_v");
        AnnotationCommentsCurrent = text.GetString("windows.annotation.comments_current");
        AnnotationCommentsEmptyPage = text.GetString("windows.annotation.comments_empty_page");
        AnnotationCommentsEmptyHint = text.GetString("windows.annotation.comments_empty_hint");
        SignatureLibrary = text.GetString("panel.annotations.signature_library");
        SignatureImportImage = text.GetString("panel.annotations.import_image");
        SignatureDeleteSelected = text.GetString("panel.annotations.delete_signature");
        SignatureDraw = text.GetString("panel.annotations.draw_signature");
        SignatureNamePlaceholder = text.GetString("panel.annotations.signature_name.placeholder");
        SignatureClearDrawing = text.GetString("panel.annotations.clear_drawing");
        SignatureSave = text.GetString("panel.annotations.save_signature");
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
        InfoDescription = text.GetString("panel.info.description");
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
    public string SaveAs
    {
        get;
    }
    public string Close
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
    public string PageOrganizer
    {
        get;
    }

    public string MenuSelection
    {
        get;
    }
    public string PageRotateLeft
    {
        get;
    }
    public string PageRotateRight
    {
        get;
    }
    public string PageMoveUp
    {
        get;
    }
    public string PageMoveDown
    {
        get;
    }
    public string PageDelete
    {
        get;
    }
    public string Insert
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
    public string AnnotationFill
    {
        get;
    }
    public string AnnotationFillColor
    {
        get;
    }
    public string AnnotationText
    {
        get;
    }
    public string AnnotationTextPlaceholder
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
    public string AnnotationMenuDelete
    {
        get;
    }
    public string AnnotationMenuLock
    {
        get;
    }
    public string AnnotationMenuUnlock
    {
        get;
    }
    public string AnnotationMenuHide
    {
        get;
    }
    public string AnnotationMenuShow
    {
        get;
    }
    public string AnnotationMenuEdit
    {
        get;
    }
    public string AnnotationMenuRotate90
    {
        get;
    }
    public string AnnotationMenuResetRotation
    {
        get;
    }
    public string AnnotationMenuFlipH
    {
        get;
    }
    public string AnnotationMenuFlipV
    {
        get;
    }
    public string AnnotationCommentsCurrent
    {
        get;
    }
    public string AnnotationCommentsEmptyPage
    {
        get;
    }
    public string AnnotationCommentsEmptyHint
    {
        get;
    }
    public string SignatureLibrary
    {
        get;
    }
    public string SignatureImportImage
    {
        get;
    }
    public string SignatureDeleteSelected
    {
        get;
    }
    public string SignatureDraw
    {
        get;
    }
    public string SignatureNamePlaceholder
    {
        get;
    }
    public string SignatureClearDrawing
    {
        get;
    }
    public string SignatureSave
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
    public string InfoDescription
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
