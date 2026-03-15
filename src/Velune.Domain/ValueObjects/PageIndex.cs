using System.Globalization;

namespace Velune.Domain.ValueObjects;

public readonly record struct PageIndex
{
    public int Value
    {
        get;
    }

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
