namespace Velune.Windows.ViewModels.UndoSystem;

public sealed class PreferenceChangeAction : IUndoableAction
{
    private readonly string _previousValue;
    private readonly string _newValue;
    private readonly Action<string> _applier;

    public PreferenceChangeAction(
        string preferenceName,
        string previousValue,
        string newValue,
        Action<string> applier)
    {
        Description = $"Change {preferenceName}";
        _previousValue = previousValue;
        _newValue = newValue;
        _applier = applier;
    }

    public string Description
    {
        get; private set;
    }

    public void Execute()
    {
        _applier(_newValue);
    }

    public void Undo()
    {
        _applier(_previousValue);
    }
}
