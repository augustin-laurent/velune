using Velune.Domain.Annotations;

namespace Velune.Windows.ViewModels.UndoSystem;

public sealed class AnnotationAddAction : IUndoableAction
{
    private readonly WindowsDocumentTabViewModel _tab;
    private readonly DocumentAnnotation _annotation;

    public AnnotationAddAction(WindowsDocumentTabViewModel tab, DocumentAnnotation annotation)
    {
        _tab = tab;
        _annotation = annotation;
    }

    public string Description => "Add annotation";

    public void Execute()
    {
        _tab.AddAnnotation(_annotation);
    }

    public void Undo()
    {
        _tab.DeleteAnnotationById(_annotation.Id);
    }
}
