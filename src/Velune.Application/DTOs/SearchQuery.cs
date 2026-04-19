namespace Velune.Application.DTOs;

public sealed record SearchQuery
{
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
