namespace Velune.Windows.ViewModels.UndoSystem;

public sealed class PageOperationAction : IUndoableAction
{
    private readonly Action _execute;
    private readonly Action _undo;

    public PageOperationAction(string description, Action execute, Action undo)
    {
        Description = description;
        _execute = execute;
        _undo = undo;
    }

    public string Description
    {
        get; private set;
    }

    public void Execute()
    {
        _execute();
    }

    public void Undo()
    {
        _undo();
    }
}
