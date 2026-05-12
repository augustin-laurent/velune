namespace Velune.Application.DTOs;

/// <summary>Validated search query containing trimmed, non-empty text.</summary>
public sealed record SearchQuery
{
    /// <summary>Creates a search query, validating that text is non-empty.</summary>
    /// <param name="text">The search text (will be trimmed).</param>
    public SearchQuery(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Text = text.Trim();
    }

    public string Text
    {
        get;
    }
}
