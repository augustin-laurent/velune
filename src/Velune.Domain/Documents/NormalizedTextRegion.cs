namespace Velune.Domain.Documents;

/// <summary>
/// A rectangular region with coordinates and dimensions normalized to [0,1] relative to page size.
/// </summary>
public sealed record NormalizedTextRegion
{
    /// <summary>
    /// Creates a normalized region with all values in [0,1] range.
    /// </summary>
    /// <param name="x">Left edge (0 = left of page, 1 = right).</param>
    /// <param name="y">Top edge (0 = top of page, 1 = bottom).</param>
    /// <param name="width">Region width as a fraction of page width.</param>
    /// <param name="height">Region height as a fraction of page height.</param>
    public NormalizedTextRegion(double x, double y, double width, double height)
    {
        if (x < 0 || x > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "X must be between 0 and 1.");
        }

        if (y < 0 || y > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(y), "Y must be between 0 and 1.");
        }

        if (width <= 0 || width > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than 0 and at most 1.");
        }

        if (height <= 0 || height > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than 0 and at most 1.");
        }

        if (x + width > 1.000001)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "The region exceeds the normalized width.");
        }

        if (y + height > 1.000001)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "The region exceeds the normalized height.");
        }

        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public double X
    {
        get;
    }

    public double Y
    {
        get;
    }

    public double Width
    {
        get;
    }

    public double Height
    {
        get;
    }
}
