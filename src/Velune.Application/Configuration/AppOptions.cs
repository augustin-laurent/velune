namespace Velune.Application.Configuration;

public sealed class AppOptions
{
    public const string SectionName = "App";

    public string Name
    {
        get; set;
    } = "Velune";

    public string Environment
    {
        get; set;
    } = "Development";

    public int RecentFilesLimit
    {
        get; set;
    } = 10;

    public int RenderCacheEntryLimit
    {
        get; set;
    } = 64;

    public string? UserPreferencesPath
    {
        get; set;
    }

    public string? ThumbnailDiskCachePath
    {
        get; set;
    }

    public int ThumbnailDiskCacheMaxAgeDays
    {
        get; set;
    } = 30;

    public string? OcrCachePath
    {
        get; set;
    }

    public string? SignatureLibraryPath
    {
        get; set;
    }

    public string TesseractExecutablePath
    {
        get; set;
    } = "tesseract";

    public string? TesseractDataPath
    {
        get; set;
    }

    public string[] DefaultOcrLanguages
    {
        get; set;
    } = [];

    public string QpdfExecutablePath
    {
        get; set;
    } = "qpdf";
}
