using System.Text.Json;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Velune.Application.Abstractions;
using Velune.Application.Configuration;
using Velune.Application.Results;
using Velune.Domain.Annotations;

namespace Velune.Infrastructure.Annotations;

public sealed class JsonSignatureAssetStore : ISignatureAssetStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _manifestPath;
    private readonly string _assetsDirectoryPath;
    private readonly object _gate = new();

    public JsonSignatureAssetStore(IOptions<AppOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var rootPath = ResolveRootPath(options.Value);
        _manifestPath = Path.Combine(rootPath, "signatures.json");
        _assetsDirectoryPath = Path.Combine(rootPath, "assets");
    }

    public IReadOnlyList<SignatureAsset> GetAll()
    {
        lock (_gate)
        {
            return LoadManifest();
        }
    }

    public Result<SignatureAsset> Import(string sourceImagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceImagePath);

        if (!File.Exists(sourceImagePath))
        {
            return ResultFactory.Failure<SignatureAsset>(
                AppError.NotFound("signature.asset.missing", "The selected signature image does not exist."));
        }

        try
        {
            Directory.CreateDirectory(_assetsDirectoryPath);
            var assetId = Guid.NewGuid().ToString("N");
            var extension = Path.GetExtension(sourceImagePath);
            var outputPath = Path.Combine(_assetsDirectoryPath, $"{assetId}{extension}");
            File.Copy(sourceImagePath, outputPath, overwrite: false);

            using var bitmap = SKBitmap.Decode(outputPath);
            if (bitmap is null)
            {
                File.Delete(outputPath);
                return ResultFactory.Failure<SignatureAsset>(
                    AppError.Infrastructure("signature.asset.decode_failed", "The imported file is not a supported image."));
            }

            var asset = new SignatureAsset(
                assetId,
                Path.GetFileNameWithoutExtension(sourceImagePath),
                outputPath,
                bitmap.Width,
                bitmap.Height,
                DateTimeOffset.UtcNow);

            lock (_gate)
            {
                var items = LoadManifest().ToList();
                items.Add(asset);
                SaveManifest(items);
            }

            return ResultFactory.Success(asset);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ResultFactory.Failure<SignatureAsset>(
                AppError.Infrastructure("signature.asset.import_failed", exception.Message));
        }
    }

    public Result Delete(string assetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        try
        {
            lock (_gate)
            {
                var items = LoadManifest().ToList();
                var asset = items.FirstOrDefault(item => string.Equals(item.Id, assetId, StringComparison.Ordinal));
                if (asset is null)
                {
                    return ResultFactory.Failure(
                        AppError.NotFound("signature.asset.not_found", "The selected signature no longer exists."));
                }

                items.Remove(asset);
                SaveManifest(items);

                try
                {
                    if (File.Exists(asset.FilePath))
                    {
                        File.Delete(asset.FilePath);
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    return ResultFactory.Failure(
                        AppError.Infrastructure("signature.asset.delete_failed", exception.Message));
                }
            }

            return ResultFactory.Success();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ResultFactory.Failure(
                AppError.Infrastructure("signature.asset.delete_failed", exception.Message));
        }
    }

    public Result<SignatureAsset> SaveInkSignature(
        string displayName,
        IReadOnlyList<NormalizedPoint> points)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(points);

        if (points.Count == 0)
        {
            return ResultFactory.Failure<SignatureAsset>(
                AppError.Validation("signature.asset.points.empty", "Draw a signature before saving it."));
        }

        try
        {
            Directory.CreateDirectory(_assetsDirectoryPath);
            var assetId = Guid.NewGuid().ToString("N");
            var outputPath = Path.Combine(_assetsDirectoryPath, $"{assetId}.png");
            var pngBytes = SkiaAnnotationRenderer.RenderInkSignaturePng(points, 420, 180);
            File.WriteAllBytes(outputPath, pngBytes);

            using var bitmap = SKBitmap.Decode(outputPath);
            if (bitmap is null)
            {
                File.Delete(outputPath);
                return ResultFactory.Failure<SignatureAsset>(
                    AppError.Infrastructure("signature.asset.encode_failed", "Unable to encode the drawn signature."));
            }

            var asset = new SignatureAsset(
                assetId,
                displayName.Trim(),
                outputPath,
                bitmap.Width,
                bitmap.Height,
                DateTimeOffset.UtcNow);

            lock (_gate)
            {
                var items = LoadManifest().ToList();
                items.Add(asset);
                SaveManifest(items);
            }

            return ResultFactory.Success(asset);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ResultFactory.Failure<SignatureAsset>(
                AppError.Infrastructure("signature.asset.save_failed", exception.Message));
        }
    }

    private List<SignatureAsset> LoadManifest()
    {
        if (!File.Exists(_manifestPath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(_manifestPath);
            return JsonSerializer.Deserialize<List<SignatureAsset>>(json, SerializerOptions) ?? [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new InvalidOperationException("Unable to read the signature library.", exception);
        }
    }

    private void SaveManifest(IReadOnlyList<SignatureAsset> items)
    {
        var directory = Path.GetDirectoryName(_manifestPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(items, SerializerOptions);
        File.WriteAllText(_manifestPath, json);
    }

    private static string ResolveRootPath(AppOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.SignatureLibraryPath))
        {
            return Path.GetFullPath(options.SignatureLibraryPath);
        }

        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(basePath, string.IsNullOrWhiteSpace(options.Name) ? "Velune" : options.Name, "SignatureLibrary");
    }
}
