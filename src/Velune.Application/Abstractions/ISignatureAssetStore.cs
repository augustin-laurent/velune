using Velune.Application.Results;
using Velune.Domain.Annotations;

namespace Velune.Application.Abstractions;

public interface ISignatureAssetStore
{
    IReadOnlyList<SignatureAsset> GetAll();

    Result<SignatureAsset> Import(string sourceImagePath);

    Result Delete(string assetId);

    Result<SignatureAsset> SaveInkSignature(
        string displayName,
        IReadOnlyList<NormalizedPoint> points);
}
