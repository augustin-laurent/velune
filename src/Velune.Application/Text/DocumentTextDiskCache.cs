using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Velune.Application.Abstractions;
using Velune.Application.Configuration;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Text;

public sealed partial class DocumentTextDiskCache : IDocumentTextCache
{
    private const int CacheFileVersion = 2;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly ILogger<DocumentTextDiskCache> _logger;
    private readonly string _rootPath;

    public DocumentTextDiskCache(
        ILogger<DocumentTextDiskCache> logger,
        IOptions<AppOptions> options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _rootPath = ResolveRootPath(options.Value);
    }

    public bool TryGet(
        IDocumentSession session,
        string engineFingerprint,
        IReadOnlyList<string> languages,
        bool forceOcr,
        out DocumentTextIndex? index)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(languages);

        index = null;

        if (!TryCreateCachePath(session, engineFingerprint, languages, forceOcr, out var cacheFilePath))
        {
            return false;
        }

        if (!File.Exists(cacheFilePath))
        {
            return false;
        }

        try
        {
            var payload = File.ReadAllText(cacheFilePath);
            var cacheEntry = JsonSerializer.Deserialize<CacheEntry>(payload, SerializerOptions);
            if (cacheEntry is null || cacheEntry.Version != CacheFileVersion)
            {
                DeleteFile(cacheFilePath);
                return false;
            }

            index = ToDocumentTextIndex(cacheEntry.Index);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            LogReadFailure(_logger, ex, cacheFilePath);
            DeleteFile(cacheFilePath);
            return false;
        }
    }

    public void Store(
        IDocumentSession session,
        string engineFingerprint,
        IReadOnlyList<string> languages,
        bool forceOcr,
        DocumentTextIndex index)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(languages);
        ArgumentNullException.ThrowIfNull(index);

        if (!TryCreateCachePath(session, engineFingerprint, languages, forceOcr, out var cacheFilePath))
        {
            return;
        }

        var tempPath = $"{cacheFilePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cacheFilePath)!);

            var cacheEntry = new CacheEntry(
                CacheFileVersion,
                ToCacheIndex(index));
            var payload = JsonSerializer.Serialize(cacheEntry, SerializerOptions);
            File.WriteAllText(tempPath, payload, Encoding.UTF8);
            File.Move(tempPath, cacheFilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            LogStoreFailure(_logger, ex, cacheFilePath);
            DeleteFile(tempPath);
        }
    }

    private bool TryCreateCachePath(
        IDocumentSession session,
        string engineFingerprint,
        IReadOnlyList<string> languages,
        bool forceOcr,
        out string cacheFilePath)
    {
        cacheFilePath = string.Empty;

        var filePath = session.Metadata.FilePath;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            return false;
        }

        var documentKey = ComputeHash(
            $"{session.Metadata.DocumentType}|{Path.GetFullPath(fileInfo.FullName)}");
        var fingerprint = ComputeHash(
            $"{fileInfo.Length}|{fileInfo.LastWriteTimeUtc.Ticks}|{forceOcr}|{engineFingerprint}|{string.Join(",", languages)}");

        cacheFilePath = Path.Combine(_rootPath, documentKey, $"{fingerprint}.json");
        return true;
    }

    private static string ResolveRootPath(AppOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.OcrCachePath))
        {
            return Path.GetFullPath(options.OcrCachePath);
        }

        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(basePath, "Velune", "OcrCache");
    }

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var builder = new StringBuilder(bytes.Length * 2);

        foreach (var current in bytes)
        {
            builder.Append(current.ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static CacheIndex ToCacheIndex(DocumentTextIndex index)
    {
        return new CacheIndex(
            index.FilePath,
            index.DocumentType,
            [.. index.Languages],
            [.. index.Pages.Select(page => new CachePage(
                page.PageIndex.Value,
                page.SourceKind,
                page.Text,
                page.SourceWidth,
                page.SourceHeight,
                [.. page.Runs.Select(run => new CacheRun(
                    run.Text,
                    run.StartIndex,
                    run.Length,
                    [.. run.Regions.Select(region => new CacheRegion(region.X, region.Y, region.Width, region.Height))]))],
                [.. page.CharacterRegionsByIndex.Select(entry => new CacheCharacterRegion(
                    entry.Key,
                    new CacheRegion(entry.Value.X, entry.Value.Y, entry.Value.Width, entry.Value.Height)))]))]);
    }

    private static DocumentTextIndex ToDocumentTextIndex(CacheIndex index)
    {
        return new DocumentTextIndex(
            index.FilePath,
            index.DocumentType,
            [.. index.Pages.Select(page => new PageTextContent(
                new PageIndex(page.PageIndex),
                page.SourceKind,
                page.Text,
                [.. page.Runs.Select(run => new TextRun(
                    run.Text,
                    run.StartIndex,
                    run.Length,
                    [.. run.Regions.Select(region => new NormalizedTextRegion(region.X, region.Y, region.Width, region.Height))]))],
                page.SourceWidth,
                page.SourceHeight,
                page.CharacterRegions.ToDictionary(
                    region => region.CharacterIndex,
                    region => new NormalizedTextRegion(region.Region.X, region.Region.Y, region.Region.Width, region.Region.Height))))],
            index.Languages);
    }

    private static void DeleteFile(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup of invalid cache entries.
        }
    }

    [LoggerMessage(
        EventId = 6200,
        Level = LogLevel.Warning,
        Message = "Unable to read OCR/text cache file {CacheFilePath}.")]
    private static partial void LogReadFailure(
        ILogger logger,
        Exception exception,
        string cacheFilePath);

    [LoggerMessage(
        EventId = 6201,
        Level = LogLevel.Warning,
        Message = "Unable to store OCR/text cache file {CacheFilePath}.")]
    private static partial void LogStoreFailure(
        ILogger logger,
        Exception exception,
        string cacheFilePath);

    private sealed record CacheEntry(int Version, CacheIndex Index);

    private sealed record CacheIndex(
        string FilePath,
        DocumentType DocumentType,
        IReadOnlyList<string> Languages,
        IReadOnlyList<CachePage> Pages);

    private sealed record CachePage(
        int PageIndex,
        TextSourceKind SourceKind,
        string Text,
        double SourceWidth,
        double SourceHeight,
        IReadOnlyList<CacheRun> Runs,
        IReadOnlyList<CacheCharacterRegion> CharacterRegions);

    private sealed record CacheRun(
        string Text,
        int StartIndex,
        int Length,
        IReadOnlyList<CacheRegion> Regions);

    private sealed record CacheCharacterRegion(int CharacterIndex, CacheRegion Region);

    private sealed record CacheRegion(double X, double Y, double Width, double Height);
}
