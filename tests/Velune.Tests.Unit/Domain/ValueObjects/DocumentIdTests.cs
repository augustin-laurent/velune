using Velune.Domain.ValueObjects;

namespace Velune.Tests.Unit.Domain.ValueObjects;

public sealed class DocumentIdTests
{
    [Fact]
    public void New_ShouldGenerateNonEmptyGuid()
    {
        var documentId = DocumentId.New();

        Assert.NotEqual(Guid.Empty, documentId.Value);
    }
}
