using Velune.Domain.ValueObjects;

namespace Velune.Domain.Documents;

/// <summary>
/// Extracted text content for a single page, including positional text runs.
/// </summary>
public sealed record PageTextContent
{
    /// <summary>
    /// Creates page text content with spatial layout information.
    /// </summary>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="sourceKind">How the text was extracted (embedded or OCR).</param>
    /// <param name="text">Full page text as a single string.</param>
    /// <param name="runs">Positioned text fragments on the page.</param>
    /// <param name="sourceWidth">Original page width used for normalization.</param>
    /// <param name="sourceHeight">Original page height used for normalization.</param>
    /// <param name="characterRegionsByIndex">Optional per-character bounding regions keyed by character index.</param>
    public PageTextContent(
        PageIndex pageIndex,
        TextSourceKind sourceKind,
        string text,
        IReadOnlyList<TextRun> runs,
        double sourceWidth,
        double sourceHeight,
        IReadOnlyDictionary<int, NormalizedTextRegion>? characterRegionsByIndex = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(runs);

        if (sourceWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceWidth), "Source width must be greater than zero.");
        }

        if (sourceHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceHeight), "Source height must be greater than zero.");
        }

        PageIndex = pageIndex;
        SourceKind = sourceKind;
        Text = text;
        Runs = runs;
        SourceWidth = sourceWidth;
        SourceHeight = sourceHeight;
        CharacterRegionsByIndex = characterRegionsByIndex ?? new Dictionary<int, NormalizedTextRegion>();
    }

    public PageIndex PageIndex
    {
        get;
    }

    public TextSourceKind SourceKind
    {
        get;
    }

    /// <summary>
    /// Concatenated plain text of the entire page.
    /// </summary>
    public string Text
    {
        get;
    }

    /// <summary>
    /// Individual text fragments with their normalized positions on the page.
    /// </summary>
    public IReadOnlyList<TextRun> Runs
    {
        get;
    }

    /// <summary>
    /// Width of the source page used to compute normalized coordinates.
    /// </summary>
    public double SourceWidth
    {
        get;
    }

    /// <summary>
    /// Height of the source page used to compute normalized coordinates.
    /// </summary>
    public double SourceHeight
    {
        get;
    }

    /// <summary>
    /// Mapping from character index to its bounding region in normalized coordinates.
    /// </summary>
    public IReadOnlyDictionary<int, NormalizedTextRegion> CharacterRegionsByIndex
    {
        get;
    }
}
