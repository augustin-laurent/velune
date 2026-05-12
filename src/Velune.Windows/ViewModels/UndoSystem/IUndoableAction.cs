namespace Velune.Windows.ViewModels.UndoSystem;

public interface IUndoableAction
{
    string Description
    {
        get;
    }

    void Execute();

    void Undo();
}
