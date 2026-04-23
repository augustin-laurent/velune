namespace Velune.Presentation.Localization;

public sealed class LocalizedOption<TValue>
{
    public LocalizedOption(TValue value, string label)
    {
        Value = value;
        Label = label;
    }

    public TValue Value
    {
        get;
    }

    public string Label
    {
        get;
    }

    public override string ToString() => Label;
}
