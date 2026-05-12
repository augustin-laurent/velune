namespace Velune.Application.Configuration;

/// <summary>Application-level configuration options bound from the "App" settings section.</summary>
public sealed class AppOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "App";

    /// <summary>Gets or sets the application display name.</summary>
    public string Name
    {
        get; set;
    } = "Velune";

    /// <summary>Gets or sets the runtime environment name.</summary>
    public string Environment
    {
        get; set;
    } = "Development";

    /// <summary>Gets or sets the maximum number of recent files to track.</summary>
    public int RecentFilesLimit
    {
        get; set;
    } = 10;

    /// <summary>Gets or sets the file path for persisting recent files.</summary>
    public string? RecentFilesPath
    {
        get; set;
    }

    /// <summary>Gets or sets the maximum number of entries in the render memory cache.</summary>
    public int RenderCacheEntryLimit
    {
        get; set;
    } = 64;

    /// <summary>Gets or sets the file path for persisting user preferences.</summary>
    public string? UserPreferencesPath
    {
        get; set;
    }

    /// <summary>Gets or sets the directory path for the thumbnail disk cache.</summary>
    public string? ThumbnailDiskCachePath
    {
        get; set;
    }

    /// <summary>Gets or sets the maximum age in days for cached thumbnails before eviction.</summary>
    public int ThumbnailDiskCacheMaxAgeDays
    {
        get; set;
    } = 30;

    /// <summary>Gets or sets the directory path for the OCR text cache.</summary>
    public string? OcrCachePath
    {
        get; set;
    }

    /// <summary>Gets or sets the directory path for stored signature assets.</summary>
    public string? SignatureLibraryPath
    {
        get; set;
    }

    /// <summary>Gets or sets the directory path for localization resource files.</summary>
    public string? LocalizationPath
    {
        get; set;
    }

    /// <summary>Gets or sets the path to the Tesseract OCR executable.</summary>
    public string TesseractExecutablePath
    {
        get; set;
    } = "tesseract";

    /// <summary>Gets or sets the directory path for Tesseract language data files.</summary>
    public string? TesseractDataPath
    {
        get; set;
    }

    /// <summary>Gets or sets the default OCR language codes.</summary>
    public string[] DefaultOcrLanguages
    {
        get; set;
    } = [];

    /// <summary>Gets or sets the path to the qpdf executable.</summary>
    public string QpdfExecutablePath
    {
        get; set;
    } = "qpdf";
}
