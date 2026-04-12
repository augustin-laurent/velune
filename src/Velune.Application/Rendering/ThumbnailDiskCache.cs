using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Velune.Application.Abstractions;
using Velune.Application.Configuration;
using Velune.Application.DTOs;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;

namespace Velune.Application.Rendering;

public sealed partial class ThumbnailDiskCache : IThumbnailDiskCache
{
    private const int CacheFileVersion = 1;
    private readonly ILogger<ThumbnailDiskCache> _logger;
    private readonly string _rootPath;
    private readonly TimeSpan _maxAge;

    public ThumbnailDiskCache(
        ILogger<ThumbnailDiskCache> logger,
        IOptions<AppOptions> options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _rootPath = ResolveRootPath(options.Value);
        _maxAge = options.Value.ThumbnailDiskCacheMaxAgeDays <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromDays(options.Value.ThumbnailDiskCacheMaxAgeDays);

        CleanupExpiredEntries();
    }

    public bool TryGet(
        IDocumentSession session,
        RenderRequest request,
        out RenderedPage? renderedPage)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);

        renderedPage = null;

        if (!IsEligible(request) ||
            !TryCreateCacheLocation(session, request, out var location))
        {
            return false;
        }

        if (!File.Exists(location.CacheFilePath))
        {
            return false;
        }

        try
        {
            using var stream = File.OpenRead(location.CacheFilePath);
            using var reader = new BinaryReader(stream);

            var version = reader.ReadInt32();
            var width = reader.ReadInt32();
            var height = reader.ReadInt32();
            var pixelDataLength = reader.ReadInt32();

            var remainingLength = stream.Length - stream.Position;
            if (version != CacheFileVersion ||
                width <= 0 ||
                height <= 0 ||
                pixelDataLength <= 0 ||
                remainingLength != pixelDataLength)
            {
                DeleteFile(location.CacheFilePath);
                return false;
            }

            var pixelData = reader.ReadBytes(pixelDataLength);
            if (pixelData.Length != pixelDataLength)
            {
                DeleteFile(location.CacheFilePath);
                return false;
            }

            renderedPage = new RenderedPage(
                request.PageIndex,
                pixelData,
                width,
                height);

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or EndOfStreamException)
        {
            LogReadFailure(_logger, ex, location.CacheFilePath);

            DeleteFile(location.CacheFilePath);
            return false;
        }
    }

    public void Store(
        IDocumentSession session,
        RenderRequest request,
        RenderedPage renderedPage)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(renderedPage);

        if (!IsEligible(request) ||
            !TryCreateCacheLocation(session, request, out var location))
        {
            return;
        }

        var tempPath = $"{location.CacheFilePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            Directory.CreateDirectory(location.FingerprintDirectoryPath);
            RemoveObsoleteDocumentVersions(location);

            using (var stream = File.Create(tempPath))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(CacheFileVersion);
                writer.Write(renderedPage.Width);
                writer.Write(renderedPage.Height);
                writer.Write(renderedPage.PixelData.Length);
                writer.Write(renderedPage.PixelData);
            }

            File.Move(tempPath, location.CacheFilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            LogStoreFailure(_logger, ex, location.CacheFilePath);

            DeleteFile(tempPath);
        }
    }

    private void CleanupExpiredEntries()
    {
        if (_maxAge <= TimeSpan.Zero || !Directory.Exists(_rootPath))
        {
            return;
        }

        var expirationThreshold = DateTime.UtcNow - _maxAge;

        foreach (var filePath in Directory.EnumerateFiles(_rootPath, "*", SearchOption.AllDirectories))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(filePath) >= expirationThreshold)
                {
                    continue;
                }

                File.Delete(filePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LogExpiredFileDeletionFailure(_logger, ex, filePath);
            }
        }

        DeleteEmptyDirectories();
    }

    private void RemoveObsoleteDocumentVersions(CacheLocation location)
    {
        if (!Directory.Exists(location.DocumentDirectoryPath))
        {
            return;
        }

        foreach (var directoryPath in Directory.EnumerateDirectories(location.DocumentDirectoryPath))
        {
            if (string.Equals(directoryPath, location.FingerprintDirectoryPath, StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                Directory.Delete(directoryPath, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LogObsoleteDirectoryDeletionFailure(_logger, ex, directoryPath);
            }
        }
    }

    private void DeleteEmptyDirectories()
    {
        foreach (var directoryPath in Directory
                     .EnumerateDirectories(_rootPath, "*", SearchOption.AllDirectories)
                     .OrderByDescending(path => path.Length))
        {
            TryDeleteDirectoryIfEmpty(directoryPath);
        }

        TryDeleteDirectoryIfEmpty(_rootPath);
    }

    private static void TryDeleteDirectoryIfEmpty(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        if (Directory.EnumerateFileSystemEntries(directoryPath).Any())
        {
            return;
        }

        Directory.Delete(directoryPath, recursive: false);
    }

    private bool TryCreateCacheLocation(
        IDocumentSession session,
        RenderRequest request,
        out CacheLocation location)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);

        location = default;

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

        var normalizedPath = Path.GetFullPath(fileInfo.FullName);
        var documentDirectoryName = ComputeHash(
            $"{session.Metadata.DocumentType}|{normalizedPath}");
        var fingerprintDirectoryName = ComputeHash(
            $"{fileInfo.Length}|{fileInfo.LastWriteTimeUtc.Ticks}");

        var documentDirectoryPath = Path.Combine(
            _rootPath,
            documentDirectoryName);
        var fingerprintDirectoryPath = Path.Combine(
            documentDirectoryPath,
            fingerprintDirectoryName);
        var cacheFilePath = Path.Combine(
            fingerprintDirectoryPath,
            BuildCacheFileName(request));

        location = new CacheLocation(
            documentDirectoryPath,
            fingerprintDirectoryPath,
            cacheFilePath);

        return true;
    }

    private static bool IsEligible(RenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.RequestedWidth.HasValue && request.RequestedHeight.HasValue;
    }

    private static string BuildCacheFileName(RenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var roundedZoom = Math.Round(
            request.ZoomFactor,
            4,
            MidpointRounding.AwayFromZero);

        return FormattableString.Invariant(
            $"page-{request.PageIndex.Value}-zoom-{roundedZoom:0.####}-rotation-{(int)request.Rotation}-width-{request.RequestedWidth!.Value}-height-{request.RequestedHeight!.Value}.thumb");
    }

    private static string ComputeHash(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ResolveRootPath(AppOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.ThumbnailDiskCachePath))
        {
            return Path.GetFullPath(options.ThumbnailDiskCachePath);
        }

        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(basePath))
        {
            basePath = Path.GetTempPath();
        }

        var appName = string.IsNullOrWhiteSpace(options.Name)
            ? "Velune"
            : options.Name;

        return Path.Combine(basePath, appName, "cache", "thumbnails");
    }

    [LoggerMessage(
        EventId = 30,
        Level = LogLevel.Warning,
        Message = "Unable to read cached thumbnail from {CacheFilePath}.")]
    private static partial void LogReadFailure(
        ILogger logger,
        Exception exception,
        string cacheFilePath);

    [LoggerMessage(
        EventId = 31,
        Level = LogLevel.Warning,
        Message = "Unable to store cached thumbnail at {CacheFilePath}.")]
    private static partial void LogStoreFailure(
        ILogger logger,
        Exception exception,
        string cacheFilePath);

    [LoggerMessage(
        EventId = 32,
        Level = LogLevel.Warning,
        Message = "Unable to delete expired thumbnail cache file {CacheFilePath}.")]
    private static partial void LogExpiredFileDeletionFailure(
        ILogger logger,
        Exception exception,
        string cacheFilePath);

    [LoggerMessage(
        EventId = 33,
        Level = LogLevel.Warning,
        Message = "Unable to delete obsolete thumbnail cache directory {CacheDirectoryPath}.")]
    private static partial void LogObsoleteDirectoryDeletionFailure(
        ILogger logger,
        Exception exception,
        string cacheDirectoryPath);

    private static void DeleteFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        try
        {
            File.Delete(filePath);
        }
        catch
        {
        }
    }

    private readonly record struct CacheLocation(
        string DocumentDirectoryPath,
        string FingerprintDirectoryPath,
        string CacheFilePath);
}
