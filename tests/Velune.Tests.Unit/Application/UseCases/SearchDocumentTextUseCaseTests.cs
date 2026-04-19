using Velune.Application.DTOs;
using Velune.Application.UseCases;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Tests.Unit.Application.UseCases;

public sealed class SearchDocumentTextUseCaseTests
{
    [Fact]
    public void Execute_ShouldFindCaseInsensitiveMatchesAndReturnExcerpt()
    {
        var useCase = new SearchDocumentTextUseCase();
        var index = CreateIndex("Velune integration sample. Another velune result.");

        var result = useCase.Execute(
            new SearchDocumentTextRequest(
                index,
                new SearchQuery("velune")));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.Count);
        Assert.All(result.Value, hit => Assert.Equal(0, hit.PageIndex.Value));
        Assert.All(result.Value, hit => Assert.Contains("velune", hit.Excerpt, StringComparison.OrdinalIgnoreCase));
        Assert.All(result.Value, hit => Assert.NotEmpty(hit.Regions));
    }

    [Fact]
    public void Execute_ShouldReturnEmpty_WhenIndexHasNoSearchableText()
    {
        var useCase = new SearchDocumentTextUseCase();

        var result = useCase.Execute(
            new SearchDocumentTextRequest(
                CreateIndex(" "),
                new SearchQuery("velune")));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value);
    }

    private static DocumentTextIndex CreateIndex(string text)
    {
        IReadOnlyList<TextRun> runs = string.IsNullOrWhiteSpace(text)
            ? []
            : [
                new TextRun(
                    text,
                    0,
                    text.Length,
                    [new NormalizedTextRegion(0.1, 0.1, 0.7, 0.08)])
            ];

        return new DocumentTextIndex(
            "/tmp/document.pdf",
            DocumentType.Pdf,
            [
                new PageTextContent(
                    new PageIndex(0),
                    TextSourceKind.EmbeddedPdfText,
                    string.IsNullOrWhiteSpace(text) ? string.Empty : text,
                    runs,
                    1000,
                    1400)
            ],
            ["eng"]);
    }
}
