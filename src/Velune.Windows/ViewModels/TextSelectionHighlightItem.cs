namespace Velune.Windows.ViewModels;

public sealed class TextSelectionHighlightItem
{
    public double Left
    {
        get; init;
    }

    public double Top
    {
        get; init;
    }

    public double Width
    {
        get; init;
    }

    public double Height
    {
        get; init;
    }

    public Microsoft.UI.Xaml.Thickness Margin => new(Left, Top, 0, 0);
}
