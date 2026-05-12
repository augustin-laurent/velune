using Velune.Application.Results;
using Velune.Domain.Annotations;

namespace Velune.Application.Abstractions;

/// <summary>Manages stored signature assets for annotation use.</summary>
public interface ISignatureAssetStore
{
    /// <summary>Gets all stored signature assets.</summary>
    /// <returns>The list of available signature assets.</returns>
    IReadOnlyList<SignatureAsset> GetAll();

    /// <summary>Imports a signature from an image file.</summary>
    /// <param name="sourceImagePath">The path to the image to import.</param>
    /// <returns>The imported signature asset or an error.</returns>
    Result<SignatureAsset> Import(string sourceImagePath);

    /// <summary>Deletes a signature asset by its identifier.</summary>
    /// <param name="assetId">The identifier of the asset to delete.</param>
    /// <returns>A result indicating success or failure.</returns>
    Result Delete(string assetId);

    /// <summary>Saves a hand-drawn ink signature as a new asset.</summary>
    /// <param name="displayName">The display name for the signature.</param>
    /// <param name="points">The normalized ink stroke points.</param>
    /// <returns>The saved signature asset or an error.</returns>
    Result<SignatureAsset> SaveInkSignature(
        string displayName,
        IReadOnlyList<NormalizedPoint> points);
}
