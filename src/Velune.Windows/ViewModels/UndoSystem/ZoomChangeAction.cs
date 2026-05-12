namespace Velune.Windows.ViewModels.UndoSystem;

public sealed class ZoomChangeAction : IUndoableAction
{
    private readonly WindowsDocumentTabViewModel _tab;
    private readonly double _previousZoom;
    private readonly double _newZoom;
    private readonly Action<WindowsDocumentTabViewModel> _applyCallback;

    public ZoomChangeAction(
        WindowsDocumentTabViewModel tab,
        double previousZoom,
        double newZoom,
        Action<WindowsDocumentTabViewModel> applyCallback)
    {
        _tab = tab;
        _previousZoom = previousZoom;
        _newZoom = newZoom;
        _applyCallback = applyCallback;
    }

    public string Description => "Zoom change";

    public void Execute()
    {
        _tab.ZoomFactor = _newZoom;
        _tab.ZoomText = $"{_newZoom * 100:0}%";
        _applyCallback(_tab);
    }

    public void Undo()
    {
        _tab.ZoomFactor = _previousZoom;
        _tab.ZoomText = $"{_previousZoom * 100:0}%";
        _applyCallback(_tab);
    }
}
