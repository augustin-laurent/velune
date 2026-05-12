namespace Velune.Application.DTOs;

/// <summary>Determines how a newly opened document is displayed in the viewer.</summary>
public enum DocumentOpenMode
{
    /// <summary>Replaces the currently active document.</summary>
    ReplaceCurrent,

    /// <summary>Opens the document in a new tab.</summary>
    AddToTabs
}
