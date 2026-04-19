using Velune.Domain.ValueObjects;

namespace Velune.Domain.Documents;

public sealed record PageTextContent
{
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

    public string Text
    {
        get;
    }

    public IReadOnlyList<TextRun> Runs
    {
        get;
    }

    public double SourceWidth
    {
        get;
    }

    public double SourceHeight
    {
        get;
    }

    public IReadOnlyDictionary<int, NormalizedTextRegion> CharacterRegionsByIndex
    {
        get;
    }
}
