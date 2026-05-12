namespace Velune.Domain.Documents;

/// <summary>
/// A contiguous run of text with its position on the page in normalized coordinates.
/// </summary>
public sealed record TextRun
{
    /// <summary>
    /// Creates a text run with positional information.
    /// </summary>
    /// <param name="text">The text content of this run.</param>
    /// <param name="startIndex">Character offset into the full page text.</param>
    /// <param name="length">Number of characters in this run.</param>
    /// <param name="regions">Normalized bounding regions for this text run.</param>
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

    /// <summary>
    /// Zero-based character offset into the page's full text string.
    /// </summary>
    public int StartIndex
    {
        get;
    }

    /// <summary>
    /// Number of characters in this run.
    /// </summary>
    public int Length
    {
        get;
    }

    /// <summary>
    /// Normalized bounding regions where this text appears on the page.
    /// </summary>
    public IReadOnlyList<NormalizedTextRegion> Regions
    {
        get;
    }
}
