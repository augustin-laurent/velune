using CommunityToolkit.Mvvm.ComponentModel;
using Velune.Domain.Annotations;

namespace Velune.Windows.ViewModels;

public sealed partial class WindowsAnnotationToolItem : ObservableObject
{
    public WindowsAnnotationToolItem(AnnotationTool tool, string label, string glyph)
    {
        Tool = tool;
        Label = label;
        Glyph = glyph;
    }

    public AnnotationTool Tool
    {
        get;
    }

    public string Label
    {
        get;
    }

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
