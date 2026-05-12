namespace Velune.Windows.ViewModels.UndoSystem;

public sealed class NavigationAction : IUndoableAction
{
    private readonly WindowsDocumentTabViewModel _tab;
    private readonly int _previousPage;
    private readonly int _newPage;
    private readonly Func<int, Task> _navigateCallback;

    public NavigationAction(
        WindowsDocumentTabViewModel tab,
        int previousPage,
        int newPage,
        Func<int, Task> navigateCallback)
    {
        _tab = tab;
        _previousPage = previousPage;
        _newPage = newPage;
        _navigateCallback = navigateCallback;
    }

    public string Description => "Navigate page";

    public void Execute()
    {
        _ = _navigateCallback(_newPage);
    }

    public void Undo()
    {
        _ = _navigateCallback(_previousPage);
    }
}
