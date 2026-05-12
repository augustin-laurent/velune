using Velune.Domain.Annotations;

namespace Velune.Windows.ViewModels.UndoSystem;

public sealed class AnnotationMutationAction : IUndoableAction
{
    private readonly WindowsDocumentTabViewModel _tab;
    private readonly DocumentAnnotation _before;
    private readonly DocumentAnnotation _after;

    public AnnotationMutationAction(
        WindowsDocumentTabViewModel tab,
        DocumentAnnotation before,
        DocumentAnnotation after)
    {
        _tab = tab;
        _before = before;
        _after = after;
    }

    public string Description => "Modify annotation";

    public void Execute()
    {
        ReplaceAnnotation(_after);
    }

    public void Undo()
    {
        ReplaceAnnotation(_before);
    }

    private void ReplaceAnnotation(DocumentAnnotation target)
    {
        int index = _tab.FindAnnotationIndex(target.Id);
        if (index < 0)
        {
            return;
        }

        _tab.Annotations[index] = target;
        _tab.RefreshAnnotationOverlays(target.Id);
    }
}
