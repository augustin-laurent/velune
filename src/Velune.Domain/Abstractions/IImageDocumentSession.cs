using Velune.Domain.Documents;

namespace Velune.Domain.Abstractions;

public interface IImageDocumentSession : IDocumentSession
{
    ImageMetadata ImageMetadata
    {
        get;
    }
}
