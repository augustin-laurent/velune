using System.Globalization;

namespace Velune.Domain.ValueObjects;

/// <summary>
/// Zero-based page index value object (must be non-negative).
/// </summary>
public readonly record struct PageIndex
{
    public int Value
    {
        get;
    }

    /// <summary>
    /// Creates a page index from the given value.
    /// </summary>
    /// <param name="value">Zero-based page number (must be non-negative).</param>
    public PageIndex(int value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Page index cannot be negative.");
        }

        Value = value;
    }

    public static implicit operator int(PageIndex pageIndex) => pageIndex.Value;

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
