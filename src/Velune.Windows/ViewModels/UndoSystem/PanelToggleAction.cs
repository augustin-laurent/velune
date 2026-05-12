namespace Velune.Windows.ViewModels.UndoSystem;

public sealed class PanelToggleAction : IUndoableAction
{
    private readonly Action _toggle;

    public PanelToggleAction(string panelName, Action toggle)
    {
        Description = $"Toggle {panelName}";
        _toggle = toggle;
    }

    public string Description
    {
        get; private set;
    }

    public void Execute()
    {
        _toggle();
    }

    public void Undo()
    {
        _toggle();
    }
}
