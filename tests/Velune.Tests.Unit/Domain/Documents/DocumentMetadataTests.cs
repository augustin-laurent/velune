using Velune.Domain.Documents;

namespace Velune.Tests.Unit.Domain.Documents;

public sealed class DocumentMetadataTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenFileSizeIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DocumentMetadata(
            fileName: "test.pdf",
            filePath: "/tmp/test.pdf",
            documentType: DocumentType.Pdf,
            fileSizeInBytes: -1));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenPageCountIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DocumentMetadata(
            fileName: "test.pdf",
            filePath: "/tmp/test.pdf",
            documentType: DocumentType.Pdf,
            fileSizeInBytes: 100,
            pageCount: -1));
    }
}
