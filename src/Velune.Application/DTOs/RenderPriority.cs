namespace Velune.Application.DTOs;

/// <summary>Priority level for render operations.</summary>
public enum RenderPriority
{
    /// <summary>High priority render for the main viewer.</summary>
    Viewer = 0,

    /// <summary>Lower priority render for thumbnail generation.</summary>
    Thumbnail = 1
}
