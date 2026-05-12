namespace Velune.Presentation.Localization;

/// <summary>
/// Represents a selectable option with a typed value and a localized display label.
/// </summary>
/// <typeparam name="TValue">The type of the option value.</typeparam>
public sealed class LocalizedOption<TValue>
{
    /// <summary>
    /// Initializes a new localized option.
    /// </summary>
    /// <param name="value">The option value.</param>
    /// <param name="label">The localized display label.</param>
    public LocalizedOption(TValue value, string label)
    {
        Value = value;
        Label = label;
    }

    /// <summary>
    /// Gets the option value.
    /// </summary>
    public TValue Value
    {
        get;
    }

    /// <summary>
    /// Gets the localized display label.
    /// </summary>
    public string Label
    {
        get;
    }

    /// <inheritdoc />
    public override string ToString() => Label;
}
