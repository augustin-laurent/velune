namespace Velune.Application.DTOs;

/// <summary>Page orientation options for printing.</summary>
public enum PrintOrientationOption
{
    /// <summary>Automatically determine orientation based on page dimensions.</summary>
    Automatic = 0,

    /// <summary>Force portrait orientation.</summary>
    Portrait = 1,

    /// <summary>Force landscape orientation.</summary>
    Landscape = 2
}
