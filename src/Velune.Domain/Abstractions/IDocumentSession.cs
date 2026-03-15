using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Domain.Abstractions;

public interface IDocumentSession
{
    DocumentId Id
    {
        get;
    }
    DocumentMetadata Metadata
    {
        get;
    }
    ViewportState Viewport
    {
        get;
    }

    IDocumentSession WithViewport(ViewportState viewport);
}
