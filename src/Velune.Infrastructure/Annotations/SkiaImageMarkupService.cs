using SkiaSharp;
using Velune.Application.Abstractions;
using Velune.Application.Documents;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Domain.Annotations;
using Velune.Domain.ValueObjects;
using Velune.Infrastructure.Image;

namespace Velune.Infrastructure.Annotations;

/// <summary>
/// Flattens annotations onto image documents using SkiaSharp rendering.
/// </summary>
public sealed class SkiaImageMarkupService : IImageMarkupService
{
    private readonly ISignatureAssetStore _signatureAssetStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkiaImageMarkupService"/> class.
    /// </summary>
    /// <param name="signatureAssetStore">Store for resolving signature image assets.</param>
    public SkiaImageMarkupService(ISignatureAssetStore signatureAssetStore)
    {
        ArgumentNullException.ThrowIfNull(signatureAssetStore);
        _signatureAssetStore = signatureAssetStore;
    }

    /// <inheritdoc />
    public async Task<Result<string>> FlattenAnnotationsAsync(
        ApplyImageAnnotationsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Session is not ImageDocumentSession imageSession)
        {
            return ResultFactory.Failure<string>(
                AppError.Validation("document.annotation.image_session.invalid", "The current document session is not an image session."));
        }

        if (string.IsNullOrWhiteSpace(request.OutputPath))
        {
            return ResultFactory.Failure<string>(
                AppError.Validation("document.annotation.output.empty", "The output path is required."));
        }

        try
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            using var sourceBitmap = SKBitmap.Decode(imageSession.Resource.FileBytes);
            if (sourceBitmap is null)
            {
                return ResultFactory.Failure<string>(
                    AppError.Infrastructure("document.annotation.image_decode_failed", "Unable to decode the source image."));
            }

            using var surface = SKSurface.Create(new SKImageInfo(sourceBitmap.Width, sourceBitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
            SKCanvas? canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(sourceBitmap, new SKRect(0, 0, sourceBitmap.Width, sourceBitmap.Height));

            var signatureAssets = _signatureAssetStore.GetAll().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
            DocumentAnnotation[] pageAnnotations = request.Annotations
                .Where(annotation => annotation.PageIndex.Value == 0)
                .Select(annotation => annotation.DeepCopy())
                .ToArray();

            SkiaAnnotationRenderer.DrawAnnotations(
                canvas,
                sourceBitmap.Width,
                sourceBitmap.Height,
                Rotation.Deg0,
                pageAnnotations,
                signatureAssets);

            using SKImage? image = surface.Snapshot();
            using SKData? data = image.Encode(
                ResolveOutputFormat(request.OutputPath),
                ResolveQuality(request.OutputPath));

            if (data is null)
            {
                return ResultFactory.Failure<string>(
                    AppError.Infrastructure("document.annotation.image_encode_failed", "Unable to encode the annotated image."));
            }

            string? directory = Path.GetDirectoryName(request.OutputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using FileStream stream = File.Create(request.OutputPath);
            data.SaveTo(stream);
            return ResultFactory.Success(request.OutputPath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return ResultFactory.Failure<string>(
                AppError.Infrastructure(
                    "document.annotation.image_failed",
                    exception.Message));
        }
    }

    private static SKEncodedImageFormat ResolveOutputFormat(string outputPath)
    {
        string extension = Path.GetExtension(outputPath);
        return SupportedDocumentFormats.IsImage(extension) switch
        {
            true when extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                      extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) => SKEncodedImageFormat.Jpeg,
            true when extension.Equals(".webp", StringComparison.OrdinalIgnoreCase) => SKEncodedImageFormat.Webp,
            _ => SKEncodedImageFormat.Png
        };
    }

    private static int ResolveQuality(string outputPath)
    {
        string extension = Path.GetExtension(outputPath);
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)
            ? 92
            : 100;
    }
}
