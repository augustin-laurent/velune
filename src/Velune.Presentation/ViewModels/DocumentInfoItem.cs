namespace Velune.Presentation.ViewModels;

/// <summary>
/// Represents a key-value metadata entry displayed in the document info panel.
/// </summary>
/// <param name="Label">The display label.</param>
/// <param name="Value">The display value.</param>
public sealed record DocumentInfoItem(
    string Label,
    string Value);
