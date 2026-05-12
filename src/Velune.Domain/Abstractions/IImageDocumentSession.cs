using Velune.Domain.Documents;

namespace Velune.Domain.Abstractions;

/// <summary>
/// A document session specifically for image files, exposing image dimensions.
/// </summary>
public interface IImageDocumentSession : IDocumentSession
{
    /// <summary>
    /// Pixel dimensions of the image.
    /// </summary>
    ImageMetadata ImageMetadata
    {
        get;
    }
}
