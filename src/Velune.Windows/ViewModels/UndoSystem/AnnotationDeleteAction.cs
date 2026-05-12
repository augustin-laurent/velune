using Velune.Domain.Annotations;

namespace Velune.Windows.ViewModels.UndoSystem;

public sealed class AnnotationDeleteAction : IUndoableAction
{
    private readonly WindowsDocumentTabViewModel _tab;
    private readonly DocumentAnnotation _annotation;

    public AnnotationDeleteAction(WindowsDocumentTabViewModel tab, DocumentAnnotation annotation)
    {
        _tab = tab;
        _annotation = annotation;
    }

    public string Description => "Delete annotation";

    public void Execute()
    {
        _tab.DeleteAnnotationById(_annotation.Id);
    }

    public void Undo()
    {
        _tab.AddAnnotation(_annotation);
    }
}
