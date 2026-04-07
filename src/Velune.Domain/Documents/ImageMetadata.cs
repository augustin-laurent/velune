namespace Velune.Domain.Documents;

public sealed record ImageMetadata(
    int Width,
    int Height)
{
    public int Width { get; } = Width;
    public int Height { get; } = Height;
}
