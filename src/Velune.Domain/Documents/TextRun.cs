namespace Velune.Domain.Documents;

public sealed record TextRun
{
    public TextRun(
        string text,
        int startIndex,
        int length,
        IReadOnlyList<NormalizedTextRegion> regions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);

        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(regions);

        Text = text;
        StartIndex = startIndex;
        Length = length;
        Regions = regions;
    }

    public string Text
    {
        get;
    }

    public int StartIndex
    {
        get;
    }

    public int Length
    {
        get;
    }

    public IReadOnlyList<NormalizedTextRegion> Regions
    {
        get;
    }
}
