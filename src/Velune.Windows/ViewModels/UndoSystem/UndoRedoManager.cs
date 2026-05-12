namespace Velune.Windows.ViewModels.UndoSystem;

public sealed class UndoRedoManager
{
    private const int MaxDepth = 50;
    private readonly Stack<IUndoableAction> _undoStack = new();
    private readonly Stack<IUndoableAction> _redoStack = new();

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    public void Push(IUndoableAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        _redoStack.Clear();
        _undoStack.Push(action);

        while (_undoStack.Count > MaxDepth)
        {
            TrimBottom(_undoStack);
        }
    }

    public void Undo()
    {
        if (_undoStack.Count == 0)
        {
            return;
        }

        IUndoableAction action = _undoStack.Pop();
        action.Undo();
        _redoStack.Push(action);
    }

    public void Redo()
    {
        if (_redoStack.Count == 0)
        {
            return;
        }

        IUndoableAction action = _redoStack.Pop();
        action.Execute();
        _undoStack.Push(action);
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }

    private static void TrimBottom(Stack<IUndoableAction> stack)
    {
        IUndoableAction[] items = stack.ToArray();
        stack.Clear();
        for (int i = 0; i < items.Length - 1; i++)
        {
            stack.Push(items[i]);
        }
    }
}
