using Velune.Application.DTOs;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Velune.Infrastructure.Text;

namespace Velune.Tests.Unit.Infrastructure.Text;

public sealed class DocumentTextSelectionServiceTests
{
    [Fact]
    public void Select_ShouldSnapToNearestOcrRunAndSelectContinuousRange()
    {
        var service = new DocumentTextSelectionService();
        var pageContent = CreateOcrPageContent();
        var request = new DocumentTextSelectionRequest(
            new StubDocumentSession(
                new DocumentMetadata("image.png", "/tmp/image.png", DocumentType.Image, 1024, 1),
                ViewportState.Default),
            new DocumentTextIndex("/tmp/image.png", DocumentType.Image, [pageContent], ["eng"]),
            pageContent.PageIndex,
            new DocumentTextSelectionPoint(235, 165),
            new DocumentTextSelectionPoint(150, 315));

        var result = service.Resolve(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value!.HasSelection);
        Assert.Equal("beta gamma", result.Value.SelectedText);
        Assert.Equal(2, result.Value.Regions.Count);
        Assert.Equal(TextSourceKind.Ocr, result.Value.SourceKind);
    }

    [Fact]
    public void Select_ShouldCollapseWrappedWhitespaceIntoSingleSpace()
    {
        var service = new DocumentTextSelectionService();
        var pageContent = CreateWrappedOcrPageContent();
        var request = new DocumentTextSelectionRequest(
            new StubDocumentSession(
                new DocumentMetadata("image.png", "/tmp/image.png", DocumentType.Image, 1024, 1),
                ViewportState.Default),
            new DocumentTextIndex("/tmp/image.png", DocumentType.Image, [pageContent], ["eng"]),
            pageContent.PageIndex,
            new DocumentTextSelectionPoint(235, 165),
            new DocumentTextSelectionPoint(150, 315));

        var result = service.Resolve(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("beta gamma", result.Value!.SelectedText);
    }

    [Fact]
    public void Select_ShouldReturnSingleOcrWord_WhenAnchorAndActiveStayOnSameRun()
    {
        var service = new DocumentTextSelectionService();
        var pageContent = CreateOcrPageContent();
        var request = new DocumentTextSelectionRequest(
            new StubDocumentSession(
                new DocumentMetadata("image.png", "/tmp/image.png", DocumentType.Image, 1024, 1),
                ViewportState.Default),
            new DocumentTextIndex("/tmp/image.png", DocumentType.Image, [pageContent], ["eng"]),
            pageContent.PageIndex,
            new DocumentTextSelectionPoint(290, 170),
            new DocumentTextSelectionPoint(330, 180));

        var result = service.Resolve(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("beta", result.Value!.SelectedText);
        Assert.Single(result.Value.Regions);
    }

    [Fact]
    public void Select_ShouldMergeOverlappingOcrRegions()
    {
        var service = new DocumentTextSelectionService();
        var pageContent = CreateOverlappingOcrPageContent();
        var request = new DocumentTextSelectionRequest(
            new StubDocumentSession(
                new DocumentMetadata("image.png", "/tmp/image.png", DocumentType.Image, 1024, 1),
                ViewportState.Default),
            new DocumentTextIndex("/tmp/image.png", DocumentType.Image, [pageContent], ["eng"]),
            pageContent.PageIndex,
            new DocumentTextSelectionPoint(140, 170),
            new DocumentTextSelectionPoint(280, 170));

        var result = service.Resolve(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("alpha beta", result.Value!.SelectedText);
        Assert.Single(result.Value.Regions);
        Assert.InRange(result.Value.Regions[0].X, 0.091, 0.092);
        Assert.InRange(result.Value.Regions[0].Y, 0.091, 0.092);
        Assert.InRange(result.Value.Regions[0].Width, 0.236, 0.237);
        Assert.InRange(result.Value.Regions[0].Height, 0.068, 0.069);
    }

    [Fact]
    public void Select_ShouldReturnSingleRegionForSameLineBoxesWithDifferentHeights()
    {
        var service = new DocumentTextSelectionService();
        var pageContent = CreateAccentLikeLinePageContent();
        var request = new DocumentTextSelectionRequest(
            new StubDocumentSession(
                new DocumentMetadata("image.png", "/tmp/image.png", DocumentType.Image, 1024, 1),
                ViewportState.Default),
            new DocumentTextIndex("/tmp/image.png", DocumentType.Image, [pageContent], ["eng"]),
            pageContent.PageIndex,
            new DocumentTextSelectionPoint(120, 170),
            new DocumentTextSelectionPoint(280, 170));

        var result = service.Resolve(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("alpha beta", result.Value!.SelectedText);
        Assert.Single(result.Value.Regions);
        Assert.InRange(result.Value.Regions[0].Width, 0.26, 0.27);
    }

    private static PageTextContent CreateOcrPageContent()
    {
        const string text = "alpha beta\ngamma";

        return new PageTextContent(
            new PageIndex(0),
            TextSourceKind.Ocr,
            text,
            [
                new TextRun("alpha", 0, 5, [new NormalizedTextRegion(0.10, 0.10, 0.10, 0.05)]),
                new TextRun("beta", 6, 4, [new NormalizedTextRegion(0.26, 0.10, 0.10, 0.05)]),
                new TextRun("gamma", 11, 5, [new NormalizedTextRegion(0.10, 0.20, 0.12, 0.05)])
            ],
            1000,
            1400);
    }

    private static PageTextContent CreateWrappedOcrPageContent()
    {
        const string text = "alpha beta \n gamma";

        return new PageTextContent(
            new PageIndex(0),
            TextSourceKind.Ocr,
            text,
            [
                new TextRun("alpha", 0, 5, [new NormalizedTextRegion(0.10, 0.10, 0.10, 0.05)]),
                new TextRun("beta", 6, 4, [new NormalizedTextRegion(0.26, 0.10, 0.10, 0.05)]),
                new TextRun("gamma", 13, 5, [new NormalizedTextRegion(0.10, 0.20, 0.12, 0.05)])
            ],
            1000,
            1400);
    }

    private static PageTextContent CreateOverlappingOcrPageContent()
    {
        const string text = "alpha beta";

        return new PageTextContent(
            new PageIndex(0),
            TextSourceKind.Ocr,
            text,
            [
                new TextRun("alpha", 0, 5, [new NormalizedTextRegion(0.10, 0.10, 0.12, 0.05)]),
                new TextRun("beta", 6, 4, [new NormalizedTextRegion(0.20, 0.10, 0.12, 0.05)])
            ],
            1000,
            1400);
    }

    private static PageTextContent CreateAccentLikeLinePageContent()
    {
        const string text = "alpha beta";

        return new PageTextContent(
            new PageIndex(0),
            TextSourceKind.Ocr,
            text,
            [
                new TextRun("alpha", 0, 5, [new NormalizedTextRegion(0.10, 0.095, 0.12, 0.065)]),
                new TextRun("beta", 6, 4, [new NormalizedTextRegion(0.23, 0.10, 0.11, 0.05)])
            ],
            1000,
            1400);
    }

    private sealed record StubDocumentSession(
        DocumentMetadata Metadata,
        ViewportState Viewport) : IDocumentSession
    {
        public DocumentId Id
        {
            get;
        } = DocumentId.New();

        public IDocumentSession WithViewport(ViewportState viewport)
        {
            return this with
            {
                Viewport = viewport
            };
        }
    }
}
