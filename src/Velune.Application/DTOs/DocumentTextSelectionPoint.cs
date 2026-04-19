namespace Velune.Application.DTOs;

public sealed record DocumentTextSelectionPoint
{
    public DocumentTextSelectionPoint(double x, double y)
    {
        if (double.IsNaN(x) || double.IsInfinity(x) || x < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "X must be a finite non-negative value.");
        }

        if (double.IsNaN(y) || double.IsInfinity(y) || y < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(y), "Y must be a finite non-negative value.");
        }

        X = x;
        Y = y;
    }

    public double X
    {
        get;
    }

    public double Y
    {
        get;
    }
}
