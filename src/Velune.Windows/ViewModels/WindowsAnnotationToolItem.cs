using CommunityToolkit.Mvvm.ComponentModel;
using Velune.Domain.Annotations;

namespace Velune.Windows.ViewModels;

/// <summary>
/// Represents an annotation tool button in the annotation toolbar.
/// </summary>
public sealed partial class WindowsAnnotationToolItem : ObservableObject
{
    /// <summary>
    /// Initializes a new annotation tool item.
    /// </summary>
    /// <param name="tool">The annotation tool type.</param>
    /// <param name="label">The display label.</param>
    /// <param name="glyph">The Segoe MDL2 glyph character.</param>
    public WindowsAnnotationToolItem(AnnotationTool tool, string label, string glyph)
    {
        Tool = tool;
        Label = label;
        Glyph = glyph;
    }

    /// <summary>
    /// Gets the annotation tool type.
    /// </summary>
    public AnnotationTool Tool
    {
        get;
    }

    /// <summary>
    /// Gets the display label for the tool.
    /// </summary>
    public string Label
    {
        get;
    }

    /// <summary>
    /// Gets the icon glyph character.
    /// </summary>
    public string Glyph
    {
        get;
    }

    [ObservableProperty]
    public partial bool IsSelected
    {
        get; set;
    }

    [ObservableProperty]
    public partial bool IsEnabled { get; set; } = true;
}
